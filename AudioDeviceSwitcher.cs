using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace ControllerWheel;

/// <summary>
/// Switches the Windows default audio render endpoint via the undocumented
/// IPolicyConfig COM interface — the same approach used by SoundSwitch et al.
/// </summary>
internal static class AudioDeviceSwitcher
{
    /// <summary>
    /// If <paramref name="deviceName"/> is non-empty, switches to the first device on the
    /// given <paramref name="flow"/> whose friendly name contains it (case-insensitive).
    /// If empty, cycles to the next active device on that flow.  <paramref name="flow"/> is
    /// <see cref="DataFlow.Render"/> for outputs (speakers/headphones) or
    /// <see cref="DataFlow.Capture"/> for inputs (microphones).
    /// Returns the friendly name of the device switched to (for the wheel's hub readout), or null when
    /// nothing was switched (no devices / no match).
    /// </summary>
    public static string? Switch(string? deviceName, DataFlow flow = DataFlow.Render)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator
            .EnumerateAudioEndPoints(flow, DeviceState.Active)
            .ToList();
        try
        {
            if (devices.Count < 1) return null;

            // Resolve the target's device ID + name (strings we can use after the MMDevice objects are freed).
            string? targetId, targetName;
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                // No match → no-op (don't throw into the action executor); a renamed/absent device is benign.
                var match = devices.FirstOrDefault(d =>
                    d.FriendlyName.Contains(deviceName, StringComparison.OrdinalIgnoreCase));
                if (match is null) return null;
                (targetId, targetName) = (match.ID, match.FriendlyName);
            }
            else
            {
                // Cycle: find the current default, pick the next.
                using var current = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                int idx = devices.FindIndex(d => d.ID == current.ID);
                var next = devices[(idx + 1) % devices.Count];
                (targetId, targetName) = (next.ID, next.FriendlyName);
            }

            SetDefaultEndpointAllRoles(targetId, role =>
            {
                using var actual = enumerator.GetDefaultAudioEndpoint(flow, role);
                return actual.ID;
            });
            return targetName;
        }
        finally
        {
            // EnumerateAudioEndPoints hands back MMDevice COM objects we own — dispose them so repeated
            // switch-audio slices don't leak native audio-endpoint handles.
            foreach (var d in devices) d.Dispose();
        }
    }

    /// <summary>Number of active render (output) endpoints — onboarding uses it to decide whether a
    /// Switch Audio slice is worth adding to the starter wheel. 0 on any failure.</summary>
    public static int OutputCount()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
            try { return devices.Count; }
            finally { foreach (var d in devices) d.Dispose(); }
        }
        catch { return 0; }
    }

    /// <summary>Friendly name of the current default device on the given flow, or null if none.</summary>
    public static string? CurrentDefaultName(DataFlow flow)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device     = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return device.FriendlyName;
        }
        catch { return null; }
    }

    /// <summary>Set the default endpoint for all three roles via the undocumented PolicyConfig COM object.
    /// The object's interface IID varies by Windows build (identical vtable) — QI the newer Win10/11 "X"
    /// IID first, then the classic Win7 one; keep that order. The COM object is released so repeated
    /// switch-audio fires don't leak an RCW until GC.</summary>
    private static void SetDefaultEndpointAllRoles(string targetId, Func<Role, string> get)
    {
        object client = new PolicyConfigClient();
        try
        {
            if (client is IPolicyConfigX x)   // QI: newer builds
            {
                ApplyDefaultRoles(targetId, x.SetDefaultEndpoint, get);
            }
            else                              // classic IID (Win7+; still the common case on Win10/11)
            {
                var c = (IPolicyConfig)client;
                ApplyDefaultRoles(targetId, c.SetDefaultEndpoint, get);
            }
        }
        finally { Marshal.FinalReleaseComObject(client); }
    }

    private static void ApplyDefaultRoles(string targetId, Func<string, Role, int> set, Func<Role, string> get)
    {
        Role[] roles = [Role.Console, Role.Multimedia, Role.Communications];
        var previous = roles.ToDictionary(role => role, get); // Read every role before making any change.
        try
        {
            foreach (var role in roles) Marshal.ThrowExceptionForHR(set(targetId, role));
            foreach (var role in roles)
                if (!string.Equals(get(role), targetId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Audio default for {role} did not change to the requested device.");
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var role in roles)
            {
                try
                {
                    // Restore only a role still pointing at our target. Preserve another tool's later choice.
                    if (previous[role] != targetId && string.Equals(get(role), targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        Marshal.ThrowExceptionForHR(set(previous[role], role));
                        if (!string.Equals(get(role), previous[role], StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"Audio default for {role} could not be restored.");
                    }
                }
                catch (Exception rollbackFailure) { errors.Add(rollbackFailure); }
            }
            if (errors.Count > 1) throw new AggregateException("Audio switching failed and some default roles could not be restored.", errors);
            throw;
        }
    }

    // ── COM interop ───────────────────────────────────────────────────────────

    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string dev, IntPtr ppFmt);
        [PreserveSig] int GetDeviceFormat(string dev, bool bDefault, IntPtr ppFmt);
        [PreserveSig] int ResetDeviceFormat(string dev);
        [PreserveSig] int SetDeviceFormat(string dev, IntPtr pFmt, IntPtr pMix);
        [PreserveSig] int GetProcessingPeriod(string dev, bool bDefault, IntPtr pDef, IntPtr pMin);
        [PreserveSig] int SetProcessingPeriod(string dev, IntPtr pPeriod);
        [PreserveSig] int GetShareMode(string dev, IntPtr pMode);
        [PreserveSig] int SetShareMode(string dev, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string dev, bool bFx, IntPtr pKey, IntPtr pv);
        [PreserveSig] int SetPropertyValue(string dev, bool bFx, IntPtr pKey, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint(string dev, Role role);
        [PreserveSig] int SetEndpointVisibility(string dev, bool bVisible);
    }

    // Same vtable as IPolicyConfig — only the IID differs on newer Windows builds. QI'd first in
    // SetDefaultEndpointAllRoles.
    [Guid("8F9FB2AA-1C0B-4D54-B6BB-B2F2A10CE03C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfigX
    {
        [PreserveSig] int GetMixFormat(string dev, IntPtr ppFmt);
        [PreserveSig] int GetDeviceFormat(string dev, bool bDefault, IntPtr ppFmt);
        [PreserveSig] int ResetDeviceFormat(string dev);
        [PreserveSig] int SetDeviceFormat(string dev, IntPtr pFmt, IntPtr pMix);
        [PreserveSig] int GetProcessingPeriod(string dev, bool bDefault, IntPtr pDef, IntPtr pMin);
        [PreserveSig] int SetProcessingPeriod(string dev, IntPtr pPeriod);
        [PreserveSig] int GetShareMode(string dev, IntPtr pMode);
        [PreserveSig] int SetShareMode(string dev, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string dev, bool bFx, IntPtr pKey, IntPtr pv);
        [PreserveSig] int SetPropertyValue(string dev, bool bFx, IntPtr pKey, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint(string dev, Role role);
        [PreserveSig] int SetEndpointVisibility(string dev, bool bVisible);
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }
}
