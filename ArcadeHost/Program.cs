namespace ControllerWheel.ArcadeHost;

/// <summary>Entry point of the jailed helper. One argument. <c>--xinput-count</c> selects the
/// read-only isolation probe (see <see cref="XInputCount"/> — launched directly by Radiata, no jail);
/// anything else is the pipe name for a jailed script session. Everything else a session needs —
/// script text, seed, kv snapshot — arrives over the pipe, never on the command line and never as
/// a path (the AppContainer couldn't read one anyway).</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0])) return 2;
        if (args[0] == "--xinput-count")
        {
            try { return XInputCount.Run(); }
            catch { return 1; }
        }
        try { return ScriptEngineHost.Run(args[0]); }
        catch { return 1; }   // the Job Object reclaims us either way; never show UI
    }
}
