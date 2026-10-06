using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Radiata.TestHarness;

internal static class T_RpcFaults
{
    public static void Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var type = H.AppType("DiscordIpc");
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var operationToken = (AsyncLocal<CancellationToken>)type.GetField("OperationToken", flags).GetValue(null);
        async Task WithPipe(Func<NamedPipeServerStream, NamedPipeClientStream, Task> test)
        {
            string name = "radiata-rpc-regression-" + Guid.NewGuid().ToString("N");
            using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 512, 512);
            using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            var accepting = server.WaitForConnectionAsync();
            await client.ConnectAsync(2000);
            await accepting;
            await test(server, client);
        }
        byte[] Frame(string json, int? length = null)
        {
            byte[] body = Encoding.UTF8.GetBytes(json), bytes = new byte[8 + body.Length];
            BitConverter.GetBytes(1).CopyTo(bytes, 0);
            BitConverter.GetBytes(length ?? body.Length).CopyTo(bytes, 4);
            body.CopyTo(bytes, 8);
            return bytes;
        }

        await WithPipe(async (server, client) =>
        {
            using var deadline = new CancellationTokenSource(2000);
            var read = (Task<string>)type.GetMethod("ReadUntilCmdAsync", flags).Invoke(null,
                new object[] { client, "GET_VOICE_SETTINGS", "current", deadline.Token });
            await server.WriteAsync(Frame("{\"cmd\":\"GET_VOICE_SETTINGS\",\"nonce\":\"old\",\"data\":{}}"));
            byte[] matching = Frame("{\"cmd\":\"GET_VOICE_SETTINGS\",\"nonce\":\"current\",\"data\":{}}");
            await server.WriteAsync(matching.AsMemory(0, 3));
            await server.WriteAsync(matching.AsMemory(3));
            string result = await read.WaitAsync(TimeSpan.FromSeconds(3));
            H.Check("fragmented RPC reply matches the current nonce rather than an old reply", result.Contains("current"));
        });

        foreach (int prefixLength in new[] { 0, 3, 9 })
            await WithPipe(async (server, client) =>
            {
                using var deadline = new CancellationTokenSource(100);
                if (prefixLength > 0)
                    await server.WriteAsync(Frame("{}", 100).AsMemory(0, prefixLength));
                var read = (Task)type.GetMethod("ReadFrameAsync", flags).Invoke(null,
                    new object[] { client, deadline.Token });
                bool cancelled = false;
                try { await read.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (OperationCanceledException) { cancelled = true; }
                H.Check($"RPC read cancels with {prefixLength} bytes supplied", cancelled);
            });

        await WithPipe(async (server, client) =>
        {
            await server.WriteAsync(Frame("", int.MaxValue));
            var read = (Task)type.GetMethod("ReadFrameAsync", flags).Invoke(null,
                new object[] { client, CancellationToken.None });
            bool refused = false;
            try { await read.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (InvalidDataException) { refused = true; }
            H.Check("oversized RPC frame is refused before allocation", refused);
        });

        await WithPipe(async (_, client) =>
        {
            using var deadline = new CancellationTokenSource(100);
            var previous = operationToken.Value;
            operationToken.Value = deadline.Token;
            try
            {
                var write = (Task)type.GetMethod("WriteFrameAsync", flags).Invoke(null,
                    new object[] { client, 1, new string('x', 1024 * 1024) });
                bool cancelled = false;
                try { await write.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (OperationCanceledException) { cancelled = true; }
                H.Check("RPC write to a peer that never reads respects operation cancellation", cancelled);
            }
            finally { operationToken.Value = previous; }
        });
    }
}
