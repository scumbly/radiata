using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Radiata.TestHarness;

/// <summary>Check bookkeeping + the reflection helpers the groups share.
///
/// Several things worth testing live as PRIVATE STATIC members of types whose public surface is a UI
/// window (Restore's zip guards on SettingsWindow, FindGameExe/DedupePlaynite on GameLibrary). Reaching
/// them by reflection tests the real guard rather than a copy of it — but it does mean a rename silently
/// turns a check into a MISSING, never a false pass. That's what <see cref="Fail"/> vs
/// <see cref="Missing"/> distinguishes, and why a missing member is counted as a failure.</summary>
internal static class H
{
    public static int Passed, Failed, Skipped, SkippedDevOnly;
    private static string _group = "";

    public static void Group(string name)
    {
        _group = name;
        Console.WriteLine();
        Console.WriteLine($"══ {name} ".PadRight(78, '═'));
    }

    public static void Pass(string what, string detail = null)
    {
        Passed++;
        Console.WriteLine($"  PASS  {what}{(detail is null ? "" : "  — " + detail)}");
    }

    public static void Fail(string what, string detail = null)
    {
        Failed++;
        Console.WriteLine($"  FAIL  {what}{(detail is null ? "" : "  — " + detail)}");
    }

    /// <summary>Couldn't run the check at all (no API key, member renamed, precondition absent). Counted
    /// separately: a skip is never evidence of anything.</summary>
    public static void Skip(string what, string why)
    {
        Skipped++;
        Console.WriteLine($"  SKIP  {what}  — {why}");
    }

    public static void Check(string what, bool ok, string detail = null)
    {
        if (ok) Pass(what, detail); else Fail(what, detail);
    }

    /// <summary>Run a check that's expected to complete without throwing; an exception is a failure and
    /// the type/message is reported (a crash IS the thing most of these tests are looking for).</summary>
    public static void Try(string what, Action body)
    {
        try { body(); }
        catch (TargetInvocationException tie)
        {
            Fail(what, $"threw {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}");
        }
        catch (Exception ex) { Fail(what, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    // ── paths ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Walk up from this binary's own build output to the checkout that owns it. A worktree holds
    /// its own complete copy of the repo at its own root, so this resolves to the running worktree's root
    /// from inside one — never the main checkout's. Null if no ancestor within the search depth carries the
    /// marker (e.g. a published build with no source tree beside it).</summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ControllerWheel.csproj"))) return dir.FullName;
        return null;
    }

    /// <summary>True when <paramref name="root"/> is the development repository: the one checkout that carries
    /// <c>tools/export-public.ps1</c>, the script that produces the public snapshot. The snapshot is an
    /// allow-list export, so it never carries that script or the files only development reads (the docs
    /// ledgers, the generated controls reference, the site docroot, the sample packages, the SFX ingest
    /// manifest). One marker, so every development-only check agrees on what "development" means.</summary>
    public static bool IsDevRepo(string root) =>
        root is not null && File.Exists(Path.Combine(root, "tools", "export-public.ps1"));

    /// <summary>Gate for a check that reads files the public snapshot does not carry. In the development
    /// repository it returns true and the check runs as written, so a missing file there is still a failure.
    /// Anywhere else it records a visible SKIP, counted apart in the summary, and returns false.</summary>
    public static bool DevRepoOnly(string what, string root)
    {
        if (IsDevRepo(root)) return true;
        SkippedDevOnly++;
        Skip(what, "development-repository check; the public snapshot does not carry the files it reads");
        return false;
    }

    // ── reflection ────────────────────────────────────────────────────────────────────────────────────

    public static Assembly App  =>typeof(ControllerWheel.HidHideManager).Assembly;
    public static Assembly Core => typeof(ControllerWheel.WheelSlice).Assembly;

    public static Type AppType(string name) =>
        App.GetType("ControllerWheel." + name) ?? App.GetType(name);

    /// <summary>A private/public static method by name, or null if it's gone (renamed = MISSING, not pass).</summary>
    public static MethodInfo StaticMethod(Type t, string name, int argCount = -1)
    {
        foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            if (m.Name == name && (argCount < 0 || m.GetParameters().Length == argCount)) return m;
        return null;
    }

    public static object InvokeStatic(Type t, string name, params object[] args)
    {
        var m = StaticMethod(t, name, args.Length)
                ?? throw new MissingMethodException($"{t.Name}.{name}({args.Length} args)");
        return m.Invoke(null, args);
    }

    /// <summary>A private/public static field by name, or null if it's gone.</summary>
    public static FieldInfo StaticField(Type t, string name) =>
        t.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    public static object GetStatic(Type t, string name) =>
        (StaticField(t, name) ?? throw new MissingFieldException($"{t.Name}.{name}")).GetValue(null);

    /// <summary>Why the last <see cref="TrySetStatic"/> failed, so a SKIP can say something useful.</summary>
    public static string SetStaticError { get; private set; }

    /// <summary>Set a static field, including a <c>static readonly</c> one. Used to point a cache directory at
    /// a temp folder so a destructive path (the art-cache trim) can be driven against synthetic files instead
    /// of the machine's real data. Returns false if the runtime refuses — callers must Skip, not pretend.</summary>
    public static bool TrySetStatic(Type t, string name, object value)
    {
        var f = StaticField(t, name);
        if (f is null) return false;
        try { f.SetValue(null, value); return Equals(f.GetValue(null), value); }
        catch (Exception ex) { SetStaticError = $"{ex.GetType().Name}: {ex.Message}"; return false; }
    }

    // ── trace capture ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Collect Trace output while an action runs. Most of the guards under test announce what
    /// they refused via Trace and return nothing visible, so the trace IS the observable.</summary>
    public sealed class TraceGrab : TraceListener, IDisposable
    {
        private readonly List<string> _lines = new();
        public TraceGrab() { Trace.Listeners.Add(this); }
        public override void Write(string message) { }
        public override void WriteLine(string message) { lock (_lines) _lines.Add(message ?? ""); }
        public IReadOnlyList<string> Lines { get { lock (_lines) return _lines.ToArray(); } }
        public bool Saw(string fragment)
        {
            foreach (var l in Lines)
                if (l.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        public new void Dispose() { Trace.Listeners.Remove(this); base.Dispose(); }
    }
}
