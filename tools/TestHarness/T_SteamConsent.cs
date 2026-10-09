using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ControllerWheel;

namespace Radiata.TestHarness;

/// <summary>Exercise actual WPF consent controls and deadline logic without downloading, installing,
/// starting the app, or restarting Steam. Process presence is supplied at the UI boundary.</summary>
internal static class T_SteamConsent
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);
    static T Get<T>(object target, string name) =>
        (T)target.GetType().GetField(name, Private).GetValue(target);

    public static void Run()
    {
        H.Group("Steam restart consent: real controls and expiry, no process mutation");
        var window = new UpdateWindow(new UpdateFeed { Version = "0.15.9999",
            Url = "https://github.com/scumbly/radiata/releases/download/test/test.exe",
            Sha256 = new string('a', 64) });
        try
        {
            var choice = Get<CheckBox>(window, "_restartSteam");
            Call(window, "RefreshSteamRestartOffer", false);
            H.Check("Steam absent hides default-checked offer", choice.Visibility == Visibility.Collapsed && choice.IsChecked == true);
            H.Check("arrival before unseen offer cannot grant consent", !(bool)Call(window, "AcceptSteamRestartChoice", true));
            Call(window, "RefreshSteamRestartOffer", true);
            H.Check("Steam arrival displays existing default choice", choice.Visibility == Visibility.Visible && choice.IsChecked == true);
            choice.IsChecked = false;
            Call(window, "RefreshSteamRestartOffer", false);
            Call(window, "RefreshSteamRestartOffer", true);
            H.Check("explicit opt-out survives Steam exit and return", choice.IsChecked == false);
            H.Check("unchecked offer cannot authorize restart", !(bool)Call(window, "AcceptSteamRestartChoice", true));
            choice.IsChecked = true;
            H.Check("Steam exit before acceptance revokes stale offer", !(bool)Call(window, "AcceptSteamRestartChoice", false));
            bool accepted = (bool)Call(window, "AcceptSteamRestartChoice", true);
            H.Check("visible checked running-Steam offer grants consent", accepted);
            H.Check("acceptance freezes controls and stops polling", !choice.IsEnabled && !Get<DispatcherTimer>(window, "_steamOfferTimer").IsEnabled);
            choice.IsChecked = false;
            H.Check("accepted value is independent of later checkbox state", accepted);
            Call(window, "ResumeSteamRestartOffer");
            H.Check("retry re-enables choice without discarding opt-out", choice.IsEnabled && choice.IsChecked == false);
        }
        finally { window.Close(); }

        // Bypass Application construction: only the isolated consent fields are exercised. No startup,
        // config, drivers, tray or sentry relaunch path is initialized or invoked by these calls.
        var app = (App)RuntimeHelpers.GetUninitializedObject(typeof(App));
        Set(app, "_userSteamRestartPending", true);
        Set(app, "_userSteamRestartDeadline", 1000L);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        Set(app, "_userSteamRestartExpiryTimer", timer);
        timer.Start();
        H.Check("consent remains valid just before deadline", !(bool)Call(app, "ExpireUserSteamRestart", 999L) && Get<bool>(app, "_userSteamRestartPending"));
        H.Check("expiry works without isolation", (bool)Call(app, "ExpireUserSteamRestart", 1000L) && !Get<bool>(app, "_userSteamRestartPending"));
        H.Check("expiry stops its timer", !timer.IsEnabled);
        H.Check("repeated expiry is inert", !(bool)Call(app, "ExpireUserSteamRestart", 1001L));
        Set(app, "_userSteamRestartPending", true);
        Set(app, "_userSteamRestartDeadline", 2000L);
        H.Check("new consent uses new deadline", !(bool)Call(app, "ExpireUserSteamRestart", 1500L) && Get<bool>(app, "_userSteamRestartPending"));
        H.Check("late dispatcher tick still expires consent", (bool)Call(app, "ExpireUserSteamRestart", 2500L));

        Set(app, "_steamSentry", new SteamSentry());
        Set(app, "_userSteamRestartPending", true);
        Set(app, "_userSteamRestartDeadline", Environment.TickCount64 + 100);
        var expiryTimer = (DispatcherTimer)Call(app, "CreateSteamRestartExpiryTimer");
        Set(app, "_userSteamRestartExpiryTimer", expiryTimer);
        expiryTimer.Interval = TimeSpan.FromMilliseconds(1);
        expiryTimer.Start();
        PumpFor(350);
        H.Check("real timer retries early tick and expires without a capture pass", !Get<bool>(app, "_userSteamRestartPending") && !expiryTimer.IsEnabled);
        H.Check("expiry does not fabricate a Steam episode", Get<SteamSentry>(app, "_steamSentry").Current == SteamSentry.State.Idle);
        Set(app, "_userSteamRestartPending", true);
        Set(app, "_shuttingDown", true);
        expiryTimer.Interval = TimeSpan.FromMilliseconds(1);
        expiryTimer.Start();
        PumpFor(60);
        H.Check("shutdown timer tick stops without evaluating consent", !expiryTimer.IsEnabled && Get<bool>(app, "_userSteamRestartPending"));

        RestartMarker();
    }

    /// <summary>The cross-restart marker file behind the update prompt: a withdrawn request is gone, and an
    /// armed one is honoured once and only while fresh. Runs in the harness folder, never a real profile.</summary>
    static void RestartMarker()
    {
        // SteamRestartFlag is internal to the app assembly.
        var flag = H.AppType("SteamRestartFlag");
        void Do(string name) => H.InvokeStatic(flag, name);
        bool Consume() => (bool)H.InvokeStatic(flag, "Consume");
        var maxAge = (TimeSpan)H.GetStatic(flag, "MaxAge");
        string marker = System.IO.Path.Combine(AppPaths.AppDataDir, "pending-steam-restart.txt");
        H.Check("marker lives in the isolated data folder",
                AppPaths.AppDataDir == (string)AppContext.GetData("Radiata.TestDataDirectory"));
        if (System.IO.File.Exists(marker)) System.IO.File.Delete(marker);

        Do("Arm");
        H.Check("Arm writes the marker", System.IO.File.Exists(marker));
        Do("Disarm");
        H.Check("Disarm removes it", !System.IO.File.Exists(marker));
        H.Check("a disarmed marker is not honoured", !Consume());
        Do("Disarm");   // no marker: inert
        H.Check("Disarm with nothing armed is a no-op", !System.IO.File.Exists(marker));

        Do("Arm");
        H.Check("a fresh marker is honoured", Consume());
        H.Check("...and consumed", !System.IO.File.Exists(marker) && !Consume());

        Do("Arm");
        System.IO.File.SetLastWriteTimeUtc(marker, DateTime.UtcNow - maxAge + TimeSpan.FromMinutes(1));
        H.Check("a marker just inside the window is honoured", Consume());

        using var trace = new H.TraceGrab();
        Do("Arm");
        System.IO.File.SetLastWriteTimeUtc(marker, DateTime.UtcNow - maxAge - TimeSpan.FromMinutes(1));
        H.Check("a marker past the window is NOT honoured", !Consume());
        H.Check("...it is deleted", !System.IO.File.Exists(marker));
        H.Check("...and the discard is traced", trace.Saw("stale Steam restart marker"));
        H.Check("the window is longer than an install", maxAge >= TimeSpan.FromMinutes(10));
    }
    static void PumpFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        stop.Tick += (_, _) => { stop.Stop(); frame.Continue = false; };
        stop.Start();
        Dispatcher.PushFrame(frame);
    }
}
