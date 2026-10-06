namespace ControllerWheel;

/// <summary>The public-release feature gate. <c>tools\build-installer.ps1 -Public</c> (the release workflow)
/// passes <c>RadiataPublic=true</c>, which defines <c>PUBLIC_RELEASE</c> in every project; a dev build or any
/// build made without the switch keeps every gated feature. This is the only file that reads the define.
///
/// <para>Gated: Internode (the whole game), drop-in Arcade script games, drop-in Material themes, the tray's Show in
/// Explorer item, and the open wheel's Select-button material cycle (Start still opens Settings). Each gate is a
/// build const so the catalog, the package scanner and the generated docs follow the build; the
/// <c>*Offered</c> properties add the harness-only <see cref="PreviewPublic"/> flip, mirroring
/// <c>Arcade.Enabled</c> / <c>Arcade.Available</c>, so the in-app Help set can be exercised in both postures
/// from one Debug build. Reintroducing a feature = flip its const to true; nothing else moves, and the gated
/// Help strings stay in the translation maps meanwhile (HelpLocalization.SourceStrings walks the ungated set).</para>
///
/// <para>A gated feature's code, assets and UI strings stay compiled; only its registration is withheld:
/// ArcadeCatalog omits the Internode entry, PackageStore neither creates nor scans a gated package folder. A
/// config that still names a gated id degrades on the existing paths (an unknown arcade id opens the Launcher,
/// an unregistered material token normalizes to Pearl). Nothing in this class may be referenced by a save
/// format or a probe's pass condition. New surfaces of a gated feature (a Help line, a picker entry, a folder, a tray item) consult these gates.</para></summary>
public static class ReleaseGates
{
#if PUBLIC_RELEASE
    public const bool PublicRelease = true;
#else
    public const bool PublicRelease = false;
#endif

    public const bool Internode        = !PublicRelease;
    public const bool ArcadePackages   = !PublicRelease;
    public const bool MaterialPackages = !PublicRelease;
    public const bool TrayShowInExplorer = !PublicRelease;
    public const bool WheelMaterialCycle = !PublicRelease;

    /// <summary>Harness-only. Nothing in the app ever sets this - <c>TestHarness.exe help</c> flips it to
    /// prove the Help topic set drops every gated topic and block and rebuilds rather than latching.</summary>
    public static bool PreviewPublic { get; set; }

    public static bool InternodeOffered        => Internode        && !PreviewPublic;
    public static bool ArcadePackagesOffered   => ArcadePackages   && !PreviewPublic;
    public static bool MaterialPackagesOffered => MaterialPackages && !PreviewPublic;
    public static bool TrayShowInExplorerOffered => TrayShowInExplorer && !PreviewPublic;
}