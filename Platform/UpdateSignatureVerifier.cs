using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace ControllerWheel;

/// <summary>
/// Authenticode check for a downloaded installer: WinVerifyTrust (WINTRUST_ACTION_GENERIC_VERIFY_V2, embedded
/// signature, no UI) for the trust verdict, then the signer certificate and its chain root read for
/// <see cref="UpdateSignature.Accept"/>.
///
/// Revocation is deliberately OFF. The file's SHA-256 already matched the feed, Artifact Signing leaf
/// certificates live only days (a revocation entry buys almost nothing), and the signature is valid after the
/// leaf expires only because of its RFC 3161 timestamp — a CRL/OCSP hiccup on the user's network must not
/// leave every future update permanently refused. Do not switch this to a revocation-mandatory mode without
/// a fail-open path for unreachable responders.
/// </summary>
public static class UpdateSignatureVerifier
{
    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_IGNORE = 0;
    private const uint WTD_REVOCATION_CHECK_NONE = 0x10;
    private static readonly IntPtr InvalidHandle = new(-1);
    private static Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref WINTRUST_DATA data);

    /// <summary>True when <paramref name="path"/> carries a trusted signature from the required signer
    /// (see <see cref="UpdateSignature"/>). Never throws; <paramref name="reason"/> is a trace string.</summary>
    public static bool Verify(string path, out string reason)
    {
        try
        {
            bool trusted = WinTrust(path, out int hr);
            if (!trusted)
            {
                reason = $"WinVerifyTrust returned 0x{hr:X8}";
                return false;
            }
            ReadSignerAndRoot(path, out var subject, out var root);
            return UpdateSignature.Accept(true, subject, root, out reason);
        }
        catch (Exception ex)
        {
            reason = $"signature check threw: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool WinTrust(string path, out int hr)
    {
        IntPtr pPath = Marshal.StringToHGlobalUni(path);
        IntPtr pFile = IntPtr.Zero;
        try
        {
            var fi = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = pPath };
            pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fi, pFile, false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = pFile,
                dwStateAction = WTD_STATEACTION_IGNORE,
                dwProvFlags = WTD_REVOCATION_CHECK_NONE,
            };
            hr = WinVerifyTrust(InvalidHandle, ref GenericVerifyV2, ref data);
            return hr == 0;
        }
        finally
        {
            if (pFile != IntPtr.Zero) Marshal.FreeHGlobal(pFile);
            Marshal.FreeHGlobal(pPath);
        }
    }

    /// <summary>Signer subject plus the subject of the chain's top certificate. A chain that is partial or
    /// has any problem beyond expiry (the leaf is short-lived; the timestamp carries validity) yields a null
    /// root, so the decision rejects it.</summary>
    private static void ReadSignerAndRoot(string path, out string? subject, out string? root)
    {
        subject = root = null;
        using var leaf = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
        subject = leaf.SubjectName.Name;

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf);
        foreach (var st in chain.ChainStatus)
        {
            if ((st.Status & ~(X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.NotTimeNested)) != 0)
            {
                Trace.WriteLine($"[Update] signer chain status {st.Status}: {st.StatusInformation?.Trim()}");
                return;
            }
        }
        if (chain.ChainElements.Count < 2) return;
        root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate.SubjectName.Name;
    }
}
