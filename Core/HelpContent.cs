namespace ControllerWheel;

/// <summary>Block kinds for help-topic bodies. The WPF renderer (HelpEditorControl) and the CONTROLS.md
/// exporter both consume these, so authored content renders in-app and generates the doc.</summary>
public enum HelpBlockKind { Para, Bullet, Tip, Warning, Heading, Figure }

/// <summary>One block of a help topic. <paramref name="Indent"/> nests bullets; <paramref name="LiveOnly"/>
/// blocks render only in-app (they resolve the user's live bindings via tokens — meaningless in the
/// exported generic doc). Inline styling: **bold** and `code`; tokens in {braces} (see HelpContent.Tokens).
/// <para>On a <see cref="HelpBlockKind.Figure"/> block, <paramref name="Figure"/> names the diagram in
/// <see cref="HelpFigures"/> and <paramref name="Text"/> is its caption — which must stand on its own as a
/// sentence, because the exported doc has the caption and no drawing.</para></summary>
/// <para><paramref name="When"/> gates the block on a <see cref="ReleaseGates"/> posture: a block whose
/// predicate is false is dropped from <see cref="HelpContent.Topics"/> and <see cref="HelpContent.ExportTopics"/>
/// alike, so the feature it describes vanishes from the Help tab, the search index, CONTROLS.md and the site
/// pages together. The string still counts as authored (translations are kept for it).</para></summary>
public sealed record HelpBlock(HelpBlockKind Kind, string Text, int Indent = 0, bool LiveOnly = false,
                               string? Figure = null, Func<bool>? When = null);

/// <summary>One help topic: shown in the Settings ▸ Help tab and exported as a CONTROLS.md section.
/// <paramref name="Keywords"/> is a space-separated haystack for search (title + body are searched too).
/// <paramref name="When"/> gates the whole topic like <see cref="HelpBlock.When"/> does a block.</summary>
public sealed record HelpTopic(string Id, string Category, string Title, string Keywords, HelpBlock[] Body,
                               Func<bool>? When = null);

/// <summary>The canonical source for user-facing controls/feature documentation:
/// the Settings ▸ Help tab renders these topics (with the user's live chord + glyph set via
/// tokens), and CONTROLS.md is generated from them (`Radiata.exe --export-controls [path]`) — so the doc,
/// the in-app help, and the trigger labels can never drift apart. Edit topics here, then re-export.
///
/// Tokens (resolved by the host): {invoke} = the user's summon chord; {disable} = its enable/disable
/// chord; {cross} {circle} {square} {triangle} = face-button glyphs in the user's glyph set (✕○□△ or
/// A/B/X/Y). The export resolver substitutes generic PlayStation-style defaults.
/// <para>⚠ The two chord tokens resolve to a plural phrase ("your chosen chords") when the user has more
/// than one invocation chord enabled, so any sentence using them must read correctly both ways — put the
/// token at the end as an object ("open a wheel with {invoke}"), never as the subject of a verb that has
/// to agree with it ("{invoke} opens a wheel"). See TriggerModes.DescribeChoice.</para></summary>
public static class HelpContent
{
    // Category display order (the Help tab's group order and the exported doc's section order).
    public static readonly string[] CategoryOrder =
    [
        "Welcome", "Getting Around", "Editing Wheels", "Game Grid", "Actions",
        "Controllers & Isolation", "Tray & Settings", "Workshop", "Troubleshooting",
    ];

    // ── Authoring shorthands ────────────────────────────────────────────────────
    private static HelpBlock P(string t)             => new(HelpBlockKind.Para, t);
    private static HelpBlock B(string t, int i = 0)  => new(HelpBlockKind.Bullet, t, i);
    private static HelpBlock Tip(string t)           => new(HelpBlockKind.Tip, t);
    private static HelpBlock Warn(string t)          => new(HelpBlockKind.Warning, t);
    private static HelpBlock H(string t)             => new(HelpBlockKind.Heading, t);
    private static HelpBlock Live(string t)          => new(HelpBlockKind.Tip, t, 0, LiveOnly: true);
    // Illustration + caption. The id must exist in HelpFigures.All, and every figure there must be placed
    // by one of these — see the warning on HelpFigures.
    private static HelpBlock Fig(string id, string caption) => new(HelpBlockKind.Figure, caption, 0, false, id);
    // A block that exists only while a ReleaseGates posture holds — see HelpBlock.When.
    private static HelpBlock Gated(HelpBlock b, Func<bool> when) => b with { When = when };
    // The Workshop category's two postures. A block that links an Arcade topic also needs Arcade.Available:
    // nothing outside a gated topic may link to one (see ArcadeTopicIds).
    private static bool MaterialsOn() => ReleaseGates.MaterialPackagesOffered;
    private static bool GamesOn() => ReleaseGates.ArcadePackagesOffered && Arcade.Available;

    // The authored set. Read through Topics below, never directly — that's where feature-gated topics are
    // filtered out.
    private static readonly HelpTopic[] Authored =
    [
        // ═══ Welcome ═════════════════════════════════════════════════════════════
        new("intro", "Welcome", "What is Radiata?",
            "intro welcome about purpose design overview couch overlay start here",
            [
                P("Radiata is a feature-rich, controller-based radial menu utility for a Windows gaming PC. Launch a game, join the Discord call, swap to headphones, start your stream, all from a controller."),
                // Phrased chord-last so it stays grammatical whether {invoke} resolves to one chord or to
                // the plural "your chosen chords" ("**X** opens a wheel" doesn't).
                Live("Try it now: open a wheel with **{invoke}**."),
                B("**Configurable:** each slice's action, icon, color and position, the summon chord, the look, the sounds. Edit at the desk in Settings, or from the couch in the in-wheel editor."),
                B("**Game Grid** - a universal launcher for every installed game across Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft and EA."),
                B("**Nothing hooked, nothing injected** - Radiata reads your controller directly and allows your input through only when a wheel isn't up, so the game underneath doesn't pick up duplicate input. For strict-anticheat titles, see [[passthru-mode|Passthru Mode]]."),
                Tip("Start with [[opening-a-wheel|Opening a wheel]]. Then open one and click the aiming stick (L3/R3) when you're ready to start editing."),
            ]),

        new("installing", "Welcome", "Installing Radiata",
            "install installing installer setup download smartscreen windows protected your pc unknown publisher unsigned signature certificate antivirus false positive virus admin administrator uac elevation drivers vigem hidhide requirements windows 10 11 x64 arm browser blocked keep discard first run update uninstall remove",
            [
                P("Radiata ships as a single installer, **Radiata-<version>-setup.exe**. Download it from [getradiata.app](https://getradiata.app)."),
                Warn("**Only download Radiata from known sources.** Anything else claiming to be Radiata isn't from the developer."),
                H("What you need"),
                B("**Windows 10 or 11, 64-bit (x64)** on an Intel or AMD PC. Windows on ARM isn't supported."),
                B("A supported controller - see [[supported-controllers|Supported controllers]]."),
                B("**Administrator account needed** for driver installation (optional but strongly recommended)"),
                H("\"Windows protected your PC\""),
                P("Windows SmartScreen may show a blue **\"Windows protected your PC\"** box the first time you run the installer, and your browser may warn that the file **\"isn't commonly downloaded\"**."),
                B("**In the browser:** if the download itself is blocked, keep it (Chrome and Edge: the **⋯** menu beside the download ▸ **Keep** ▸ **Show more** ▸ **Keep anyway**)."),
                B("**At the SmartScreen box:** click **More info**, then the **Run anyway** button that appears below it. If there's no **More info** link you might be seeing your browser's warning instead. See the previous step."),
                B("**No Run anyway button at all?** A managed or locked-down PC can have SmartScreen set to block outright. Radiata can't work around it."),
                Tip("You can confirm you have the genuine file before running it: every GitHub release lists the installer's **SHA-256**, and `Get-FileHash .\\Radiata-<version>-setup.exe` in PowerShell should print the same value. Radiata's updater automatically runs the same verification check on every update."),
                B("**Antivirus false positives** happen for the same reason. If yours quarantines the installer, restore it and run it again, or download it fresh from getradiata.app."),
                H("What the installer does"),
                B("Installs **for your user account only**, into `%LOCALAPPDATA%\\Programs\\Radiata`. It never touches other accounts on the PC."),
                B("Adds a **Start menu** shortcut, and on a **first** install sets Radiata to **start with Windows**. You can turn that off in the tray menu or **Settings ▸ Advanced**."),
                B("Nothing sneaky or malicious comes with Radiata. Radiata is GPLv3 free software."),
                B("Installing over an existing copy is an **upgrade in place**. Your wheels, settings and game art are left alone."),
                H("Driver prompts (UAC prompts)"),
                B("Radiata installs without admin rights; Windows asks for permission when you install the controller drivers. Leave **Install drivers (recommended)** selected and approve the Windows prompts that follow. **ViGEmBus** and **HidHide** are the open-source drivers that keep duplicate controller input out of the game. See [[input-isolation|Input isolation]]."),
                B("**Declining is safe.** Radiata still works; games just also see your controller while a wheel is open, which is annoying. Install them later any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**."),
                B("The drivers are shared system components other tools may also use, so if you uninstall Radiata, removing the drivers as well is optional."),
                H("First run"),
                B("Setup opens by itself and walks you through the drivers, a controller check, the look, cover art, and a set of starter wheels. Re-run it any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Run First-Run Setup…** - see [[system-actions|System tools]]."),
                B("Radiata then lives in the **system tray**. Click the icon for Settings or right-click for the tray menu. Then start at [[opening-a-wheel|Opening a wheel]]."),
                H("Updating and uninstalling"),
                B("**Updates are discovered automatically** by default. You can manually check for updates at **Settings ▸ Advanced ▸ Check for Updates**. Radiata always verifies the download before running it."),
                B("**Uninstall** from **Settings ▸ Advanced ▸ Troubleshooting ▸ Uninstall Radiata…**, or from Windows' **Installed apps** list. Your settings and the shared drivers can be removed in the same step."),
            ]),

        // ═══ Getting around ══════════════════════════════════════════════════════
        new("opening-a-wheel", "Getting Around", "Opening a wheel",
            "summon invoke trigger chord fn bumper touchpad swipe hold toggle swap sides activation flip left right",
            [
                Live("Your current setting: open a wheel with **{invoke}**."),
                B("Set your own chords (button combos that open a wheel) in [[triggers|Settings ▸ Customize ▸ Triggers]]."),
                B("Hold the bumper/trigger; the second button is a **tap** that brings the wheel up."),
                B("**Fn / L4/R4 and squeeze chords** (Bumper+Trigger, +Home, +Select/Start) - the hand you squeeze opens the **opposite** wheel, so the free hand aims (R1+R2 → Left; L1+L2 → Right). For Select/Start, either button works."),
                B("**L3/R3** and **D-Pad L/R** - the clicked stick or the direction determines which wheel; either bumper/trigger is the hold."),
                B("**Touchpad swipe** - in from the left edge → Left wheel, right edge → Right."),
                B("**Swap left/right** ([[accessibility|Accessibility setting]]) reverses all of these."),
                B("**Flip mid-gesture:** while holding a chord, tap the opposite bumper or trigger to switch to the other wheel without having to re-input the entire chord."),
                B("**Activation** is **Hold** (up while held; release fires) or **Toggle** (trigger opens; **{cross} confirms, {circle} cancels**; re-trigger dismisses) - set it in [[accessibility|Accessibility]]. Touchpad swipe is always toggle-style."),
                B("**Either** analog stick aims, but it's easiest if you use the hand that's not holding the shoulder button. **Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) narrows it to one stick per wheel.", 1),
            ]),

        new("picking-an-action", "Getting Around", "Aiming & firing",
            "aim arm fire cancel release deadzone sticky esc escape keyboard hub center state toggle mute hdr configure guard confirm dwell sleep reboot shutdown",
            [
                B("**Tilt the stick** toward a slice and it lights up. **Release the trigger** to fire it."),
                B("**Release while centered** (stick in the deadzone) **cancels**."),
                H("Center hub"),
                B("Arming a **toggle** slice (mic/volume mute, HDR, process toggle) shows its **current state** before you fire, e.g. `Mute Mic / Unmuted`. After firing, the hub shows the new state."),
                B("A slice that still **needs configuring** (such as a voice-join with no URL) arms as **\"Configure in Settings\"**. Choosing it takes you to the configuration screen or the setup wizard it needs."),
                H("Hold-to-confirm slices"),
                B("Slices with **Hold to confirm** are protected from accidental triggering, which is useful for Sleep, Power Down, etc. Hold the stick on them (0.8 s) and they'll activate. You can set this on any slice in the Settings wheel editors."),
            ]),

        new("wheel-open-extras", "Getting Around", "While a wheel is open",
            "volume dpad scrub repeat mic microphone alt-tab window switcher desktop song enable disable chord toggle wheels off keyboard arrow esc",
            [
                B("**D-Pad 🡅 🡇** adjusts system volume."),
                B("**D-Pad 🡄 🡆** steps the Alt-Tab window switcher by default - or [[volume-mixer|virtual desktops, track skip, or mic volume]]. Choose in **Settings ▸ Customize ▸ D-Pad 🡄 🡆**. "),
                B("**Enable / disable the wheels** with the \"both sides\" of your invocation chord, pressed **together**:"),
                B("**Bumper/Trigger + D-Pad** is directional: **D-Pad Up = enable**, **D-Pad Down = disable**.", 1),
                B("You can also toggle wheels on/off from tray menu's **Disable/Enable Wheels** or add a **Disable Wheels** slice action.", 1),
                B("Disabling the wheels changes wheel routing only. The virtual controller and cloak stay exactly where they are, basic controls keep running through that controller, and native features do **not** come back. To release capture and get your real controller, use [[passthru-mode|Passthru Mode]]. This will re-enable vendor features like special haptics and touchpads."),
                Live("Your current setting: toggle the wheels with **{disable}**."),
            ]),

        // ═══ Editing wheels ══════════════════════════════════════════════════════
        new("edit-mode", "Editing Wheels", "Edit mode (in-wheel, controller-only)",
            "edit stick click move add delete reorder undo redo picker installed game full capacity 12 limit thickness",
            [
                B("**Start editing a wheel:** with a wheel open, **click either stick** (L3/R3). The wheel centers and stays up after you release the trigger."),
                B("**Use either stick**. While you're editing, non-logo slices always show their labels regardless of the [[show-labels|Label setting]]."),
                B("**Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) can override this.", 1),
                B("**Move:** **{cross}** picks up the selected slice; aim at a target slot and **{cross}** drops it."),
                Fig("edit-move", " **D-Pad 🡄 🡆** will nudge a slice one spot left or right."),
                B("**Remove:** **hold {square}** on a slice until it disappears. Removing the **last** slice disables that wheel; see [[empty-wheel|Single-wheel mode]]."),
                B("**Add:** **{triangle}** opens the category→type **Add picker** ({cross} drills in, {circle} backs out). **Installed Game** opens the [[game-grid|Game Grid]] to pick a game, then returns to edit carrying the slice. Other types drop a slice immediately; free-text types (raw URL / keypress) land as placeholders you finish in Settings."),
                B("A wheel holds up to **12** slices."),
                B("**Undo / Redo:** **L1 / R1**. You can undo/redo multiple steps while you remain in Edit mode."),
                B("**Exit + save:** **{circle}**, click the stick again, or press an **Fn** / **L4/R4** button."),
            ]),

        new("empty-wheel", "Editing Wheels", "Single-wheel mode",
            "empty disabled free gesture resurrect rebuild",
            [
                B("**Want only one wheel?** Delete every slice off the other one. A wheel with **no slices is disabled**. That side's chord stays fully usable in the game. Remove slices from Settings, or in [[edit-mode|edit mode]] with **hold {square}** until the last one is gone."),
                Fig("single-wheel", "Emptying one wheel turns its side off: the chord that used to open it passes through to the game untouched."),
                B("To bring it back: **invoke it (hold the gesture) and click the aiming stick (L3/R3)**. The wheel opens centered, straight into the Add picker. (Toggle-style gestures have no hold, so they allow the stick click for a few seconds after the invoke.)"),
                B("If you accidentally empty **both** wheels, Settings opens so you can rebuild one or both of them."),
            ]),

        // Sits beside empty-wheel deliberately: both are about what a wheel's slice count does to its
        // gesture, which is the question a reader has once they start emptying wheels. ⚠ Arcade-gated —
        // its id is in ArcadeTopicIds, and nothing outside a gated topic may link to it.
        new("arcade-direct-launch", "Editing Wheels", "Arcade direct-launch (a wheel that is just the Arcade)",
            "arcade direct launch shortcut lone only one single slice launcher skip wheel straight cabinets instant gesture dedicated side",
            [
                P("If a wheel has only one slice and it's **the Arcade Launcher**, then that wheel is replaced by the Arcade Launcher directly."),
                B("**To set it up:** just delete all slices on a wheel except a single **Arcade ▸ Arcade Launcher** slice. You can do that in Settings, or in [[edit-mode|edit mode]] with **hold {square}**."),
                B("**The arcade opens where that wheel would have been** - the left or right quarter of the screen, the same spot the wheel uses. **{circle}** closes it as usual."),
                B("**This applies to Arcade Launcher only.** A single slice with just an individual Arcade game on it still draws as a one-slice wheel."),
                Warn("**An Arcade Launcher wheel** can't be edited with R3/L3; edit it in **Settings ▸ Left/Right Wheel**, or add a second slice to get the wheel back."),
                Tip("If you want, you can pair this with [[empty-wheel|Single-wheel mode]]: empty the OTHER wheel and you have one gesture that opens the arcade and one that passes straight through to the game."),
            ]),

        new("editor-desktop", "Editing Wheels", "Slice editor tricks (Settings, mouse & keyboard)",
            "drag drop exe lnk shortcut launch slice reorder icon color color ctrl+s save autosave draft revert logo arrows cycle fetch steamgriddb",
            [
                B("**Drag an app (.exe or .lnk) from Explorer into the slice list**. A **Launch** slice lands at the drop position with its icon already extracted. Drop several at once for several slices."),
                B("**Drag a slice within the list** to reorder."),
                B("**Set a slice's icon and color** in the Icon & Color panel below the label."),
                B("**Color swatches are paired** - the wheel shows whichever variation suits its [[customize|material]]. If you type an exact **Hex** value instead, that color is used exactly as-is, without tinting lighter or darker based on wheel Material. **Reset to Default** returns to the action-type color."),
                B("**Show Label** toggles the slice's text on the wheel. It appears only when [[show-labels|Show labels on]] is set to **Slices I Choose**."),
                H("Artwork on a slice"),
                B("**Drop your own image onto the preview well** to give any slice custom artwork. It needs a **transparent background**, so a logo-style PNG is best. Something like a screenshot or a photo with no transparency would be a solid block so it's rejected. The file is **copied** into Radiata's art cache, so moving the original later won't blank the slice."),
                B("When you choose a game, Radiata fetches its transparent **logo** automatically. The **↻ button** restores that logo (downloading it if needed), and the **🡄 🡆** buttons cycle every logo already downloaded for that game. **Requires [[integrations|SteamGridDB]] to be configured.**"),
                B("**Choosing a different icon drops the original game logo**. A slice's logo is independent of the [[cover-art|Game Grid's]]."),
                B("**Everything auto-saves** - adds, removals, reorders, and edits to an existing slice (**Revert** undoes an in-progress edit). **A new slice created in Settings is not saved until you click Save Slice**. Entering **Ctrl+S** forces a save at any point."),
                B("**A slice's action type locks once you Save it.** To change it, delete the slice and add a new one."),
            ]),

        // ═══ Game Grid ═══════════════════════════════════════════════════════════
        new("game-grid", "Game Grid", "Game Grid basics",
            "game grid browser launch navigate filter storefront chips footer add wheel pick mode installed assign favorite",
            [
                P("The Game Grid is a controller-scrollable view of every installed game across your storefronts, sorted **most-recently-launched first**, favorites pinned on top. The button controls are spelled out along the bottom of the grid."),
                B("**D-Pad / arrow keys** or the **left stick** move the selection."),
                B("**{cross} / Enter** or a mouse double-click - launch the selected game. **{circle} / Esc** - close the grid."),
                B("**{triangle}** - **favorite** the selected game. Favorites sit in their own row of larger tiles at the top of every view. Press again to un-favorite."),
                B("**Hold {square}** - **hide the game from the grid**. Bring hidden games back with **Settings ▸ Advanced ▸ Game Grid ▸ Reset Hidden Games**. The same hold on a storefront's **Open <store>** card hides that whole store - see [[storefronts|Hiding a storefront]]."),
                B("**L1 / R1** (or **PgUp / PgDn**) - cycle the storefront filter. A chip appears for each store you have installed."),
                B("**Select** and **Start** - cycle the selected game's **cover** and **logo**; see [[cover-art|Cover art & logos]]."),
                H("Add a game to a wheel"),
                B("Add games from the **in-wheel editor**: open a wheel → click the aiming stick to enter [[edit-mode|Edit]] → **{triangle} Add → Installed Game**, which opens the grid in **pick mode**. **{cross}** picks the highlighted game and carries it into the editor ({circle} cancels)."),
            ]),

        new("cover-art", "Game Grid", "Cover art & logos",
            "cover art logo cycle select start share options steamgriddb sgdb dots spinner flat colors",
            [
                B("**Select** (Create/Share) - **cycle the selected game's cover** and save it. Your choices will cycle through default, then up to 10 top-rated SteamGridDB covers, then 5 flat colors if you just want the logo on a clean background."),
                B("**Start** (Options/Menu) - **cycle the logo overlay** and save it: default logo → up to 2 SteamGridDB alternates → **off** (raw cover) → wrap. A game with no logo art uses its centered title text."),
                Tip("Covers come from [[integrations|SteamGridDB]] - add a free API key in **Settings ▸ Advanced ▸ Integrations** for portrait covers and logos across every storefront. This is the best option. However, without SteamGridDB, you still get Steam's own art, [[playnite|Playnite]]'s covers, and the flat colors."),
                B("**Art is fetched once and kept on disk**, so the grid opens instantly and offline after that. A big library fills in over the first few seconds of browsing. Reopen the grid and the stragglers should be there."),
                B("**A game with no art found is re-checked every couple of weeks** on its own, since art gets added over time. To recheck now, use **Retry Missing Game Art** ([[game-grid-options|Advanced ▸ Game Grid]])."),
            ]),

        // ═══ Actions ═════════════════════════════════════════════════════════════
        new("action-types", "Actions", "Action types",
            "actions advanced launch keypress key combo volume audio display hdr sleep discord voice text chat url settings xbox mode obs mixer game bar windows",
            [
                P("Each slice runs one action, grouped by the editor's categories:"),
                B("**Games & Apps** - installed game (direct launch), the Game Grid, storefront launcher (big-picture), launch/focus an app, **Exit Current App** (closes the frontmost app), and **Game Bar** (open, screenshot, start/stop recording, record the last 30 s, toggle mic - via Windows' Xbox Game Bar)."),
                B("**Chat & Streaming** - Discord (launch, join/leave a voice channel, deafen), Steam Chat (open chat) - see [[steam-xbox-voice|Steam voice chat]] - plus **Mic Mute** (the one Windows mic mute, offered in both groups and under System ▸ Audio), [[text-chat|Text Chat]], and **OBS Studio** ([[obs-studio|streaming, recording, replay, scenes, source mute]])."),
                B("**System** - [[controller-mode|controller mode]] (**Xbox** / **DualShock**); audio (switch output, mute, mic mute, set volume, play/pause, next/previous); display (**Toggle Extend/Clone** and HDR toggle); **Windows** (**Show Desktop** - fire again to put the windows back - and **Empty Recycle Bin**); power (sleep/hibernate/reboot/shut down/log out/lock, and **Power Plan**, which flips between two plans you pick)."),
                B("**Reboot** has a **Log In after Reboot** checkbox: checked (the default), Windows signs you back in where policy allows. Unchecked is a traditional restart, which can land at the sign-in screen.", 1),
                B("**Custom** - [[open-uri|Open URI]] and [[key-combo|Key Combo]] (send a keyboard shortcut like `Win+D` or `PlayPause`)."),
                B("**Radiata** - the Game Grid, open Settings, and disable wheels. [[passthru-mode|Passthru Mode]] is not a slice: turn it on or off from the tray or Settings ▸ Passthru Mode."),
                Tip("A slice missing a required value arms as **\"Configure in Settings\"**. Firing it opens that slice's editor."),
            ]),

        // Arcade: filtered out of Topics when Arcade.Available is false (build flag off) — see the Topics
        // property below — so those builds show it neither in Settings ▸ Help nor in the generated
        // CONTROLS.md. Deliberately filed under "Actions" rather than getting its own category: an empty
        // category would render as visible evidence of a feature nobody can reach.
        new("arcade", "Actions", "Arcade",
            "arcade game games minigame mini-game kabloom connate petal pop twist breakout paddle brick pentagon square hexagon octagon smash win minesweeper merge bee flower bomb picker play waiting loading queue lobby kill time score",
            [
                P("An **Arcade** slice opens a little game in a **round window, right where the wheel was**. Play a game while you wait on a loading screen or a big lobby, no alt-tabbing required."),
                B("**Arcade Launcher** opens the whole arcade. Each game is a cabinet on a round carousel with a live screenshot of where it was left. **Left/right** on the stick or D-Pad swings the next cabinet to the front, **{cross}** plays it. It **picks up where you left off** - straight back into the game you were last playing, or at the cabinets if that's where you closed it. Each game can also be directly-launched by adding a slice for it."),
                B("**A wheel with the Arcade Launcher and nothing else** skips the wheel and goes straight to the Arcade Launcher - see [[arcade-direct-launch|Arcade direct-launch]]."),
                B("**{circle} always backs you out**. One press closes a menu or help card, the next steps out of the game: back to the **Arcade Launcher** when that's how you got in, otherwise straight out. **The game freezes exactly as you left it**, so you can come back later and carry on. Each game stores its own state and scoreboard."),
                B("**{cross}** acts, **{square}** is each game's second action, and the **left stick** aims. The **D-Pad** drives the menus. **{triangle} explains the game you're in**, and **START pauses** with **Resume**, **How to play**, **Reset**, and that game's own settings."),
                // The game list exists in two gated variants rather than one edited string: a build with Internode
                // must not show a line that omits it, and a build without it must not name a page that isn't there.
                Gated(B("**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] and [[arcade-internode|Internode]]."),
                      () => ReleaseGates.InternodeOffered),
                Gated(B("**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] and [[arcade-petalpop|Petalpop]]."),
                      () => !ReleaseGates.InternodeOffered),
                Warn("Over a game, the arcade only plays while your controller is **isolated** from it. Otherwise a card explains why and offers **hold {triangle} to play anyway**. Passthru Mode will mean no Arcade games can be played while you're in another game. See [[input-isolation|Input isolation]]."),
                Gated(B("**You can write your own games** and drop them in - see [[custom-arcade-games|Custom Arcade games]]."),
                      () => ReleaseGates.ArcadePackagesOffered),
            ]),

        // ⚠ One topic per game, and every id belongs in ArcadeTopicIds below — a game topic left out of that
        // list survives the build gate and advertises a feature the user cannot reach. A game that is also
        // withheld from a public release carries a `When` on top (Internode), and so does every cross-link to it.
        // The bodies are the arcade topic's own paragraphs, moved word for word: Help translations are keyed
        // on the string, not on the topic, so a verbatim move carries es/de/ja/ar with it and a reworded one
        // silently drops that line back to English until the next batched pass.
        new("arcade-kabloom", "Actions", "Arcade: Kabloom",
            "kabloom arcade minesweeper petal petals flower tile disc bee flag mark question mark cursor reveal solvable no guessing guess solver proof certified baked stacked gem diamond level campaign",
            [
                B("**Kabloom**: Minesweeper logic on a Floret Pentagonal Tiled field of flower petals. Move the cursor with the stick or d-pad. **{cross}** reveals a tile, **{square}** flags where you think there's a bee (or multiple bees, on later levels). Press **{square}** again to increase the flag count, or hold it for a question mark flag. Remaining bees are shown at the bottom of the screen. At the center of each floret is a nectar gem, collected when all the petals around it are cleared, which adds to your total score. Every board is solvable with no forced guesses."),
                B("**More about \"no forced guesses\":** Boards 1-10 are generated on the fly and validated by the Solver before play. Every board from level 11 up is baked ahead of time and certified by a complete solver before it ships. Individual petal tiles can hold up to three bees on the later levels. The solver plays the board from every zero-clue petal you could open on. It runs the human patterns first (saturation, subset difference, overlap bounds, chained constraints) and when those run dry it groups the unknown petals that share the same set of clues into boxes, enumerates every way the remaining bees can be spread over each connected group of boxes, and folds in the total bee count so the petals no clue touches get reasoned about too. A level ships only when the certified start-points cover the whole crop, so the first petal you open is always one the proof began from, ensuring every possible start guarantees a solvable board. "),
                B("Part of the [[arcade|Arcade]]."),
            ]),

        new("arcade-connate", "Actions", "Arcade: Connate",
            "connate arcade merge merging numbers number cluster pile rim ring colors colours families bomb charge fire lob orb doubling",
            [
                B("**Connate**: Your craft rides the rim around a cluster of orbs and garbage blocks. Shoot orbs to merge the numbers before the pile grows past the inner ring. **{cross}** fires your held number into the cluster. Hold to fire with more force. Star and Star-Gap pieces merge to make orbs of 3, and matching numbers from 3 up combines their values. Only **matching colors** merge, although mixed-color orbs can be created by matching stars and gaps of opposite colors; these merge with either color or with other mixed-color orbs. Combos charge up a bomb you can fire. Bomb high-value orbs to collect them to your score."),
                B("**Stages**: the pace follows your score. Each time your collected total crosses 100, 250, 450, 700 and 1,000, the shot clock gets a little shorter, garbage arrives a little sooner, and a bomb takes one more combo charge to fill. The current stage is shown under the score. From stage 2, every 48 seconds of play ends with 8 seconds of relief: no garbage, and a longer shot clock."),
                B("Part of the [[arcade|Arcade]]."),
            ]),

        new("arcade-petalpop", "Actions", "Arcade: Petalpop",
            "petal pop petalpop arcade paddle paddles breakout brick bricks ball flower core gold split multiball smash slingshot spring rail square pentagon hexagon heptagon octagon lives win 5-8",
            [
                B("**Petalpop**: a ring of paddles around a flower of petals. Your analog stick controls every paddle together, so be careful! Hold **{cross}** to draw the paddles back like a slingshot, then release to **smash** the ball. Hit the core with a smash shot to clear the level. Pop a blue petal for multi-ball. \n\nFour sides and four levels to start, then five, six, seven and eight. You get one extra life for each size increase. Switch between spring and rail control via the **Start** menu."),
                B("Part of the [[arcade|Arcade]]."),
            ]),

        new("arcade-internode", "Actions", "Arcade: Internode",
            "internode arcade half-pipe halfpipe pipe bike runner token tokens mine mines jump hop gate gold gate quota checkpoint stage bank score camera roll rim wall swing gap break fall",
            [
                B("**Internode**: shoot down a twisting half-pipe and collect tokens while avoiding mines and gaps. Hitting a mine will drop your tokens, and hitting one when carrying no tokens sets you back one stretch. Falling in a gap always sets you back one stretch. **{cross}** jumps. \n\nCatching a full token streak will upgrade the final token in the pattern to a gold 10x token. Reach each checkpoint with enough tokens to bank them in your score, with extra bonuses for passing a stretch on the first try and for collecting every token in a stretch. Insufficient tokens keeps you looping the same stretch until you have enough. The course twists and turns harder every stage. \n\nCamera roll can be enabled/disabled in the **START** menu."),
                B("Part of the [[arcade|Arcade]]."),
            ], When: () => ReleaseGates.InternodeOffered),

        new("app-slices", "Actions", "App & launcher slices",
            "launch focus toggle kill process name override executable path storefront big picture installed apps store uwp",
            [
                B("**Choose the app** two ways: **Browse for App…** picks an `.exe` from disk, and **Installed Apps…** lists everything with a Start-menu entry (including **Microsoft Store apps**, which have no `.exe` to browse to). Either way the icon is pulled in automatically. You can also drag an `.exe`, a shortcut, or a Start-menu app straight into the slice list."),
                B("**Behavior** - **Run** launches the app, or focuses it if it's already running. **Toggle** launches the app or requests a normal close, like clicking its ✕. The hub reports **Close requested**, not a confirmed exit: unsaved-work prompts stay open until you answer them, apps may refuse to close or keep running in the tray, and Toggle leaves windowless processes running."),
                Tip("A **Store app** can't be detected as already-running, so **Run** just re-opens it and **Toggle** won't reliably close it. And an app started through an updater or launcher **stub** may run under a different name than the file you picked, so picking the app's real `.exe` is the reliable choice."),
                B("**Storefront** (launcher slices) - opens the store's big-picture/fullscreen mode. Only Steam and Playnite have a true one; the Xbox app is maximized; the rest just open. A storefront is offered only when its launcher app is actually installed."),
                B("**Installed Game** slices launch the game **directly** where possible. A GOG game runs its own exe even without GOG Galaxy installed; only stores that need their client running (like Steam) will route through the launcher."),
            ]),

        new("controller-mode", "Actions", "Controller mode (Xbox / DualShock)",
            "controller mode xbox dualshock emulation virtual pad game pass xinput button prompts glyphs playstation switch pad type unsupported controller",
            [
                P("Radiata forwards a **virtual controller**, and **Controller mode** decides which kind the game sees: an **Xbox** pad or a **DualShock** pad. Two slices under **System ▸ Controller** flip it - **Toggle Xbox Mode** and **Toggle DualShock Mode**."),
                B("**What it's for:** many games only accept one class of controller. A Game Pass or Xbox-app title that refuses a DualSense controller will allow it if it's in **Xbox Mode** in Radiata. Games that want PlayStation input go the other way."),
                B("**Button prompts follow the mode.** The glyphs a game draws come from the pad it thinks is plugged in, so Xbox Mode gets you **A B X Y** and DualShock Mode **{cross} {circle} {square} {triangle}**."),
                B("Arming the slice shows **On** or **Off** in the hub, so you can check which mode you're in without changing it."),
                B("The switch is instant and stays put until you flip it back, but the game sees a controller swap at that moment. **Flip it before launching the game** for best results."),
                B("**A wired Xbox pad stays an Xbox pad.** If your physical controller is an Xbox pad on USB, or a USB wireless dongle, the game always gets a virtual Xbox pad and DualShock Mode cannot change it."),
                Warn("This requires installing the isolation drivers. There's no virtual pad to switch without them, and firing the slice puts up an on-screen notice saying so. In [[passthru-mode|Passthru Mode]] the game reads your real controller, so the mode has nothing to change. See [[input-isolation|Input isolation]]."),
            ]),

        new("switch-audio", "Actions", "Switch Audio Output",
            "switch audio output device mic microphone speakers headset cycle default endpoint",
            [
                P("**Switch Audio Output** changes the Windows default audio device(s). Both fields match by **partial name**, case-insensitively - \"Speakers\" matches \"Speakers (Realtek…)\". The **▾ button** beside each field lists your connected devices, and a new slice starts pre-filled with your current defaults."),
                B("**Output Device** - the playback device to switch to. **Blank = cycle** through your outputs on each fire (unless a Mic Device is set, which leaves the output alone)."),
                B("**Mic Device** - the recording device to switch to. **Blank = leave the mic unchanged.**"),
                Tip("Set both fields to switch output + mic in one slice - \"TV + no mic\", \"Headset + headset mic\". Firing shows the device switched to in the hub."),
            ]),

        new("open-uri", "Actions", "Open URI",
            "uri url link scheme https steam discord ms-settings deep link protocol",
            [
                P("An **Open URI** slice hands its value to Windows to open with whatever handles that scheme. That covers a lot more than web links:"),
                B("**Web URLs** - `https://twitch.tv/yourchannel` opens in your default browser."),
                B("**App deep links** - `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…` - anything an installed app registers a protocol for."),
                B("**Windows pages** - `ms-settings:display` opens that Settings page."),
                B("The value must be a complete, absolute URI. A bare `twitch.tv/...` won't launch - include the `https://`."),
                Tip("Game-launch URIs (`steam://rungameid/…`) work here too, but the **Installed Game** slice type builds them for you, which is easier."),
            ]),

        // The slice editor's Custom ▸ Key Combo Help chip lands here. The key list documents
        // KeypressSender.KeyMap/ModVk — keep the two in step when keys are added.
        // ⚠ Never write a literal backtick in Help text: ParseInlines scans single backticks as
        // `code` spans and has no escape, so one stray backtick shifts every span after it. Name such keys
        // by their KeyMap word alias (Backtick, Slash) instead.
        new("key-combo", "Actions", "Key Combo (send a keyboard shortcut)",
            "key combo keypress keyboard shortcut hotkey send keys format grammar win ctrl alt shift del delete media volup voldown mute playpause next prev navy blue modifier plus",
            [
                P("A **Key Combo** slice (Custom) presses a keyboard shortcut for you. Write it as key names joined with **`+`** - `Win+D`, `Ctrl+Shift+Esc`, `Alt+F4` - or a single key like `PlayPause`. The **last** name is the key pressed; everything before it is a modifier held around it. Names aren't case-sensitive."),
                Fig("key-combo-anatomy", "In a combo, the last name is the key that actually gets pressed and everything before it is a modifier held down around it."),
                B("**Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`."),
                B("**Keys:** letters and digits; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; the arrows `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` and `Slash`; and the **media keys** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop` - media keys work on their own, no modifier needed."),
                B("The combo is sent as real keystrokes. One thing can block it: an app running **as administrator** won't accept keystrokes from Radiata. This is a Windows limitation."),
            ]),

        new("discord-setup", "Actions", "Discord voice-channel setup",
            "discord credentials client id secret oauth voice join leave mute deafen keybind",
            [
                P("**Launch Discord** works out of the box. **Join/Leave Voice Channel**, **Deafen** and **Mute Me** talk to Discord directly, so those three need your own free Discord application credentials. Until they exist the slice editor shows a **Configure Discord Integration** button, and the same wizard sits in **Settings ▸ Advanced**; firing one of those slices from the wheel opens it too, rather than doing nothing."),
                P("**Deafen** and **Mute Me** toggle Discord's own switches - the same ones the headphone and microphone buttons at the bottom-left of Discord flip - and they work while a game has focus. No keybind to set up, and the game never sees a keystroke."),
                P("To get the **channel link**: in Discord, right-click the voice channel → **Copy Link**, and paste it into the slice's **Discord URL** field (Radiata normalizes it to the `discord://` form)."),
                Tip("Discord must be running. These slices talk to the Discord app on your PC, not to Discord's website. "),
            ]),

        // The id keeps its old name: [[links]] and the translation maps are keyed by it.
        new("steam-xbox-voice", "Actions", "Steam voice chat",
            "steam chat friends voice mic mute push to talk hotkey open limits deafen join leave",
            [
                P("The **Steam Chat** group does everything Steam allows a third-party app to do, which is less than Discord allows. Discord provides a local control channel that Radiata's Join/Leave slice uses; **Steam provides none**. What you can do:"),
                B("**Open Steam Chat** - opens Steam's **Friends & Chat** window. Join a group's voice channel from there. Steam gives another app no way to join, leave or switch voice channels, and has no mute-incoming-voice control at all."),
                B("**Mic Mute** - in the group, and the same slice as System ▸ Audio. It mutes your **Windows microphone**, which is what Steam transmits from, so the others can't hear you. Every other app loses the mic as well, since Steam exposes no app-scoped mute. Live Muted/Unmuted state shows in the hub."),
                Tip("For full voice control from a slice - join, leave, mute, deafen - Discord remains the best-supported option; see [[discord-setup|Discord voice-channel setup]]."),
            ]),

        new("obs-studio", "Actions", "OBS Studio",
            "obs studio websocket streaming recording replay buffer scene source mute setup port password",
            [
                P("**OBS Studio** ([obsproject.com](https://obsproject.com)) is the free, open-source program that dominates the Twitch and YouTube streaming space, and also allows you to record the screen. It builds a broadcast out of **scenes** - named layouts of game capture, camera, mic and overlays - that you switch between while live."),
                P("**OBS Studio** slices - Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute… - drive OBS over the **obs-websocket** protocol."),
                B("**One-time setup:** in OBS, **Tools ▸ WebSocket Server Settings** ▸ enable the server, then copy its **port** (default `4455`) and **password** into **Settings ▸ Advanced ▸ Integrations ▸ Configure OBS Integration…**. It's one shared setting, and **Test** confirms the connection on the spot. Until it's set up, OBS slices arm as **\"Configure in Settings\"**, and an OBS slice's editor offers the same setup pane."),
                B("**Scene** and **audio-source** names on the slice must match OBS exactly."),
                B("**What's supported:** **OBS Studio 28 or later** (the WebSocket server is built in) and **OBS 27 or earlier with the obs-websocket 5.x plugin**. Forks that speak the same protocol (**StreamElements OBS.Live**, for one) work identically. **Streamlabs Desktop does NOT** - it's a different app without obs-websocket."),
            ]),

        // (No "vendor-recording" topic: the NVIDIA/AMD capture actions are out of the taxonomy — legacy
        // slices silently no-op. Game Bar capture remains and is covered in action-types.)

        new("text-chat", "Actions", "Text Chat (send a message into a game)",
            "text chat message send game keybind enter t y chat button custom quick phrase gg glhf cooldown",
            [
                P("A **Text Chat** slice (Chat & Streaming) types a message into the running game's text chat: it presses the game's **chat-open key**, types your text, and presses Enter."),
                B("**Chat Button** - **Try Game Default** looks up the chat key for whatever game is running **each time the slice fires**, from a built-in index of around 1,000 PC games, so the same slice works across multiple games. **Custom…** lets you enter the key yourself, so use it for games with remapped or unusual chat keys, or for a game the index doesn't cover."),
                B("**Message** - the line of text to send. An empty message (or Custom with no key) arms as **\"Configure in Settings\"**, and firing opens the slice's editor."),
                Warn("**Try Game Default sends nothing unless the focused game is in the index.** When the game in front isn't covered, the hub names it and says **\"No chat key default found. Configure in Settings\"** - switch that slice to **Custom…** and set the key."),
                B("**Sends are limited to one per 15 seconds**. Hub shows the remaining cooldown. No spamming, please!"),
                B("**Limits:** the game must be focused and must accept its chat key at that moment."),
            ]),

        // The id keeps its old name: [[links]] and the translation maps are keyed by it. The per-app
        // Volume Mixer itself is not selectable (SystemConfig ▸ the "mixer" token), so it is not described
        // here — only the four modes the picker offers.
        new("volume-mixer", "Tray & Settings", "D-Pad 🡄 🡆",
            "volume mixer balance dpad left right game chat discord music spotify browser desktop song track app session advanced mic microphone input alt-tab task switcher window",
            [
                P("**While a wheel is open, D-Pad 🡄 🡆** does one of four things. Pick in the **Settings ▸ Customize ▸ D-Pad 🡄 🡆** section:"),
                B("**Cycles Windows** (default) - steps through the Alt-Tab switcher, one window per press. The switcher stays up while the wheel is open and lands on the highlighted window when the wheel closes."),
                B("**Cycles Desktops** - switches Windows virtual desktops, the same as **Win+Ctrl+🡄 🡆**."),
                B("**Skips Songs** - the same track-skip keys as the Audio slices; each skip flashes a ⏮ / ⏭ icon in the hub."),
                B("**Mic Volume** - turns your default mic up and down in 5% steps, holding to repeat, and un-mutes it on the way up. The level shows in the hub with a **microphone** icon."),
                Tip("See [[wheel-open-extras|While a wheel is open]] for everything else the D-Pad does with a wheel up."),
            ]),

        // ═══ Controllers & isolation ═════════════════════════════════════════════
        new("supported-controllers", "Controllers & Isolation", "Supported controllers",
            "dualsense edge dualshock ds4 xbox xinput bluetooth usb bleed through shared input",
            [
                B("**Sony family** (DualSense Edge, DualSense, DualShock 4) over Bluetooth or USB: good support with clean input isolation. The **Edge's Fn buttons** are the reference trigger. Haptic triggers and touchpad will not be seen by games due to driver limitations unless you are in Passthru Mode."),
                B("**Third-party Xbox-style pads over Bluetooth** - most present as a DualShock 4 over BT, so they get the full isolation path too."),
                B("**Pads with extra paddles or buttons** (e.g. **8BitDo Ultimate 2C**) - over **Bluetooth** the extra **L4/R4** buttons are read directly, so you can pick them in [[triggers|Settings ▸ Customize ▸ Triggers]] to summon a wheel on their own, or paired with a second button if you also use them in games. They're offered, not assumed: the default stays the standard bumper chord. Over **USB** the extra buttons are invisible to Radiata, except as mapped by the controller's own drivers. So connect over Bluetooth if you want native L4/R4 support, or map them deliberately."),
                Tip("**If you've remapped L4 or R4 on the pad itself** (holding L4/R4 + a button + the mapping key), that paddle now sends the button you assigned and Radiata can no longer see it - so it stops opening wheels. Clear the remap on the pad to get it back, or pick a chord in [[triggers|Settings ▸ Customize ▸ Triggers]] instead."),
                B("**Xbox pads over USB or wireless dongle (XInput)** - isolated with the same cloak, presenting a virtual **Xbox 360** pad to the game. If a pad can't be cloaked for any reason, Radiata falls back automatically to **shared-input mode**: the wheel still works, but the game also sees your input while a wheel is up."),
                B("**Genuine Xbox pads over Bluetooth** - isolated too, with an extra safeguard. Before presenting the stand-in pad, Radiata has a separate helper process **observe** that games really can't see the physical pad any more. If that check can't pass - or can't run - it falls back to shared-input mode instead of guessing, so a hidden pad with no stand-in won't leave you without a working controller."),
                B("**Two or more Xbox pads plugged in at once** - isolation switches off and both pads keep working in shared-input mode, since cloaking would make a second player's controller disappear. Unplug the second pad and isolation comes back on its own."),
                Tip("Isolation needs the drivers (ViGEm + HidHide). Without them every pad runs in shared-input mode (e.g. \"bleed-thru\"). Install from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**. Not sure which mode you're in? Hover the tray icon - see [[input-isolation|Input isolation]]."),
            ]),

        new("input-isolation", "Controllers & Isolation", "Input isolation (what the drivers do)",
            "isolation virtual pad cloak hidhide vigem drivers double input neutral joy.cpl lag latency delay ms milliseconds input lag polling rate overhead rumble",
            [
                P("With the optional isolation drivers installed, Radiata gives games a **virtual controller** - a DualShock 4 for Sony pads, an Xbox 360 pad for Xbox pads or in Xbox Mode - and **cloaks the physical pad** (HidHide) so the game can't see it twice. That's when capture succeeds. Other remappers, existing device access and unsupported input paths can all sabotage full isolation, and without the drivers games keep seeing your controller while a wheel is up. This is how you get bleed-thru."),
                Fig("isolation-modes", "With successful isolation, the game reads the virtual pad; in Passthru Mode or with no drivers installed, the game reads your controller directly and Radiata simply watches alongside it."),
                B("**Which mode am I actually in? Hover the tray icon.** It reads **\"Radiata - isolated\"**, or names the reason it isn't (no drivers, Passthru Mode, a pad that can't be cloaked). When capture drops to shared-input mode you also get an on-screen notice."),
                B("Isolation forwards sticks, triggers, D-Pad and standard buttons. Sony touchpad, gyro, speaker/mic, adaptive triggers, haptics and rumble are not forwarded. Captured Xbox input supports standard two-motor rumble, but not Share or impulse-trigger motors. [[passthru-mode|Passthru Mode]] or quitting requests removal of Radiata's capture; games may need to reconnect or restart, and other remappers can still affect native features."),
                H("Input latency"),
                B("**Sony pads and Xbox pads over Bluetooth** - **imperceptible**. Reports are passed straight through as they arrive."),
                B("**Xbox pads over USB or a wireless dongle (XInput)** - **a few milliseconds at most**, because these have to be polled."),
                B("**Passthru Mode, or no drivers installed** - **none**. The game reads your physical controller directly."),
                B("For scale: a 60 fps game draws a frame every **16.7 ms**. Radiata never injects into or hooks a game, so it adds nothing to rendering or frame pacing."),
                Warn("**While a wheel, Game Grid or editor is on screen, Radiata holds its virtual pad neutral.** If the game still reacts, another input path may be active - follow the [[controller-conflict-checklist|Controller conflict checklist]]. Passthru Mode deliberately lets controller input reach the game."),
                B("For driver repair, HP OMEN buses and version checks, see [[driver-conflicts|HP OMEN & driver version conflicts]]. For busy or hidden-device access, see [[hidhide-troubleshooting|HidHide troubleshooting]]."),
            ]),

        new("passthru-mode", "Controllers & Isolation", "Set Passthru Mode (Bypass Input Isolation)",
            "passthru mode passthrough safe mode bypass input isolation controller interception anticheat valorant vanguard eac battleye exceptions per game automatic capture",
            [
                P("Some competitive titles with kernel anticheat (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) could theoretically react to emulated or hidden devices. **Passthru Mode** bypasses [[input-isolation|input isolation]] entirely: Radiata removes its virtual pad and requests release of its own controller blocks. Other tools may still hide or remap the controller. The wheel still works; the trade-off is that input reaches the game while a wheel is open, which is what \"bleed-through\" or \"double input\" means. Don't confuse it with disabling the **wheels** - that keeps the virtual pad in place and only stops summons. Passthru Mode removes the virtual pad altogether."),
                B("**Global toggle:** the tray's checkable **Passthru Mode** item, or the checkbox on **Settings ▸ Passthru Mode**. The tab appears only when the isolation drivers are installed."),
                B("**Automatic per-game:** add a game to the **Always use Passthru Mode for** list - from the installed-games dropdown, or **Add Application…** for a specific .exe. Radiata enters Passthru Mode while it runs and restores capture on exit; the tray shows \"(auto: <game>)\"."),
                B("Each entry engages **While Running** (recommended - anticheat watches from launch) or **While Frontmost** (only while the game's window is focused)."),
                Warn("Passthru Mode is **best-effort**. Per-game detection polls every ~2 seconds, so a just-launched game can briefly see normal capture. For the strictest titles, toggle Passthru Mode on manually *before* launching. The isolation drivers also stay installed system-wide either way."),
                B("**Switch Passthru Mode on or off between play sessions, not mid-game.** Either direction swaps the controller a running game is reading - the virtual pad for your real one, or back - and most games don't go looking for a new controller once they've started, so the game loses input until it's relaunched."),
            ]),

        // ═══ Tray & Settings ═════════════════════════════════════════════════════
        new("tray-and-settings", "Tray & Settings", "Tray & Settings (mouse/keyboard, at the desk)",
            "tray icon left click right click menu settings tabs f1 f2 test",
            [
                Gated(B("**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Show in Explorer, Start with Windows, Exit)."),
                      () => ReleaseGates.TrayShowInExplorerOffered),
                Gated(B("**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Start with Windows, Exit)."),
                      () => !ReleaseGates.TrayShowInExplorerOffered),
                B("**Test Wheel** - each Wheel tab has a **Test Left/Right Wheel** button under the slice list that opens that wheel on screen (with the wheels enabled)."),
                B("**Settings tabs:** Left Wheel, Right Wheel, **Customize** (look, feel, sound, triggers, the [[volume-mixer|D-Pad 🡄 🡆]] picker and the [[show-labels|Show labels on]] picker), **Passthru Mode** ([[passthru-mode|Passthru Mode]] - shown when the isolation drivers are installed), **Advanced** (a **Current Controller** readout, [[integrations|Integrations]], [[game-grid-options|Game Grid]] housekeeping, [[accessibility|Accessibility]], **Start with Windows**, and [[system-actions|System tools]] incl. backup & reset and **Quit Radiata**), **Help**, and **About**. Settings auto-save; **Ctrl+S** forces a save."),
                B("**Language** - **Settings ▸ Advanced ▸ Language** sets the language for the whole app. Help language switches immediately. Everything else follows at the next launch."),
            ]),

        new("customize", "Tray & Settings", "Customize (look, feel & sound)",
            "customize material light dark flat pearl obsidian mesa gloss terra theme slices thickness ring thick medium thin button icons glyphs best guess sound effects themed material digital physical silent none preview appearance look feel triggers accessibility",
            [
                P("**Settings ▸ Customize** sets how the wheels look, feel and sound. Every pick applies **live**."),
                B("**Material** - the resting-slice look. Eight, in two groups. **Simple** contains solid-color **Flat Light** and **Flat Dark**; **Deluxe** contains the glassy **Pearl** and **Obsidian**, plus four styled looks:"),
                B("**Kawaii** - pastel wedges, each slice a different hue; firing bursts heart-and-star confetti.", 1),
                B("**Salvage** - charcoal slices with a rusted-metal texture, fluorescent-light highlighting, and a stamped plate edge.", 1),
                B("**Mesa** - rounded cream wedges on cracked terracotta; the armed slice lifts like a 3D card.", 1),
                B("**Reactor** - dark hollow wedges; the armed slice lights an animated circuit-board of traces and sparks.", 1),
                B("**Sound Effects** - **Themed** (the default) plays whatever sound set matches the material above. **Digital** and **Physical** are fixed sets if you'd rather pin one, and **Silent** turns the sounds off. Picking a material switches the choice back to Themed."),
                B("On the Kawaii sound set, arming a slice strikes the next note of a xylophone melody, so scrubbing around the ring plays the song. There are 5 melodies... you might recognize a few of them :)"),
                B("**Slice Thickness** - the radial thickness of the slice ring. **Thick** only reads well up to **8 slices**, so a 9th slice on either wheel switches the setting to **Medium** - and it switches back on its own once you're at 8 or fewer again."),
                B("**Button Icons** - the symbols in on-screen prompts: **Best Guess** (the default - follows the connected pad), **PlayStation** (✕ ○ □ △), or **Xbox** (A B X Y)."),
                // The Triggers and Accessibility sections have their own topics — the Help chips beside
                // those headings link straight there. Keep these bullets one-line pointers.
                B("**Triggers** - which controller gestures summon a wheel. See [[triggers|Triggers]]."),
                B("**D-Pad 🡄 🡆** - what left and right on the D-Pad do while a wheel is open. See [[volume-mixer|D-Pad controls]]."),
                B("**Show labels on** - which slices draw their text label. See [[show-labels|Show labels on]]."),
                B("**Accessibility** - activation, wheel sides, both-stick aiming, the hub, Reduce motion and narration - lives on **Settings ▸ Advanced**. See [[accessibility|Accessibility]]."),
                Gated(B("**Make your own material** - drop a theme package into Radiata's Materials folder and it joins the list. See [[custom-materials|Custom materials]]."),
                      () => ReleaseGates.MaterialPackagesOffered),
                Tip("A wheel holds up to **12** slices, but **6-8** is the sweet spot."),
            ]),

        // ═══ Workshop ════════════════════════════════════════════════════════════
        // Every topic here is withheld from a public release (ReleaseGates) along with the package features.
        // The site's /workshop/ page is the long-form guide; these topics are the in-app reference and link to it.
        new("workshop", "Workshop", "Workshop: make your own",
            "workshop make build create author own custom package packages theme material game arcade javascript sample samples template download guide folder restart confirm share tutorial",
            [
                P("Radiata can load things you make yourself: **materials** that restyle the wheel, and **Arcade games** that play in the Arcade's round window. Each one is a **package** - a folder of plain files you can write in any text editor."),
                Gated(B("**Materials** are data only: colors, gradients, a font name, and optionally images and sounds. See [[custom-materials|Custom materials]]."), MaterialsOn),
                Gated(B("**Arcade games** are a `game.json` and one JavaScript file, run in a sandbox. See [[custom-arcade-games|Custom Arcade games]]."), GamesOn),
                P("The **Workshop** on the Radiata website is the full guide: step-by-step walkthroughs, every setting with its range, design advice, and **sample packages to download**. Start there: [getradiata.app/workshop](https://getradiata.app/workshop/)."),
                H("How making a package works"),
                B("**1.** Make a folder for your package - or unzip a sample - inside Radiata's packages folder. Paste the path into File Explorer's address bar to open it:"),
                Gated(B("a material: `%APPDATA%\\Radiata\\Packages\\Materials\\<your theme>`", 1), MaterialsOn),
                Gated(B("a game: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<your game>`", 1), GamesOn),
                B("**2.** Write the manifest (`material.json` or `game.json`) and put every file it names directly inside that folder."),
                B("**3.** **Restart Radiata** - right-click the tray icon, choose **Exit**, then start it again. Packages are read once, at startup."),
                B("**4.** Accept the confirmation Radiata shows for a new or changed package."),
                B("**5.** Try it out: pick the material in **Settings ▸ Customize**, or open the game from the Arcade. Change something, then go back to step 3."),
                Fig("workshop-loop", "Making a package is a loop: every change goes back through a restart and the confirmation before you can try it."),
                Tip("Keep a copy of your work somewhere else too. Radiata reads a package where it sits, but nothing backs it up for you."),
                Warn("**Only install packages from people you trust.** Radiata asks before it loads a package and asks again whenever one changes, but it can't tell you whether a package is any good."),
                B("When a package doesn't show up, or you want to give one to a friend, see [[workshop-sharing|Testing and sharing packages]]."),
            ], When: () => MaterialsOn() || GamesOn()),


        new("custom-materials", "Workshop", "Custom materials (build your own theme)",
            "custom material theme package material.json drop in author make build own skin palette colors colours gradient fill hue walk outline glyph glow texture png jpg sound wav appdata packages folder soundtheme sound set kawaii mesa salvage reactor obsidian digital physical format token consent confirm restart trace log rejected not showing workshop sample starter ember comments drag drop zip install uninstall remove delete recycle bin right-click",
            [
                P("Beyond the eight built-in materials you can drop in **your own theme**. A theme is one folder holding a text file called `material.json` - colors, gradients, a system font name, and optionally images and sounds sitting beside it. Themes are **data only**: the format cannot express code, a network address, or a file outside the theme's own folder, so a theme can restyle the wheel and do nothing else."),
                B("**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has two to download: **Starter**, the smallest complete theme with every line explained, and **Ember**, which uses every block below. The Workshop also covers color, contrast and texture advice this topic leaves out."),
                H("Where it goes"),
                B("One folder per theme under `%APPDATA%\\Radiata\\Packages\\Materials` - for example `…\\Materials\\Lava\\material.json`. Paste that path into Explorer's address bar; Radiata creates the folder on first run."),
                B("That folder also holds a **README.txt** written by Radiata, carrying a copy-paste example of every field. It's the reference; this topic is the tour."),
                B("**Restart Radiata after adding a theme to that folder by hand, or changing one.** Packages are scanned once, at startup, on purpose."),
                B("**Or drag and drop it.** Drop the theme's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. What you dropped stays where it was."),
                B("Dropping a theme you already have asks before replacing it; the old copy goes to the **Recycle Bin**. A changed version takes effect after a restart."),
                B("**To remove a theme,** right-click its tile in **Settings ▸ Customize** and choose **Uninstall theme**. Its folder goes to the Recycle Bin; if it was your material, the wheel switches to **Pearl**. Restore the folder from the Recycle Bin and restart Radiata to get it back."),
                B("The first time Radiata sees a new **or changed** package it asks you to confirm before loading anything. Accepting will show it in **Settings ▸ Customize** in a third group, **Custom**, below Simple and Deluxe."),
                Warn("A package is content from whoever wrote it. **Only install themes from a source you trust**. The confirmation prompt returns whenever any file in the package changes."),
                H("The smallest theme that works"),
                P("A **format 1** manifest is a JSON object with the keys below. `format`, `id`, `name`, `dark`, and a `colors` object holding at least `resting` and `armed` are required; everything else is optional."),
                B("`\"format\": 1` - which manifest version you wrote. Radiata reads **1 to 3**; the richer blocks further down need the higher number."),
                B("`\"id\": \"lava\"` - 2-31 characters of lowercase a-z, 0-9 and `-`, starting with a letter or digit. This is the theme's identity: it becomes the token `custom-lava` in your config, so changing it later makes a **different** theme."),
                B("`\"name\": \"Lava\"` - up to 24 characters; the label on the Customize tile. `\"author\"` (up to 64) is optional."),
                B("`\"dark\": true` - whether the slices are dark. It flips labels and the hub to light ink and picks the dark fallbacks, so getting it wrong shows up as unreadable text rather than a wrong color."),
                B("`\"colors\"` - `\"resting\"` and `\"armed\"` are required; `\"confirm\"` (defaults to the armed color), `\"label\"` and `\"outline\"` are optional. Each is `#RRGGBB` or `#AARRGGBB`, where the leading pair is alpha - `\"#40FFFFFF\"` is a 25%-opaque white outline."),
                B("`\"labelFont\": \"Cascadia Code\"` - optional, and it must be a font **already installed on the PC**. An unknown name is ignored rather than treated as an error. Font *files* inside a package are never supported, deliberately."),
                B("`\"soundTheme\": \"physical\"` - which sounds the wheel makes on your theme. Name a sound set directly - `physical` (the default), `digital`, `kawaii`, `mesa`, `salvage`, `reactor` or `obsidian` - **or name a built-in material** and borrow whatever that one uses, so `\"pearl\"` gives you the digital set and `\"flat-dark\"` the physical set."),
                Tip("Naming the **material** is usually the better choice: your theme keeps sounding like the look you styled it after, even if that look's sounds are retuned in a later release. Naming a set pins it exactly."),
                Tip("`material.json` may contain `//` comments and trailing commas, so you can leave yourself notes. A color can also be written short as `#RGB`."),
                Tip("Custom themes render through Radiata's **flat** slice paths with your colors substituted, so they can't reach the built-in styled materials' procedural effects (Kawaii's confetti, Reactor's circuit board)."),
                H("Richer looks - format 2"),
                P("Set `\"format\": 2` and add any of these optional blocks. Every number is clamped to a safe range, so an extreme value is pulled back rather than rejected."),
                B("`\"fills\"` - a gradient per state (`resting` / `armed` / `confirm`) instead of a flat color. `\"type\"` is `solid`, `bowed` (the glassy Pearl/Obsidian ramp), or `linear` with an `\"angle\"`, plus a list of `\"stops\"` (each an `\"at\"` from 0 to 1 and a `\"color\"`)."),
                B("`\"hueWalk\"` - gives every slice its own hue around the ring, Kawaii-style, from `sat` / `light` / `armedSat` / `armedLight` (0-1) and `hueOffset`. It **overrides** the resting and armed fills."),
                B("`\"outline\"` and `\"armedOutline\"` - `color`, `width`, and an optional `dash` pattern for the slice edge."),
                B("`\"armed\"` - how an armed slice moves: `liftPx` (up to 24), `northPx` (±12), `scale` (1.0-1.15). It's a state treatment rather than continuous motion, so it survives [[accessibility|Reduce motion]]."),
                B("`\"glyph\"` - how slice icons are treated: `edge` (`inner`, `outer` or `none`) with `edgeColor` / `edgeWidth` / `edgeShadow`; a `glow` whose `color` can be the literal `\"slice\"` to take each slice's own accent, with `strength` 0-1; plus `castShadow` and `armedWash`."),
                B("`\"label\"` - `case` (`upper` for stamped all-caps labels) and `sizeMul` (0.8-1.3). `\"gapPx\"` (0-14) sets the gap between slices."),
                B("`\"tile\"` - how the theme's swatch looks on the Customize tab: `edgeColor`, `sheen`, `lifted`, and an optional `texture`."),
                H("Images and sounds - format 3"),
                P("Set `\"format\": 3` to reference files that live **in the theme's own folder**, by bare file name - a path isn't expressible in the format."),
                B("`\"textures\"` - `slice` and `hub` paint over the fill; `backdrop` draws behind the whole wheel. Each takes a `\"file\"` and an `\"opacity\"`, and slice/hub also take `\"tile\": true` to repeat the image at its natural size instead of stretching it. **PNG or JPG, up to 4 MB**; anything wider than 2048px is scaled down as it's decoded."),
                B("`\"sounds\"` - one file per event: `armed`, `fired`, `enableWheels`, `disableWheels`. **WAV only, up to 1 MB and 3 seconds each**; events you leave out keep the paired sound theme's own sound."),
                B("A theme that ships sounds is **badged** on its Customize tile and takes over the **Sound Effects** picker - the Digital and Physical overrides go inactive, while Themed and Silent stay live."),
                B("Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**. Changing *any* file - not just the manifest - re-asks for your confirmation on the next start."),
                H("If your theme doesn't appear"),
                B("**A package loads whole or not at all.** One bad value rejects the theme rather than half-applying it, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped material`."),
                B("The usual causes: a missing `dark` or `colors`, a color that isn't `#RRGGBB` / `#AARRGGBB`, an `id` with capitals or spaces, or a `format` number lower than the blocks you used."),
                B("**A theme you had selected that stops loading** - package removed, or a change you declined - falls back to **Pearl**, quietly. Nothing else in your wheels changes."),
                B("Custom themes are never offered during first-run setup, and the theme folder isn't part of a settings backup - copy the folder itself to move a theme to another PC."),
            ], When: () => ReleaseGates.MaterialPackagesOffered),

        new("custom-arcade-games", "Workshop", "Custom Arcade games - build your own!",
            "custom arcade game package game.json javascript js script write author make build own sandbox jint helper polar disc draw tick input kv hiscore cue sound appdata packages folder restart consent confirm trace log rejected not showing workshop sample firefly strict console error budget tint colour color cabinet preview screenshot nameplate launcher drag drop zip install",
            [
                P("The Arcade also plays **games you write yourself**. One is a folder holding a `game.json` and a single **JavaScript** file. Drop it into Radiata's Arcade Games folder and it plays in the same round window as the built-in games, with the same buttons and the same best-score tracking."),
                B("**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has **Firefly** to download, a short but complete game with every line explained. The Workshop also walks through writing a game step by step."),
                Warn("**A game package contains code.** Radiata asks you to confirm a package before it ever runs, and again whenever any file in it changes - but the sandbox below is a limit on what a game *can* do, not a judgement about whether it's worth running. **Only install games from a source you trust.**"),
                H("Where it goes"),
                B("One folder per game under `%APPDATA%\\Radiata\\Packages\\Arcade Games` - for example `…\\Arcade Games\\Firefly\\game.json` beside `firefly.js`. Radiata creates the folder on first run."),
                B("That folder's **README.txt** is the full API reference, kept current by Radiata itself."),
                B("**Or drag and drop it.** Drop the game's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. A game you already have asks before replacing it, and a changed version takes effect after a restart."),
                B("**Restart Radiata** after adding a game to that folder by hand, or changing one, then accept the confirmation. It shows up in the Arcade picker beside the built-in games, and as a choice when you add an **Arcade** slice. In your config it's the token `pkg-<id>`."),
                H("The manifest"),
                P("`game.json` is a small JSON object. `format`, `id`, `title` and `entry` are required:"),
                B("`\"format\": 1` - the manifest version."),
                B("`\"id\": \"firefly\"` - 1-32 characters of lowercase a-z, 0-9 and `-`. The game's identity, and the `pkg-<id>` token."),
                B("`\"title\": \"Firefly\"` - up to 24 characters, shown in the picker."),
                B("`\"entry\": \"firefly.js\"` - the script, as a **bare file name** in the same folder (no paths), up to **256 KB**."),
                B("`\"howTo\"` - optional, up to **5 lines of 80 characters**, which become the **{triangle}** help card. `{cross}` `{circle}` `{square}` `{triangle}` in a line are replaced with the player's own button glyphs. No lines means no help card."),
                B("`\"tint\": \"#5B8DEF\"` - optional: your cabinet's colour in the **Arcade Launcher**, as `#RGB` or `#RRGGBB`. Leave it out for the plain grey cabinet. Either way, your `title` is printed on the cabinet's nameplate."),
                B("`\"preview\": \"preview.png\"` - optional: the picture on your cabinet's screen until the game has been played, as a **bare PNG or JPG file name** in the same folder. Make it square, with the round playfield filling it."),
                B("`\"badge\": \"badge.png\"` - optional: an illustration for your cabinet's nameplate, drawn to the left of your title the way the built-in cabinets carry theirs. A **PNG with a transparent background**, in its own colours; it overflows the nameplate above and below."),
                B("`\"glyph\": \"glyph.png\"` - optional: your game's icon on its wheel slices. A **PNG** whose transparency is the shape - the wheel colours it like every other slice icon, so draw it in one colour on a transparent background. Without one, drop-in games share a script icon."),
                Tip("Once your game has been played, its cabinet shows the player's own last board instead - Radiata saves it as `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. That file is also the easiest way to make a preview: play your game, close it, and copy the file into your package as `preview.png`."),
                B("Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**."),
                H("How a game runs"),
                B("Your script runs in a **locked-down interpreter inside a separate sandboxed process**: no files, no network, no clipboard, no other programs. Only the functions below exist at all. Memory is capped by Windows at 128 MB."),
                B("Define **`tick(dt)`**, called for every fixed **1/120 second** step, and **`draw()`**, called once per screen frame. Issue drawing commands only from inside `draw()`."),
                B("There's a hard time and instruction budget **per drawn frame**. A script that overruns is stopped and restarted (your saved data survives); one that keeps overrunning ends in a plain card you can back out of. It can slow itself down - it can't slow the PC down."),
                B("**Radiata owns {circle} and {triangle}** - closing the game and the help card - so your script never sees those two buttons."),
                H("Drawing: the playfield is a disc"),
                B("Everything is drawn in **polar coordinates**: `r` runs 0 at the center to 1 at the rim, angles are **degrees** with 0 at 12 o'clock, increasing clockwise. Radiata does the trigonometry and clips to the circle, so a game can't draw outside its window."),
                Fig("polar-playfield", "A game places everything by radius and angle: r runs from 0 at the center to 1 at the rim, and angles are degrees clockwise from 12 o'clock."),
                B("Colors are numbers in **`0xAARRGGBB`** form - alpha first, so `0xFFFF0000` is opaque red."),
                B("The commands, up to **1024 per frame**: `arc(r0, r1, a0, a1, color)` for a ring segment, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` for 3-16 points, and `text(r, a, size, \"str\", color)` for up to 64 characters."),
                H("Input"),
                B("Read-only globals, refreshed every frame: **`stickX`** / **`stickY`** (-1 to 1, with y positive **downward**, matching the screen), **`crossDown`** / **`squareDown`** while held, **`crossPressed`** / **`squarePressed`** true for one frame per press, and **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, also one frame per press."),
                H("Saving, randomness, and sound"),
                B("**`kvSet(\"key\", \"value\")`** and **`kvGet(\"key\")`** are the **only** state that survives closing a game. Contains strings only, keys up to 64 characters, values up to 1024, 4 KB per game in total. Everything else resets on dismiss, so design for it."),
                B("Write the reserved key **`hiscore`** (a whole number as text) to publish a best score to the Arcade picker."),
                B("**`rand()`** returns 0-1 and is seeded per session, so a replay of the same inputs behaves the same way."),
                B("**`cue(\"name\")`** plays one of nine built-in sounds: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Any other name is silent, and there's no way to ship your own audio."),
                H("Things that trip people up"),
                B("Scripts run in **strict mode**, so a variable you forget to declare is an error. There's no `console`, `eval`, timer or `import` - to see a value while you work, draw it with `text()`."),
                B("**Keep every drawing value in range**: radii 0-1, `dot` size up to 0.5, `line` width up to 0.1, `text` size up to 0.3, angles within ±3600. Radiata treats an out-of-range call as a broken game and restarts the script; after three restarts it shows a problem card. Clamp your numbers."),
                B("JavaScript's bit operators (`|`, `&`, `<<`) produce **signed** numbers, and a negative color draws as nothing. Build colors with arithmetic, or finish the expression with `>>> 0`."),
                B("Input is read **once per drawn frame**, but `tick` can run several times in that frame, and each run sees the same `crossPressed`. Make a press count once - the Firefly sample shows a way."),
                B("`text()` centers the string on its point. The last argument of `ring()` is a thin edge color, or `0` for none."),
                B("Each drawn frame gets **2,000,000 statements and 8 ms** for all of its `tick` runs plus `draw`, at most **8** `cue` calls and **16** `kvSet` writes. `kvSet` throws an error when a key or value is too long or the 4 KB store is full."),
                H("If your game doesn't appear"),
                B("**A package loads whole or not at all**, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped arcade game`."),
                B("The usual causes: an `entry` that isn't a plain `.js` file name sitting in the same folder, a script over 256 KB, an `id` with capitals or spaces, or a confirmation that was declined (it only re-asks once the package changes)."),
                B("A game that loaded but misbehaves shows its card in the window rather than an error - and a package you delete simply stops being offered."),
                B("**Script errors** go to the same log: search it for `[Arcade] script` to see the error message."),
            ], When: () => ReleaseGates.ArcadePackagesOffered),

        new("workshop-sharing", "Workshop", "Testing and sharing packages",
            "workshop test testing debug debugging share sharing zip unzip send friend install license trace log skipped error not showing missing folder nested backup move another pc drag drop",
            [
                H("Testing"),
                B("Radiata reads packages **only at startup**: exit from the tray and start it again after every change."),
                B("A change to **any** file in a package brings the confirmation back on the next start. If you decline it, the package stays off until its files change again."),
                B("**Nothing happening?** Open `%APPDATA%\\Radiata\\radiata-trace.log` in a text editor and search for `[Packages] skipped`. Each line names the package folder and the exact problem - a missing field, a value out of range, a file that isn't there."),
                B("**The most common mistake is one folder too many.** Unzipping often makes `Materials\\Lava\\Lava\\material.json`; Radiata looks for the manifest directly inside `Materials\\Lava`. Move the files up a level."),
                Fig("package-folder", "Radiata looks for the manifest directly inside the package's own folder; the extra folder level an unzip often adds hides it."),
                Gated(B("A game that loads but misbehaves writes its script errors to the same log - search for `[Arcade] script`."), GamesOn),
                H("Sharing"),
                B("To share a package, zip the files **inside** its folder (not the folder itself) and name the zip after the package. The person installing it drops the zip onto Radiata's **Settings** window and accepts the confirmation - or uses **Extract All** into their own Materials or Arcade Games folder and restarts Radiata."),
                B("Say what the package is and what it does, and only include images, sounds and code you have the right to share."),
                B("Radiata's license doesn't extend to your package: what you make in these formats is yours to license however you like."),
                Tip("Moving to another PC? Settings backups don't include packages - copy the `Packages` folder across yourself."),
            ], When: () => MaterialsOn() || GamesOn()),

        // Triggers and Accessibility are separate from "customize" so each section's Help chip on the
        // Customize tab lands on exactly its own material; "customize" keeps pointer bullets.
        new("triggers", "Tray & Settings", "Triggers (summon chords)",
            "triggers chord builder hold tap add another trigger remove row fn bumper trigger touchpad swipe select start l3 r3 dpad combined summon invoke gesture customize",
            [
                P("**Settings ▸ Customize ▸ Triggers** is the chord builder: which controller gestures summon a wheel. Each row is one live chord - a **button** (Fn or L4/R4 / Bumper / Trigger / Touchpad) paired with how it's **combined** (Trigger, Home, L3/R3, Select/Start, D-Pad L/R, or a touchpad edge-swipe). The options adapt to the detected pad: **Fn** appears for a DualSense Edge, **L4/R4** for a pad with extra buttons on Bluetooth, and **Touchpad** only for pads that have one. Those dedicated buttons need no second button - they open a wheel on their own."),
                B("**+ Add Another Trigger** appends a row; a row's **✕** removes it. Every row stays live at once - up to **three** - and the set is remembered **per controller type**, so an Edge and an Xbox pad each keep their own chords."),
                B("How each chord behaves (hold + tap, which wheel it opens, Hold vs Toggle) is in [[opening-a-wheel|Opening a wheel]]; the both-sides version of a chord toggles the wheels on and off, see [[wheel-open-extras|While a wheel is open]]."),
                Live("Your current setting: open a wheel with **{invoke}**."),
            ]),

        new("accessibility", "Tray & Settings", "Accessibility (Settings ▸ Advanced)",
            "accessibility wheels toggle on off swap left right both sticks either stick one stick ignores opposite stick sidedness aim drift always show hub battery reduce motion confetti fade parallax animation effects still narration speak speech spoken screen reader narrator voice volume system-wide windows narrator settings onboarding blind low vision",
            [
                P("The **Accessibility** section gathers six checkboxes, in **Settings ▸ Advanced**. The same set is offered during first-run setup from the **Accessibility…** button on the Look step:"),
                B("Two of them are **indented under the box that ticks them**: turning on **Wheels toggle on/off** also ticks **Swap left/right**, and turning on **Reduce motion** also ticks **Always show hub**, because each pair works best together. Both children stay yours to tick or clear on their own, and once you set one by hand it stops following its parent."),
                B("**Wheel ignores opposite stick** - normally **either** thumbstick aims (whichever you tilt further), and either stick's click opens [[edit-mode|edit mode]]. This option has each wheel listen to **one** stick only: the free hand's under Hold, the wheel's own side under Toggle. "),
                B("**Wheels toggle on/off** and **Swap left/right** - whether an invoked wheel stays up until you dismiss it, and which wheel each hand opens (see [[opening-a-wheel|Opening a wheel]])."),
                B("**Reduce motion** - stops decorative movement everywhere. No confetti or sparks, no parallax, no zooming or drifting; wheels fade in in place, edit-mode rearranging is instant, and every hold-to-confirm effect becomes the same steady progress arc. Some Arcade features are automatically disabled. Progress meters, selection highlights and state readouts all stay. This setting respects Windows' own **Animation effects** switch as well."),
                B("**Always show hub** - off (the default), the wheel's centre hub appears only when it has something to show. On, the hub is always drawn and also shows the controller battery: a steadier centre to read. **Reduce motion** assumes you want this checked as well, but you can set them independently too."),
                B("**Narration** - speaks what you're doing aloud: which wheel opened, the slice you arm and its current state, hold-to-confirm progress, what a fire actually did, volume levels as you scrub, edit-mode moves, and Game Grid browsing. It works alongside a screen reader."),
                B("**Narration covers the wheel and the Game Grid only** - the overlay surfaces a screen reader can't see. Settings, first-run setup and every other ordinary window are **Windows Narrator's** job, so run Narrator alongside Radiata if you want those read too. Ticking **Narration** offers a button to turn Narrator on; and if Narrator is running when you first set Radiata up, Narration starts on by itself.", 1),
            ]),

        // Its own topic so the picker's Help chip lands on the four modes and nothing else; the Customize
        // topic keeps a one-line pointer here.
        new("show-labels", "Tray & Settings", "Show labels on (slice text)",
            "show labels on slice labels label text names icons not logos standard icons all slices no slices slices i choose show label checkbox unlabelled artwork logo cover png",
            [
                P("**Show labels on** - which slices draw their text label on the wheel."),
                B("**Icons, not Logos** (the default) - only slices showing one of Radiata's built-in icons are labelled. A slice carrying artwork (a game logo, cover art, or a PNG you added) goes unlabelled, assuming the artwork includes or replaces the name."),
                B("**All Slices** - every slice is labelled, artwork ones included."),
                B("**No Slices** - no slice is labelled."),
                B("**Slices I Choose** - each slice's own **Show Label** checkbox decides, and it's the only mode in which that checkbox appears in the slice editor (see [[editor-desktop|Slice editor tricks]]). Every slice is created with **Show Label** unchecked, so switching to this mode starts you from an unlabelled wheel: check the few slices you want named."),
                B("**Editing a wheel is exempt:** in edit mode and its Add picker, non-logo slices always show their labels whatever this is set to."),
                Tip("The setting lives in **Settings ▸ Customize**, directly under the **D-Pad 🡄 🡆** selector, and applies **live** - bring up a wheel to see it."),
            ]),

        new("integrations", "Tray & Settings", "Integrations (SteamGridDB, Discord & OBS)",
            "integrations steamgriddb sgdb api key discord obs websocket port password configure cover art logos",
            [
                P("**Settings ▸ Advanced ▸ Integrations** connects optional external services:"),
                B("**Configure Discord Integration…** - sets the Discord credentials (Client ID/Secret) that Join/Leave Voice Channel slices use, the same wizard the slice editor offers. Stored encrypted (Windows DPAPI) and sent only to Discord."),
                B("**SteamGridDB API key** - unlocks portrait cover art and logos for every storefront's games (the Game Grid's **Select**/**Start** cycling). Get a free key at [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). It's checked when you finish entering it, and entering your first key automatically fills in covers skipped while you had none."),
                B("**OBS Studio** - **Configure OBS Integration…** sets the WebSocket port and password, one connection shared by every OBS slice; see [[obs-studio|OBS slices]]. **Test** checks it against a running OBS. The password is stored encrypted (DPAPI)."),
                B("With [[playnite|Playnite]] and a SteamGridDB key both set up, a **Prefer Playnite covers** toggle appears in the **Game Grid** section. Without Playnite, an **Install Playnite…** button appears here instead."),
            ]),

        new("playnite", "Tray & Settings", "Playnite (optional library manager)",
            "playnite library manager games covers metadata optional install third party emulators",
            [
                P("**Playnite** is a free, open-source game-library manager for Windows ([playnite.link](https://playnite.link)) that gathers all your games - Steam, Epic, GOG, Xbox, emulators, standalone - with metadata and cover art."),
                P("Radiata works fully **without** it, scanning your storefronts directly. Playnite is an optional enhancement:"),
                B("**More games found** - Radiata reads Playnite's library, so emulated and manually-added games a raw storefront scan misses can appear in the Game Grid."),
                B("**Curated cover art** - Playnite's own covers become an art source, with a **Prefer Playnite covers** toggle (Advanced ▸ Game Grid) to favor them over SteamGridDB."),
                B("Radiata only ever **reads** Playnite's local database. Playnite does not need to be running."),
                Tip("Not installed? **Install Playnite…** in Advanced ▸ Integrations opens its download page. Set up your libraries in Playnite and Radiata picks them up automatically."),
            ]),

        new("system-actions", "Tray & Settings", "System tools & Backup (Settings ▸ Advanced)",
            "run first run setup onboarding wizard reset starter slices customize recommended install repair drivers recover controller setup email log hid diagnostics quit exit backup restore reset wipe zip factory defaults undo clean install always show hub practice reduce motion check for updates automatic update skip version",
            [
                P("**Settings ▸ Advanced ▸ System** holds the update controls, the **Start with Windows** toggle, the **Troubleshooting** dropdown, and **Quit Radiata**:"),
                B("**Recover Controller** - the ↻ button beside the **Current Controller** name. This is a soft input reset for a wedged pad. If the pad stays silent afterwards, turn it off (hold its home button until the light goes out), turn it back on, and reconnect it — a controller whose input has frozen at the device only comes back from a power-cycle."),
                B("**Battery** - beside the **Current Controller** heading, the same reading the wheel hub shows. PlayStation pads report a percentage (in 10% steps); Xbox-compatible pads only report four coarse levels. Nothing shows until the pad has reported a level."),
                B("**Quit Radiata** - releases its virtual controller and requests removal of the HidHide blocks Radiata owns. Another tool's blocks remain; see [[hidhide-troubleshooting|HidHide troubleshooting]] if the pad stays hidden."),
                H("The Troubleshooting dropdown"),
                B("**Run First-Run Setup…** - re-runs the setup wizard (controller check, drivers, look, cover art, starter wheels). Your customized wheels are never overwritten without asking."),
                B("**Install/Repair Drivers…** - installs or repairs the isolation drivers (ViGEmBus + HidHide). Fixes most isolation problems, and shows a result log."),
                B("**HID Diagnostics…** - a live view of the raw controller reports Radiata reads. Useful when support asks what your pad is actually sending."),
                B("**Controller Setup…** - re-runs the controller detection and mapping wizard on its own, without the rest of first-run setup."),
                B("**Email Log to Developer…** - saves a diagnostic ZIP to your Desktop and opens an addressed email. Review the ZIP before attaching and sending it: logs can include device identifiers, account names in file paths, and application or game names. Radiata does not automatically send the attachment."),
                B("**Back Up Settings…** and **Restore Settings…** sit lower in the same dropdown, and the **resets** below them - all described under Backup & reset."),
                H("Backup & reset"),
                B("**Back Up Settings…** - saves everything that makes Radiata yours (wheels, colors, settings, and your Game Grid cover/logo picks) to a .zip in `Documents\\Radiata Backups`."),
                B("**Restore Settings…** - replaces settings and art picks only after validation and a successful save, then restarts Radiata. Automatic recovery backups are encrypted for your Windows account; restore them through this command. Exported ZIPs contain readable settings and artwork, but protected credentials may need re-entry on another account or PC."),
                B("**Reset All Settings…** - factory defaults for wheels, colors and settings; first-run setup runs again on the next launch. Cached art survives."),
                B("**Wipe App Data and Reset…** - deletes everything in `%APPDATA%\\Radiata`, including automatic backups. Only backups saved outside that folder survive. Export a settings ZIP first if you want. Radiata restarts after a successful reset."),
                B("**Uninstall Radiata…** - removes the startup entry, the HidHide registration, and Radiata's own files, with OFF-by-default opt-ins for the shared drivers and your settings. Anything else in Radiata's folder is left alone."),
                H("Updates"),
                B("**Check for Updates** - asks `getradiata.app/update` for a newer version right now. When one is found the button becomes **Install Update** and opens the update prompt."),
                B("**Automatic** - the checkbox beside the button: when on, the same check runs at startup and once a day. Found updates announce themselves with an on-screen notice (click it to open the update) and a line in this tab; a version you choose to **skip** stops announcing itself, though the line here still shows it. **Skip This Version** is offered only after you have pressed **Later** on that version once."),
                Warn("Resets can't be undone - **back up first**."),
                Tip("The game-art buttons live in their own **Game Grid** section - see [[game-grid-options|Game Grid options]]."),
            ]),

        new("game-grid-options", "Game Grid", "Game Grid options (Settings ▸ Advanced)",
            "game grid options clear game art cache retry missing game art reset hidden games unhide covers redownload",
            [
                P("**Settings ▸ Advanced ▸ Game Grid** collects the grid's housekeeping buttons:"),
                B("**Retry Missing Game Art** - re-attempts only the covers and logos that came up empty, keeping everything already downloaded and every cover you picked by hand (see [[cover-art|Cover art & logos]])."),
                B("**Clear Game Art Cache** - deletes ALL cached covers, so everything re-downloads. **Images you dropped onto a slice are kept.** Try **Retry Missing Game Art** first if you only want to fill blanks."),
                B("**Reset Hidden Games** - brings back every game and storefront you hid with **hold {square}** (see [[storefronts|Hiding a storefront]])."),
                B("**Prefer Playnite covers** - shown when [[playnite|Playnite]] and a SteamGridDB key are both set up: favors Playnite's own cover art."),
            ]),

        new("storefronts", "Game Grid", "Hiding a storefront",
            "storefront steam epic gog xbox battle.net amazon itch ubisoft ea hide exclude opt out include reset hidden games launcher card",
            [
                P("A whole storefront can be hidden from the [[game-grid|Game Grid]], the same way a single game can."),
                B("**Filter to the store with L1 / R1**, then **hold {square}** on its **Open <store>** card. A notice asks **\"Hide <store> and all its games in Radiata?\"** - **{cross}** hides it, **{circle}** cancels."),
                B("Hiding a store **hides its games** in the Game Grid, the [[edit-mode|Add picker]], and the starter wheels the first-run wizard suggests."),
                B("Bring it back with **Settings ▸ Advanced ▸ Game Grid ▸** [[game-grid-options|Reset Hidden Games]], which un-hides storefronts as well as games."),
                B("Newly installed storefronts appear **automatically** the next time Radiata scans."),
                Warn("Only a store with its own **Open <store>** card can be hidden this way. A [[playnite|Playnite]] game you added by hand, or one from a third-party Playnite plugin, carries no storefront card. Hide those games individually."),
            ]),

        // ("backup-reset" is merged into "system-actions", mirroring the Settings tab; the old topic id's
        // deep-link (SystemEditorControl.HelpBackupReset_Click) lands on system-actions.)

        // ═══ Troubleshooting ═════════════════════════════════════════════════════
        new("controller-not-detected", "Troubleshooting", "Controller not detected",
            "controller dead not detected blind hidhide lockout whitelist recover reset bluetooth radio frozen stuck wedge",
            [
                B("**Replug / re-pair first.** Bluetooth stacks occasionally wedge, and power-cycling the pad fixes most one-offs."),
                B("**Bluetooth pad connected but frozen** (Windows still lists it, input never moves)? That's a Windows Bluetooth wedge that power-cycling the pad **won't** fix - toggle the PC's **Bluetooth off and on** instead. Radiata shows a \"toggle Bluetooth\" notification when it spots this."),
                B("**Settings ▸ Advanced** shows a ↻ button beside the Current Controller name. This does a soft input reset - drops and reopens the HID stream - without restarting Radiata."),
                B("**HidHide lockout:** if the cloak hides the pad while Radiata isn't on its allow-list, Radiata goes blind. That means no input, and the Current Controller readout shows nothing even though Windows sees the pad. Radiata catches this at startup and offers a one-click fix via a clickable on-screen notice; repair can help with registration, but it does not cure every access problem. See [[hidhide-troubleshooting|HidHide troubleshooting]]."),
                B("**Peer controller tools** can remap or hide the controller and create additional outputs. Installed software alone does not establish a conflict; see [[controller-tool-conflicts|reWASD, DS4Windows & other tools]]."),
            ]),

        new("controller-conflict-checklist", "Troubleshooting", "Controller conflict checklist",
            "double input duplicate bleed through wrong pad player slot remote play diagnostic log",
            [
                P("Use this sequence when one press moves a menu twice, the game reacts beneath a wheel, the wrong controller responds, or input disappears. Change one thing at a time and test between changes."),
                B("**1. Save and exit the game.** Controller mode, Passthru Mode, remapper output changes and reconnects can all replace the device a game is using. Relaunch after the test setup is stable."),
                B("**2. Establish a simple baseline.** Temporarily use one controller and one connection (USB, Bluetooth or receiver). Disable other tools' remapping and automatic profile switching; close their tray agents and HidHide configuration windows. For a local test, end unused streaming sessions that create virtual pads."),
                B("**3. Check Radiata first.** Read **Settings ▸ Advanced ▸ Current Controller** and hover its tray icon for isolation status. No controller points to detection or hiding; a working wheel with game input underneath points to isolation or another input path."),
                B("**4. Reconnect in a controlled order.** With the game and competing readers closed, start Radiata, connect the controller, and wait for its status to settle. Start Steam or the launcher afterwards, then the game. A reader that opened the device before cloaking may retain access until it closes or the device reconnects."),
                Fig("startup-order", "Start Radiata before anything else that reads the controller, connect the pad and let its status settle, then open Steam or your launcher, and the game last."),
                B("**5. Test in a safe game menu.** Opening a wheel should stop Radiata's virtual pad from driving the game until the wheel closes. Windows' `joy.cpl` can help identify extra controllers, but one entry there does not prove isolation in every game or input API."),
                B("**6. Restore other tools one at a time.** The combination that brings back the symptom is useful evidence. Reading a controller and successfully isolating it are separate things: Radiata does not provide a general physical-device-to-XInput-slot picker."),
                Tip("For comparison, enable [[passthru-mode|Passthru Mode]] before launching the game. Other tools can still hide or remap the device, and wheel input reaching the game is expected in this mode. Disabling the wheels alone does not release capture."),
                H("What to include in a support report"),
                B("Record Windows and Radiata versions, controller model and transport, controller mode, tray status, driver versions, other tools and active profiles, startup order, and the first step that changes the result. Include whether input works with Radiata closed and in Passthru Mode."),
                B("Use **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to Developer…** to prepare a diagnostic ZIP. Review it before attaching it; it may contain device identifiers and personal paths. Include the actual error text from driver setup or HID Diagnostics."),
            ]),

        new("controller-tool-conflicts", "Troubleshooting", "reWASD, DS4Windows & other controller tools",
            "rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapper conflict virtual controller duplicate xbox slot autodetect",
            [
                P("Two tools can read one pad and produce two outputs, or one can hide the pad from the other. Start with one active remapper for that controller. Coexistence depends on versions, hiding rules, transport and game."),
                H("reWASD"),
                B("Turn **Remap OFF** for the affected device or group and pause **Autodetect** for the test. Closing the main window does not necessarily stop mappings. Check the tray agent and confirm its virtual output is gone before retesting. See [reWASD Tray Agent](https://help.rewasd.com/interface/tray-agent.html)."),
                B("reWASD has its own virtual-device and hiding settings. Repairing ViGEmBus or adding Radiata to HidHide cannot fix every reWASD visibility rule. Record which devices remain visible; consult [reWASD troubleshooting](https://help.rewasd.com/faq/troubleshooting.html) for its own errors."),
                H("DS4Windows, DSX and InputMapper"),
                B("Stop controller output and fully exit the tool, including its tray process, for the baseline. Check automatic startup and profiles if it returns. Another virtual Xbox or DualShock controller can cause duplicate actions or change which device the game selects."),
                B("If the setup uses HidHide, physical-device blocks can remain after the remapper exits. Check Radiata's access using [[hidhide-troubleshooting|HidHide troubleshooting]]. Radiata does not take ownership of another tool's existing blocks, so quitting Radiata does not clear them."),
                H("Other sources of input"),
                B("**JoyToKey, AntiMicroX, macros and hardware profiles** can emit keyboard or mouse events alongside gamepad input. Neutralizing Radiata's virtual pad does not neutralize those events. Disable the mapping or controller [[turbo-mode|Turbo mode]] for the test."),
                B("**x360ce, vJoy-based tools, streaming clients and vendor utilities** can add controllers or translation layers. Check Steam Remote Play, Sunshine/Moonlight, Parsec and controller software when relevant. End only unused sessions; a remote player's virtual controller may be their only input."),
                B("**Old HidGuardian or ScpToolkit installations** can leave filtering or replacement drivers behind. Use the original project's removal guidance or support; do not delete arbitrary HID devices, Bluetooth drivers or registry filters. HidHide and HidGuardian are different components."),
                B("**Wrong player or no spare Xbox slot?** XInput exposes four slots, which may include virtual pads. Temporarily stop unused virtual outputs and reconnect in the intended order. If Radiata reports uncertainty about its own output, wait for reconnection or quit and reopen Radiata; reinstalling drivers is not the first fix."),
                Tip("If another remapper is essential, test it with Radiata in [[passthru-mode|Passthru Mode]] first. That avoids a second Radiata stand-in, but it does not promise isolation or preservation of native controller features through the other tool."),
            ]),

        new("hidhide-troubleshooting", "Troubleshooting", "HidHide: contention, lockouts & shared settings",
            "hidhide contention busy access denied configuration client cli lockout whitelist allow list inverse cloak path moved renamed usb bluetooth shared hidden controller recovery",
            [
                H("Busy or access-failed status"),
                P("The **HidHide Configuration Client** holds the driver's exclusive configuration connection while it's open. A running or stuck **HidHideCLI** can also contend with Radiata. Close those tools completely, then allow about **15 seconds** for Radiata's retry before trying **Recover Controller**. Contention does not mean the driver needs reinstalling."),
                H("Windows sees the controller, but Radiata does not"),
                B("Close the game and quit Radiata before inspecting HidHide. In normal mode, the **Applications** list grants access to hidden controllers. Verify the exact `Radiata.exe` you launch is listed - installed, portable, renamed and moved copies all have different paths. Radiata normally registers itself; **Install/Repair Drivers…** can repair registration, subject to its reported result."),
                B("Check the selected physical device on **Devices**. USB and Bluetooth can have separate entries. Do not hide the virtual stand-in the game needs. Use [Nefarius's setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) to identify the device, and close the client before restarting Radiata."),
                B("**Inverse application cloak reverses the list's meaning.** Radiata preserves this shared setting and declines capture when it is enabled. Record the configuration and coordinate with the tool that needs it before choosing normal mode for Radiata; changing it affects other applications too."),
                H("The game still receives input"),
                B("An application allowed through HidHide can still read the physical device. Review entries deliberately; do not add the game, Steam, or every executable as a general fix for double input."),
                B("After a fresh install, reconnect the controller or restart Windows if requested, so the filter can attach. Restart readers that opened the device before cloaking. Configuration readback alone does not verify what a running game receives."),
                B("HidHide has limitations, including some Raw Input readers and Xbox/XInput configurations. If leakage survives a clean startup, record the game and transport rather than assuming a successful hide operation guarantees exclusive input. See the [HidHide FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/)."),
                H("The controller stays hidden after exit"),
                B("Radiata adopts existing hidden-device entries matching the controller model it manages, including entries from another connection method, so they can be released on exit. This can also release another tool's matching entries; you should never have two tools manage hiding for the same controller. Uninstall clears all hidden-device entries unless another Radiata copy is running. A failed release must be recovered before removal can finish."),
                Warn("Keep keyboard and mouse access available while changing controller visibility. Record existing settings first, change only the identified controller or application entry, and close the configuration client before retesting."),
            ]),

        new("driver-conflicts", "Troubleshooting", "HP OMEN & driver version conflicts",
            "hp omen gaming hub fusion vigem vigembus foreign fork driver version mismatch 10.x 1.22.0 1.5.230 oculus virtual desktop repair install bus device manager restart",
            [
                P("**ViGEmBus creates the virtual controller; HidHide controls access to the physical one.** A working driver of one kind does not establish that the other works. Check the driver result log and Radiata's isolation status before repeating an installer."),
                H("HP OMEN Gaming Hub / OMEN Fusion"),
                B("Some HP OMEN systems have a vendor-modified ViGEmBus, and Radiata can connect to that bus instead of the correct one. A reported **10.x** version can be HP's old fork, not a newer compatible Nefarius driver."),
                B("Radiata names detected foreign buses and skips installing over them. Its driver removal also leaves another program's bus in place. Repeated **Install/Repair Drivers…** attempts will not switch an HP-owned bus to Nefarius's."),
                B("Follow [Nefarius's HP OMEN guidance](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) and its linked [HP issue and switching instructions](https://github.com/nefarius/ViGEmBus/issues/99), or contact HP. Disabling the vendor bus can break dependent OMEN features; Radiata does not perform the device or registry changes for you."),
                B("A foreign bus that refuses a virtual DualShock 4 may make Radiata fall back to an **Xbox 360 stand-in for that session**. Unexpected Xbox prompts can therefore be a driver clue rather than a changed glyph setting. This fallback is not a compatibility guarantee."),
                H("Versions and duplicate buses"),
                B("This Radiata build bundles **ViGEmBus 1.22.0** and **HidHide 1.5.230**. ViGEmBus is retired; 1.22.0 is its final official release. Installer, application, client-library and driver versions are different numbers - they are not supposed to match each other."),
                B("Open **Device Manager ▸ View ▸ Devices by connection** and inspect virtual gamepad bus entries. Record each bus's name, provider, driver version and device status. Multiple buses, unexpected providers, or a version different from the one Radiata bundles all warrant investigation; a higher number alone does not prove compatibility."),
                B("**Oculus and Virtual Desktop** setups can also supply their own buses. Several virtual gamepads beneath one bus are different from several competing bus drivers. Identify the owning program before changing either."),
                B("For an ordinary missing or older bundled driver, use **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers…**, review its result, and complete any requested restart. Close HidHide tools first. If repair fails, keep the error text and driver versions for support."),
                Warn("Driver removal affects every application using that shared component. Use the [official ViGEmBus install/remove guide](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) for a confirmed conflict. Its full purge is advanced recovery, not a first step for double input. Do not force-delete unrelated drivers or use unofficial download sites."),
            ]),

        new("overlay-not-visible", "Troubleshooting", "Wheel not visible over a game",
            "overlay fullscreen borderless windowed exclusive primary display monitor uac",
            [
                B("**Run games Borderless Windowed**, not exclusive fullscreen. Exclusive fullscreen bypasses the compositor Radiata draws through. The setting is in most games' display options, and the performance difference on Windows 10/11 is negligible."),
                B("Radiata draws on the **primary display only** - on a multi-monitor rig, make your gaming display the Windows primary (Settings ▸ System ▸ Display)."),
                B("Windows-secured screens (UAC prompts, the lock screen) can never be drawn over. That's a Windows limitation, and nothing can work around it."),
            ]),

        new("steam-conflicts", "Troubleshooting", "Steam Input & Steam quirks",
            "steam playstation controller support big picture guide magnifier chord double input unlock controller",
            [
                P("Steam Input can translate a controller into gamepad, keyboard or mouse input. Another mapping layer can change prompts and bindings or create duplicate actions. Test per game before changing global settings."),
                B("**Wrong buttons or duplicate actions?** With the game closed, open its **Steam Library ▸ Properties ▸ Controller** and try **Disable Steam Input** in the per-game override. Relaunch and compare; restore the previous setting if the game or remote setup needs Steam Input. Global options are under **Steam ▸ Settings ▸ Controller**, with names that vary by Steam version."),
                B("**No input with Steam Input disabled?** The game may not support Radiata's virtual DualShock controller. For a Sony pad, try [[controller-mode|Xbox Mode]] before launching, or restore Steam Input. That's a game compatibility choice, not necessarily a driver failure."),
                B("**Double input despite a successful cloak?** Steam may have opened the physical controller before Radiata hid it. Save and close Steam games before fully exiting Steam, then start Radiata and let capture settle before reopening Steam. Reconnecting the pad can also release stale handles. Follow any controller-unblock notice; closing Steam's window alone may leave it running."),
                B("**Desktop keys or mouse movement?** Check Steam's **Desktop Layout** and **Guide Button Chord** layout as well as the game's layout. These can emit input outside the game. See [[controller-conflict-checklist|Controller conflict checklist]] for a controlled comparison."),
                Tip("**Steam Remote Play may rely on Steam Input.** Keep a local fallback before changing its input path. Valve explains the translation layer in [Steam Input gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)."),
                B("**Windows Magnifier opens by itself?** That's Steam's *Guide Button Chord* layout (Guide + face button), not Radiata - and powering a pad off by holding the PS button can leave that layout latched. One clean Guide press-and-release clears it; disable it under Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout."),
            ]),

        new("turbo-mode", "Troubleshooting", "Controller Turbo / rapid-fire mode",
            "turbo rapid fire auto repeat macro cycling flicker wheel closes dismiss premature bounce double input jitter",
            [
                P("Many third-party pads have a hardware **Turbo** / rapid-fire mode that auto-repeats a held button. An accidental button combo can toggle it on in the controller firmware, and that can look exactly like a bug:"),
                B("The wheel **flickers open and shut**, or **dismisses on its own** right after opening."),
                B("The Game Grid's **filters cycle rapidly**, or the selection jumps on its own."),
                B("Slices **fire the instant a wheel opens**, or a hold-to-confirm slice never settles."),
                Tip("Turn Turbo off on the controller itself - usually a button combo (often Home/Guide + a face or shoulder button, or a dedicated Turbo button), frequently with its own LED. Check your pad's manual for the exact combo."),
                B("If it persists with Turbo confirmed off, it's something else - see [[opening-a-wheel|Opening a wheel]] and [[picking-an-action|Aiming & firing]]."),
            ]),

        new("common-issues", "Troubleshooting", "Other common issues",
            "hdr unavailable rdp remote play streaming config json double input",
            [
                B("**HDR shows `Unavailable`** - the display state can't be read in that context, such as in Remote Play or streaming."),
                B("**Editing `config.json` by hand** (`%APPDATA%\\Radiata`) - supported. The app hot-reloads its own writes reliably, but outside edits are occasionally missed, so restart Radiata after manual edits."),
            ]),
    ];

    /// <summary>The topics the app shows and <c>--export-controls</c> writes.
    ///
    /// <para>Feature-gated topics are dropped here, which is the one place that has to know: the Help tab,
    /// the search index, the cross-reference resolver and the CONTROLS.md exporter all read this.</para>
    ///
    /// <para>A property keyed on <c>Arcade.Available</c>, not a static readonly field: the harness conceals
    /// Arcade through <c>Arcade.ConcealedForHarness</c> to prove the topic set follows the gate. Cached and
    /// rebuilt only when <c>Arcade.Available</c> moves, since this is walked by every Help search
    /// keystroke.</para></summary>
    public static IReadOnlyList<HelpTopic> Topics
    {
        get
        {
            var gate = (Arcade.Available, ReleaseGates.PreviewPublic);
            if (_topics is not null && _topicsGate == gate) return _topics;
            _topicsGate = gate;
            return _topics = Filter(Arcade.Available);
        }
    }

    private static IReadOnlyList<HelpTopic>? _topics;
    private static (bool, bool) _topicsGate;

    /// <summary>Topics for the generated doc (CONTROLS.md, the site pages). Reads the build const
    /// (<c>Arcade.Enabled</c>) directly rather than <c>Arcade.Available</c>: the doc is a generic build
    /// artifact, not a per-user view — the same reason the export resolves chord tokens to generic defaults
    /// instead of one machine's chords — so the harness's runtime concealment must not reach it. The
    /// <see cref="ReleaseGates"/> predicates follow the build the same way, because nothing in the app ever
    /// sets <see cref="ReleaseGates.PreviewPublic"/>; the public site pages are therefore exported from a
    /// public build (docs/LOCALIZATION.md ▸ Web Help site contract).</summary>
    public static IReadOnlyList<HelpTopic> ExportTopics => Filter(Arcade.Enabled);

    /// <summary>The authored set with no gate applied — every string any build can show. This is what the
    /// translation tooling walks (<c>HelpLocalization.SourceStrings</c>), so a gated topic's translations
    /// are maintained while the feature is withheld and nothing is re-translated when it returns. Never
    /// render from this.</summary>
    public static IReadOnlyList<HelpTopic> AllAuthored => Authored;

    /// <summary>Apply both gates: the Arcade id list, then each topic's and block's <c>When</c>. A topic
    /// whose body loses a block is returned as a copy, so the authored array is never mutated.</summary>
    private static IReadOnlyList<HelpTopic> Filter(bool arcade)
    {
        var list = new List<HelpTopic>(Authored.Length);
        foreach (var t in Authored)
        {
            if (!arcade && ArcadeTopicIds.Contains(t.Id)) continue;
            if (t.When is { } when && !when()) continue;
            list.Add(t.Body.Any(b => b.When is { } w && !w())
                ? t with { Body = [.. t.Body.Where(b => b.When is null || b.When())] }
                : t);
        }
        return list;
    }

    /// <summary>Every topic that must vanish with the Arcade gate — the feature's own topic and the
    /// package-authoring one. Both filters above read this; a new Arcade topic belongs here too.</summary>
    private static readonly string[] ArcadeTopicIds =
        ["arcade", "arcade-kabloom", "arcade-connate", "arcade-petalpop", "arcade-internode", "custom-arcade-games",
         // Filed under Editing Wheels, but it documents an Arcade slice and goes with the feature.
         "arcade-direct-launch"];

    // ── Token resolution ────────────────────────────────────────────────────────

    /// <summary>Replace {token}s via <paramref name="resolve"/> (unknown tokens are left as-is).</summary>
    public static string ResolveTokens(string text, Func<string, string?> resolve)
    {
        if (!text.Contains('{')) return text;
        var sb = new System.Text.StringBuilder(text.Length + 16);
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('{', i);
            if (open < 0) { sb.Append(text, i, text.Length - i); break; }
            int close = text.IndexOf('}', open + 1);
            if (close < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, open - i);
            var token = text.Substring(open + 1, close - open - 1);
            sb.Append(resolve(token) ?? text.Substring(open, close - open + 1));
            i = close + 1;
        }
        return sb.ToString();
    }

    /// <summary>Generic (doc) token values — PlayStation-style symbols, default-chord phrasing.</summary>
    public static string? ExportToken(string token) => token switch
    {
        "cross" => "✕", "circle" => "○", "square" => "☐", "triangle" => "△",
        "invoke" => "your invocation chord", "disable" => "its both-sides chord",
        _ => null,
    };

    // ── CONTROLS.md generator ───────────────────────────────────────────────────

    /// <summary>Render every topic (LiveOnly blocks skipped) as the CONTROLS.md document. Invoked by
    /// `Radiata.exe --export-controls [path]` — regenerate + commit whenever topics change.</summary>
    public static string ExportControlsMarkdown()
    {
        var sb = new System.Text.StringBuilder(32 * 1024);
        sb.AppendLine("# Controls & gestures");
        sb.AppendLine();
        sb.AppendLine("<!-- GENERATED from Core/HelpContent.cs (the Settings ▸ Help tab's topics) — do NOT hand-edit.");
        sb.AppendLine("     Regenerate with:  Radiata.exe --export-controls CONTROLS.md  -->");
        sb.AppendLine();
        sb.AppendLine("How the overlay is driven from the controller (and keyboard fallbacks). For the JSON that");
        sb.AppendLine("defines slices and actions, see [CONFIG.md](CONFIG.md). This document is also available");
        sb.AppendLine("in-app: **Settings ▸ Help** (where chord names and button glyphs follow YOUR configuration).");
        foreach (var cat in CategoryOrder)
        {
            foreach (var t in ExportTopics)   // build-gated, not opt-in-gated — see ExportTopics
            {
                if (t.Category != cat) continue;
                sb.AppendLine();
                sb.AppendLine($"## {t.Title}");
                sb.AppendLine();
                foreach (var b in t.Body)
                {
                    if (b.LiveOnly) continue;
                    // Cross-links ([[topic-id|Label]]) are an in-app affordance; the exported doc renders
                    // them as plain bold (the topics are all sections of this one file anyway).
                    var text = System.Text.RegularExpressions.Regex.Replace(
                        ResolveTokens(b.Text, ExportToken),
                        @"\[\[(?:[^\]|]+)\|([^\]]+)\]\]|\[\[([^\]|]+)\]\]", m =>
                            $"**{(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)}**");
                    switch (b.Kind)
                    {
                        case HelpBlockKind.Para:    sb.AppendLine(text); sb.AppendLine(); break;
                        case HelpBlockKind.Bullet:  sb.AppendLine($"{new string(' ', b.Indent * 2)}- {text}"); break;
                        case HelpBlockKind.Tip:     sb.AppendLine(); sb.AppendLine($"> 💡 {text}"); sb.AppendLine(); break;
                        case HelpBlockKind.Warning: sb.AppendLine(); sb.AppendLine($"> ⚠ {text}"); sb.AppendLine(); break;
                        case HelpBlockKind.Heading: sb.AppendLine(); sb.AppendLine($"### {text}"); sb.AppendLine(); break;
                        // The doc is text: a figure contributes its caption, which is authored to read as a
                        // standalone sentence precisely so this line isn't a reference to a missing picture.
                        case HelpBlockKind.Figure:  sb.AppendLine(); sb.AppendLine($"*{text}*"); sb.AppendLine(); break;
                    }
                }
            }
        }
        return sb.ToString().Replace("\r\n", "\n").Replace("\n\n\n", "\n\n");
    }

    /// <summary>Case-insensitive topic search over title, keywords, and body text.</summary>
    public static IEnumerable<HelpTopic> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Topics;
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Topics.Where(t => terms.All(term =>
            t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            || t.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase)
            || t.Body.Any(b => b.Text.Contains(term, StringComparison.OrdinalIgnoreCase))));
    }
}
