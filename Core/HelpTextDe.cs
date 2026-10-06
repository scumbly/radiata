namespace ControllerWheel;

/// <summary>GERMAN Help-tab strings. Keys are the EXACT English text authored in
/// <see cref="HelpContent"/> — copy the C# literal across unchanged (escapes included) when adding an
/// entry, and let anything not listed here fall through to English. Run
/// <c>Radiata.exe --check-help-locales</c> after editing HelpContent.cs to see what needs work, and
/// <c>Radiata.exe --dump-help-locale de</c> to regenerate this file in source order.
/// Conventions (see docs/LOCALIZATION.md): a UI path or label reads in this language, bold, with no English gloss
/// (tools/help-flip.pl applies this; the notice sends support-seekers to English instead); markup (<c>**</c>,
/// <c>`</c>, <c>[[id|label]]</c>, <c>[label](url)</c>) and <c>{tokens}</c> are
/// preserved verbatim, a cross-link's topic id is never translated, and product names stay as they are.
/// <para>Entry ORDER follows <c>HelpLocalization.SourceStrings()</c> — the notice, the Help-pane chrome,
/// the category names, then each topic's title/keywords/blocks, then the figure labels.</para></summary>
internal static class HelpTextDe
{
    internal static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {

        // ── notice ──
        [HelpLocalization.NoticeKey] =
            "**Diese Hilfeseiten und die Oberfläche von Radiata wurden von einem KI-Sprachmodell übersetzt.** Die Übersetzung kann fehlerhaft oder unvollständig sein. Der Entwickler übernimmt keine Verantwortung für Fehler im übersetzten Text; die englische Fassung ist maßgeblich. Supportanfragen können nur auf Englisch beantwortet werden; Nachrichten in anderen Sprachen erhalten keine Antwort. Eine Supportantwort nennt Schaltflächen, Registerkarten und Einstellungen bei ihren englischen Bezeichnungen; stelle Radiata kurz auf Englisch, um ihr zu folgen.",

        // ── chrome ──
        ["Contents"] =
            "Inhalt",
        ["No topics match."] =
            "Keine Themen gefunden.",
        ["Language"] =
            "Sprache",
        ["Search help topics"] =
            "Hilfethemen durchsuchen",
        ["Help topics language"] =
            "Sprache der Hilfethemen",
        ["Back to where you were"] =
            "Zurück zur vorherigen Stelle",
        ["Open this Help topic"] =
            "Dieses Hilfethema öffnen",

        // ── category ──
        ["Welcome"] =
            "Willkommen",
        ["Getting Around"] =
            "Grundlagen der Bedienung",
        ["Editing Wheels"] =
            "Räder bearbeiten",
        ["Game Grid"] =
            "Spieleraster",
        ["Actions"] =
            "Aktionen",
        ["Controllers & Isolation"] =
            "Controller und Isolierung",
        ["Tray & Settings"] =
            "Infobereich und Einstellungen",
        ["Workshop"] =
            "Werkstatt",
        ["Troubleshooting"] =
            "Problembehandlung",

        // ── topic:intro ──
        ["What is Radiata?"] =
            "Was ist Radiata?",
        ["intro welcome about purpose design overview couch overlay start here"] =
            "einführung willkommen über zweck gestaltung überblick sofa overlay hier beginnen",
        ["**Configurable:** each slice's action, icon, color and position, the summon chord, the look, the sounds. Edit at the desk in Settings, or from the couch in the in-wheel editor."] =
            "**Konfigurierbar:** Aktion, Symbol, Farbe und Position jedes Segments, der Aufrufgriff, das Aussehen, die Klänge. Bearbeite am Schreibtisch in den Einstellungen oder von der Couch aus im Editor im Rad.",

        // ── topic:installing ──
        ["Installing Radiata"] =
            "Radiata installieren",
        ["install installing installer setup download smartscreen windows protected your pc unknown publisher unsigned signature certificate antivirus false positive virus admin administrator uac elevation drivers vigem hidhide requirements windows 10 11 x64 arm browser blocked keep discard first run update uninstall remove"] =
            "installieren installation installer setup download smartscreen der computer wurde durch windows geschützt unbekannter herausgeber unsigniert signatur zertifikat antivirus fehlalarm virus admin administrator uac erhöhung treiber vigem hidhide voraussetzungen windows 10 11 x64 arm browser blockiert beibehalten verwerfen erster start aktualisieren deinstallieren entfernen",
        ["What you need"] =
            "Was du brauchst",
        ["**Windows 10 or 11, 64-bit (x64)** on an Intel or AMD PC. Windows on ARM isn't supported."] =
            "**Windows 10 oder 11, 64-Bit (x64)** auf einem Intel- oder AMD-PC. Windows on ARM wird nicht unterstützt.",
        ["A supported controller - see [[supported-controllers|Supported controllers]]."] =
            "Einen unterstützten Controller – siehe [[supported-controllers|Unterstützte Controller]].",
        ["\"Windows protected your PC\""] =
            "\"Der Computer wurde durch Windows geschützt\"",
        ["**In the browser:** if the download itself is blocked, keep it (Chrome and Edge: the **⋯** menu beside the download ▸ **Keep** ▸ **Show more** ▸ **Keep anyway**)."] =
            "**Im Browser:** Wird der Download selbst blockiert, behalte ihn (Chrome und Edge: das Menü **⋯** neben dem Download ▸ **Beibehalten** ▸ **Mehr anzeigen** ▸ **Trotzdem beibehalten**).",
        ["**At the SmartScreen box:** click **More info**, then the **Run anyway** button that appears below it. If there's no **More info** link you might be seeing your browser's warning instead. See the previous step."] =
            "**Im SmartScreen-Feld:** klicke auf **Weitere Informationen** und dann auf die darunter erscheinende Schaltfläche **Trotzdem ausführen**. Wenn es keinen Link **Weitere Informationen** gibt, siehst du womöglich die Warnung deines Browsers. Siehe den vorherigen Schritt.",
        ["What the installer does"] =
            "Was der Installer tut",
        ["First run"] =
            "Erster Start",
        ["Setup opens by itself and walks you through the drivers, a controller check, the look, cover art, and a set of starter wheels. Re-run it any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Run First-Run Setup…** - see [[system-actions|System tools]]."] =
            "Die Einrichtung öffnet sich von selbst und führt dich durch die Treiber, eine Controllerprüfung, das Aussehen, Coverbilder und einen Satz Start-Räder. Starte sie jederzeit erneut über **Einstellungen ▸ Erweitert ▸ Fehlerbehebung ▸ Ersteinrichtung ausführen…** – siehe [[system-actions|Systemwerkzeuge]].",
        ["Updating and uninstalling"] =
            "Aktualisieren und Deinstallieren",

        // ── topic:opening-a-wheel ──
        ["Opening a wheel"] =
            "Ein Rad öffnen",
        ["summon invoke trigger chord fn bumper touchpad swipe hold toggle swap sides activation flip left right"] =
            "aufrufen auslösen Trigger Kombination Fn Bumper Touchpad Wischen halten umschalten Seiten tauschen Aktivierung wechseln links rechts",
        ["Hold the bumper/trigger; the second button is a **tap** that brings the wheel up."] =
            "Halte den Bumper bzw. Trigger gedrückt; die zweite Taste ist ein **Tippen**, das das Rad hervorholt.",
        ["**Fn / L4/R4 and squeeze chords** (Bumper+Trigger, +Home, +Select/Start) - the hand you squeeze opens the **opposite** wheel, so the free hand aims (R1+R2 → Left; L1+L2 → Right). For Select/Start, either button works."] =
            "**Fn / L4/R4 und Drückgriffe** (Bumper+Trigger, +Home, +Select/Start) – die Hand, mit der du drückst, öffnet das **gegenüberliegende** Rad, sodass die freie Hand zielt (R1+R2 → links; L1+L2 → rechts). Bei Select/Start funktioniert jede der beiden Tasten.",
        ["**L3/R3** and **D-Pad L/R** - the clicked stick or the direction determines which wheel; either bumper/trigger is the hold."] =
            "**L3/R3** und **Steuerkreuz links/rechts** – der geklickte Stick bzw. die Richtung bestimmt, welches Rad sich öffnet; jeder Bumper oder Trigger dient als Halten.",
        ["**Touchpad swipe** - in from the left edge → Left wheel, right edge → Right."] =
            "**Touchpad-Wischen** – vom linken Rand nach innen → linkes Rad, vom rechten Rand → rechtes Rad.",
        ["**Swap left/right** ([[accessibility|Accessibility setting]]) reverses all of these."] =
            "**Links/rechts tauschen** (links und rechts tauschen, [[accessibility|Barrierefreiheits-Einstellung]]) kehrt all das um.",
        ["**Activation** is **Hold** (up while held; release fires) or **Toggle** (trigger opens; **{cross} confirms, {circle} cancels**; re-trigger dismisses) - set it in [[accessibility|Accessibility]]. Touchpad swipe is always toggle-style."] =
            "Die **Aktivierung** ist **Halten** (Rad bleibt offen, solange gehalten wird; Loslassen löst aus) oder **Toggle** (die Geste öffnet; **{cross} bestätigt, {circle} bricht ab**; erneutes Auslösen schließt) – einzustellen unter [[accessibility|Barrierefreiheit]]. Das Touchpad-Wischen arbeitet immer im Toggle-Stil.",

        // ── topic:picking-an-action ──
        ["Aiming & firing"] =
            "Zielen und ausführen",
        ["aim arm fire cancel release deadzone sticky esc escape keyboard hub center state toggle mute hdr configure guard confirm dwell sleep reboot shutdown"] =
            "zielen vorwählen ausführen abbrechen loslassen totzone esc tastatur mitte zustand umschalten stummschalten hdr konfigurieren bestätigen halten energiesparmodus neu starten herunterfahren",
        ["Center hub"] =
            "Radmitte",
        ["Arming a **toggle** slice (mic/volume mute, HDR, process toggle) shows its **current state** before you fire, e.g. `Mute Mic / Unmuted`. After firing, the hub shows the new state."] =
            "Beim Anvisieren eines **Umschalt-Segments** (Mikrofon-/Lautstärkestummschaltung, HDR, Prozess umschalten) wird sein **aktueller Zustand** vor dem Auslösen angezeigt, zum Beispiel `Mute Mic / Unmuted`. Nach dem Auslösen zeigt die Nabe den neuen Zustand.",
        ["Hold-to-confirm slices"] =
            "Segmente mit Bestätigung durch Halten",

        // ── topic:wheel-open-extras ──
        ["While a wheel is open"] =
            "Solange ein Rad geöffnet ist",
        ["volume dpad scrub repeat mic microphone alt-tab window switcher desktop song enable disable chord toggle wheels off keyboard arrow esc"] =
            "lautstärke steuerkreuz wiederholen mikrofon alt-tab fensterwechsel desktop titel aktivieren deaktivieren umschalten tastatur pfeiltaste esc",
        ["**Enable / disable the wheels** with the \"both sides\" of your invocation chord, pressed **together**:"] =
            "**Aktiviere oder deaktiviere die Ringe**, indem du beide Seiten deiner Aufrufkombination **gleichzeitig** drückst:",
        ["**Bumper/Trigger + D-Pad** is directional: **D-Pad Up = enable**, **D-Pad Down = disable**."] =
            "**Bumper/Trigger + Steuerkreuz** ist richtungsabhängig: **Steuerkreuz oben = aktivieren**, **Steuerkreuz unten = deaktivieren**.",

        // ── topic:edit-mode ──
        ["Edit mode (in-wheel, controller-only)"] =
            "Bearbeitungsmodus (im Rad, nur mit dem Controller)",
        ["edit stick click move add delete reorder undo redo picker installed game full capacity 12 limit thickness"] =
            "bearbeiten stick drücken verschieben hinzufügen löschen umsortieren rückgängig wiederherstellen auswahl installiertes spiel voll 12 grenze dicke",
        ["**Add:** **{triangle}** opens the category→type **Add picker** ({cross} drills in, {circle} backs out). **Installed Game** opens the [[game-grid|Game Grid]] to pick a game, then returns to edit carrying the slice. Other types drop a slice immediately; free-text types (raw URL / keypress) land as placeholders you finish in Settings."] =
            "**Hinzufügen:** **{triangle}** öffnet den **Hinzufügen-Auswahl** (Kategorie→Typ; {cross} geht hinein, {circle} zurück). **Installiertes Spiel** öffnet das [[game-grid|Spieleraster]] zur Spielauswahl und kehrt anschließend mit dem Segment in den Editor zurück. Andere Typen legen sofort ein Segment an; Freitext-Typen (reine URL / Tastendruck) landen als Platzhalter, die du in Einstellungen fertigstellst.",

        // ── topic:empty-wheel ──
        ["Single-wheel mode"] =
            "Betrieb mit nur einem Rad",
        ["empty disabled free gesture resurrect rebuild"] =
            "leer deaktiviert freie geste wiederherstellen neu aufbauen",
        ["Emptying one wheel turns its side off: the chord that used to open it passes through to the game untouched."] =
            "Einen Ring zu leeren schaltet dessen Seite ab: Die Kombination, die ihn früher geöffnet hat, geht unverändert an das Spiel durch.",
        ["To bring it back: **invoke it (hold the gesture) and click the aiming stick (L3/R3)**. The wheel opens centered, straight into the Add picker. (Toggle-style gestures have no hold, so they allow the stick click for a few seconds after the invoke.)"] =
            "So holst du es zurück: **rufe es auf (Geste halten) und klicke den Zielstick (L3/R3)**. Das Rad öffnet sich zentriert, direkt in der Hinzufügen-Auswahl. (Umschaltartige Gesten haben kein Halten, deshalb erlauben sie den Stick-Klick für einige Sekunden nach dem Aufruf.)",

        // ── topic:arcade-direct-launch ──
        ["Arcade direct-launch (a wheel that is just the Arcade)"] =
            "Arcade-Direktstart (ein Rad, das nur die Arcade ist)",
        ["arcade direct launch shortcut lone only one single slice launcher skip wheel straight cabinets instant gesture dedicated side"] =
            "arcade direktstart verknüpfung einzeln nur ein segment starter rad überspringen direkt automaten sofort geste dediziert seite",
        ["**The arcade opens where that wheel would have been** - the left or right quarter of the screen, the same spot the wheel uses. **{circle}** closes it as usual."] =
            "**Die Arcade öffnet sich dort, wo das Rad gewesen wäre** – im linken oder rechten Viertel des Bildschirms, an derselben Stelle wie das Rad. **{circle}** schließt sie wie gewohnt.",

        // ── topic:editor-desktop ──
        ["Slice editor tricks (Settings, mouse & keyboard)"] =
            "Tipps zum Segment-Editor (Einstellungen, mit Maus und Tastatur)",
        ["drag drop exe lnk shortcut launch slice reorder icon color color ctrl+s save autosave draft revert logo arrows cycle fetch steamgriddb"] =
            "ziehen ablegen exe lnk verknüpfung starten segment umsortieren symbol farbe strg+s speichern automatisches speichern entwurf zurücksetzen logo pfeile durchblättern herunterladen steamgriddb",
        ["**Drag a slice within the list** to reorder."] =
            "**Ziehe ein Segment innerhalb der Liste**, um die Reihenfolge zu ändern.",
        ["**Set a slice's icon and color** in the Icon & Color panel below the label."] =
            "**Symbol und Farbe eines Segments** legst du im Bereich Icon & Color unter der Beschriftung fest.",
        ["**Show Label** toggles the slice's text on the wheel. It appears only when [[show-labels|Show labels on]] is set to **Slices I Choose**."] =
            "**Beschriftung anzeigen** schaltet den Text des Segments auf dem Ring ein oder aus. Die Option erscheint nur, wenn [[show-labels|Beschriftungen anzeigen bei]] auf **Segmente meiner Wahl** steht.",
        ["Artwork on a slice"] =
            "Grafik auf einem Segment",
        ["**A slice's action type locks once you Save it.** To change it, delete the slice and add a new one."] =
            "**Der Aktionstyp eines Segments wird beim Speichern festgelegt.** Um ihn zu ändern, lösche das Segment und füge ein neues hinzu.",

        // ── topic:game-grid ──
        ["Game Grid basics"] =
            "Grundlagen des Spielerasters",
        ["game grid browser launch navigate filter storefront chips footer add wheel pick mode installed assign favorite"] =
            "game grid Spieleraster Browser starten navigieren filtern Store Chips Fußzeile hinzufügen Rad Auswahlmodus installiert zuweisen Favorit",
        ["The Game Grid is a controller-scrollable view of every installed game across your storefronts, sorted **most-recently-launched first**, favorites pinned on top. The button controls are spelled out along the bottom of the grid."] =
            "Das Spieleraster ist eine mit dem Controller scrollbare Ansicht aller installierten Spiele aus deinen Stores, sortiert nach **zuletzt gestartet**, mit oben angehefteten Favoriten. Die Tastenbelegung steht am unteren Rand des Rasters.",
        ["**D-Pad / arrow keys** or the **left stick** move the selection."] =
            "**D-Pad / Pfeiltasten** oder der **linke Stick** bewegen die Auswahl.",
        ["**{cross} / Enter** or a mouse double-click - launch the selected game. **{circle} / Esc** - close the grid."] =
            "**{cross} / Enter** oder ein Maus-Doppelklick – das ausgewählte Spiel starten. **{circle} / Esc** – das Raster schließen.",
        ["**{triangle}** - **favorite** the selected game. Favorites sit in their own row of larger tiles at the top of every view. Press again to un-favorite."] =
            "**{triangle}** – markiert das ausgewählte Spiel als **Favorit**. Favoriten stehen in einer eigenen Reihe größerer Kacheln oben in jeder Ansicht. Erneut drücken hebt die Markierung auf.",
        ["**Hold {square}** - **hide the game from the grid**. Bring hidden games back with **Settings ▸ Advanced ▸ Game Grid ▸ Reset Hidden Games**. The same hold on a storefront's **Open <store>** card hides that whole store - see [[storefronts|Hiding a storefront]]."] =
            "**{square} halten** – **das Spiel im Raster ausblenden**. Ausgeblendete Spiele holst du mit **Einstellungen ▸ Erweitert ▸ Spieleraster ▸ Ausgeblendete Spiele zurücksetzen** zurück. Dasselbe Halten auf der Karte **Open <Store>** blendet den gesamten Store aus – siehe [[storefronts|Einen Store ausblenden]].",
        ["**L1 / R1** (or **PgUp / PgDn**) - cycle the storefront filter. A chip appears for each store you have installed."] =
            "**L1 / R1** (oder **PgUp / PgDn**) – den Store-Filter durchschalten. Für jeden installierten Store erscheint ein Chip.",
        ["**Select** and **Start** - cycle the selected game's **cover** and **logo**; see [[cover-art|Cover art & logos]]."] =
            "**Auswählen** und **Start** – Titelbild und Logo des ausgewählten Spiels durchschalten; siehe [[cover-art|Titelbilder und Logos]].",
        ["Add a game to a wheel"] =
            "Ein Spiel zu einem Rad hinzufügen",
        ["Add games from the **in-wheel editor**: open a wheel → click the aiming stick to enter [[edit-mode|Edit]] → **{triangle} Add → Installed Game**, which opens the grid in **pick mode**. **{cross}** picks the highlighted game and carries it into the editor ({circle} cancels)."] =
            "Spiele fügst du im **Editor im Ring** hinzu: Ring öffnen → Zielstick klicken, um in [[edit-mode|Bearbeiten]] zu gelangen → **{triangle} Hinzufügen → Installiertes Spiel**, wodurch das Raster im **Auswahlmodus** geöffnet wird. **{cross}** übernimmt das markierte Spiel und trägt es in den Editor ({circle} bricht ab).",

        // ── topic:cover-art ──
        ["Cover art & logos"] =
            "Titelbilder und Logos",
        ["cover art logo cycle select start share options steamgriddb sgdb dots spinner flat colors"] =
            "titelbild grafik logo durchblättern select start share options steamgriddb sgdb punkte fortschrittsanzeige einfarbig",
        ["**Start** (Options/Menu) - **cycle the logo overlay** and save it: default logo → up to 2 SteamGridDB alternates → **off** (raw cover) → wrap. A game with no logo art uses its centered title text."] =
            "**Start** (Options/Menu) – **die Logo-Überlagerung durchschalten** und speichern: Standardlogo → bis zu 2 SteamGridDB-Alternativen → **aus** (reines Titelbild) → von vorn. Ein Spiel ohne Logografik verwendet seinen zentrierten Titeltext.",
        ["**Art is fetched once and kept on disk**, so the grid opens instantly and offline after that. A big library fills in over the first few seconds of browsing. Reopen the grid and the stragglers should be there."] =
            "**Grafiken werden einmal geladen und auf der Festplatte behalten**, sodass das Raster danach sofort und offline öffnet. Eine große Bibliothek füllt sich in den ersten Sekunden des Blätterns. Öffne das Raster erneut, und die Nachzügler sollten da sein.",
        ["**A game with no art found is re-checked every couple of weeks** on its own, since art gets added over time. To recheck now, use **Retry Missing Game Art** ([[game-grid-options|Advanced ▸ Game Grid]])."] =
            "**Ein Spiel ohne gefundene Grafik wird alle paar Wochen von selbst erneut geprüft**, da mit der Zeit Grafiken hinzukommen. Um jetzt zu prüfen, nutze **Fehlende Spielgrafiken erneut versuchen** ([[game-grid-options|Erweitert ▸ Spieleraster]]).",

        // ── topic:action-types ──
        ["Action types"] =
            "Aktionstypen",
        ["actions advanced launch keypress key combo volume audio display hdr sleep discord voice text chat url settings xbox mode obs mixer game bar windows"] =
            "aktionen erweitert starten tastendruck tastenkombination lautstärke audio anzeige hdr energiesparmodus discord sprache textchat url einstellungen xbox-modus obs mischer game bar windows",
        ["Each slice runs one action, grouped by the editor's categories:"] =
            "Jedes Segment führt eine Aktion aus, gruppiert nach den Kategorien des Editors:",
        ["**Games & Apps** - installed game (direct launch), the Game Grid, storefront launcher (big-picture), launch/focus an app, **Exit Current App** (closes the frontmost app), and **Game Bar** (open, screenshot, start/stop recording, record the last 30 s, toggle mic - via Windows' Xbox Game Bar)."] =
            "**Spiele & Apps** – installiertes Spiel (Direktstart), das Spieleraster, Store-Starter (Big-Picture-Modus), App starten/fokussieren, **Aktuelle App beenden** (schließt die vorderste App) und **Game Bar** (öffnen, Screenshot, Aufnahme starten/stoppen, die letzten 30 s aufnehmen, Mikrofon umschalten) – über die Xbox Game Bar von Windows.",
        ["**Chat & Streaming** - Discord (launch, join/leave a voice channel, deafen), Steam Chat (open chat) - see [[steam-xbox-voice|Steam voice chat]] - plus **Mic Mute** (the one Windows mic mute, offered in both groups and under System ▸ Audio), [[text-chat|Text Chat]], and **OBS Studio** ([[obs-studio|streaming, recording, replay, scenes, source mute]])."] =
            "**Chat und Streaming** – Discord (starten, einem Sprachkanal beitreten oder ihn verlassen, Ton aus), Steam-Chat (Chat öffnen) – siehe [[steam-xbox-voice|Steam-Sprachchat]] –, dazu **Mikro stumm** (die eine Mikrofonstummschaltung von Windows, in beiden Gruppen und unter System ▸ Audio), [[text-chat|Textchat]] und **OBS Studio** ([[obs-studio|Streaming, Aufnahme, Wiederholung, Szenen, Quelle stumm]]).",
        ["**System** - [[controller-mode|controller mode]] (**Xbox** / **DualShock**); audio (switch output, mute, mic mute, set volume, play/pause, next/previous); display (**Toggle Extend/Clone** and HDR toggle); **Windows** (**Show Desktop** - fire again to put the windows back - and **Empty Recycle Bin**); power (sleep/hibernate/reboot/shut down/log out/lock, and **Power Plan**, which flips between two plans you pick)."] =
            "**System** – [[controller-mode|Controller-Modus]] (**Xbox** / **DualShock**); Audio (Ausgabe wechseln, stummschalten, Mikrofon stummschalten, Lautstärke setzen, Wiedergabe/Pause, weiter/zurück); Anzeige (**Erweitern/Duplizieren umschalten** und HDR umschalten); **Windows** (**Desktop anzeigen** – erneut auslösen stellt die Fenster wieder her – und **Papierkorb leeren**); Energie (Energiesparmodus, Ruhezustand, Neustart, Herunterfahren, Abmelden, Sperren sowie **Energiesparplan**, das zwischen zwei von dir gewählten Energieplänen wechselt).",
        ["**Reboot** has a **Log In after Reboot** checkbox: checked (the default), Windows signs you back in where policy allows. Unchecked is a traditional restart, which can land at the sign-in screen."] =
            "**Neu starten** hat ein Kontrollkästchen **Nach dem Neustart anmelden**: angehakt (Standard) meldet Windows dich wieder an, soweit die Richtlinien es zulassen. Nicht angehakt ist es ein klassischer Neustart, der auf dem Anmeldebildschirm enden kann.",
        ["**Custom** - [[open-uri|Open URI]] and [[key-combo|Key Combo]] (send a keyboard shortcut like `Win+D` or `PlayPause`)."] =
            "**Eigene** (benutzerdefiniert) – [[open-uri|URI öffnen]] und [[key-combo|Tastenkombination]] (sendet ein Tastenkürzel wie `Win+D` oder `PlayPause`).",
        ["**Radiata** - the Game Grid, open Settings, and disable wheels. [[passthru-mode|Passthru Mode]] is not a slice: turn it on or off from the tray or Settings ▸ Passthru Mode."] =
            "**Radiata** – das Spieleraster, Einstellungen öffnen und Räder deaktivieren. [[passthru-mode|Direktmodus]] ist kein Segment: Schalte ihn im Tray oder unter Einstellungen ▸ Direktmodus ein oder aus.",
        ["A slice missing a required value arms as **\"Configure in Settings\"**. Firing it opens that slice's editor."] =
            "Einem Segment, dem ein erforderlicher Wert fehlt, wird **\"In den Einstellungen einrichten\"** angezeigt. Beim Auslösen öffnet sich der Editor dieses Segments.",

        // ── topic:arcade ──
        ["Arcade"] =
            "Arcade",
        ["arcade game games minigame mini-game kabloom connate petal pop twist breakout paddle brick pentagon square hexagon octagon smash win minesweeper merge bee flower bomb picker play waiting loading queue lobby kill time score"] =
            "Arcade Spiel Spiele Minispiel Mini-Spiel Kabloom Connate petal pop Petalpop Kurve Breakout Paddel Stein Fünfeck Quadrat Sechseck Achteck Schmettern gewinnen Minesweeper Minensucher zusammenführen Biene Blume Bombe Auswahl spielen warten Ladebildschirm Warteschlange Lobby Zeit totschlagen Punktzahl",
        ["**{cross}** acts, **{square}** is each game's second action, and the **left stick** aims. The **D-Pad** drives the menus. **{triangle} explains the game you're in**, and **START pauses** with **Resume**, **How to play**, **Reset**, and that game's own settings."] =
            "**{cross}** handelt, **{square}** ist die zweite Aktion jedes Spiels, und der **linke Stick** zielt. Das **D-Pad** steuert die Menüs. **{triangle} erklärt das Spiel, in dem du gerade bist**, und **START pausiert** mit **Weiter**, **Spielanleitung**, **Neu starten** und den eigenen Einstellungen dieses Spiels.",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] and [[arcade-internode|Internode]]."] =
            "**Jedes Spiel hat seine eigene Seite**: [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] und [[arcade-internode|Internode]].",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] and [[arcade-petalpop|Petalpop]]."] =
            "**Jedes Spiel hat seine eigene Seite** – [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] und [[arcade-petalpop|Petalpop]].",
        ["**You can write your own games** and drop them in - see [[custom-arcade-games|Custom Arcade games]]."] =
            "**Du kannst eigene Spiele schreiben** und hinzufügen – siehe [[custom-arcade-games|Eigene Arcade-Spiele]].",

        // ── topic:arcade-kabloom ──
        ["Arcade: Kabloom"] =
            "Arcade: Kabloom",
        ["kabloom arcade minesweeper petal petals flower tile disc bee flag mark question mark cursor reveal solvable no guessing guess solver proof certified baked stacked gem diamond level campaign"] =
            "kabloom arcade minesweeper minensucher blütenblatt blütenblätter blume kachel scheibe biene fahne markierung fragezeichen cursor aufdecken lösbar ohne raten raten löser beweis zertifiziert vorberechnet gestapelt edelstein diamant level kampagne",
        ["Part of the [[arcade|Arcade]]."] =
            "Teil des [[arcade|Arcade]].",

        // ── topic:arcade-connate ──
        ["Arcade: Connate"] =
            "Arcade: Connate",
        ["connate arcade merge merging numbers number cluster pile rim ring colors colours families bomb charge fire lob orb doubling"] =
            "connate arcade verschmelzen zusammenführen zahlen zahl gruppe haufen rand ring farben familien bombe aufladen feuern werfen kugel verdoppeln",

        // ── topic:arcade-petalpop ──
        ["Arcade: Petalpop"] =
            "Arcade: Petalpop",
        ["petal pop petalpop arcade paddle paddles breakout brick bricks ball flower core gold split multiball smash slingshot spring rail square pentagon hexagon heptagon octagon lives win 5-8"] =
            "petal pop petalpop arcade schläger paddle breakout stein steine ball blume kern gold teilen multiball schmettern schleuder feder schiene quadrat fünfeck sechseck siebeneck achteck leben sieg 5-8",

        // ── topic:arcade-internode ──
        ["Arcade: Internode"] =
            "Arcade: Internode",
        ["internode arcade half-pipe halfpipe pipe bike runner token tokens mine mines jump hop gate gold gate quota checkpoint stage bank score camera roll rim wall swing gap break fall"] =
            "internode arcade halfpipe röhre bike motorrad läufer token marke mine minen springen sprung tor goldenes tor soll kontrollpunkt stufe bank punkte kamera rollen rand wand schwung lücke bruch sturz",

        // ── topic:app-slices ──
        ["App & launcher slices"] =
            "Segmente für Apps und Starter",
        ["launch focus toggle kill process name override executable path storefront big picture installed apps store uwp"] =
            "starten vordergrund umschalten beenden prozessname pfad der programmdatei shop großbildmodus installierte anwendungen microsoft store uwp",
        ["**Behavior** - **Run** launches the app, or focuses it if it's already running. **Toggle** launches the app or requests a normal close, like clicking its ✕. The hub reports **Close requested**, not a confirmed exit: unsaved-work prompts stay open until you answer them, apps may refuse to close or keep running in the tray, and Toggle leaves windowless processes running."] =
            "**Verhalten** – **Ausführen** startet die App oder fokussiert sie, wenn sie schon läuft. **Umschalten** startet die App oder fordert ein normales Schließen an, wie ein Klick auf ihr ✕. Die Nabe meldet **Schließen angefordert**, nicht ein bestätigtes Beenden: Abfragen zu ungespeicherter Arbeit bleiben offen, bis du antwortest, Apps können sich weigern zu schließen oder im Infobereich weiterlaufen, und Umschalten lässt fensterlose Prozesse laufen.",
        ["**Storefront** (launcher slices) - opens the store's big-picture/fullscreen mode. Only Steam and Playnite have a true one; the Xbox app is maximized; the rest just open. A storefront is offered only when its launcher app is actually installed."] =
            "**Store** (Launcher-Segmente) – öffnet den Großbild- bzw. Vollbildmodus des Stores. Nur Steam und Playnite haben einen echten; die Xbox-App wird maximiert, der Rest öffnet einfach. Ein Store wird nur angeboten, wenn seine Launcher-App tatsächlich installiert ist.",

        // ── topic:controller-mode ──
        ["Controller mode (Xbox / DualShock)"] =
            "Controller-Modus (Xbox / DualShock)",
        ["controller mode xbox dualshock emulation virtual pad game pass xinput button prompts glyphs playstation switch pad type unsupported controller"] =
            "Controller-Modus Xbox DualShock Emulation virtueller Controller Game Pass XInput Tastensymbole Glyphen PlayStation Padtyp wechseln nicht unterstützter Controller",
        ["Arming the slice shows **On** or **Off** in the hub, so you can check which mode you're in without changing it."] =
            "Beim Anvisieren des Segments zeigt die Nabe **An** oder **Aus**, sodass du prüfen kannst, in welchem Modus du bist, ohne ihn zu ändern.",

        // ── topic:switch-audio ──
        ["Switch Audio Output"] =
            "Audioausgabe wechseln",
        ["switch audio output device mic microphone speakers headset cycle default endpoint"] =
            "wechseln audio ausgabe gerät mikrofon lautsprecher headset durchblättern standard",
        ["**Switch Audio Output** changes the Windows default audio device(s). Both fields match by **partial name**, case-insensitively - \"Speakers\" matches \"Speakers (Realtek…)\". The **▾ button** beside each field lists your connected devices, and a new slice starts pre-filled with your current defaults."] =
            "**Audioausgabe wechseln** wechselt die Windows-Standardaudiogeräte. Beide Felder vergleichen über **Namensteile**, ohne Groß-/Kleinschreibung – \"Speakers\" passt auf \"Speakers (Realtek…)\". Die **Schaltfläche ▾** neben jedem Feld listet deine angeschlossenen Geräte, und ein neues Segment ist mit deinen aktuellen Standardgeräten vorbelegt.",
        ["**Output Device** - the playback device to switch to. **Blank = cycle** through your outputs on each fire (unless a Mic Device is set, which leaves the output alone)."] =
            "**Ausgabegerät** (Wiedergabegerät) – das Wiedergabegerät, auf das gewechselt wird. **Leer = durchschalten** deiner Ausgaben bei jedem Auslösen (es sei denn, ein Mic Device ist gesetzt, dann bleibt die Ausgabe unangetastet).",
        ["**Mic Device** - the recording device to switch to. **Blank = leave the mic unchanged.**"] =
            "**Mikrofon** (Aufnahmegerät) – das Aufnahmegerät, auf das gewechselt wird. **Leer = Mikrofon unverändert lassen.**",
        ["Set both fields to switch output + mic in one slice - \"TV + no mic\", \"Headset + headset mic\". Firing shows the device switched to in the hub."] =
            "Fülle beide Felder aus, um Ausgabe und Mikrofon in einem Segment zu wechseln – \"TV ohne Mikrofon\", \"Headset mit Headset-Mikrofon\". Beim Auslösen zeigt die Nabe das Gerät, zu dem gewechselt wurde.",

        // ── topic:open-uri ──
        ["Open URI"] =
            "URI öffnen",
        ["uri url link scheme https steam discord ms-settings deep link protocol"] =
            "uri url verweis schema https steam discord ms-settings deep link protokoll",
        ["An **Open URI** slice hands its value to Windows to open with whatever handles that scheme. That covers a lot more than web links:"] =
            "Ein Segment **URI öffnen** übergibt seinen Wert an Windows, damit es ihn mit dem zuständigen Programm öffnet. Das umfasst weit mehr als Weblinks:",
        ["**Web URLs** - `https://twitch.tv/yourchannel` opens in your default browser."] =
            "**Web-Adressen** – `https://twitch.tv/deinkanal` öffnet sich in deinem Standardbrowser.",
        ["**App deep links** - `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…` - anything an installed app registers a protocol for."] =
            "**App-Deeplinks** – `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…` – alles, wofür eine installierte App ein Protokoll registriert.",
        ["**Windows pages** - `ms-settings:display` opens that Settings page."] =
            "**Windows-Seiten** – `ms-settings:display` öffnet diese Einstellungsseite.",
        ["The value must be a complete, absolute URI. A bare `twitch.tv/...` won't launch - include the `https://`."] =
            "Der Wert muss ein vollständiger, absoluter URI sein. Ein bloßes `twitch.tv/...` startet nicht – gib das `https://` mit an.",
        ["Game-launch URIs (`steam://rungameid/…`) work here too, but the **Installed Game** slice type builds them for you, which is easier."] =
            "Spielstart-URIs (`steam://rungameid/…`) funktionieren hier ebenfalls, aber der Segmenttyp **Installiertes Spiel** baut sie für dich zusammen, was einfacher ist.",

        // ── topic:key-combo ──
        ["Key Combo (send a keyboard shortcut)"] =
            "Key Combo (ein Tastenkürzel senden)",
        ["key combo keypress keyboard shortcut hotkey send keys format grammar win ctrl alt shift del delete media volup voldown mute playpause next prev navy blue modifier plus"] =
            "Key Combo Tastendruck Tastatur Tastenkürzel Hotkey Tasten senden Format Syntax Win Strg Ctrl Alt Umschalt Shift Entf Entfernen Medientasten VolUp VolDown Stumm PlayPause weiter zurück marineblau Modifikator Plus",
        ["A **Key Combo** slice (Custom) presses a keyboard shortcut for you. Write it as key names joined with **`+`** - `Win+D`, `Ctrl+Shift+Esc`, `Alt+F4` - or a single key like `PlayPause`. The **last** name is the key pressed; everything before it is a modifier held around it. Names aren't case-sensitive."] =
            "Ein Segment **Tastenkombination** (Benutzerdefiniert) drückt für dich ein Tastenkürzel. Schreibe es als Tastennamen, verbunden mit **`+`** – `Win+D`, `Ctrl+Shift+Esc`, `Alt+F4` – oder als einzelne Taste wie `PlayPause`. Der **letzte** Name ist die gedrückte Taste; alles davor ist ein Modifikator, der dabei gehalten wird. Groß- und Kleinschreibung spielt keine Rolle.",
        ["In a combo, the last name is the key that actually gets pressed and everything before it is a modifier held down around it."] =
            "In einer Kombination ist der letzte Name die Taste, die tatsächlich gedrückt wird; alles davor sind Modifikatoren, die währenddessen gehalten werden.",
        ["**Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`."] =
            "**Modifikatoren:** `Ctrl`, `Alt`, `Shift`, `Win`.",
        ["**Keys:** letters and digits; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; the arrows `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` and `Slash`; and the **media keys** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop` - media keys work on their own, no modifier needed."] =
            "**Tasten:** Buchstaben und Ziffern; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; die Pfeile `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` und `Slash`; sowie die **Medientasten** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop` – Medientasten funktionieren allein, ohne Modifikator.",
        ["The combo is sent as real keystrokes. One thing can block it: an app running **as administrator** won't accept keystrokes from Radiata. This is a Windows limitation."] =
            "Die Kombination wird als echte Tastenanschläge gesendet. Nur eines kann sie blockieren: eine App, die **als Administrator** läuft, nimmt keine Tastenanschläge von Radiata an. Das ist eine Einschränkung von Windows.",

        // ── topic:discord-setup ──
        ["Discord voice-channel setup"] =
            "Einrichtung der Discord-Sprachkanäle",
        ["discord credentials client id secret oauth voice join leave mute deafen keybind"] =
            "discord anmeldedaten client id geheimnis oauth sprache beitreten verlassen stummschalten ton aus tastenbelegung",
        ["**Launch Discord** works out of the box. **Join/Leave Voice Channel**, **Deafen** and **Mute Me** talk to Discord directly, so those three need your own free Discord application credentials. Until they exist the slice editor shows a **Configure Discord Integration** button, and the same wizard sits in **Settings ▸ Advanced**; firing one of those slices from the wheel opens it too, rather than doing nothing."] =
            "**Discord starten** funktioniert ohne Weiteres. **Sprachkanal betreten/verlassen**, **Ton aus** und **Mich stummschalten** sprechen direkt mit Discord, deshalb brauchen diese drei deine eigenen kostenlosen Discord-Anwendungsdaten. Solange es sie nicht gibt, zeigt der Segment-Editor eine Schaltfläche **Discord-Integration einrichten**, und derselbe Assistent steckt unter **Einstellungen ▸ Erweitert**; eines dieser Segmente aus dem Rad auszulösen öffnet ihn ebenfalls, statt nichts zu tun.",
        ["**Deafen** and **Mute Me** toggle Discord's own switches - the same ones the headphone and microphone buttons at the bottom-left of Discord flip - and they work while a game has focus. No keybind to set up, and the game never sees a keystroke."] =
            "**Ton aus** und **Mich stummschalten** schalten Discords eigene Schalter um – dieselben, die die Kopfhörer- und Mikrofonschaltflächen unten links in Discord betätigen – und sie funktionieren, während ein Spiel im Vordergrund ist. Keine Tastenbelegung einzurichten, und das Spiel sieht nie einen Tastenanschlag.",
        ["To get the **channel link**: in Discord, right-click the voice channel → **Copy Link**, and paste it into the slice's **Discord URL** field (Radiata normalizes it to the `discord://` form)."] =
            "So erhältst du den **Kanal-Verweis**: Klicke in Discord mit der rechten Maustaste auf den Sprachkanal → **Copy Link** (Link kopieren) und füge ihn in das Feld **Discord-URL** des Segments ein (Radiata wandelt ihn in die Form `discord://` um).",

        // ── topic:steam-xbox-voice ──
        ["Steam voice chat"] =
            "Steam-Sprachchat",
        ["steam chat friends voice mic mute push to talk hotkey open limits deafen join leave"] =
            "steam chat freunde sprache stimme mikrofon stumm push to talk sprechtaste tastenkürzel öffnen grenzen ton aus beitreten verlassen",
        ["The **Steam Chat** group does everything Steam allows a third-party app to do, which is less than Discord allows. Discord provides a local control channel that Radiata's Join/Leave slice uses; **Steam provides none**. What you can do:"] =
            "Die Gruppe **Steam-Chat** kann alles, was Steam einer Drittanbieter-App erlaubt, und das ist weniger als bei Discord. Discord bietet einen lokalen Steuerkanal, den Radiatas Betreten/Verlassen-Segment nutzt; **Steam bietet keinen**. Möglich ist:",
        ["**Open Steam Chat** - opens Steam's **Friends & Chat** window. Join a group's voice channel from there. Steam gives another app no way to join, leave or switch voice channels, and has no mute-incoming-voice control at all."] =
            "**Steam-Chat öffnen** – öffnet das Fenster **Freunde & Chat** von Steam. Von dort kannst du dem Sprachkanal einer Gruppe beitreten. Steam gibt einer anderen App keine Möglichkeit, Sprachkanäle zu betreten, zu verlassen oder zu wechseln, und hat überhaupt keine Steuerung, um eingehende Sprache stummzuschalten.",
        ["**Mic Mute** - in the group, and the same slice as System ▸ Audio. It mutes your **Windows microphone**, which is what Steam transmits from, so the others can't hear you. Every other app loses the mic as well, since Steam exposes no app-scoped mute. Live Muted/Unmuted state shows in the hub."] =
            "**Mikrofon stumm** – in der Gruppe, und dasselbe Segment wie unter System ▸ Audio. Es schaltet dein **Windows-Mikrofon** stumm, von dem Steam überträgt, sodass dich die anderen nicht hören. Jede andere App verliert das Mikrofon ebenfalls, da Steam keine app-bezogene Stummschaltung anbietet. Der Zustand stumm/aktiv wird live in der Nabe angezeigt.",
        ["For full voice control from a slice - join, leave, mute, deafen - Discord remains the best-supported option; see [[discord-setup|Discord voice-channel setup]]."] =
            "Für vollständige Sprachsteuerung aus einem Segment heraus – beitreten, verlassen, stummschalten, Ton aus – bleibt Discord die am besten unterstützte Option; siehe [[discord-setup|Einrichtung des Discord-Sprachkanals]].",

        // ── topic:obs-studio ──
        ["OBS Studio"] =
            "OBS Studio",
        ["obs studio websocket streaming recording replay buffer scene source mute setup port password"] =
            "obs studio websocket streaming aufnahme wiederholungspuffer szene quelle stummschalten einrichtung port kennwort",
        ["**OBS Studio** slices - Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute… - drive OBS over the **obs-websocket** protocol."] =
            "Die **OBS Studio**-Segmente – Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute… – steuern OBS über das Protokoll **obs-websocket**.",
        ["**One-time setup:** in OBS, **Tools ▸ WebSocket Server Settings** ▸ enable the server, then copy its **port** (default `4455`) and **password** into **Settings ▸ Advanced ▸ Integrations ▸ Configure OBS Integration…**. It's one shared setting, and **Test** confirms the connection on the spot. Until it's set up, OBS slices arm as **\"Configure in Settings\"**, and an OBS slice's editor offers the same setup pane."] =
            "**Einmalige Einrichtung:** in OBS **Werkzeuge ▸ WebSocket-Servereinstellungen** ▸ Server aktivieren, dann **Port** (Standard `4455`) und **Passwort** nach **Einstellungen ▸ Erweitert ▸ Integrationen ▸ OBS-Integration einrichten…** kopieren. Es ist eine einzige gemeinsame Einstellung, und **Testen** bestätigt die Verbindung auf der Stelle. Bis sie eingerichtet ist, werden OBS-Segmente als **\"In den Einstellungen einrichten\"** anvisiert, und der Editor eines OBS-Segments bietet dasselbe Einrichtungsfeld.",
        ["**Scene** and **audio-source** names on the slice must match OBS exactly."] =
            "Die im Segment angegebenen Namen von **Szene** und **Audioquelle** müssen genau denen in OBS entsprechen.",
        ["**What's supported:** **OBS Studio 28 or later** (the WebSocket server is built in) and **OBS 27 or earlier with the obs-websocket 5.x plugin**. Forks that speak the same protocol (**StreamElements OBS.Live**, for one) work identically. **Streamlabs Desktop does NOT** - it's a different app without obs-websocket."] =
            "**Was unterstützt wird:** **OBS Studio 28 oder neuer** (der WebSocket-Server ist eingebaut) und **OBS 27 oder älter mit dem Plug-in obs-websocket 5.x**. Forks, die dasselbe Protokoll sprechen (etwa **StreamElements OBS.Live**), funktionieren genauso. **Streamlabs Desktop NICHT** – das ist eine andere App, ohne obs-websocket.",

        // ── topic:text-chat ──
        ["Text Chat (send a message into a game)"] =
            "Text Chat (eine Nachricht in ein Spiel senden)",
        ["text chat message send game keybind enter t y chat button custom quick phrase gg glhf cooldown"] =
            "Textchat Nachricht senden Spiel Tastenkürzel Enter t y Chat-Taste benutzerdefiniert Schnellphrase gg glhf Abklingzeit",
        ["**Try Game Default sends nothing unless the focused game is in the index.** When the game in front isn't covered, the hub names it and says **\"No chat key default found. Configure in Settings\"** - switch that slice to **Custom…** and set the key."] =
            "**Spielstandard versuchen sendet nichts, wenn das Spiel im Vordergrund nicht im Verzeichnis steht.** Ist das vordere Spiel nicht abgedeckt, nennt die Nabe es und meldet **\"Keine Standard-Chat-Taste gefunden. In den Einstellungen festlegen\"** – stelle dieses Segment auf **Benutzerdefiniert…** um und lege die Taste fest.",
        ["**Limits:** the game must be focused and must accept its chat key at that moment."] =
            "**Grenzen:** das Spiel muss im Vordergrund sein und seine Chat-Taste in diesem Moment annehmen.",

        // ── topic:volume-mixer ──
        ["D-Pad 🡄 🡆"] =
            "Steuerkreuz 🡄 🡆",
        ["volume mixer balance dpad left right game chat discord music spotify browser desktop song track app session advanced mic microphone input alt-tab task switcher window"] =
            "lautstärkemischer balance steuerkreuz links rechts spiel chat discord musik spotify browser desktop titel sitzung erweitert mikrofon eingang alt-tab fensterwechsel",
        ["**While a wheel is open, D-Pad 🡄 🡆** does one of four things. Pick in the **Settings ▸ Customize ▸ D-Pad 🡄 🡆** section:"] =
            "**Während ein Rad offen ist, macht das Steuerkreuz 🡄 🡆** eines von vier Dingen. Wähle im Abschnitt **Einstellungen ▸ Anpassen ▸ Steuerkreuz 🡄 🡆**:",
        ["**Cycles Windows** (default) - steps through the Alt-Tab switcher, one window per press. The switcher stays up while the wheel is open and lands on the highlighted window when the wheel closes."] =
            "**Wechselt Fenster** (Fenster durchschalten, Standard) – schaltet durch den Alt-Tab-Wechsler, ein Fenster pro Druck. Der Wechsler bleibt sichtbar, solange das Rad offen ist, und landet beim Schließen des Rades auf dem hervorgehobenen Fenster.",
        ["**Cycles Desktops** - switches Windows virtual desktops, the same as **Win+Ctrl+🡄 🡆**."] =
            "**Wechselt Desktops** – schaltet zwischen den virtuellen Desktops von Windows um, genau wie **Win+Ctrl+🡄 🡆**.",
        ["**Skips Songs** - the same track-skip keys as the Audio slices; each skip flashes a ⏮ / ⏭ icon in the hub."] =
            "**Springt zwischen Titeln** (Titel überspringen) – dieselben Titelsprung-Tasten wie die Audio-Segmente; jeder Sprung blendet kurz ein ⏮ / ⏭ in der Nabe ein.",
        ["**Mic Volume** - turns your default mic up and down in 5% steps, holding to repeat, and un-mutes it on the way up. The level shows in the hub with a **microphone** icon."] =
            "**Mikrofonlautstärke** – regelt dein Standardmikrofon in 5-%-Schritten hoch und runter, mit Wiederholung beim Halten, und hebt beim Hochregeln die Stummschaltung auf. Der Pegel erscheint mit einem **Mikrofon**-Symbol in der Nabe.",
        ["See [[wheel-open-extras|While a wheel is open]] for everything else the D-Pad does with a wheel up."] =
            "Alles Weitere, was das D-Pad bei offenem Rad tut, steht unter [[wheel-open-extras|Während ein Rad offen ist]].",

        // ── topic:supported-controllers ──
        ["Supported controllers"] =
            "Unterstützte Controller",
        ["dualsense edge dualshock ds4 xbox xinput bluetooth usb bleed through shared input"] =
            "dualsense edge dualshock ds4 xbox xinput bluetooth usb durchschlagen geteilte eingabe",
        ["**Third-party Xbox-style pads over Bluetooth** - most present as a DualShock 4 over BT, so they get the full isolation path too."] =
            "**Xbox-artige Controller von Drittanbietern über Bluetooth** – die meisten geben sich über BT als DualShock 4 aus und erhalten damit ebenfalls den vollen Isolationsweg.",
        ["**If you've remapped L4 or R4 on the pad itself** (holding L4/R4 + a button + the mapping key), that paddle now sends the button you assigned and Radiata can no longer see it - so it stops opening wheels. Clear the remap on the pad to get it back, or pick a chord in [[triggers|Settings ▸ Customize ▸ Triggers]] instead."] =
            "**Wenn du L4 oder R4 auf dem Pad selbst umbelegt hast** (L4/R4 + eine Taste + die Belegungstaste halten), sendet dieses Paddle jetzt die zugewiesene Taste und Radiata kann es nicht mehr sehen – es öffnet also keine Räder mehr. Lösche die Umbelegung auf dem Pad, um es zurückzubekommen, oder wähle stattdessen einen Akkord in [[triggers|Einstellungen ▸ Anpassen ▸ Aufrufgesten]].",
        ["**Xbox pads over USB or wireless dongle (XInput)** - isolated with the same cloak, presenting a virtual **Xbox 360** pad to the game. If a pad can't be cloaked for any reason, Radiata falls back automatically to **shared-input mode**: the wheel still works, but the game also sees your input while a wheel is up."] =
            "**Xbox-Controller über USB oder Funkadapter (XInput)** – werden mit derselben Tarnung isoliert und dem Spiel als virtueller **Xbox 360**-Controller präsentiert. Lässt sich ein Controller aus irgendeinem Grund nicht tarnen, fällt Radiata automatisch auf den **Modus mit geteilter Eingabe** zurück: Das Rad funktioniert weiter, aber das Spiel sieht deine Eingabe ebenfalls, solange ein Rad offen ist.",
        ["**Two or more Xbox pads plugged in at once** - isolation switches off and both pads keep working in shared-input mode, since cloaking would make a second player's controller disappear. Unplug the second pad and isolation comes back on its own."] =
            "**Zwei oder mehr gleichzeitig angeschlossene Xbox-Controller** – die Isolation schaltet sich ab und beide Controller funktionieren im Modus mit geteilter Eingabe weiter, denn eine Verdeckung würde den Controller eines zweiten Spielers verschwinden lassen. Ziehe den zweiten Controller ab, und die Isolation kommt von selbst zurück.",

        // ── topic:input-isolation ──
        ["Input isolation (what the drivers do)"] =
            "Eingabeisolierung (was die Treiber bewirken)",
        ["isolation virtual pad cloak hidhide vigem drivers double input neutral joy.cpl lag latency delay ms milliseconds input lag polling rate overhead rumble"] =
            "Isolation virtueller Controller Verdeckung HidHide ViGEm Treiber doppelte Eingabe neutral joy.cpl Verzögerung Latenz ms Millisekunden Abfragerate Overhead Vibration",
        ["With successful isolation, the game reads the virtual pad; in Passthru Mode or with no drivers installed, the game reads your controller directly and Radiata simply watches alongside it."] =
            "Bei erfolgreicher Isolation liest das Spiel den virtuellen Controller; im Direktmodus oder ohne installierte Treiber liest das Spiel deinen Controller direkt, und Radiata schaut lediglich daneben zu.",
        ["Isolation forwards sticks, triggers, D-Pad and standard buttons. Sony touchpad, gyro, speaker/mic, adaptive triggers, haptics and rumble are not forwarded. Captured Xbox input supports standard two-motor rumble, but not Share or impulse-trigger motors. [[passthru-mode|Passthru Mode]] or quitting requests removal of Radiata's capture; games may need to reconnect or restart, and other remappers can still affect native features."] =
            "Die Isolation leitet Sticks, Trigger, Steuerkreuz und Standardtasten weiter. Touchpad, Gyroskop, Lautsprecher/Mikrofon, adaptive Trigger, Haptik und Vibration von Sony-Controllern werden nicht weitergeleitet. Erfasste Xbox-Eingabe unterstützt die übliche Vibration mit zwei Motoren, aber nicht Share oder die Impuls-Trigger-Motoren. Der [[passthru-mode|Direktmodus]] oder das Beenden fordern die Aufhebung von Radiatas Erfassung an; Spiele müssen sich unter Umständen neu verbinden oder neu starten, und andere Remapper können native Funktionen weiterhin beeinflussen.",
        ["Input latency"] =
            "Eingabelatenz",
        ["**Sony pads and Xbox pads over Bluetooth** - **imperceptible**. Reports are passed straight through as they arrive."] =
            "**Sony-Controller und Xbox-Controller über Bluetooth** – **nicht wahrnehmbar**. Die Berichte werden direkt durchgereicht, sobald sie eintreffen.",
        ["**Xbox pads over USB or a wireless dongle (XInput)** - **a few milliseconds at most**, because these have to be polled."] =
            "**Xbox-Controller über USB oder Funkadapter (XInput)** – **höchstens ein paar Millisekunden**, weil diese abgefragt werden müssen.",
        ["**Passthru Mode, or no drivers installed** - **none**. The game reads your physical controller directly."] =
            "**Direktmodus, oder keine Treiber installiert** – **keine**. Das Spiel liest deinen physischen Controller direkt.",
        ["For scale: a 60 fps game draws a frame every **16.7 ms**. Radiata never injects into or hooks a game, so it adds nothing to rendering or frame pacing."] =
            "Zur Einordnung: Ein Spiel mit 60 fps zeichnet alle **16,7 ms** ein Bild. Radiata injiziert nie Code in ein Spiel und hängt sich nie ein, trägt also nichts zu Rendering oder Frame-Pacing bei.",
        ["**While a wheel, Game Grid or editor is on screen, Radiata holds its virtual pad neutral.** If the game still reacts, another input path may be active - follow the [[controller-conflict-checklist|Controller conflict checklist]]. Passthru Mode deliberately lets controller input reach the game."] =
            "**Solange ein Rad, das Spieleraster oder der Editor auf dem Bildschirm ist, hält Radiata seinen virtuellen Controller neutral.** Reagiert das Spiel trotzdem, ist womöglich ein anderer Eingabepfad aktiv – arbeite die [[controller-conflict-checklist|Checkliste für Controller-Konflikte]] durch. Der Direktmodus lässt Controller-Eingabe bewusst zum Spiel durch.",
        ["For driver repair, HP OMEN buses and version checks, see [[driver-conflicts|HP OMEN & driver version conflicts]]. For busy or hidden-device access, see [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "Zu Treiberreparatur, HP-OMEN-Bussen und Versionsprüfungen siehe [[driver-conflicts|HP OMEN & Treiberversionskonflikte]]. Zu belegtem Zugriff oder versteckten Geräten siehe [[hidhide-troubleshooting|HidHide-Fehlerbehebung]].",

        // ── topic:passthru-mode ──
        ["Set Passthru Mode (Bypass Input Isolation)"] =
            "Direktmodus (Eingabeisolation umgehen)",
        ["passthru mode passthrough safe mode bypass input isolation controller interception anticheat valorant vanguard eac battleye exceptions per game automatic capture"] =
            "passthru modus durchreichen passthrough sicherer modus umgehen eingabeisolation controller abfangen anticheat valorant vanguard eac battleye ausnahmen pro spiel automatisch erfassung",
        ["Some competitive titles with kernel anticheat (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) could theoretically react to emulated or hidden devices. **Passthru Mode** bypasses [[input-isolation|input isolation]] entirely: Radiata removes its virtual pad and requests release of its own controller blocks. Other tools may still hide or remap the controller. The wheel still works; the trade-off is that input reaches the game while a wheel is open, which is what \"bleed-through\" or \"double input\" means. Don't confuse it with disabling the **wheels** - that keeps the virtual pad in place and only stops summons. Passthru Mode removes the virtual pad altogether."] =
            "Einige kompetitive Titel mit Kernel-Anti-Cheat (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) könnten theoretisch auf emulierte oder versteckte Geräte reagieren. Der **Direktmodus** umgeht die [[input-isolation|Eingabe-Isolation]] vollständig: Radiata entfernt seinen virtuellen Controller und fordert die Freigabe seiner eigenen Controller-Sperren an. Andere Programme können den Controller weiterhin verstecken oder umbelegen. Das Rad funktioniert weiter; der Preis ist, dass Eingaben das Spiel erreichen, solange ein Rad offen ist – genau das meint \"Durchschlagen\" oder \"doppelte Eingabe\". Verwechsle es nicht mit dem Deaktivieren der **Räder**: das lässt den virtuellen Controller an Ort und Stelle und stoppt nur die Aufrufe. Der Direktmodus entfernt den virtuellen Controller ganz.",
        ["**Automatic per-game:** add a game to the **Always use Passthru Mode for** list - from the installed-games dropdown, or **Add Application…** for a specific .exe. Radiata enters Passthru Mode while it runs and restores capture on exit; the tray shows \"(auto: <game>)\"."] =
            "**Automatisch pro Spiel:** füge ein Spiel der Liste **Immer den Direktmodus verwenden für** hinzu – aus dem Dropdown der installierten Spiele oder mit **Anwendung hinzufügen…** für eine bestimmte .exe. Radiata wechselt in den Direktmodus, solange es läuft, und stellt die Erfassung beim Beenden wieder her; im Infobereich steht \"(auto: <Spiel>)\".",
        ["Each entry engages **While Running** (recommended - anticheat watches from launch) or **While Frontmost** (only while the game's window is focused)."] =
            "Jeder Eintrag greift **Solange sie läuft** (während es läuft – empfohlen, da Anti-Cheat ab dem Start beobachtet) oder **Solange sie im Vordergrund ist** (nur, solange das Fenster des Spiels im Vordergrund ist).",
        ["Passthru Mode is **best-effort**. Per-game detection polls every ~2 seconds, so a just-launched game can briefly see normal capture. For the strictest titles, toggle Passthru Mode on manually *before* launching. The isolation drivers also stay installed system-wide either way."] =
            "Direktmodus arbeitet **nach bestem Bemühen**. Die Erkennung pro Spiel fragt etwa alle 2 Sekunden ab, sodass ein gerade gestartetes Spiel kurz die normale Erfassung sehen kann. Schalte für die strengsten Titel den Direktmodus manuell *vor* dem Start ein. Die Isolationstreiber bleiben so oder so systemweit installiert.",

        // ── topic:tray-and-settings ──
        ["Tray & Settings (mouse/keyboard, at the desk)"] =
            "Infobereich und Einstellungen (mit Maus und Tastatur am Schreibtisch)",
        ["tray icon left click right click menu settings tabs f1 f2 test"] =
            "infobereich symbol linksklick rechtsklick menü einstellungen registerkarten f1 f2 testen",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Show in Explorer, Start with Windows, Exit)."] =
            "**Tray-Symbol:** Linksklick öffnet die Einstellungen; Rechtsklick zeigt das Menü (Räder deaktivieren/Räder aktivieren, Direktmodus, **Spieleraster**, Einstellungen, Hilfe, Über Radiata, Im Explorer anzeigen, Mit Windows starten, Beenden).",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Start with Windows, Exit)."] =
            "**Tray-Symbol:** Linksklick öffnet die Einstellungen; Rechtsklick zeigt das Menü (Räder deaktivieren/Räder aktivieren, Direktmodus, **Spieleraster**, Einstellungen, Hilfe, Über Radiata, Mit Windows starten, Beenden).",
        ["**Test Wheel** - each Wheel tab has a **Test Left/Right Wheel** button under the slice list that opens that wheel on screen (with the wheels enabled)."] =
            "**Rad testen** – jede Rad-Registerkarte hat unter der Segmentliste eine Schaltfläche **Linkes/Rechtes Rad testen**, die dieses Rad auf dem Bildschirm öffnet (bei aktivierten Rädern).",

        // ── topic:customize ──
        ["Customize (look, feel & sound)"] =
            "Anpassen (Aussehen, Verhalten und Klang)",
        ["customize material light dark flat pearl obsidian mesa gloss terra theme slices thickness ring thick medium thin button icons glyphs best guess sound effects themed material digital physical silent none preview appearance look feel triggers accessibility"] =
            "anpassen material hell dunkel flach pearl obsidian mesa glanz terra thema segmente dicke ring dick mittel dünn tastensymbole glyphen beste vermutung soundeffekte thematisch material digital physisch stumm keine vorschau erscheinungsbild aussehen gefühl auslöser barrierefreiheit",
        ["**Mesa** - rounded cream wedges on cracked terracotta; the armed slice lifts like a 3D card."] =
            "**Mesa** – abgerundete cremefarbene Segmente auf rissigem Terrakotta; das anvisierte Segment hebt sich wie eine 3D-Karte.",
        ["**Sound Effects** - **Themed** (the default) plays whatever sound set matches the material above. **Digital** and **Physical** are fixed sets if you'd rather pin one, and **Silent** turns the sounds off. Picking a material switches the choice back to Themed."] =
            "**Klangeffekte** – **Thematisch** (Standard) spielt den Klangsatz, der zum Material darüber passt. **Digital** und **Physisch** sind feste Sätze, falls du lieber einen festlegst, und **Stumm** schaltet die Klänge ab. Wählst du ein Material, springt die Einstellung zurück auf Thematisch.",
        ["**Slice Thickness** - the radial thickness of the slice ring. **Thick** only reads well up to **8 slices**, so a 9th slice on either wheel switches the setting to **Medium** - and it switches back on its own once you're at 8 or fewer again."] =
            "**Segmentdicke** – die radiale Dicke des Segmentrings. **Dick** liest sich nur bis **8 Segmente** gut, deshalb stellt ein neuntes Segment auf einem der Räder die Einstellung auf **Mittel** – und sie stellt sich von selbst zurück, sobald es wieder 8 oder weniger sind.",
        ["**Button Icons** - the symbols in on-screen prompts: **Best Guess** (the default - follows the connected pad), **PlayStation** (✕ ○ □ △), or **Xbox** (A B X Y)."] =
            "**Tastensymbole** – die Zeichen in Bildschirmhinweisen: **Beste Vermutung** (Standard – folgt dem angeschlossenen Controller), **PlayStation** (✕ ○ □ △) oder **Xbox** (A B X Y).",
        ["**Triggers** - which controller gestures summon a wheel. See [[triggers|Triggers]]."] =
            "**Aufrufgesten** (Auslöser) – welche Controller-Gesten ein Rad aufrufen. Siehe [[triggers|Aufrufgesten]].",
        ["**D-Pad 🡄 🡆** - what left and right on the D-Pad do while a wheel is open. See [[volume-mixer|D-Pad controls]]."] =
            "**Steuerkreuz 🡄 🡆** – was links und rechts auf dem D-Pad tun, während ein Rad offen ist. Siehe [[volume-mixer|D-Pad-Steuerung]].",
        ["**Show labels on** - which slices draw their text label. See [[show-labels|Show labels on]]."] =
            "**Beschriftungen anzeigen bei** – welche Segmente ihre Textbeschriftung zeichnen. Siehe [[show-labels|Beschriftungen anzeigen bei]].",
        ["**Accessibility** - activation, wheel sides, both-stick aiming, the hub, Reduce motion and narration - lives on **Settings ▸ Advanced**. See [[accessibility|Accessibility]]."] =
            "**Barrierefreiheit** – Aktivierung, Radseiten, Zielen mit beiden Sticks, die Nabe, Reduce motion und Sprachausgabe – befindet sich unter **Einstellungen ▸ Erweitert**. Siehe [[accessibility|Barrierefreiheit]].",
        ["**Make your own material** - drop a theme package into Radiata's Materials folder and it joins the list. See [[custom-materials|Custom materials]]."] =
            "**Eigenes Material erstellen** – lege ein Themenpaket in Radiatas Ordner Materials, und es erscheint in der Liste. Siehe [[custom-materials|Eigene Materialien]].",
        ["A wheel holds up to **12** slices, but **6-8** is the sweet spot."] =
            "Ein Rad fasst bis zu **12** Segmente, aber **6–8** sind ideal.",

        // ── topic:workshop ──
        ["Workshop: make your own"] =
            "Werkstatt: selbst gemacht",
        ["workshop make build create author own custom package packages theme material game arcade javascript sample samples template download guide folder restart confirm share tutorial"] =
            "werkstatt workshop erstellen bauen autor eigen eigenes paket pakete thema material spiel arcade javascript beispiel beispiele vorlage herunterladen anleitung ordner neustart bestätigen teilen tutorial",
        ["Radiata can load things you make yourself: **materials** that restyle the wheel, and **Arcade games** that play in the Arcade's round window. Each one is a **package** - a folder of plain files you can write in any text editor."] =
            "Radiata kann Dinge laden, die du selbst machst: **Materialien**, die das Rad umgestalten, und **Arcade-Spiele**, die im runden Arcade-Fenster laufen. Jedes davon ist ein **Paket** – ein Ordner mit einfachen Dateien, die du in jedem Texteditor schreiben kannst.",
        ["**Materials** are data only: colors, gradients, a font name, and optionally images and sounds. See [[custom-materials|Custom materials]]."] =
            "**Materialien** sind reine Daten: Farben, Verläufe, ein Schriftname und optional Bilder und Töne. Siehe [[custom-materials|Eigene Materialien]].",
        ["**Arcade games** are a `game.json` and one JavaScript file, run in a sandbox. See [[custom-arcade-games|Custom Arcade games]]."] =
            "**Arcade-Spiele** bestehen aus einer `game.json` und einer JavaScript-Datei und laufen in einer Sandbox. Siehe [[custom-arcade-games|Eigene Arcade-Spiele]].",
        ["The **Workshop** on the Radiata website is the full guide: step-by-step walkthroughs, every setting with its range, design advice, and **sample packages to download**. Start there: [getradiata.app/workshop](https://getradiata.app/workshop/)."] =
            "Die **Werkstatt** auf der Radiata-Website ist die vollständige Anleitung: Schritt-für-Schritt-Durchgänge, jede Einstellung mit ihrem Bereich, Gestaltungstipps und **Beispielpakete zum Herunterladen**. Fang dort an: [getradiata.app/workshop](https://getradiata.app/workshop/de.html).",
        ["How making a package works"] =
            "So entsteht ein Paket",
        ["**1.** Make a folder for your package - or unzip a sample - inside Radiata's packages folder. Paste the path into File Explorer's address bar to open it:"] =
            "**1.** Lege im Paketordner von Radiata einen Ordner für dein Paket an – oder entpacke ein Beispiel dorthin. Füge den Pfad in die Adressleiste des Datei-Explorers ein, um ihn zu öffnen:",
        ["a material: `%APPDATA%\\Radiata\\Packages\\Materials\\<your theme>`"] =
            "ein Material: `%APPDATA%\\Radiata\\Packages\\Materials\\<dein Thema>`",
        ["a game: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<your game>`"] =
            "ein Spiel: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<dein Spiel>`",
        ["**2.** Write the manifest (`material.json` or `game.json`) and put every file it names directly inside that folder."] =
            "**2.** Schreibe das Manifest (`material.json` oder `game.json`) und lege jede Datei, die es nennt, direkt in diesen Ordner.",
        ["**3.** **Restart Radiata** - right-click the tray icon, choose **Exit**, then start it again. Packages are read once, at startup."] =
            "**3.** **Starte Radiata neu** – Rechtsklick auf das Tray-Symbol, **Beenden** wählen, dann wieder starten. Pakete werden einmal gelesen, beim Start.",
        ["**4.** Accept the confirmation Radiata shows for a new or changed package."] =
            "**4.** Akzeptiere die Bestätigung, die Radiata für ein neues oder geändertes Paket anzeigt.",
        ["**5.** Try it out: pick the material in **Settings ▸ Customize**, or open the game from the Arcade. Change something, then go back to step 3."] =
            "**5.** Probiere es aus: Wähle das Material unter **Einstellungen ▸ Anpassen** oder öffne das Spiel im Arcade. Ändere etwas und mach dann bei Schritt 3 weiter.",
        ["Making a package is a loop: every change goes back through a restart and the confirmation before you can try it."] =
            "Ein Paket zu bauen ist eine Schleife: Jede Änderung geht erneut durch einen Neustart und die Bestätigung, bevor du sie ausprobieren kannst.",
        ["Keep a copy of your work somewhere else too. Radiata reads a package where it sits, but nothing backs it up for you."] =
            "Bewahre deine Arbeit auch anderswo auf. Radiata liest ein Paket dort, wo es liegt, aber nichts sichert es für dich.",
        ["**Only install packages from people you trust.** Radiata asks before it loads a package and asks again whenever one changes, but it can't tell you whether a package is any good."] =
            "**Installiere nur Pakete von Leuten, denen du vertraust.** Radiata fragt, bevor es ein Paket lädt, und erneut, sobald es sich ändert – aber es kann dir nicht sagen, ob ein Paket etwas taugt.",
        ["When a package doesn't show up, or you want to give one to a friend, see [[workshop-sharing|Testing and sharing packages]]."] =
            "Wenn ein Paket nicht erscheint oder du eins weitergeben willst, siehe [[workshop-sharing|Pakete testen und teilen]].",

        // ── topic:custom-materials ──
        ["Custom materials (build your own theme)"] =
            "Eigene Materialien (ein eigenes Thema bauen)",
        ["Beyond the eight built-in materials you can drop in **your own theme**. A theme is one folder holding a text file called `material.json` - colors, gradients, a system font name, and optionally images and sounds sitting beside it. Themes are **data only**: the format cannot express code, a network address, or a file outside the theme's own folder, so a theme can restyle the wheel and do nothing else."] =
            "Über die acht eingebauten Materialien hinaus kannst du **ein eigenes Thema** hinzufügen. Ein Thema ist ein Ordner mit einer Textdatei namens `material.json` – Farben, Verläufe, ein Systemschriftname und optional daneben liegende Bilder und Töne. Themen sind **reine Daten**: Das Format kann weder Code noch eine Netzwerkadresse noch eine Datei außerhalb des eigenen Themenordners ausdrücken, sodass ein Thema das Rad umgestalten und sonst nichts tun kann.",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has two to download: **Starter**, the smallest complete theme with every line explained, and **Ember**, which uses every block below. The Workshop also covers color, contrast and texture advice this topic leaves out."] =
            "**Fang mit einem Beispiel an.** Die [Werkstatt](https://getradiata.app/workshop/de.html#samples) hat zwei zum Herunterladen: **Starter**, das kleinste vollständige Thema mit jeder Zeile erklärt, und **Ember**, das jeden Block unten nutzt. Die Werkstatt behandelt auch Farbe, Kontrast und Texturen, die dieses Thema auslässt.",
        ["Where it goes"] =
            "Wo es hingehört",
        ["One folder per theme under `%APPDATA%\\Radiata\\Packages\\Materials` - for example `…\\Materials\\Lava\\material.json`. Paste that path into Explorer's address bar; Radiata creates the folder on first run."] =
            "Ein Ordner pro Thema unter `%APPDATA%\\Radiata\\Packages\\Materials` – zum Beispiel `…\\Materials\\Lava\\material.json`. Füge diesen Pfad in die Adressleiste des Explorers ein; Radiata legt den Ordner beim ersten Start an.",
        ["That folder also holds a **README.txt** written by Radiata, carrying a copy-paste example of every field. It's the reference; this topic is the tour."] =
            "Dieser Ordner enthält außerdem eine von Radiata geschriebene **README.txt** mit einem kopierbaren Beispiel für jedes Feld. Sie ist die Referenz; dieses Thema ist die Führung.",
        ["The smallest theme that works"] =
            "Das kleinste Thema, das funktioniert",
        ["A **format 1** manifest is a JSON object with the keys below. `format`, `id`, `name`, `dark`, and a `colors` object holding at least `resting` and `armed` are required; everything else is optional."] =
            "Ein Manifest im **Format 1** ist ein JSON-Objekt mit den folgenden Schlüsseln. `format`, `id`, `name`, `dark` und ein `colors`-Objekt mit mindestens `resting` und `armed` sind erforderlich; alles andere ist optional.",
        ["`\"format\": 1` - which manifest version you wrote. Radiata reads **1 to 3**; the richer blocks further down need the higher number."] =
            "`\"format\": 1` – welche Manifestversion du geschrieben hast. Radiata liest **1 bis 3**; die umfangreicheren Blöcke weiter unten brauchen die höhere Zahl.",
        ["`\"id\": \"lava\"` - 2-31 characters of lowercase a-z, 0-9 and `-`, starting with a letter or digit. This is the theme's identity: it becomes the token `custom-lava` in your config, so changing it later makes a **different** theme."] =
            "`\"id\": \"lava\"` – 2–31 Zeichen aus Kleinbuchstaben a–z, 0–9 und `-`, beginnend mit Buchstabe oder Ziffer. Das ist die Identität des Themas: Sie wird zum Token `custom-lava` in deiner Konfiguration, eine spätere Änderung ergibt also ein **anderes** Thema.",
        ["`\"name\": \"Lava\"` - up to 24 characters; the label on the Customize tile. `\"author\"` (up to 64) is optional."] =
            "`\"name\": \"Lava\"` – bis zu 24 Zeichen; die Beschriftung der Kachel unter Anpassen. `\"author\"` (bis 64) ist optional.",
        ["`\"dark\": true` - whether the slices are dark. It flips labels and the hub to light ink and picks the dark fallbacks, so getting it wrong shows up as unreadable text rather than a wrong color."] =
            "`\"dark\": true` – ob die Segmente dunkel sind. Es schaltet Beschriftungen und Nabe auf helle Tinte und wählt die dunklen Rückfallwerte, sodass ein Fehler als unlesbarer Text auffällt und nicht als falsche Farbe.",
        ["`\"colors\"` - `\"resting\"` and `\"armed\"` are required; `\"confirm\"` (defaults to the armed color), `\"label\"` and `\"outline\"` are optional. Each is `#RRGGBB` or `#AARRGGBB`, where the leading pair is alpha - `\"#40FFFFFF\"` is a 25%-opaque white outline."] =
            "`\"colors\"` – `\"resting\"` und `\"armed\"` sind erforderlich; `\"confirm\"` (Standard: die Armed-Farbe), `\"label\"` und `\"outline\"` sind optional. Jede ist `#RRGGBB` oder `#AARRGGBB`, wobei das führende Paar der Alphawert ist – `\"#40FFFFFF\"` ist eine zu 25 % deckende weiße Kontur.",
        ["`\"labelFont\": \"Cascadia Code\"` - optional, and it must be a font **already installed on the PC**. An unknown name is ignored rather than treated as an error. Font *files* inside a package are never supported, deliberately."] =
            "`\"labelFont\": \"Cascadia Code\"` – optional, und es muss eine Schriftart sein, die **bereits auf dem PC installiert** ist. Ein unbekannter Name wird ignoriert statt als Fehler behandelt. Schrift*dateien* innerhalb eines Pakets werden bewusst nie unterstützt.",
        ["`\"soundTheme\": \"physical\"` - which sounds the wheel makes on your theme. Name a sound set directly - `physical` (the default), `digital`, `kawaii`, `mesa`, `salvage`, `reactor` or `obsidian` - **or name a built-in material** and borrow whatever that one uses, so `\"pearl\"` gives you the digital set and `\"flat-dark\"` the physical set."] =
            "`\"soundTheme\": \"physical\"` – welche Töne das Rad mit deinem Thema macht. Nenne direkt einen Klangsatz – `physical` (Standard), `digital`, `kawaii`, `mesa`, `salvage`, `reactor` oder `obsidian` – **oder nenne ein eingebautes Material** und übernimm, was dieses nutzt, sodass `\"pearl\"` den digitalen Satz und `\"flat-dark\"` den physischen ergibt.",
        ["Naming the **material** is usually the better choice: your theme keeps sounding like the look you styled it after, even if that look's sounds are retuned in a later release. Naming a set pins it exactly."] =
            "Das **Material** zu nennen ist meist die bessere Wahl: dein Thema klingt weiter wie der Look, dem du es nachempfunden hast, selbst wenn dessen Töne in einer späteren Version neu abgestimmt werden. Einen Satz zu nennen legt ihn exakt fest.",
        ["`material.json` may contain `//` comments and trailing commas, so you can leave yourself notes. A color can also be written short as `#RGB`."] =
            "`material.json` darf `//`-Kommentare und Kommas am Ende enthalten, du kannst dir also Notizen hinterlassen. Eine Farbe lässt sich auch kurz als `#RGB` schreiben.",
        ["Richer looks - format 2"] =
            "Reichere Looks – Format 2",
        ["`\"fills\"` - a gradient per state (`resting` / `armed` / `confirm`) instead of a flat color. `\"type\"` is `solid`, `bowed` (the glassy Pearl/Obsidian ramp), or `linear` with an `\"angle\"`, plus a list of `\"stops\"` (each an `\"at\"` from 0 to 1 and a `\"color\"`)."] =
            "`\"fills\"` – ein Verlauf pro Zustand (`resting` / `armed` / `confirm`) statt einer flachen Farbe. `\"type\"` ist `solid`, `bowed` (die glasige Pearl/Obsidian-Rampe) oder `linear` mit einem `\"angle\"`, plus eine Liste von `\"stops\"` (je ein `\"at\"` von 0 bis 1 und eine `\"color\"`).",
        ["`\"hueWalk\"` - gives every slice its own hue around the ring, Kawaii-style, from `sat` / `light` / `armedSat` / `armedLight` (0-1) and `hueOffset`. It **overrides** the resting and armed fills."] =
            "`\"hueWalk\"` – gibt jedem Segment seinen eigenen Farbton rund um den Ring, im Kawaii-Stil, aus `sat` / `light` / `armedSat` / `armedLight` (0–1) und `hueOffset`. Es **überschreibt** die Resting- und Armed-Füllungen.",
        ["`\"outline\"` and `\"armedOutline\"` - `color`, `width`, and an optional `dash` pattern for the slice edge."] =
            "`\"outline\"` und `\"armedOutline\"` – `color`, `width` und ein optionales `dash`-Muster für die Segmentkante.",
        ["`\"armed\"` - how an armed slice moves: `liftPx` (up to 24), `northPx` (±12), `scale` (1.0-1.15). It's a state treatment rather than continuous motion, so it survives [[accessibility|Reduce motion]]."] =
            "`\"armed\"` – wie sich ein scharfes Segment bewegt: `liftPx` (bis 24), `northPx` (±12), `scale` (1.0–1.15). Es ist eine Zustandsbehandlung und keine fortlaufende Bewegung, überlebt also [[accessibility|Bewegung reduzieren]].",
        ["`\"glyph\"` - how slice icons are treated: `edge` (`inner`, `outer` or `none`) with `edgeColor` / `edgeWidth` / `edgeShadow`; a `glow` whose `color` can be the literal `\"slice\"` to take each slice's own accent, with `strength` 0-1; plus `castShadow` and `armedWash`."] =
            "`\"glyph\"` – wie Segmentsymbole behandelt werden: `edge` (`inner`, `outer` oder `none`) mit `edgeColor` / `edgeWidth` / `edgeShadow`; ein `glow`, dessen `color` das Literal `\"slice\"` sein kann, um den Akzent jedes Segments zu übernehmen, mit `strength` 0–1; dazu `castShadow` und `armedWash`.",
        ["`\"label\"` - `case` (`upper` for stamped all-caps labels) and `sizeMul` (0.8-1.3). `\"gapPx\"` (0-14) sets the gap between slices."] =
            "`\"label\"` – `case` (`upper` für gestempelte Großbuchstaben-Beschriftungen) und `sizeMul` (0.8–1.3). `\"gapPx\"` (0–14) setzt den Abstand zwischen Segmenten.",
        ["`\"tile\"` - how the theme's swatch looks on the Customize tab: `edgeColor`, `sheen`, `lifted`, and an optional `texture`."] =
            "`\"tile\"` – wie die Kachel des Themas im Reiter Anpassen aussieht: `edgeColor`, `sheen`, `lifted` und eine optionale `texture`.",
        ["Images and sounds - format 3"] =
            "Bilder und Töne – Format 3",
        ["Set `\"format\": 3` to reference files that live **in the theme's own folder**, by bare file name - a path isn't expressible in the format."] =
            "Setze `\"format\": 3`, um auf Dateien zu verweisen, die **im eigenen Ordner des Themas** liegen, per reinem Dateinamen – ein Pfad ist im Format nicht ausdrückbar.",
        ["`\"textures\"` - `slice` and `hub` paint over the fill; `backdrop` draws behind the whole wheel. Each takes a `\"file\"` and an `\"opacity\"`, and slice/hub also take `\"tile\": true` to repeat the image at its natural size instead of stretching it. **PNG or JPG, up to 4 MB**; anything wider than 2048px is scaled down as it's decoded."] =
            "`\"textures\"` – `slice` und `hub` werden über die Füllung gemalt; `backdrop` wird hinter das ganze Rad gezeichnet. Jedes nimmt eine `\"file\"` und eine `\"opacity\"`, und slice/hub akzeptieren zusätzlich `\"tile\": true`, um das Bild in natürlicher Größe zu wiederholen statt es zu strecken. **PNG oder JPG, bis 4 MB**; alles breiter als 2048 px wird beim Dekodieren verkleinert.",
        ["`\"sounds\"` - one file per event: `armed`, `fired`, `enableWheels`, `disableWheels`. **WAV only, up to 1 MB and 3 seconds each**; events you leave out keep the paired sound theme's own sound."] =
            "`\"sounds\"` – eine Datei pro Ereignis: `armed`, `fired`, `enableWheels`, `disableWheels`. **Nur WAV, bis 1 MB und 3 Sekunden je Datei**; ausgelassene Ereignisse behalten den Ton des gepaarten Klangthemas.",
        ["A theme that ships sounds is **badged** on its Customize tile and takes over the **Sound Effects** picker - the Digital and Physical overrides go inactive, while Themed and Silent stay live."] =
            "Ein Thema, das Töne mitliefert, erhält ein **Abzeichen** auf seiner Kachel unter Anpassen und übernimmt die Auswahl **Soundeffekte** – die Überschreibungen Digital und Physical werden inaktiv, Themed und Silent bleiben aktiv.",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**. Changing *any* file - not just the manifest - re-asks for your confirmation on the next start."] =
            "Ordnergrenzen: höchstens **32 Dateien**, **4 MB pro Datei**, **16 MB insgesamt**. Das Ändern *irgendeiner* Datei – nicht nur des Manifests – fragt beim nächsten Start erneut nach deiner Bestätigung.",
        ["If your theme doesn't appear"] =
            "Wenn dein Thema nicht erscheint",
        ["**A package loads whole or not at all.** One bad value rejects the theme rather than half-applying it, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped material`."] =
            "**Ein Paket wird ganz oder gar nicht geladen.** Ein einzelner fehlerhafter Wert lehnt das Thema ab, statt es halb anzuwenden, und der Grund steht in `%APPDATA%\\Radiata\\radiata-trace.log` – suche in dieser Datei nach `[Packages] skipped material`.",
        ["The usual causes: a missing `dark` or `colors`, a color that isn't `#RRGGBB` / `#AARRGGBB`, an `id` with capitals or spaces, or a `format` number lower than the blocks you used."] =
            "Die üblichen Ursachen: fehlendes `dark` oder `colors`, eine Farbe, die nicht `#RRGGBB` / `#AARRGGBB` ist, eine `id` mit Großbuchstaben oder Leerzeichen, oder eine `format`-Zahl, die niedriger ist als die verwendeten Blöcke.",
        ["**A theme you had selected that stops loading** - package removed, or a change you declined - falls back to **Pearl**, quietly. Nothing else in your wheels changes."] =
            "**Ein ausgewähltes Thema, das nicht mehr lädt** – Paket entfernt oder eine abgelehnte Änderung – fällt stillschweigend auf **Perle** zurück. Sonst ändert sich in deinen Rädern nichts.",
        ["Custom themes are never offered during first-run setup, and the theme folder isn't part of a settings backup - copy the folder itself to move a theme to another PC."] =
            "Eigene Themen werden bei der Ersteinrichtung nie angeboten, und der Themenordner ist nicht Teil einer Einstellungssicherung – kopiere den Ordner selbst, um ein Thema auf einen anderen PC zu bringen.",

        // ── topic:custom-arcade-games ──
        ["The Arcade also plays **games you write yourself**. One is a folder holding a `game.json` and a single **JavaScript** file. Drop it into Radiata's Arcade Games folder and it plays in the same round window as the built-in games, with the same buttons and the same best-score tracking."] =
            "Die Arcade spielt auch **Spiele, die du selbst schreibst**. Eines ist ein Ordner mit einer `game.json` und einer einzelnen **JavaScript**-Datei. Leg ihn in Radiatas Ordner „Arcade Games“, und es läuft im selben runden Fenster wie die eingebauten Spiele, mit denselben Tasten und derselben Bestenwert-Erfassung.",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has **Firefly** to download, a short but complete game with every line explained. The Workshop also walks through writing a game step by step."] =
            "**Fang mit einem Beispiel an.** In der [Werkstatt](https://getradiata.app/workshop/de.html#samples) kannst du **Firefly** herunterladen, ein kurzes, aber vollständiges Spiel mit jeder Zeile erklärt. Die Werkstatt führt dich auch Schritt für Schritt durch das Schreiben eines Spiels.",
        ["**A game package contains code.** Radiata asks you to confirm a package before it ever runs, and again whenever any file in it changes - but the sandbox below is a limit on what a game *can* do, not a judgement about whether it's worth running. **Only install games from a source you trust.**"] =
            "**Ein Spielpaket enthält Code.** Radiata bittet dich, ein Paket zu bestätigen, bevor es je läuft, und erneut, sobald sich eine Datei darin ändert – die unten beschriebene Sandbox begrenzt aber nur, was ein Spiel tun *kann*, und ist kein Urteil darüber, ob es sich lohnt, es auszuführen. **Installiere Spiele nur aus einer Quelle, der du vertraust.**",
        ["One folder per game under `%APPDATA%\\Radiata\\Packages\\Arcade Games` - for example `…\\Arcade Games\\Firefly\\game.json` beside `firefly.js`. Radiata creates the folder on first run."] =
            "Ein Ordner pro Spiel unter `%APPDATA%\\Radiata\\Packages\\Arcade Games` – zum Beispiel `…\\Arcade Games\\Firefly\\game.json` neben `firefly.js`. Radiata legt den Ordner beim ersten Start an.",
        ["That folder's **README.txt** is the full API reference, kept current by Radiata itself."] =
            "Die **README.txt** in diesem Ordner ist die vollständige API-Referenz, die Radiata selbst aktuell hält.",
        ["The manifest"] =
            "Das Manifest",
        ["`game.json` is a small JSON object. `format`, `id`, `title` and `entry` are required:"] =
            "`game.json` ist ein kleines JSON-Objekt. `format`, `id`, `title` und `entry` sind erforderlich:",
        ["`\"format\": 1` - the manifest version."] =
            "`\"format\": 1` – die Manifestversion.",
        ["`\"id\": \"firefly\"` - 1-32 characters of lowercase a-z, 0-9 and `-`. The game's identity, and the `pkg-<id>` token."] =
            "`\"id\": \"firefly\"` – 1–32 Zeichen aus Kleinbuchstaben a–z, 0–9 und `-`. Die Identität des Spiels und das Token `pkg-<id>`.",
        ["`\"title\": \"Firefly\"` - up to 24 characters, shown in the picker."] =
            "`\"title\": \"Firefly\"` – bis zu 24 Zeichen, in der Auswahl angezeigt.",
        ["`\"entry\": \"firefly.js\"` - the script, as a **bare file name** in the same folder (no paths), up to **256 KB**."] =
            "`\"entry\": \"firefly.js\"` – das Skript, als **reiner Dateiname** im selben Ordner (keine Pfade), bis zu **256 KB**.",
        ["`\"tint\": \"#5B8DEF\"` - optional: your cabinet's colour in the **Arcade Launcher**, as `#RGB` or `#RRGGBB`. Leave it out for the plain grey cabinet. Either way, your `title` is printed on the cabinet's nameplate."] =
            "`\"tint\": \"#5B8DEF\"` – optional: die Farbe deines Automaten im **Arcade-Launcher**, als `#RGB` oder `#RRGGBB`. Ohne sie ist der Automat grau. So oder so steht dein `title` auf dem Namensschild des Automaten.",
        ["`\"preview\": \"preview.png\"` - optional: the picture on your cabinet's screen until the game has been played, as a **bare PNG or JPG file name** in the same folder. Make it square, with the round playfield filling it."] =
            "`\"preview\": \"preview.png\"` – optional: das Bild auf dem Bildschirm deines Automaten, bis das Spiel gespielt wurde, als **bloßer PNG- oder JPG-Dateiname** im selben Ordner. Mach es quadratisch, mit dem runden Spielfeld formatfüllend.",
        ["`\"badge\": \"badge.png\"` - optional: an illustration for your cabinet's nameplate, drawn to the left of your title the way the built-in cabinets carry theirs. A **PNG with a transparent background**, in its own colours; it overflows the nameplate above and below."] =
            "`\"badge\": \"badge.png\"` – optional: eine Illustration für das Namensschild deines Automaten, links vom Titel gezeichnet, so wie die eingebauten Automaten ihre tragen. Ein **PNG mit transparentem Hintergrund**, in eigenen Farben; es ragt oben und unten über das Namensschild hinaus.",
        ["`\"glyph\": \"glyph.png\"` - optional: your game's icon on its wheel slices. A **PNG** whose transparency is the shape - the wheel colours it like every other slice icon, so draw it in one colour on a transparent background. Without one, drop-in games share a script icon."] =
            "`\"glyph\": \"glyph.png\"` – optional: das Symbol deines Spiels auf seinen Radsegmenten. Ein **PNG**, dessen Transparenz die Form ist – das Rad färbt es wie jedes andere Segmentsymbol, also zeichne es einfarbig auf transparentem Hintergrund. Ohne eines teilen sich hinzugefügte Spiele ein Skript-Symbol.",
        ["Once your game has been played, its cabinet shows the player's own last board instead - Radiata saves it as `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. That file is also the easiest way to make a preview: play your game, close it, and copy the file into your package as `preview.png`."] =
            "Sobald dein Spiel gespielt wurde, zeigt sein Automat stattdessen das letzte Spielfeld des Spielers – Radiata speichert es als `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. Diese Datei ist auch der einfachste Weg zu einer Vorschau: Spiel dein Spiel, schließ es und kopiere die Datei als `preview.png` in dein Paket.",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**."] =
            "Ordnergrenzen: höchstens **32 Dateien**, **4 MB pro Datei**, **16 MB insgesamt**.",
        ["How a game runs"] =
            "Wie ein Spiel läuft",
        ["Your script runs in a **locked-down interpreter inside a separate sandboxed process**: no files, no network, no clipboard, no other programs. Only the functions below exist at all. Memory is capped by Windows at 128 MB."] =
            "Dein Skript läuft in einem **abgeschotteten Interpreter in einem separaten Sandbox-Prozess**: keine Dateien, kein Netzwerk, keine Zwischenablage, keine anderen Programme. Nur die unten aufgeführten Funktionen existieren überhaupt. Windows begrenzt den Speicher auf 128 MB.",
        ["Define **`tick(dt)`**, called for every fixed **1/120 second** step, and **`draw()`**, called once per screen frame. Issue drawing commands only from inside `draw()`."] =
            "Definiere **`tick(dt)`**, aufgerufen für jeden festen Schritt von **1/120 Sekunde**, und **`draw()`**, einmal pro Bildschirmframe aufgerufen. Gib Zeichenbefehle nur aus `draw()` heraus.",
        ["There's a hard time and instruction budget **per drawn frame**. A script that overruns is stopped and restarted (your saved data survives); one that keeps overrunning ends in a plain card you can back out of. It can slow itself down - it can't slow the PC down."] =
            "Es gibt ein hartes Zeit- und Anweisungsbudget **pro gezeichnetem Frame**. Ein Skript, das es überschreitet, wird gestoppt und neu gestartet (deine gespeicherten Daten bleiben erhalten); eines, das es dauernd überschreitet, endet in einer schlichten Karte, aus der du zurückkehren kannst. Es kann sich selbst ausbremsen – nicht den PC.",
        ["**Radiata owns {circle} and {triangle}** - closing the game and the help card - so your script never sees those two buttons."] =
            "**Radiata behält {circle} und {triangle}** – Spiel schließen und Hilfekarte –, sodass dein Skript diese beiden Tasten nie sieht.",
        ["Drawing: the playfield is a disc"] =
            "Zeichnen: Das Spielfeld ist eine Scheibe",
        ["Everything is drawn in **polar coordinates**: `r` runs 0 at the center to 1 at the rim, angles are **degrees** with 0 at 12 o'clock, increasing clockwise. Radiata does the trigonometry and clips to the circle, so a game can't draw outside its window."] =
            "Alles wird in **Polarkoordinaten** gezeichnet: `r` läuft von 0 in der Mitte bis 1 am Rand, Winkel sind **Grad** mit 0 bei 12 Uhr, im Uhrzeigersinn zunehmend. Radiata übernimmt die Trigonometrie und beschneidet auf den Kreis, sodass ein Spiel nicht außerhalb seines Fensters zeichnen kann.",
        ["A game places everything by radius and angle: r runs from 0 at the center to 1 at the rim, and angles are degrees clockwise from 12 o'clock."] =
            "Ein Spiel platziert alles über Radius und Winkel: r läuft von 0 in der Mitte bis 1 am Rand, und Winkel sind Grad im Uhrzeigersinn ab 12 Uhr.",
        ["Colors are numbers in **`0xAARRGGBB`** form - alpha first, so `0xFFFF0000` is opaque red."] =
            "Farben sind Zahlen in der Form **`0xAARRGGBB`** – Alpha zuerst, also ist `0xFFFF0000` deckendes Rot.",
        ["The commands, up to **1024 per frame**: `arc(r0, r1, a0, a1, color)` for a ring segment, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` for 3-16 points, and `text(r, a, size, \"str\", color)` for up to 64 characters."] =
            "Die Befehle, bis zu **1024 pro Frame**: `arc(r0, r1, a0, a1, color)` für ein Ringsegment, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` für 3–16 Punkte und `text(r, a, size, \"str\", color)` für bis zu 64 Zeichen.",
        ["Input"] =
            "Eingabe",
        ["Read-only globals, refreshed every frame: **`stickX`** / **`stickY`** (-1 to 1, with y positive **downward**, matching the screen), **`crossDown`** / **`squareDown`** while held, **`crossPressed`** / **`squarePressed`** true for one frame per press, and **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, also one frame per press."] =
            "Schreibgeschützte Globals, jeden Frame aktualisiert: **`stickX`** / **`stickY`** (−1 bis 1, wobei y nach **unten** positiv ist, passend zum Bildschirm), **`crossDown`** / **`squareDown`** während des Haltens, **`crossPressed`** / **`squarePressed`** für einen Frame pro Druck wahr, sowie **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, ebenfalls ein Frame pro Druck.",
        ["Saving, randomness, and sound"] =
            "Speichern, Zufall und Ton",
        ["Write the reserved key **`hiscore`** (a whole number as text) to publish a best score to the Arcade picker."] =
            "Schreibe den reservierten Schlüssel **`hiscore`** (eine ganze Zahl als Text), um eine Bestleistung in der Arcade-Auswahl zu veröffentlichen.",
        ["**`rand()`** returns 0-1 and is seeded per session, so a replay of the same inputs behaves the same way."] =
            "**`rand()`** liefert 0–1 und wird pro Sitzung geseedet, sodass eine Wiederholung derselben Eingaben sich gleich verhält.",
        ["**`cue(\"name\")`** plays one of nine built-in sounds: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Any other name is silent, and there's no way to ship your own audio."] =
            "**`cue(\"name\")`** spielt einen von neun eingebauten Tönen: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Jeder andere Name bleibt stumm, und eigene Audiodateien können nicht mitgeliefert werden.",
        ["Things that trip people up"] =
            "Typische Stolperfallen",
        ["Scripts run in **strict mode**, so a variable you forget to declare is an error. There's no `console`, `eval`, timer or `import` - to see a value while you work, draw it with `text()`."] =
            "Skripte laufen im **Strict Mode**, also ist eine Variable, die du nicht deklarierst, ein Fehler. Es gibt kein `console`, `eval`, keine Timer und kein `import` – um einen Wert beim Arbeiten zu sehen, zeichne ihn mit `text()`.",
        ["**Keep every drawing value in range**: radii 0-1, `dot` size up to 0.5, `line` width up to 0.1, `text` size up to 0.3, angles within ±3600. Radiata treats an out-of-range call as a broken game and restarts the script; after three restarts it shows a problem card. Clamp your numbers."] =
            "**Halte jeden Zeichenwert im Bereich**: Radien 0-1, `dot`-Größe bis 0.5, `line`-Breite bis 0.1, `text`-Größe bis 0.3, Winkel innerhalb von ±3600. Radiata behandelt einen Aufruf außerhalb des Bereichs als kaputtes Spiel und startet das Skript neu; nach drei Neustarts zeigt es eine Problemkarte. Begrenze deine Zahlen.",
        ["JavaScript's bit operators (`|`, `&`, `<<`) produce **signed** numbers, and a negative color draws as nothing. Build colors with arithmetic, or finish the expression with `>>> 0`."] =
            "Die Bit-Operatoren von JavaScript (`|`, `&`, `<<`) ergeben **vorzeichenbehaftete** Zahlen, und eine negative Farbe zeichnet nichts. Baue Farben mit Arithmetik oder beende den Ausdruck mit `>>> 0`.",
        ["Input is read **once per drawn frame**, but `tick` can run several times in that frame, and each run sees the same `crossPressed`. Make a press count once - the Firefly sample shows a way."] =
            "Die Eingabe wird **einmal pro gezeichnetem Frame** gelesen, aber `tick` kann in diesem Frame mehrmals laufen, und jeder Lauf sieht dasselbe `crossPressed`. Sorge dafür, dass ein Druck nur einmal zählt – das Beispiel Firefly zeigt einen Weg.",
        ["`text()` centers the string on its point. The last argument of `ring()` is a thin edge color, or `0` for none."] =
            "`text()` zentriert den Text auf seinem Punkt. Das letzte Argument von `ring()` ist eine dünne Randfarbe oder `0` für keinen Rand.",
        ["Each drawn frame gets **2,000,000 statements and 8 ms** for all of its `tick` runs plus `draw`, at most **8** `cue` calls and **16** `kvSet` writes. `kvSet` throws an error when a key or value is too long or the 4 KB store is full."] =
            "Jeder gezeichnete Frame bekommt **2.000.000 Anweisungen und 8 ms** für alle seine `tick`-Läufe plus `draw`, höchstens **8** `cue`-Aufrufe und **16** `kvSet`-Schreibvorgänge. `kvSet` wirft einen Fehler, wenn ein Schlüssel oder Wert zu lang oder der 4-KB-Speicher voll ist.",
        ["If your game doesn't appear"] =
            "Wenn dein Spiel nicht erscheint",
        ["**A package loads whole or not at all**, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped arcade game`."] =
            "**Ein Paket wird ganz oder gar nicht geladen**, und der Grund steht in `%APPDATA%\\Radiata\\radiata-trace.log` – suche in dieser Datei nach `[Packages] skipped arcade game`.",
        ["The usual causes: an `entry` that isn't a plain `.js` file name sitting in the same folder, a script over 256 KB, an `id` with capitals or spaces, or a confirmation that was declined (it only re-asks once the package changes)."] =
            "Die üblichen Ursachen: ein `entry`, der kein einfacher `.js`-Dateiname im selben Ordner ist, ein Skript über 256 KB, eine `id` mit Großbuchstaben oder Leerzeichen, oder eine abgelehnte Bestätigung (sie fragt erst wieder, wenn sich das Paket ändert).",
        ["A game that loaded but misbehaves shows its card in the window rather than an error - and a package you delete simply stops being offered."] =
            "Ein Spiel, das geladen wurde, sich aber fehlverhält, zeigt seine Karte im Fenster statt eines Fehlers – und ein Paket, das du löschst, wird einfach nicht mehr angeboten.",
        ["**Script errors** go to the same log: search it for `[Arcade] script` to see the error message."] =
            "**Skriptfehler** landen im selben Protokoll: Suche dort nach `[Arcade] script`, um die Fehlermeldung zu sehen.",

        // ── topic:workshop-sharing ──
        ["Testing and sharing packages"] =
            "Pakete testen und teilen",
        ["Testing"] =
            "Testen",
        ["Radiata reads packages **only at startup**: exit from the tray and start it again after every change."] =
            "Radiata liest Pakete **nur beim Start**: Beende es über das Tray-Symbol und starte es nach jeder Änderung neu.",
        ["A change to **any** file in a package brings the confirmation back on the next start. If you decline it, the package stays off until its files change again."] =
            "Eine Änderung an **irgendeiner** Datei eines Pakets bringt die Bestätigung beim nächsten Start zurück. Lehnst du sie ab, bleibt das Paket aus, bis sich seine Dateien wieder ändern.",
        ["**Nothing happening?** Open `%APPDATA%\\Radiata\\radiata-trace.log` in a text editor and search for `[Packages] skipped`. Each line names the package folder and the exact problem - a missing field, a value out of range, a file that isn't there."] =
            "**Passiert nichts?** Öffne `%APPDATA%\\Radiata\\radiata-trace.log` in einem Texteditor und suche nach `[Packages] skipped`. Jede Zeile nennt den Paketordner und das genaue Problem – ein fehlendes Feld, ein Wert außerhalb des Bereichs, eine Datei, die nicht da ist.",
        ["**The most common mistake is one folder too many.** Unzipping often makes `Materials\\Lava\\Lava\\material.json`; Radiata looks for the manifest directly inside `Materials\\Lava`. Move the files up a level."] =
            "**Der häufigste Fehler ist ein Ordner zu viel.** Beim Entpacken entsteht oft `Materials\\Lava\\Lava\\material.json`; Radiata sucht das Manifest direkt in `Materials\\Lava`. Verschiebe die Dateien eine Ebene nach oben.",
        ["Radiata looks for the manifest directly inside the package's own folder; the extra folder level an unzip often adds hides it."] =
            "Radiata sucht das Manifest direkt im eigenen Ordner des Pakets; die zusätzliche Ordnerebene, die beim Entpacken oft entsteht, versteckt es.",
        ["A game that loads but misbehaves writes its script errors to the same log - search for `[Arcade] script`."] =
            "Ein Spiel, das lädt, sich aber falsch verhält, schreibt seine Skriptfehler in dasselbe Protokoll – suche nach `[Arcade] script`.",
        ["Sharing"] =
            "Teilen",
        ["Say what the package is and what it does, and only include images, sounds and code you have the right to share."] =
            "Sag, was das Paket ist und was es tut, und nimm nur Bilder, Töne und Code auf, die du weitergeben darfst.",
        ["Radiata's license doesn't extend to your package: what you make in these formats is yours to license however you like."] =
            "Die Lizenz von Radiata erstreckt sich nicht auf dein Paket: Was du in diesen Formaten machst, kannst du lizenzieren, wie du willst.",
        ["Moving to another PC? Settings backups don't include packages - copy the `Packages` folder across yourself."] =
            "Umzug auf einen anderen PC? Einstellungssicherungen enthalten keine Pakete – kopiere den Ordner `Packages` selbst hinüber.",

        // ── topic:triggers ──
        ["Triggers (summon chords)"] =
            "Aufrufgesten (Aufrufkombinationen)",
        ["triggers chord builder hold tap add another trigger remove row fn bumper trigger touchpad swipe select start l3 r3 dpad combined summon invoke gesture customize"] =
            "Triggers Auslöser Kombination Baukasten halten antippen weiteren Auslöser hinzufügen Zeile entfernen Fn Bumper Trigger Touchpad Wischen Select Start L3 R3 D-Pad kombiniert aufrufen Geste anpassen",
        ["**Settings ▸ Customize ▸ Triggers** is the chord builder: which controller gestures summon a wheel. Each row is one live chord - a **button** (Fn or L4/R4 / Bumper / Trigger / Touchpad) paired with how it's **combined** (Trigger, Home, L3/R3, Select/Start, D-Pad L/R, or a touchpad edge-swipe). The options adapt to the detected pad: **Fn** appears for a DualSense Edge, **L4/R4** for a pad with extra buttons on Bluetooth, and **Touchpad** only for pads that have one. Those dedicated buttons need no second button - they open a wheel on their own."] =
            "**Einstellungen ▸ Anpassen ▸ Auslöser** ist der Griff-Baukasten: welche Controller-Gesten ein Rad aufrufen. Jede Zeile ist ein aktiver Griff – eine **Taste** (Fn oder L4/R4 / Bumper / Trigger / Touchpad), gepaart mit der Art der **Kombination** (Trigger, Home, L3/R3, Select/Start, Steuerkreuz links/rechts oder ein Touchpad-Wisch vom Rand). Die Optionen passen sich dem erkannten Controller an: **Fn** erscheint bei einem DualSense Edge, **L4/R4** bei einem Controller mit zusätzlichen Tasten über Bluetooth, und **Touchpad** nur bei Controllern, die eines haben. Diese dedizierten Tasten brauchen keine zweite Taste – sie öffnen ein Rad allein.",
        ["**+ Add Another Trigger** appends a row; a row's **✕** removes it. Every row stays live at once - up to **three** - and the set is remembered **per controller type**, so an Edge and an Xbox pad each keep their own chords."] =
            "**+ Weitere Geste hinzufügen** hängt eine Zeile an; das **✕** einer Zeile entfernt sie. Alle Zeilen sind gleichzeitig aktiv – bis zu **drei** – und der Satz wird **pro Controllertyp** gespeichert, sodass ein Edge und ein Xbox-Controller jeweils eigene Kombinationen behalten.",
        ["How each chord behaves (hold + tap, which wheel it opens, Hold vs Toggle) is in [[opening-a-wheel|Opening a wheel]]; the both-sides version of a chord toggles the wheels on and off, see [[wheel-open-extras|While a wheel is open]]."] =
            "Wie sich eine Kombination verhält (Halten und Tippen, welches Rad sie öffnet, Hold gegenüber Toggle), steht unter [[opening-a-wheel|Ein Rad öffnen]]; die beidseitige Fassung einer Kombination schaltet die Räder ein und aus, siehe [[wheel-open-extras|Während ein Rad offen ist]].",

        // ── topic:accessibility ──
        ["Accessibility (Settings ▸ Advanced)"] =
            "Barrierefreiheit (Einstellungen ▸ Erweitert)",
        ["accessibility wheels toggle on off swap left right both sticks either stick one stick ignores opposite stick sidedness aim drift always show hub battery reduce motion confetti fade parallax animation effects still narration speak speech spoken screen reader narrator voice volume system-wide windows narrator settings onboarding blind low vision"] =
            "barrierefreiheit räder umschalten an aus tauschen links rechts beide sticks einer der sticks ein stick ignoriert den anderen stick seitigkeit zielen drift nabe immer zeigen akku bewegung reduzieren konfetti überblenden parallaxe animation effekte ruhig sprachausgabe sprechen sprache gesprochen screenreader narrator stimme lautstärke systemweit windows narrator einstellungen ersteinrichtung blind sehbehindert",
        ["The **Accessibility** section gathers six checkboxes, in **Settings ▸ Advanced**. The same set is offered during first-run setup from the **Accessibility…** button on the Look step:"] =
            "Der Bereich **Barrierefreiheit** fasst sechs Kontrollkästchen zusammen, unter **Einstellungen ▸ Erweitert**. Denselben Satz bietet die Ersteinrichtung über die Schaltfläche **Barrierefreiheit** im Schritt Look:",
        ["Two of them are **indented under the box that ticks them**: turning on **Wheels toggle on/off** also ticks **Swap left/right**, and turning on **Reduce motion** also ticks **Always show hub**, because each pair works best together. Both children stay yours to tick or clear on their own, and once you set one by hand it stops following its parent."] =
            "Zwei davon sind **unter dem Kästchen eingerückt, das sie anhakt**: Das Einschalten von **Räder per Umschalten öffnen/schließen** hakt auch **Links/rechts tauschen** an, und das Einschalten von **Bewegung reduzieren** hakt auch **Zentrum immer anzeigen** an, weil jedes Paar zusammen am besten funktioniert. Beide Unterpunkte bleiben dir zum eigenen An- oder Abhaken überlassen, und sobald du einen von Hand setzt, folgt er seinem Elternteil nicht mehr.",
        ["**Wheels toggle on/off** and **Swap left/right** - whether an invoked wheel stays up until you dismiss it, and which wheel each hand opens (see [[opening-a-wheel|Opening a wheel]])."] =
            "**Räder per Umschalten öffnen/schließen** und **Links/rechts tauschen** – ob ein aufgerufenes Rad stehen bleibt, bis du es schließt, und welches Rad jede Hand öffnet (siehe [[opening-a-wheel|Ein Rad öffnen]]).",
        ["**Narration** - speaks what you're doing aloud: which wheel opened, the slice you arm and its current state, hold-to-confirm progress, what a fire actually did, volume levels as you scrub, edit-mode moves, and Game Grid browsing. It works alongside a screen reader."] =
            "**Sprachausgabe** – spricht aus, was du tust: welches Rad geöffnet wurde, das anvisierte Segment und dessen aktuellen Zustand, den Fortschritt beim Halten zum Bestätigen, was ein Auslösen tatsächlich bewirkt hat, Lautstärkepegel beim Regeln, Züge im Bearbeitungsmodus und das Blättern im Spieleraster. Sie arbeitet neben einem Screenreader.",
        ["**Narration covers the wheel and the Game Grid only** - the overlay surfaces a screen reader can't see. Settings, first-run setup and every other ordinary window are **Windows Narrator's** job, so run Narrator alongside Radiata if you want those read too. Ticking **Narration** offers a button to turn Narrator on; and if Narrator is running when you first set Radiata up, Narration starts on by itself."] =
            "**Die Sprachausgabe deckt nur das Rad und das Spieleraster ab** – die Overlay-Flächen, die ein Screenreader nicht sehen kann. Für die Einstellungen, die Ersteinrichtung und jedes andere normale Fenster ist die **Windows-Sprachausgabe** zuständig, führe sie also neben Radiata aus, wenn du auch diese vorgelesen haben willst. Beim Anhaken von **Sprachausgabe** wird eine Schaltfläche zum Einschalten der Windows-Sprachausgabe angeboten; und läuft sie bereits, wenn du Radiata zum ersten Mal einrichtest, startet die Sprachausgabe von selbst eingeschaltet.",

        // ── topic:show-labels ──
        ["Show labels on (slice text)"] =
            "Beschriftungen anzeigen bei (Segmentbezeichnungen)",
        ["show labels on slice labels label text names icons not logos standard icons all slices no slices slices i choose show label checkbox unlabelled artwork logo cover png"] =
            "Beschriftungen anzeigen Segmentbeschriftungen Text Namen Symbole keine Logos Standardsymbole alle Segmente keine Segmente selbst gewählte Segmente Kontrollkästchen Beschriftung anzeigen unbeschriftet Grafik Logo Cover PNG",
        ["**Show labels on** - which slices draw their text label on the wheel."] =
            "**Beschriftungen anzeigen bei** – welche Segmente ihre Textbeschriftung auf dem Rad zeichnen.",
        ["**All Slices** - every slice is labelled, artwork ones included."] =
            "**Alle Segmente** – jedes Segment wird beschriftet, auch die mit Grafik.",
        ["**No Slices** - no slice is labelled."] =
            "**Keine Segmente** – kein Segment wird beschriftet.",
        ["**Editing a wheel is exempt:** in edit mode and its Add picker, non-logo slices always show their labels whatever this is set to."] =
            "**Das Bearbeiten eines Rings ist ausgenommen:** Im Bearbeitungsmodus und in der zugehörigen Hinzufügen-Auswahl zeigen Segmente ohne Logo unabhängig von dieser Einstellung immer ihre Beschriftung.",
        ["The setting lives in **Settings ▸ Customize**, directly under the **D-Pad 🡄 🡆** selector, and applies **live** - bring up a wheel to see it."] =
            "Die Einstellung liegt unter **Einstellungen ▸ Anpassen**, direkt unter der Auswahl **Steuerkreuz 🡄 🡆**, und greift **sofort** – rufe ein Rad auf, um sie zu sehen.",

        // ── topic:integrations ──
        ["Integrations (SteamGridDB, Discord & OBS)"] =
            "Integrationen (SteamGridDB, Discord und OBS)",
        ["integrations steamgriddb sgdb api key discord obs websocket port password configure cover art logos"] =
            "integrationen steamgriddb sgdb api-schlüssel discord obs websocket port kennwort einrichten titelbilder logos",
        ["**Settings ▸ Advanced ▸ Integrations** connects optional external services:"] =
            "**Einstellungen ▸ Erweitert ▸ Integrationen** verbindet optionale externe Dienste:",
        ["**Configure Discord Integration…** - sets the Discord credentials (Client ID/Secret) that Join/Leave Voice Channel slices use, the same wizard the slice editor offers. Stored encrypted (Windows DPAPI) and sent only to Discord."] =
            "**Discord-Integration einrichten…** – legt die Discord-Zugangsdaten (Client-ID/Secret) fest, die die Segmente zum Betreten/Verlassen des Sprachkanals nutzen, derselbe Assistent, den der Segment-Editor anbietet. Verschlüsselt gespeichert (Windows DPAPI) und nur an Discord gesendet.",
        ["**SteamGridDB API key** - unlocks portrait cover art and logos for every storefront's games (the Game Grid's **Select**/**Start** cycling). Get a free key at [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). It's checked when you finish entering it, and entering your first key automatically fills in covers skipped while you had none."] =
            "**SteamGridDB-API-Schlüssel** – schaltet Hochformat-Titelbilder und Logos für die Spiele aller Stores frei (das Durchschalten mit **Auswählen**/**Start** im Spieleraster). Einen kostenlosen Schlüssel gibt es unter [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). Er wird geprüft, sobald du ihn fertig eingegeben hast, und mit dem ersten Schlüssel werden automatisch die Titelbilder nachgetragen, die ohne Schlüssel übersprungen wurden.",
        ["**OBS Studio** - **Configure OBS Integration…** sets the WebSocket port and password, one connection shared by every OBS slice; see [[obs-studio|OBS slices]]. **Test** checks it against a running OBS. The password is stored encrypted (DPAPI)."] =
            "**OBS Studio** – **OBS-Integration einrichten…** legt WebSocket-Port und -Passwort fest, eine gemeinsame Verbindung für alle OBS-Segmente; siehe [[obs-studio|OBS-Segmente]]. **Testen** prüft sie gegen ein laufendes OBS. Das Passwort wird verschlüsselt gespeichert (DPAPI).",
        ["With [[playnite|Playnite]] and a SteamGridDB key both set up, a **Prefer Playnite covers** toggle appears in the **Game Grid** section. Without Playnite, an **Install Playnite…** button appears here instead."] =
            "Sind [[playnite|Playnite]] und ein SteamGridDB-Schlüssel beide eingerichtet, erscheint im Abschnitt **Spieleraster** ein Schalter **Playnite-Titelbilder bevorzugen**. Ohne Playnite erscheint hier stattdessen eine Schaltfläche **Playnite installieren…**.",

        // ── topic:playnite ──
        ["Playnite (optional library manager)"] =
            "Playnite (optionale Bibliotheksverwaltung)",
        ["playnite library manager games covers metadata optional install third party emulators"] =
            "playnite bibliotheksverwaltung spiele titelbilder metadaten optional installieren drittanbieter emulatoren",
        ["**Playnite** is a free, open-source game-library manager for Windows ([playnite.link](https://playnite.link)) that gathers all your games - Steam, Epic, GOG, Xbox, emulators, standalone - with metadata and cover art."] =
            "**Playnite** ist ein kostenloser, quelloffener Spielebibliotheks-Verwalter für Windows ([playnite.link](https://playnite.link)), der alle deine Spiele zusammenführt – Steam, Epic, GOG, Xbox, Emulatoren, eigenständige – samt Metadaten und Titelbildern.",
        ["Radiata works fully **without** it, scanning your storefronts directly. Playnite is an optional enhancement:"] =
            "Radiata funktioniert vollständig **ohne** Playnite und durchsucht deine Shops direkt. Playnite ist eine optionale Ergänzung:",
        ["**More games found** - Radiata reads Playnite's library, so emulated and manually-added games a raw storefront scan misses can appear in the Game Grid."] =
            "**Mehr gefundene Spiele** – Radiata liest Playnites Bibliothek, sodass emulierte und von Hand hinzugefügte Spiele, die ein reiner Store-Suchlauf übersieht, im Spieleraster erscheinen können.",
        ["**Curated cover art** - Playnite's own covers become an art source, with a **Prefer Playnite covers** toggle (Advanced ▸ Game Grid) to favor them over SteamGridDB."] =
            "**Kuratierte Titelbilder** – Playnites eigene Titelbilder werden zu einer Grafikquelle, mit einem Schalter **Playnite-Cover bevorzugen**, um sie gegenüber SteamGridDB zu bevorzugen.",
        ["Not installed? **Install Playnite…** in Advanced ▸ Integrations opens its download page. Set up your libraries in Playnite and Radiata picks them up automatically."] =
            "Nicht installiert? **Playnite installieren…** unter Erweitert ▸ Integrationen öffnet die Downloadseite. Richte deine Bibliotheken in Playnite ein, und Radiata übernimmt sie automatisch.",

        // ── topic:system-actions ──
        ["System tools & Backup (Settings ▸ Advanced)"] =
            "Systemwerkzeuge und Sicherung (Einstellungen ▸ Erweitert)",
        ["run first run setup onboarding wizard reset starter slices customize recommended install repair drivers recover controller setup email log hid diagnostics quit exit backup restore reset wipe zip factory defaults undo clean install always show hub practice reduce motion check for updates automatic update skip version"] =
            "Ersteinrichtung ausführen Assistent zurücksetzen Startsegmente anpassen empfohlen Treiber installieren reparieren Controller wiederherstellen Einrichtung E-Mail Protokoll HID Diagnose beenden Sicherung wiederherstellen zurücksetzen löschen zip Werkseinstellungen rückgängig saubere Installation Nabe immer anzeigen Übung Bewegung reduzieren nach Updates suchen automatisches Update Version überspringen",
        ["**Settings ▸ Advanced ▸ System** holds the update controls, the **Start with Windows** toggle, the **Troubleshooting** dropdown, and **Quit Radiata**:"] =
            "**Einstellungen ▸ Erweitert ▸ System** enthält die Update-Steuerung, den Schalter **Mit Windows starten**, die Auswahlliste **Fehlerbehebung** und **Radiata beenden**:",
        ["**Quit Radiata** - releases its virtual controller and requests removal of the HidHide blocks Radiata owns. Another tool's blocks remain; see [[hidhide-troubleshooting|HidHide troubleshooting]] if the pad stays hidden."] =
            "**Radiata beenden** – gibt seinen virtuellen Controller frei und fordert die Aufhebung der HidHide-Sperren an, die Radiata gehören. Sperren anderer Programme bleiben bestehen; siehe [[hidhide-troubleshooting|HidHide-Fehlerbehebung]], falls der Controller versteckt bleibt.",
        ["The Troubleshooting dropdown"] =
            "Die Auswahlliste Fehlerbehebung…",
        ["**Run First-Run Setup…** - re-runs the setup wizard (controller check, drivers, look, cover art, starter wheels). Your customized wheels are never overwritten without asking."] =
            "**Ersteinrichtung ausführen…** – führt den Einrichtungsassistenten erneut aus (Controllerprüfung, Treiber, Aussehen, Titelbilder, Starträder). Deine angepassten Räder werden nie ohne Nachfrage überschrieben.",
        ["**Install/Repair Drivers…** - installs or repairs the isolation drivers (ViGEmBus + HidHide). Fixes most isolation problems, and shows a result log."] =
            "**Treiber installieren/reparieren…** – installiert oder repariert die Isolationstreiber (ViGEmBus + HidHide). Behebt die meisten Isolationsprobleme und zeigt ein Ergebnisprotokoll.",
        ["**HID Diagnostics…** - a live view of the raw controller reports Radiata reads. Useful when support asks what your pad is actually sending."] =
            "**HID-Diagnose…** – eine Live-Ansicht der rohen Controller-Berichte, die Radiata liest. Nützlich, wenn der Support fragt, was dein Controller tatsächlich sendet.",
        ["**Controller Setup…** - re-runs the controller detection and mapping wizard on its own, without the rest of first-run setup."] =
            "**Controller-Einrichtung…** – führt die Controller-Erkennung und -Zuordnung eigenständig erneut aus, ohne den Rest der Ersteinrichtung.",
        ["**Email Log to Developer…** - saves a diagnostic ZIP to your Desktop and opens an addressed email. Review the ZIP before attaching and sending it: logs can include device identifiers, account names in file paths, and application or game names. Radiata does not automatically send the attachment."] =
            "**Protokoll an den Entwickler senden…** – speichert eine Diagnose-ZIP auf deinem Desktop und öffnet eine adressierte E-Mail. Prüfe die ZIP, bevor du sie anhängst und abschickst: Protokolle können Gerätekennungen, Kontonamen in Dateipfaden sowie App- oder Spielnamen enthalten. Radiata versendet den Anhang nicht automatisch.",
        ["**Back Up Settings…** and **Restore Settings…** sit lower in the same dropdown, and the **resets** below them - all described under Backup & reset."] =
            "**Einstellungen sichern…** und **Einstellungen wiederherstellen…** stehen weiter unten in derselben Auswahlliste, und die **Zurücksetzungen** darunter – alles beschrieben unter Sicherung und Zurücksetzen.",
        ["Backup & reset"] =
            "Sicherung und Zurücksetzen",
        ["**Back Up Settings…** - saves everything that makes Radiata yours (wheels, colors, settings, and your Game Grid cover/logo picks) to a .zip in `Documents\\Radiata Backups`."] =
            "**Einstellungen sichern…** – sichert alles, was Radiata zu deiner macht (Räder, Farben, Einstellungen und deine Titelbild-/Logo-Auswahl im Spieleraster), in eine .zip-Datei unter `Documents\\Radiata Backups`.",
        ["**Reset All Settings…** - factory defaults for wheels, colors and settings; first-run setup runs again on the next launch. Cached art survives."] =
            "**Alle Einstellungen zurücksetzen…** – Werkseinstellungen für Räder, Farben und Einstellungen; die Ersteinrichtung läuft beim nächsten Start erneut. Zwischengespeicherte Grafiken bleiben erhalten.",
        ["**Uninstall Radiata…** - removes the startup entry, the HidHide registration, and Radiata's own files, with OFF-by-default opt-ins for the shared drivers and your settings. Anything else in Radiata's folder is left alone."] =
            "**Radiata deinstallieren…** – entfernt den Autostart-Eintrag, die HidHide-Registrierung und Radiatas eigene Dateien, mit standardmäßig ABGESCHALTETEN Optionen für die gemeinsam genutzten Treiber und deine Einstellungen. Alles andere in Radiatas Ordner bleibt unangetastet.",
        ["Updates"] =
            "Updates",
        ["**Check for Updates** - asks `getradiata.app/update` for a newer version right now. When one is found the button becomes **Install Update** and opens the update prompt."] =
            "**Nach Updates suchen** – fragt jetzt sofort bei `getradiata.app/update` nach einer neueren Version. Wird eine gefunden, wird die Schaltfläche zu **Update installieren** und öffnet die Update-Abfrage.",
        ["**Automatic** - the checkbox beside the button: when on, the same check runs at startup and once a day. Found updates announce themselves with an on-screen notice (click it to open the update) and a line in this tab; a version you choose to **skip** stops announcing itself, though the line here still shows it. **Skip This Version** is offered only after you have pressed **Later** on that version once."] =
            "**Automatisch** – das Kontrollkästchen neben der Schaltfläche: eingeschaltet läuft dieselbe Prüfung beim Start und einmal täglich. Gefundene Updates melden sich mit einem Hinweis auf dem Bildschirm (Klick öffnet das Update) und einer Zeile in diesem Reiter; eine Version, die du **überspringst**, meldet sich nicht mehr, auch wenn die Zeile hier sie weiterhin zeigt. **Diese Version überspringen** wird erst angeboten, nachdem du bei dieser Version einmal **Später** gedrückt hast.",
        ["Resets can't be undone - **back up first**."] =
            "Zurücksetzungen lassen sich nicht rückgängig machen – **sichere vorher**.",
        ["The game-art buttons live in their own **Game Grid** section - see [[game-grid-options|Game Grid options]]."] =
            "Die Schaltflächen für Spielgrafiken liegen im eigenen Abschnitt **Spieleraster** – siehe [[game-grid-options|Game-Grid-Optionen]].",

        // ── topic:game-grid-options ──
        ["Game Grid options (Settings ▸ Advanced)"] =
            "Spieleraster-Optionen (Einstellungen ▸ Erweitert)",
        ["game grid options clear game art cache retry missing game art reset hidden games unhide covers redownload"] =
            "game grid optionen zwischenspeicher leeren fehlende grafiken erneut laden verborgene spiele zurücksetzen wieder einblenden herunterladen",
        ["**Settings ▸ Advanced ▸ Game Grid** collects the grid's housekeeping buttons:"] =
            "**Einstellungen ▸ Erweitert ▸ Spieleraster** versammelt die Pflege-Schaltflächen des Rasters:",
        ["**Retry Missing Game Art** - re-attempts only the covers and logos that came up empty, keeping everything already downloaded and every cover you picked by hand (see [[cover-art|Cover art & logos]])."] =
            "**Fehlendes Artwork erneut abrufen** – versucht nur die Titelbilder und Logos erneut, die leer geblieben sind, und behält alles bereits Geladene sowie jedes von Hand gewählte Titelbild (siehe [[cover-art|Titelbilder und Logos]]).",
        ["**Clear Game Art Cache** - deletes ALL cached covers, so everything re-downloads. **Images you dropped onto a slice are kept.** Try **Retry Missing Game Art** first if you only want to fill blanks."] =
            "**Spielgrafik-Cache leeren** – löscht ALLE zwischengespeicherten Titelbilder, sodass alles neu geladen wird. **Bilder, die du auf ein Segment gezogen hast, bleiben erhalten.** Probiere zuerst **Fehlende Spielgrafiken erneut versuchen**, wenn du nur Lücken füllen willst.",
        ["**Reset Hidden Games** - brings back every game and storefront you hid with **hold {square}** (see [[storefronts|Hiding a storefront]])."] =
            "**Ausgeblendete Spiele zurücksetzen** – holt jedes Spiel und jeden Store zurück, den du mit **{square} halten** ausgeblendet hast (siehe [[storefronts|Einen Store ausblenden]]).",
        ["**Prefer Playnite covers** - shown when [[playnite|Playnite]] and a SteamGridDB key are both set up: favors Playnite's own cover art."] =
            "**Playnite-Cover bevorzugen** – wird angezeigt, wenn [[playnite|Playnite]] und ein SteamGridDB-Schlüssel beide eingerichtet sind: bevorzugt Playnites eigene Titelbilder.",

        // ── topic:storefronts ──
        ["Hiding a storefront"] =
            "Einen Shop verbergen",
        ["storefront steam epic gog xbox battle.net amazon itch ubisoft ea hide exclude opt out include reset hidden games launcher card"] =
            "Store Storefront Steam Epic GOG Xbox Battle.net Amazon itch Ubisoft EA ausblenden ausschließen abwählen einschließen ausgeblendete Spiele zurücksetzen Launcher Karte",
        ["A whole storefront can be hidden from the [[game-grid|Game Grid]], the same way a single game can."] =
            "Ein ganzer Store lässt sich genauso aus dem [[game-grid|Spieleraster]] ausblenden wie ein einzelnes Spiel.",
        ["**Filter to the store with L1 / R1**, then **hold {square}** on its **Open <store>** card. A notice asks **\"Hide <store> and all its games in Radiata?\"** - **{cross}** hides it, **{circle}** cancels."] =
            "**Filtere mit L1 / R1 auf den Store** und **halte dann {square}** auf dessen Karte **<Store> öffnen**. Ein Hinweis fragt **\"<Store> und alle seine Spiele in Radiata ausblenden?\"** – **{cross}** blendet ihn aus, **{circle}** bricht ab.",
        ["Hiding a store **hides its games** in the Game Grid, the [[edit-mode|Add picker]], and the starter wheels the first-run wizard suggests."] =
            "Einen Store auszublenden **blendet dessen Spiele** im Spieleraster, in der [[edit-mode|Hinzufügen-Auswahl]] und in den Starträdern aus, die der Ersteinrichtungsassistent vorschlägt.",
        ["Bring it back with **Settings ▸ Advanced ▸ Game Grid ▸** [[game-grid-options|Reset Hidden Games]], which un-hides storefronts as well as games."] =
            "Hole ihn mit **Einstellungen ▸ Erweitert ▸ Spieleraster ▸** [[game-grid-options|Ausgeblendete Spiele zurücksetzen]] zurück, das Stores ebenso wie Spiele wieder einblendet.",
        ["Newly installed storefronts appear **automatically** the next time Radiata scans."] =
            "Neu installierte Stores erscheinen **automatisch**, sobald Radiata das nächste Mal sucht.",
        ["Only a store with its own **Open <store>** card can be hidden this way. A [[playnite|Playnite]] game you added by hand, or one from a third-party Playnite plugin, carries no storefront card. Hide those games individually."] =
            "So ausblenden lässt sich nur ein Store mit einer eigenen Karte **<Store> öffnen**. Ein [[playnite|Playnite]]-Spiel, das du von Hand hinzugefügt hast, oder eines aus einem Playnite-Plug-in eines Drittanbieters trägt keine Store-Karte. Solche Spiele blendest du einzeln aus.",

        // ── topic:controller-not-detected ──
        ["Controller not detected"] =
            "Controller wird nicht erkannt",
        ["controller dead not detected blind hidhide lockout whitelist recover reset bluetooth radio frozen stuck wedge"] =
            "Controller tot nicht erkannt blind HidHide Sperre Freigabeliste wiederherstellen zurücksetzen Bluetooth Funk eingefroren hängt klemmt",
        ["**Replug / re-pair first.** Bluetooth stacks occasionally wedge, and power-cycling the pad fixes most one-offs."] =
            "**Erst neu anstecken oder neu koppeln.** Bluetooth-Stacks verklemmen sich gelegentlich, und den Controller aus- und wieder einzuschalten behebt die meisten Einzelfälle.",
        ["**Bluetooth pad connected but frozen** (Windows still lists it, input never moves)? That's a Windows Bluetooth wedge that power-cycling the pad **won't** fix - toggle the PC's **Bluetooth off and on** instead. Radiata shows a \"toggle Bluetooth\" notification when it spots this."] =
            "**Bluetooth-Controller verbunden, aber eingefroren** (Windows listet ihn noch, die Eingabe bewegt sich nie)? Das ist eine Verklemmung des Windows-Bluetooth, die das Aus- und Einschalten des Controllers **nicht** behebt – schalte stattdessen das **Bluetooth** des PCs aus und wieder ein. Radiata zeigt einen Hinweis \"Bluetooth umschalten\", wenn es das erkennt.",
        ["**HidHide lockout:** if the cloak hides the pad while Radiata isn't on its allow-list, Radiata goes blind. That means no input, and the Current Controller readout shows nothing even though Windows sees the pad. Radiata catches this at startup and offers a one-click fix via a clickable on-screen notice; repair can help with registration, but it does not cure every access problem. See [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "**HidHide-Aussperrung:** verbirgt die Tarnung den Controller, während Radiata nicht auf der Zulassungsliste steht, wird Radiata blind. Das heißt keine Eingabe, und die Anzeige „Aktueller Controller“ zeigt nichts, obwohl Windows den Controller sieht. Radiata erkennt das beim Start und bietet über einen anklickbaren Hinweis auf dem Bildschirm eine Lösung mit einem Klick an; eine Reparatur kann bei der Registrierung helfen, behebt aber nicht jedes Zugriffsproblem. Siehe [[hidhide-troubleshooting|HidHide-Fehlerbehebung]].",
        ["**Peer controller tools** can remap or hide the controller and create additional outputs. Installed software alone does not establish a conflict; see [[controller-tool-conflicts|reWASD, DS4Windows & other tools]]."] =
            "**Andere Controller-Programme** können den Controller umbelegen oder verstecken und zusätzliche Ausgaben erzeugen. Installierte Software allein belegt noch keinen Konflikt; siehe [[controller-tool-conflicts|reWASD, DS4Windows & andere Programme]].",

        // ── topic:controller-conflict-checklist ──
        ["Controller conflict checklist"] =
            "Checkliste für Controller-Konflikte",
        ["double input duplicate bleed through wrong pad player slot remote play diagnostic log"] =
            "doppelte eingabe duplikat durchschlagen falscher controller spielerplatz remote play diagnoseprotokoll",
        ["Use this sequence when one press moves a menu twice, the game reacts beneath a wheel, the wrong controller responds, or input disappears. Change one thing at a time and test between changes."] =
            "Nutze diese Reihenfolge, wenn ein Druck ein Menü zweimal bewegt, das Spiel unter einem Rad reagiert, der falsche Controller antwortet oder die Eingabe verschwindet. Ändere immer nur eine Sache und teste zwischen den Änderungen.",
        ["**1. Save and exit the game.** Controller mode, Passthru Mode, remapper output changes and reconnects can all replace the device a game is using. Relaunch after the test setup is stable."] =
            "**1. Speichere und beende das Spiel.** Controller-Modus, Direktmodus, geänderte Remapper-Ausgaben und Neuverbindungen können das Gerät ersetzen, das ein Spiel gerade nutzt. Starte es neu, wenn der Testaufbau stabil ist.",
        ["**2. Establish a simple baseline.** Temporarily use one controller and one connection (USB, Bluetooth or receiver). Disable other tools' remapping and automatic profile switching; close their tray agents and HidHide configuration windows. For a local test, end unused streaming sessions that create virtual pads."] =
            "**2. Schaffe eine einfache Ausgangslage.** Nutze vorübergehend einen Controller und eine Verbindung (USB, Bluetooth oder Empfänger). Deaktiviere das Umbelegen und den automatischen Profilwechsel anderer Programme; schließe deren Infobereich-Agenten und HidHide-Konfigurationsfenster. Beende für einen lokalen Test ungenutzte Streaming-Sitzungen, die virtuelle Controller erzeugen.",
        ["**3. Check Radiata first.** Read **Settings ▸ Advanced ▸ Current Controller** and hover its tray icon for isolation status. No controller points to detection or hiding; a working wheel with game input underneath points to isolation or another input path."] =
            "**3. Prüfe zuerst Radiata.** Lies **Einstellungen ▸ Erweitert ▸ Aktueller Controller** und fahre für den Isolationsstatus über das Symbol im Infobereich. Kein Controller deutet auf Erkennung oder Verstecken hin; ein funktionierendes Rad mit Spieleingabe darunter deutet auf die Isolation oder einen anderen Eingabepfad hin.",
        ["**4. Reconnect in a controlled order.** With the game and competing readers closed, start Radiata, connect the controller, and wait for its status to settle. Start Steam or the launcher afterwards, then the game. A reader that opened the device before cloaking may retain access until it closes or the device reconnects."] =
            "**4. Verbinde in kontrollierter Reihenfolge neu.** Starte bei geschlossenem Spiel und geschlossenen konkurrierenden Lesern zuerst Radiata, verbinde den Controller und warte, bis sich sein Status einpendelt. Starte danach Steam oder den Launcher, dann das Spiel. Ein Leser, der das Gerät vor der Tarnung geöffnet hat, behält den Zugriff möglicherweise, bis er schließt oder sich das Gerät neu verbindet.",
        ["Start Radiata before anything else that reads the controller, connect the pad and let its status settle, then open Steam or your launcher, and the game last."] =
            "Starte Radiata vor allem anderen, das den Controller liest, verbinde den Controller und lass seinen Status sich einpendeln, öffne dann Steam oder deinen Launcher und zuletzt das Spiel.",
        ["**5. Test in a safe game menu.** Opening a wheel should stop Radiata's virtual pad from driving the game until the wheel closes. Windows' `joy.cpl` can help identify extra controllers, but one entry there does not prove isolation in every game or input API."] =
            "**5. Teste in einem sicheren Spielmenü.** Ein geöffnetes Rad sollte verhindern, dass Radiatas virtueller Controller das Spiel steuert, bis das Rad schließt. Das `joy.cpl` von Windows hilft beim Aufspüren überzähliger Controller, aber ein Eintrag dort beweist keine Isolation in jedem Spiel und in jeder Eingabe-API.",
        ["**6. Restore other tools one at a time.** The combination that brings back the symptom is useful evidence. Reading a controller and successfully isolating it are separate things: Radiata does not provide a general physical-device-to-XInput-slot picker."] =
            "**6. Stelle andere Programme einzeln wieder her.** Die Kombination, die das Symptom zurückbringt, ist ein nützlicher Hinweis. Einen Controller zu lesen und ihn erfolgreich zu isolieren sind zwei verschiedene Dinge: Radiata bietet keine allgemeine Zuordnung von physischem Gerät zu XInput-Platz.",
        ["For comparison, enable [[passthru-mode|Passthru Mode]] before launching the game. Other tools can still hide or remap the device, and wheel input reaching the game is expected in this mode. Disabling the wheels alone does not release capture."] =
            "Aktiviere zum Vergleich den [[passthru-mode|Direktmodus]], bevor du das Spiel startest. Andere Programme können das Gerät weiterhin verstecken oder umbelegen, und dass Rad-Eingaben das Spiel erreichen, ist in diesem Modus zu erwarten. Die Räder allein zu deaktivieren gibt die Erfassung nicht frei.",
        ["What to include in a support report"] =
            "Was in einen Supportbericht gehört",
        ["Record Windows and Radiata versions, controller model and transport, controller mode, tray status, driver versions, other tools and active profiles, startup order, and the first step that changes the result. Include whether input works with Radiata closed and in Passthru Mode."] =
            "Notiere die Versionen von Windows und Radiata, Controller-Modell und Anschlussart, den Controller-Modus, den Status im Infobereich, die Treiberversionen, andere Programme und aktive Profile, die Startreihenfolge und den ersten Schritt, der das Ergebnis ändert. Gib auch an, ob die Eingabe bei geschlossenem Radiata und im Direktmodus funktioniert.",
        ["Use **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to Developer…** to prepare a diagnostic ZIP. Review it before attaching it; it may contain device identifiers and personal paths. Include the actual error text from driver setup or HID Diagnostics."] =
            "Nutze **Einstellungen ▸ Erweitert ▸ Fehlerbehebung ▸ Protokoll an den Entwickler senden…**, um eine Diagnose-ZIP vorzubereiten. Prüfe sie, bevor du sie anhängst; sie kann Gerätekennungen und persönliche Pfade enthalten. Füge den genauen Fehlertext aus der Treiberinstallation oder der HID-Diagnose bei.",

        // ── topic:controller-tool-conflicts ──
        ["reWASD, DS4Windows & other controller tools"] =
            "reWASD, DS4Windows & andere Controller-Programme",
        ["rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapper conflict virtual controller duplicate xbox slot autodetect"] =
            "rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapper konflikt virtueller controller duplikat xbox platz automatische erkennung",
        ["Two tools can read one pad and produce two outputs, or one can hide the pad from the other. Start with one active remapper for that controller. Coexistence depends on versions, hiding rules, transport and game."] =
            "Zwei Programme können einen Controller lesen und zwei Ausgaben erzeugen, oder eines kann den Controller vor dem anderen verstecken. Beginne mit einem aktiven Remapper für diesen Controller. Das Nebeneinander hängt von Versionen, Versteckregeln, Anschlussart und Spiel ab.",
        ["reWASD"] =
            "reWASD",
        ["Turn **Remap OFF** for the affected device or group and pause **Autodetect** for the test. Closing the main window does not necessarily stop mappings. Check the tray agent and confirm its virtual output is gone before retesting. See [reWASD Tray Agent](https://help.rewasd.com/interface/tray-agent.html)."] =
            "Schalte **Remap** für das betroffene Gerät oder die Gruppe AUS und pausiere **Autodetect** für den Test. Das Schließen des Hauptfensters stoppt die Belegungen nicht zwangsläufig. Prüfe den Agenten im Infobereich und stelle sicher, dass seine virtuelle Ausgabe verschwunden ist, bevor du erneut testest. Siehe [reWASD Tray Agent](https://help.rewasd.com/interface/tray-agent.html).",
        ["reWASD has its own virtual-device and hiding settings. Repairing ViGEmBus or adding Radiata to HidHide cannot fix every reWASD visibility rule. Record which devices remain visible; consult [reWASD troubleshooting](https://help.rewasd.com/faq/troubleshooting.html) for its own errors."] =
            "reWASD hat eigene Einstellungen für virtuelle Geräte und fürs Verstecken. ViGEmBus zu reparieren oder Radiata zu HidHide hinzuzufügen behebt nicht jede Sichtbarkeitsregel von reWASD. Notiere, welche Geräte sichtbar bleiben; ziehe für dessen eigene Fehler die [reWASD-Fehlerbehebung](https://help.rewasd.com/faq/troubleshooting.html) zurate.",
        ["DS4Windows, DSX and InputMapper"] =
            "DS4Windows, DSX und InputMapper",
        ["Stop controller output and fully exit the tool, including its tray process, for the baseline. Check automatic startup and profiles if it returns. Another virtual Xbox or DualShock controller can cause duplicate actions or change which device the game selects."] =
            "Stoppe für die Ausgangslage die Controller-Ausgabe und beende das Programm vollständig, einschließlich seines Prozesses im Infobereich. Prüfe Autostart und Profile, falls es zurückkehrt. Ein weiterer virtueller Xbox- oder DualShock-Controller kann doppelte Aktionen verursachen oder ändern, welches Gerät das Spiel auswählt.",
        ["If the setup uses HidHide, physical-device blocks can remain after the remapper exits. Check Radiata's access using [[hidhide-troubleshooting|HidHide troubleshooting]]. Radiata does not take ownership of another tool's existing blocks, so quitting Radiata does not clear them."] =
            "Nutzt der Aufbau HidHide, können Sperren für physische Geräte bestehen bleiben, nachdem der Remapper beendet wurde. Prüfe Radiatas Zugriff mit der [[hidhide-troubleshooting|HidHide-Fehlerbehebung]]. Radiata übernimmt die vorhandenen Sperren eines anderen Programms nicht, deshalb räumt das Beenden von Radiata sie nicht ab.",
        ["Other sources of input"] =
            "Andere Eingabequellen",
        ["**JoyToKey, AntiMicroX, macros and hardware profiles** can emit keyboard or mouse events alongside gamepad input. Neutralizing Radiata's virtual pad does not neutralize those events. Disable the mapping or controller [[turbo-mode|Turbo mode]] for the test."] =
            "**JoyToKey, AntiMicroX, Makros und Hardwareprofile** können neben der Gamepad-Eingabe Tastatur- oder Mausereignisse aussenden. Radiatas virtuellen Controller zu neutralisieren neutralisiert diese Ereignisse nicht. Deaktiviere für den Test die Belegung oder den [[turbo-mode|Turbo-Modus]] des Controllers.",
        ["**x360ce, vJoy-based tools, streaming clients and vendor utilities** can add controllers or translation layers. Check Steam Remote Play, Sunshine/Moonlight, Parsec and controller software when relevant. End only unused sessions; a remote player's virtual controller may be their only input."] =
            "**x360ce, Programme auf vJoy-Basis, Streaming-Clients und Herstellerprogramme** können Controller oder Übersetzungsschichten hinzufügen. Prüfe gegebenenfalls Steam Remote Play, Sunshine/Moonlight, Parsec und Controller-Software. Beende nur ungenutzte Sitzungen; der virtuelle Controller eines entfernten Mitspielers ist womöglich dessen einzige Eingabe.",
        ["**Old HidGuardian or ScpToolkit installations** can leave filtering or replacement drivers behind. Use the original project's removal guidance or support; do not delete arbitrary HID devices, Bluetooth drivers or registry filters. HidHide and HidGuardian are different components."] =
            "**Alte Installationen von HidGuardian oder ScpToolkit** können Filter- oder Ersatztreiber zurücklassen. Nutze die Deinstallationsanleitung oder den Support des ursprünglichen Projekts; lösche nicht wahllos HID-Geräte, Bluetooth-Treiber oder Registrierungsfilter. HidHide und HidGuardian sind verschiedene Komponenten.",
        ["**Wrong player or no spare Xbox slot?** XInput exposes four slots, which may include virtual pads. Temporarily stop unused virtual outputs and reconnect in the intended order. If Radiata reports uncertainty about its own output, wait for reconnection or quit and reopen Radiata; reinstalling drivers is not the first fix."] =
            "**Falscher Spieler oder kein freier Xbox-Platz?** XInput stellt vier Plätze bereit, die auch virtuelle Controller enthalten können. Stoppe ungenutzte virtuelle Ausgaben vorübergehend und verbinde in der beabsichtigten Reihenfolge neu. Meldet Radiata Unsicherheit über seine eigene Ausgabe, warte auf die Neuverbindung oder beende Radiata und öffne es erneut; die Treiber neu zu installieren ist nicht die erste Lösung.",
        ["If another remapper is essential, test it with Radiata in [[passthru-mode|Passthru Mode]] first. That avoids a second Radiata stand-in, but it does not promise isolation or preservation of native controller features through the other tool."] =
            "Ist ein anderer Remapper unverzichtbar, teste ihn zuerst mit Radiata im [[passthru-mode|Direktmodus]]. Das vermeidet einen zweiten Radiata-Ersatzcontroller, verspricht aber weder Isolation noch den Erhalt nativer Controller-Funktionen über das andere Programm.",

        // ── topic:hidhide-troubleshooting ──
        ["HidHide: contention, lockouts & shared settings"] =
            "HidHide: Konkurrenz, Aussperrungen & gemeinsame Einstellungen",
        ["hidhide contention busy access denied configuration client cli lockout whitelist allow list inverse cloak path moved renamed usb bluetooth shared hidden controller recovery"] =
            "hidhide konkurrenz belegt zugriff verweigert konfigurationsclient cli aussperrung whitelist zulassungsliste inverse tarnung pfad verschoben umbenannt usb bluetooth gemeinsam versteckter controller wiederherstellung",
        ["Busy or access-failed status"] =
            "Status „belegt“ oder „Zugriff fehlgeschlagen“",
        ["The **HidHide Configuration Client** holds the driver's exclusive configuration connection while it's open. A running or stuck **HidHideCLI** can also contend with Radiata. Close those tools completely, then allow about **15 seconds** for Radiata's retry before trying **Recover Controller**. Contention does not mean the driver needs reinstalling."] =
            "Der **HidHide-Konfigurationsclient** hält die exklusive Konfigurationsverbindung des Treibers, solange er geöffnet ist. Auch ein laufendes oder hängendes **HidHideCLI** kann mit Radiata konkurrieren. Schließe diese Programme vollständig und gib Radiatas Wiederholungsversuch etwa **15 Sekunden**, bevor du **Controller wiederherstellen** probierst. Konkurrenz bedeutet nicht, dass der Treiber neu installiert werden muss.",
        ["Windows sees the controller, but Radiata does not"] =
            "Windows sieht den Controller, Radiata nicht",
        ["Close the game and quit Radiata before inspecting HidHide. In normal mode, the **Applications** list grants access to hidden controllers. Verify the exact `Radiata.exe` you launch is listed - installed, portable, renamed and moved copies all have different paths. Radiata normally registers itself; **Install/Repair Drivers…** can repair registration, subject to its reported result."] =
            "Schließe das Spiel und beende Radiata, bevor du HidHide untersuchst. Im normalen Modus gewährt die Liste **Applications** Zugriff auf versteckte Controller. Prüfe, ob genau die `Radiata.exe` gelistet ist, die du startest – installierte, portable, umbenannte und verschobene Kopien haben alle unterschiedliche Pfade. Radiata registriert sich normalerweise selbst; **Treiber installieren/reparieren…** kann die Registrierung reparieren, je nach gemeldetem Ergebnis.",
        ["Check the selected physical device on **Devices**. USB and Bluetooth can have separate entries. Do not hide the virtual stand-in the game needs. Use [Nefarius's setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) to identify the device, and close the client before restarting Radiata."] =
            "Prüfe das ausgewählte physische Gerät unter **Devices**. USB und Bluetooth können getrennte Einträge haben. Verstecke nicht den virtuellen Ersatzcontroller, den das Spiel braucht. Nutze [Nefarius' Einrichtungsanleitung](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/), um das Gerät zu bestimmen, und schließe den Client, bevor du Radiata neu startest.",
        ["**Inverse application cloak reverses the list's meaning.** Radiata preserves this shared setting and declines capture when it is enabled. Record the configuration and coordinate with the tool that needs it before choosing normal mode for Radiata; changing it affects other applications too."] =
            "**Die inverse Anwendungstarnung kehrt die Bedeutung der Liste um.** Radiata bewahrt diese gemeinsame Einstellung und verzichtet auf die Erfassung, solange sie aktiviert ist. Notiere die Konfiguration und stimme dich mit dem Programm ab, das sie braucht, bevor du für Radiata den normalen Modus wählst; eine Änderung betrifft auch andere Anwendungen.",
        ["The game still receives input"] =
            "Das Spiel bekommt weiterhin Eingaben",
        ["An application allowed through HidHide can still read the physical device. Review entries deliberately; do not add the game, Steam, or every executable as a general fix for double input."] =
            "Eine über HidHide zugelassene Anwendung kann das physische Gerät trotzdem lesen. Prüfe die Einträge mit Bedacht; füge nicht das Spiel, Steam oder jede ausführbare Datei als Allheilmittel gegen doppelte Eingabe hinzu.",
        ["After a fresh install, reconnect the controller or restart Windows if requested, so the filter can attach. Restart readers that opened the device before cloaking. Configuration readback alone does not verify what a running game receives."] =
            "Verbinde nach einer Neuinstallation den Controller neu oder starte Windows neu, falls das verlangt wird, damit sich der Filter anhängen kann. Starte Leser neu, die das Gerät vor der Tarnung geöffnet haben. Die Konfiguration zurückzulesen belegt allein noch nicht, was ein laufendes Spiel empfängt.",
        ["HidHide has limitations, including some Raw Input readers and Xbox/XInput configurations. If leakage survives a clean startup, record the game and transport rather than assuming a successful hide operation guarantees exclusive input. See the [HidHide FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/)."] =
            "HidHide hat Grenzen, darunter einige Raw-Input-Leser und Xbox/XInput-Konfigurationen. Übersteht das Durchschlagen einen sauberen Start, notiere Spiel und Anschlussart, statt anzunehmen, ein erfolgreiches Verstecken garantiere exklusive Eingabe. Siehe die [HidHide-FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/).",
        ["The controller stays hidden after exit"] =
            "Der Controller bleibt nach dem Beenden versteckt",
        ["Radiata adopts existing hidden-device entries matching the controller model it manages, including entries from another connection method, so they can be released on exit. This can also release another tool's matching entries; you should never have two tools manage hiding for the same controller. Uninstall clears all hidden-device entries unless another Radiata copy is running. A failed release must be recovered before removal can finish."] =
            "Radiata übernimmt vorhandene Einträge für versteckte Geräte, die zu dem von ihm verwalteten Controller-Modell passen, auch solche aus einer anderen Verbindungsart, damit sie beim Beenden freigegeben werden können. Das kann auch die passenden Einträge eines anderen Programms freigeben; du solltest nie zwei Programme das Verstecken desselben Controllers verwalten lassen. Die Deinstallation räumt alle Einträge für versteckte Geräte ab, sofern nicht noch eine andere Radiata-Kopie läuft. Eine fehlgeschlagene Freigabe muss behoben werden, bevor das Entfernen abgeschlossen werden kann.",
        ["Keep keyboard and mouse access available while changing controller visibility. Record existing settings first, change only the identified controller or application entry, and close the configuration client before retesting."] =
            "Halte Tastatur- und Mauszugriff verfügbar, während du die Sichtbarkeit des Controllers änderst. Notiere zuerst die bestehenden Einstellungen, ändere nur den bestimmten Controller- oder Anwendungseintrag und schließe den Konfigurationsclient, bevor du erneut testest.",

        // ── topic:driver-conflicts ──
        ["HP OMEN & driver version conflicts"] =
            "HP OMEN & Treiberversionskonflikte",
        ["hp omen gaming hub fusion vigem vigembus foreign fork driver version mismatch 10.x 1.22.0 1.5.230 oculus virtual desktop repair install bus device manager restart"] =
            "hp omen gaming hub fusion vigem vigembus fremd fork treiberversion abweichung 10.x 1.22.0 1.5.230 oculus virtual desktop reparieren installieren bus geräte-manager neustart",
        ["**ViGEmBus creates the virtual controller; HidHide controls access to the physical one.** A working driver of one kind does not establish that the other works. Check the driver result log and Radiata's isolation status before repeating an installer."] =
            "**ViGEmBus erzeugt den virtuellen Controller; HidHide steuert den Zugriff auf den physischen.** Dass ein Treiber der einen Art funktioniert, belegt nicht, dass der andere funktioniert. Prüfe das Ergebnisprotokoll der Treiber und Radiatas Isolationsstatus, bevor du einen Installer wiederholst.",
        ["HP OMEN Gaming Hub / OMEN Fusion"] =
            "HP OMEN Gaming Hub / OMEN Fusion",
        ["Some HP OMEN systems have a vendor-modified ViGEmBus, and Radiata can connect to that bus instead of the correct one. A reported **10.x** version can be HP's old fork, not a newer compatible Nefarius driver."] =
            "Manche HP-OMEN-Systeme haben einen vom Hersteller veränderten ViGEmBus, und Radiata kann sich statt mit dem richtigen mit diesem Bus verbinden. Eine gemeldete Version **10.x** kann HPs alter Fork sein und kein neuerer kompatibler Nefarius-Treiber.",
        ["Radiata names detected foreign buses and skips installing over them. Its driver removal also leaves another program's bus in place. Repeated **Install/Repair Drivers…** attempts will not switch an HP-owned bus to Nefarius's."] =
            "Radiata benennt erkannte fremde Busse und installiert nicht darüber. Auch seine Treiberentfernung lässt den Bus eines anderen Programms stehen. Wiederholte Versuche mit **Treiber installieren/reparieren…** werden einen HP-eigenen Bus nicht gegen den von Nefarius tauschen.",
        ["Follow [Nefarius's HP OMEN guidance](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) and its linked [HP issue and switching instructions](https://github.com/nefarius/ViGEmBus/issues/99), or contact HP. Disabling the vendor bus can break dependent OMEN features; Radiata does not perform the device or registry changes for you."] =
            "Folge [Nefarius' Hinweisen zu HP OMEN](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) und den dort verlinkten [Angaben zum HP-Problem und zum Umstellen](https://github.com/nefarius/ViGEmBus/issues/99), oder wende dich an HP. Den Hersteller-Bus zu deaktivieren kann davon abhängige OMEN-Funktionen zerstören; Radiata nimmt die Geräte- oder Registrierungsänderungen nicht für dich vor.",
        ["A foreign bus that refuses a virtual DualShock 4 may make Radiata fall back to an **Xbox 360 stand-in for that session**. Unexpected Xbox prompts can therefore be a driver clue rather than a changed glyph setting. This fallback is not a compatibility guarantee."] =
            "Ein fremder Bus, der einen virtuellen DualShock 4 ablehnt, kann dazu führen, dass Radiata **für diese Sitzung auf einen Xbox-360-Ersatz** zurückfällt. Unerwartete Xbox-Symbole können also ein Treiberhinweis sein und keine geänderte Glyph-Einstellung. Dieser Rückfall ist keine Kompatibilitätsgarantie.",
        ["Versions and duplicate buses"] =
            "Versionen und doppelte Busse",
        ["This Radiata build bundles **ViGEmBus 1.22.0** and **HidHide 1.5.230**. ViGEmBus is retired; 1.22.0 is its final official release. Installer, application, client-library and driver versions are different numbers - they are not supposed to match each other."] =
            "Dieser Radiata-Build bringt **ViGEmBus 1.22.0** und **HidHide 1.5.230** mit. ViGEmBus ist eingestellt; 1.22.0 ist seine letzte offizielle Version. Die Versionen von Installer, Anwendung, Client-Bibliothek und Treiber sind verschiedene Nummern – sie sollen gar nicht übereinstimmen.",
        ["Open **Device Manager ▸ View ▸ Devices by connection** and inspect virtual gamepad bus entries. Record each bus's name, provider, driver version and device status. Multiple buses, unexpected providers, or a version different from the one Radiata bundles all warrant investigation; a higher number alone does not prove compatibility."] =
            "Öffne **Geräte-Manager ▸ Ansicht ▸ Geräte nach Verbindung** und sieh dir die Einträge der virtuellen Gamepad-Busse an. Notiere zu jedem Bus Name, Anbieter, Treiberversion und Gerätestatus. Mehrere Busse, unerwartete Anbieter oder eine andere Version als die von Radiata mitgelieferte sind allesamt einen Blick wert; eine höhere Nummer allein belegt keine Kompatibilität.",
        ["**Oculus and Virtual Desktop** setups can also supply their own buses. Several virtual gamepads beneath one bus are different from several competing bus drivers. Identify the owning program before changing either."] =
            "Auch Installationen von **Oculus und Virtual Desktop** können eigene Busse mitbringen. Mehrere virtuelle Gamepads unter einem Bus sind etwas anderes als mehrere konkurrierende Bustreiber. Bestimme das zuständige Programm, bevor du an einem von beiden etwas änderst.",
        ["For an ordinary missing or older bundled driver, use **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers…**, review its result, and complete any requested restart. Close HidHide tools first. If repair fails, keep the error text and driver versions for support."] =
            "Bei einem schlicht fehlenden oder veralteten mitgelieferten Treiber nutze **Einstellungen ▸ Erweitert ▸ Fehlerbehebung ▸ Treiber installieren/reparieren…**, sieh dir das Ergebnis an und führe einen verlangten Neustart durch. Schließe vorher die HidHide-Programme. Schlägt die Reparatur fehl, hebe den Fehlertext und die Treiberversionen für den Support auf.",
        ["Driver removal affects every application using that shared component. Use the [official ViGEmBus install/remove guide](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) for a confirmed conflict. Its full purge is advanced recovery, not a first step for double input. Do not force-delete unrelated drivers or use unofficial download sites."] =
            "Das Entfernen eines Treibers betrifft jede Anwendung, die diese gemeinsame Komponente nutzt. Nutze bei einem bestätigten Konflikt die [offizielle ViGEmBus-Anleitung zum Installieren und Entfernen](https://docs.nefarius.at/projects/ViGEm/How-to-Install/). Deren vollständige Bereinigung ist fortgeschrittene Wiederherstellung und kein erster Schritt gegen doppelte Eingabe. Erzwinge nicht das Löschen unbeteiligter Treiber und nutze keine inoffiziellen Downloadseiten.",

        // ── topic:overlay-not-visible ──
        ["Wheel not visible over a game"] =
            "Das Rad ist über dem Spiel nicht sichtbar",
        ["overlay fullscreen borderless windowed exclusive primary display monitor uac"] =
            "overlay vollbild randlos fenstermodus exklusiv primärer bildschirm monitor uac",
        ["**Run games Borderless Windowed**, not exclusive fullscreen. Exclusive fullscreen bypasses the compositor Radiata draws through. The setting is in most games' display options, and the performance difference on Windows 10/11 is negligible."] =
            "**Lass Spiele im randlosen Fenstermodus laufen**, nicht im exklusiven Vollbild. Exklusives Vollbild umgeht den Compositor, durch den Radiata zeichnet. Die Einstellung steckt bei den meisten Spielen in den Anzeigeoptionen, und der Leistungsunterschied unter Windows 10/11 ist vernachlässigbar.",
        ["Radiata draws on the **primary display only** - on a multi-monitor rig, make your gaming display the Windows primary (Settings ▸ System ▸ Display)."] =
            "Radiata zeichnet **nur auf dem Hauptbildschirm** – mache bei mehreren Monitoren deinen Spielbildschirm zum Windows-Hauptbildschirm (Einstellungen ▸ System ▸ Anzeige).",
        ["Windows-secured screens (UAC prompts, the lock screen) can never be drawn over. That's a Windows limitation, and nothing can work around it."] =
            "Über von Windows geschützte Bildschirme (UAC-Abfragen, den Sperrbildschirm) kann nie gezeichnet werden. Das ist eine Einschränkung von Windows, und es gibt keinen Weg daran vorbei.",

        // ── topic:steam-conflicts ──
        ["Steam Input & Steam quirks"] =
            "Steam Input & Eigenheiten von Steam",
        ["steam playstation controller support big picture guide magnifier chord double input unlock controller"] =
            "Steam PlayStation Controller-Unterstützung Big Picture Guide Bildschirmlupe Tastenkombination doppelte Eingabe Controller entsperren",
        ["Steam Input can translate a controller into gamepad, keyboard or mouse input. Another mapping layer can change prompts and bindings or create duplicate actions. Test per game before changing global settings."] =
            "Steam Input kann einen Controller in Gamepad-, Tastatur- oder Mauseingaben übersetzen. Eine weitere Zuordnungsschicht kann Symbole und Belegungen ändern oder doppelte Aktionen erzeugen. Teste pro Spiel, bevor du globale Einstellungen änderst.",
        ["**Wrong buttons or duplicate actions?** With the game closed, open its **Steam Library ▸ Properties ▸ Controller** and try **Disable Steam Input** in the per-game override. Relaunch and compare; restore the previous setting if the game or remote setup needs Steam Input. Global options are under **Steam ▸ Settings ▸ Controller**, with names that vary by Steam version."] =
            "**Falsche Tasten oder doppelte Aktionen?** Öffne bei geschlossenem Spiel seine **Steam-Bibliothek ▸ Eigenschaften ▸ Controller** und probiere **Steam Input deaktivieren** in der spielbezogenen Überschreibung. Starte neu und vergleiche; stelle die vorherige Einstellung wieder her, falls das Spiel oder der Remote-Aufbau Steam Input braucht. Die globalen Optionen stehen unter **Steam ▸ Einstellungen ▸ Controller**, mit Bezeichnungen, die je nach Steam-Version variieren.",
        ["**No input with Steam Input disabled?** The game may not support Radiata's virtual DualShock controller. For a Sony pad, try [[controller-mode|Xbox Mode]] before launching, or restore Steam Input. That's a game compatibility choice, not necessarily a driver failure."] =
            "**Keine Eingabe bei deaktiviertem Steam Input?** Das Spiel unterstützt Radiatas virtuellen DualShock-Controller womöglich nicht. Probiere bei einem Sony-Controller vor dem Start den [[controller-mode|Xbox-Modus]] oder stelle Steam Input wieder her. Das ist eine Frage der Spielkompatibilität und nicht zwingend ein Treiberfehler.",
        ["**Double input despite a successful cloak?** Steam may have opened the physical controller before Radiata hid it. Save and close Steam games before fully exiting Steam, then start Radiata and let capture settle before reopening Steam. Reconnecting the pad can also release stale handles. Follow any controller-unblock notice; closing Steam's window alone may leave it running."] =
            "**Doppelte Eingabe trotz erfolgreicher Tarnung?** Steam hat den physischen Controller möglicherweise geöffnet, bevor Radiata ihn versteckt hat. Speichere und schließe Steam-Spiele, bevor du Steam ganz beendest, starte dann Radiata und lass die Erfassung sich einpendeln, bevor du Steam wieder öffnest. Auch den Controller neu zu verbinden kann veraltete Handles freigeben. Folge einem etwaigen Hinweis zum Entsperren des Controllers; Steams Fenster zu schließen lässt es unter Umständen weiterlaufen.",
        ["**Desktop keys or mouse movement?** Check Steam's **Desktop Layout** and **Guide Button Chord** layout as well as the game's layout. These can emit input outside the game. See [[controller-conflict-checklist|Controller conflict checklist]] for a controlled comparison."] =
            "**Desktop-Tasten oder Mausbewegung?** Prüfe neben dem Layout des Spiels auch Steams **Desktop-Layout** und das Layout **Guide-Tastenkombination**. Diese können Eingaben außerhalb des Spiels aussenden. Siehe die [[controller-conflict-checklist|Checkliste für Controller-Konflikte]] für einen kontrollierten Vergleich.",
        ["**Steam Remote Play may rely on Steam Input.** Keep a local fallback before changing its input path. Valve explains the translation layer in [Steam Input gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)."] =
            "**Steam Remote Play kann auf Steam Input angewiesen sein.** Halte eine lokale Rückfallebene bereit, bevor du dessen Eingabepfad änderst. Valve erklärt die Übersetzungsschicht unter [Steam Input Gamepad-Emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices).",
        ["**Windows Magnifier opens by itself?** That's Steam's *Guide Button Chord* layout (Guide + face button), not Radiata - and powering a pad off by holding the PS button can leave that layout latched. One clean Guide press-and-release clears it; disable it under Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout."] =
            "**Die Windows-Bildschirmlupe öffnet sich von selbst?** Das ist Steams Layout *Guide Button Chord* (Guide + eine Fronttaste), nicht Radiata – und einen Controller durch Halten der PS-Taste auszuschalten kann dieses Layout eingerastet zurücklassen. Ein sauberes Drücken und Loslassen der Guide-Taste löst es; deaktivieren lässt es sich unter Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout.",

        // ── topic:turbo-mode ──
        ["Controller Turbo / rapid-fire mode"] =
            "Turbo- bzw. Dauerfeuermodus des Controllers",
        ["turbo rapid fire auto repeat macro cycling flicker wheel closes dismiss premature bounce double input jitter"] =
            "turbo dauerfeuer automatische wiederholung makro flackern rad schließt sich vorzeitig doppelte eingabe",
        ["Many third-party pads have a hardware **Turbo** / rapid-fire mode that auto-repeats a held button. An accidental button combo can toggle it on in the controller firmware, and that can look exactly like a bug:"] =
            "Viele Controller von Drittanbietern haben einen **Turbo**- bzw. Dauerfeuer-Modus in der Hardware, der eine gehaltene Taste automatisch wiederholt. Eine versehentliche Tastenkombination kann ihn in der Firmware des Controllers einschalten, und das kann genau wie ein Fehler aussehen:",
        ["The wheel **flickers open and shut**, or **dismisses on its own** right after opening."] =
            "Der Ring **flackert auf und zu** oder **schließt sich von selbst** direkt nach dem Öffnen.",
        ["The Game Grid's **filters cycle rapidly**, or the selection jumps on its own."] =
            "Die **Filter im Spieleraster wechseln in schneller Folge**, oder die Auswahl springt von selbst.",
        ["Slices **fire the instant a wheel opens**, or a hold-to-confirm slice never settles."] =
            "Segmente **werden im Moment des Öffnens ausgeführt**, oder ein Segment mit Bestätigung durch Halten kommt nie zur Ruhe.",
        ["Turn Turbo off on the controller itself - usually a button combo (often Home/Guide + a face or shoulder button, or a dedicated Turbo button), frequently with its own LED. Check your pad's manual for the exact combo."] =
            "Schalte Turbo am Controller selbst aus – meist über eine Tastenkombination (häufig Home/Guide plus eine Front- oder Schultertaste, oder eine eigene Turbo-Taste), oft mit einer eigenen LED. Die genaue Kombination steht im Handbuch deines Controllers.",
        ["If it persists with Turbo confirmed off, it's something else - see [[opening-a-wheel|Opening a wheel]] and [[picking-an-action|Aiming & firing]]."] =
            "Bleibt es bestehen, obwohl Turbo nachweislich aus ist, liegt es an etwas anderem – siehe [[opening-a-wheel|Ein Rad öffnen]] und [[picking-an-action|Zielen und Auslösen]].",

        // ── topic:common-issues ──
        ["Other common issues"] =
            "Weitere häufige Probleme",
        ["hdr unavailable rdp remote play streaming config json double input"] =
            "HDR nicht verfügbar RDP Remote Play Streaming config json doppelte Eingabe",
        ["**HDR shows `Unavailable`** - the display state can't be read in that context, such as in Remote Play or streaming."] =
            "**HDR zeigt `Unavailable`** – der Anzeigezustand lässt sich in diesem Kontext nicht auslesen, etwa bei Remote Play oder beim Streaming.",
        ["**Editing `config.json` by hand** (`%APPDATA%\\Radiata`) - supported. The app hot-reloads its own writes reliably, but outside edits are occasionally missed, so restart Radiata after manual edits."] =
            "**`config.json` von Hand bearbeiten** (`%APPDATA%\\Radiata`) – wird unterstützt. Die App lädt ihre eigenen Schreibvorgänge zuverlässig im laufenden Betrieb neu, Änderungen von außen werden aber gelegentlich übersehen, starte Radiata nach manuellen Bearbeitungen also neu.",

        // ── figure ──
        ["Isolated"] =
            "Isoliert",
        ["Your controller"] =
            "Dein Controller",
        ["Virtual pad"] =
            "Virtueller Controller",
        ["The game"] =
            "Das Spiel",
        ["cloaked"] =
            "verschleiert",
        ["Passthru Mode, or no drivers"] =
            "Direktmodus, oder keine Treiber",
        ["no virtual pad"] =
            "kein virtueller Controller",
        ["{cross} picks the slice up"] =
            "{cross} nimmt das Segment auf",
        ["Aim to the target slot"] =
            "Auf den Zielplatz zielen",
        ["{cross} drops it — the ring reflows"] =
            "{cross} legt es ab — der Ring ordnet sich neu",
        ["Its chord"] =
            "Seine Kombination",
        ["Slices on it — this wheel opens."] =
            "Mit Segmenten — dieser Ring öffnet sich.",
        ["No slices — this wheel draws nothing, so its chord reaches the game."] =
            "Ohne Segmente — dieser Ring zeichnet nichts, daher erreicht seine Kombination das Spiel.",
        ["Modifiers — held around it"] =
            "Modifikatoren — währenddessen gehalten",
        ["The key that's pressed"] =
            "Die Taste, die gedrückt wird",
        ["Steam or your launcher"] =
            "Steam oder dein Launcher",
        ["Game and other tools closed"] =
            "Spiel und andere Tools geschlossen",
        ["Wait for its status to settle"] =
            "Warten, bis sich der Status einpendelt",
        ["Make the folder"] =
            "Ordner anlegen",
        ["Write the manifest"] =
            "Manifest schreiben",
        ["Restart Radiata"] =
            "Radiata neu starten",
        ["Accept the confirmation"] =
            "Bestätigung annehmen",
        ["Try it out"] =
            "Ausprobieren",
        ["Change something"] =
            "Etwas ändern",
        ["clockwise"] =
            "im Uhrzeigersinn",
        ["Radiata finds it"] =
            "Radiata findet es",
        ["One folder too many"] =
            "Ein Ordner zu viel",
        ["Move the files up a level"] =
            "Dateien eine Ebene nach oben verschieben",
        ["custom material theme package material.json drop in author make build own skin palette colors colours gradient fill hue walk outline glyph glow texture png jpg sound wav appdata packages folder soundtheme sound set kawaii mesa salvage reactor obsidian digital physical format token consent confirm restart trace log rejected not showing workshop sample starter ember comments drag drop zip install uninstall remove delete recycle bin right-click"] =
            "eigene material design paket material.json ablegen hinzufügen autor erstellen bauen eigenes skin palette farben verlauf füllung farbton kontur glyphe leuchten textur png jpg klang wav appdata packages ordner soundtheme klangsatz kawaii mesa salvage reactor obsidian digital physisch format token zustimmung bestätigen neustart protokoll log abgelehnt wird nicht angezeigt workshop beispiel starter ember kommentare ziehen ablegen zip installieren deinstallieren entfernen löschen papierkorb rechtsklick",
        ["**Restart Radiata after adding a theme to that folder by hand, or changing one.** Packages are scanned once, at startup, on purpose."] =
            "**Starte Radiata neu, nachdem du ein Design von Hand in diesen Ordner gelegt oder eines geändert hast.** Pakete werden absichtlich nur einmal beim Start eingelesen.",
        ["**Or drag and drop it.** Drop the theme's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. What you dropped stays where it was."] =
            "**Oder zieh es einfach hinein.** Lege den Designordner oder eine ZIP-Datei davon irgendwo im **Einstellungen**-Fenster oder auf `Radiata.exe` oder einer Verknüpfung dazu ab. Radiata kopiert es an die richtige Stelle und bittet um Bestätigung, ohne Neustart. Was du abgelegt hast, bleibt an seinem Ort.",
        ["Dropping a theme you already have asks before replacing it; the old copy goes to the **Recycle Bin**. A changed version takes effect after a restart."] =
            "Legst du ein Design ab, das du schon hast, fragt Radiata vor dem Ersetzen nach; die alte Kopie landet im **Papierkorb**. Eine geänderte Version wird nach einem Neustart wirksam.",
        ["**To remove a theme,** right-click its tile in **Settings ▸ Customize** and choose **Uninstall theme**. Its folder goes to the Recycle Bin; if it was your material, the wheel switches to **Pearl**. Restore the folder from the Recycle Bin and restart Radiata to get it back."] =
            "**Um ein Design zu entfernen,** klicke in **Einstellungen ▸ Anpassen** mit der rechten Maustaste auf seine Kachel und wähle **Design deinstallieren**. Der Ordner landet im Papierkorb; war es dein Material, wechselt das Rad zu **Perle**. Stelle den Ordner aus dem Papierkorb wieder her und starte Radiata neu, um es zurückzubekommen.",
        ["custom arcade game package game.json javascript js script write author make build own sandbox jint helper polar disc draw tick input kv hiscore cue sound appdata packages folder restart consent confirm trace log rejected not showing workshop sample firefly strict console error budget tint colour color cabinet preview screenshot nameplate launcher drag drop zip install"] =
            "eigenes arcade spiel paket game.json javascript js skript schreiben autor erstellen bauen eigenes sandbox jint hilfsfunktion polar scheibe zeichnen tick eingabe kv hiscore signal klang appdata packages ordner neustart zustimmung bestätigen protokoll log abgelehnt wird nicht angezeigt workshop beispiel firefly strikt konsole fehler budget farbton farbe automat vorschau screenshot namensschild starter ziehen ablegen zip installieren",
        ["**Or drag and drop it.** Drop the game's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. A game you already have asks before replacing it, and a changed version takes effect after a restart."] =
            "**Oder zieh es einfach hinein.** Lege den Spieleordner oder eine ZIP-Datei davon irgendwo im **Einstellungen**-Fenster oder auf `Radiata.exe` oder einer Verknüpfung dazu ab. Radiata kopiert es an die richtige Stelle und bittet um Bestätigung, ohne Neustart. Bei einem Spiel, das du schon hast, wird vor dem Ersetzen nachgefragt, und eine geänderte Version wird nach einem Neustart wirksam.",
        ["**Restart Radiata** after adding a game to that folder by hand, or changing one, then accept the confirmation. It shows up in the Arcade picker beside the built-in games, and as a choice when you add an **Arcade** slice. In your config it's the token `pkg-<id>`."] =
            "**Starte Radiata neu,** nachdem du ein Spiel von Hand in diesen Ordner gelegt oder eines geändert hast, und bestätige dann die Abfrage. Es erscheint in der Arcade-Auswahl neben den eingebauten Spielen und als Option, wenn du ein **Arcade**-Segment hinzufügst. In deiner Konfiguration ist es das Token `pkg-<id>`.",
        ["workshop test testing debug debugging share sharing zip unzip send friend install license trace log skipped error not showing missing folder nested backup move another pc drag drop"] =
            "workshop testen test debuggen fehlersuche teilen weitergeben zip entpacken senden freund installieren lizenz protokoll log übersprungen fehler wird nicht angezeigt fehlt ordner verschachtelt sicherung umziehen anderer pc ziehen ablegen",
        ["To share a package, zip the files **inside** its folder (not the folder itself) and name the zip after the package. The person installing it drops the zip onto Radiata's **Settings** window and accepts the confirmation - or uses **Extract All** into their own Materials or Arcade Games folder and restarts Radiata."] =
            "Um ein Paket weiterzugeben, komprimiere die Dateien **im** Ordner des Pakets (nicht den Ordner selbst) und benenne die ZIP-Datei nach dem Paket. Wer es installiert, legt die ZIP-Datei im **Einstellungen**-Fenster von Radiata ab und bestätigt die Abfrage, oder er nutzt **Alle extrahieren** in seinem eigenen Materials- oder Arcade-Games-Ordner und startet Radiata neu.",
        ["Radiata is a feature-rich, controller-based radial menu utility for a Windows gaming PC. Launch a game, join the Discord call, swap to headphones, start your stream, all from a controller."] =
            "Radiata ist ein funktionsreiches, Controller-basiertes Radialmenü-Tool für einen Windows-Gaming-PC. Starte ein Spiel, tritt dem Discord-Gespräch bei, wechsle auf Kopfhörer, starte deinen Stream, alles per Controller.",
        ["Try it now: open a wheel with **{invoke}**."] =
            "Probier es jetzt aus: Öffne ein Rad mit **{invoke}**.",
        ["**Game Grid** - a universal launcher for every installed game across Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft and EA."] =
            "**Spieleraster** – ein universeller Starter für jedes installierte Spiel aus Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft und EA.",
        ["**Nothing hooked, nothing injected** - Radiata reads your controller directly and allows your input through only when a wheel isn't up, so the game underneath doesn't pick up duplicate input. For strict-anticheat titles, see [[passthru-mode|Passthru Mode]]."] =
            "**Nichts gehookt, nichts injiziert** – Radiata liest deinen Controller direkt und lässt deine Eingaben nur durch, wenn kein Rad offen ist, sodass das Spiel darunter keine doppelte Eingabe aufschnappt. Für Titel mit striktem Anti-Cheat siehe [[passthru-mode|Direktmodus]].",
        ["Start with [[opening-a-wheel|Opening a wheel]]. Then open one and click the aiming stick (L3/R3) when you're ready to start editing."] =
            "Fang mit [[opening-a-wheel|Ein Rad öffnen]] an. Öffne dann eines und klicke den Zielstick (L3/R3), wenn du mit dem Bearbeiten beginnen möchtest.",
        ["Radiata ships as a single installer, **Radiata-<version>-setup.exe**. Download it from [getradiata.app](https://getradiata.app)."] =
            "Radiata wird als einzelner Installer ausgeliefert, **Radiata-<version>-setup.exe**. Lade ihn von [getradiata.app](https://getradiata.app) herunter.",
        ["**Only download Radiata from known sources.** Anything else claiming to be Radiata isn't from the developer."] =
            "**Lade Radiata nur aus bekannten Quellen herunter.** Alles andere, das sich als Radiata ausgibt, stammt nicht vom Entwickler.",
        ["**Administrator account needed** for driver installation (optional but strongly recommended)"] =
            "**Administratorkonto nötig** für die Treiberinstallation (optional, aber dringend empfohlen)",
        ["Windows SmartScreen may show a blue **\"Windows protected your PC\"** box the first time you run the installer, and your browser may warn that the file **\"isn't commonly downloaded\"**."] =
            "Windows SmartScreen zeigt beim ersten Start des Installers möglicherweise ein blaues Feld **\"Der Computer wurde durch Windows geschützt\"**, und dein Browser warnt möglicherweise, dass die Datei **\"nicht häufig heruntergeladen wird\"**.",
        ["**No Run anyway button at all?** A managed or locked-down PC can have SmartScreen set to block outright. Radiata can't work around it."] =
            "**Gar keine Schaltfläche Trotzdem ausführen?** Auf einem verwalteten oder gesperrten PC kann SmartScreen auf vollständiges Blockieren eingestellt sein. Radiata kann das nicht umgehen.",
        ["You can confirm you have the genuine file before running it: every GitHub release lists the installer's **SHA-256**, and `Get-FileHash .\\Radiata-<version>-setup.exe` in PowerShell should print the same value. Radiata's updater automatically runs the same verification check on every update."] =
            "Du kannst vor dem Ausführen prüfen, dass du die echte Datei hast: Jedes GitHub-Release nennt den **SHA-256** des Installers, und `Get-FileHash .\\Radiata-<version>-setup.exe` in PowerShell sollte denselben Wert ausgeben. Der Updater von Radiata führt dieselbe Prüfung bei jedem Update automatisch aus.",
        ["**Antivirus false positives** happen for the same reason. If yours quarantines the installer, restore it and run it again, or download it fresh from getradiata.app."] =
            "**Antivirus-Fehlalarme** haben denselben Grund. Wenn dein Virenscanner den Installer in Quarantäne verschiebt, stelle ihn wieder her und führe ihn erneut aus, oder lade ihn frisch von getradiata.app herunter.",
        ["Installs **for your user account only**, into `%LOCALAPPDATA%\\Programs\\Radiata`. It never touches other accounts on the PC."] =
            "Installiert **nur für dein Benutzerkonto** nach `%LOCALAPPDATA%\\Programs\\Radiata`. Andere Konten auf dem PC bleiben unberührt.",
        ["Adds a **Start menu** shortcut, and on a **first** install sets Radiata to **start with Windows**. You can turn that off in the tray menu or **Settings ▸ Advanced**."] =
            "Legt eine **Startmenü**-Verknüpfung an und stellt bei einer **Erst**installation ein, dass Radiata **mit Windows startet**. Du kannst das im Tray-Menü oder unter **Einstellungen ▸ Erweitert** abschalten.",
        ["Nothing sneaky or malicious comes with Radiata. Radiata is GPLv3 free software."] =
            "Mit Radiata kommt nichts Verdächtiges oder Schädliches mit. Radiata ist freie Software unter GPLv3.",
        ["Installing over an existing copy is an **upgrade in place**. Your wheels, settings and game art are left alone."] =
            "Eine Installation über eine vorhandene Kopie ist ein **Upgrade an Ort und Stelle**. Deine Räder, Einstellungen und Spielgrafiken bleiben unangetastet.",
        ["Driver prompts (UAC prompts)"] =
            "Treiberabfragen (UAC-Abfragen)",
        ["Radiata installs without admin rights; Windows asks for permission when you install the controller drivers. Leave **Install drivers (recommended)** selected and approve the Windows prompts that follow. **ViGEmBus** and **HidHide** are the open-source drivers that keep duplicate controller input out of the game. See [[input-isolation|Input isolation]]."] =
            "Radiata wird ohne Administratorrechte installiert; Windows fragt nach deiner Erlaubnis, wenn du die Controller-Treiber installierst. Lass **Treiber installieren (empfohlen)** angehakt und bestätige die Windows-Abfragen, die darauf folgen. **ViGEmBus** und **HidHide** sind die Open-Source-Treiber, die doppelte Controller-Eingaben aus dem Spiel heraushalten. Siehe [[input-isolation|Eingabe-Isolation]].",
        ["**Declining is safe.** Radiata still works; games just also see your controller while a wheel is open, which is annoying. Install them later any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**."] =
            "**Ablehnen ist unbedenklich.** Radiata funktioniert weiterhin; Spiele sehen dann nur zusätzlich deinen Controller, solange ein Rad geöffnet ist, was lästig ist. Installiere sie später jederzeit über **Einstellungen ▸ Erweitert ▸ Fehlerbehebung ▸ Treiber installieren/reparieren**.",
        ["The drivers are shared system components other tools may also use, so if you uninstall Radiata, removing the drivers as well is optional."] =
            "Die Treiber sind gemeinsam genutzte Systemkomponenten, die auch andere Werkzeuge verwenden können. Wenn du Radiata deinstallierst, ist das Entfernen der Treiber daher optional.",
        ["Radiata then lives in the **system tray**. Click the icon for Settings or right-click for the tray menu. Then start at [[opening-a-wheel|Opening a wheel]]."] =
            "Radiata lebt danach im **Infobereich (Tray)**. Klicke das Symbol für die Einstellungen oder klicke es mit der rechten Maustaste für das Tray-Menü. Beginne dann bei [[opening-a-wheel|Ein Rad öffnen]].",
        ["**Updates are discovered automatically** by default. You can manually check for updates at **Settings ▸ Advanced ▸ Check for Updates**. Radiata always verifies the download before running it."] =
            "**Updates werden standardmäßig automatisch erkannt.** Du kannst manuell unter **Einstellungen ▸ Erweitert ▸ Nach Updates suchen** nach Updates suchen. Radiata prüft den Download immer, bevor er ausgeführt wird.",
        ["**Uninstall** from **Settings ▸ Advanced ▸ Troubleshooting ▸ Uninstall Radiata…**, or from Windows' **Installed apps** list. Your settings and the shared drivers can be removed in the same step."] =
            "**Deinstallieren** über **Einstellungen ▸ Erweitert ▸ Fehlerbehebung ▸ Radiata deinstallieren…** oder über die Windows-Liste **Installierte Apps**. Deine Einstellungen und die gemeinsam genutzten Treiber lassen sich im selben Schritt entfernen.",
        ["Your current setting: open a wheel with **{invoke}**."] =
            "Deine aktuelle Einstellung: Öffne ein Rad mit **{invoke}**.",
        ["Set your own chords (button combos that open a wheel) in [[triggers|Settings ▸ Customize ▸ Triggers]]."] =
            "Lege eigene Kombinationen (Tastenkombinationen, die ein Rad öffnen) fest unter [[triggers|Einstellungen ▸ Anpassen ▸ Aufrufgesten]].",
        ["**Flip mid-gesture:** while holding a chord, tap the opposite bumper or trigger to switch to the other wheel without having to re-input the entire chord."] =
            "**Wechsel mitten in der Geste:** Tippe, während du eine Kombination hältst, auf den gegenüberliegenden Bumper oder Trigger, um zum anderen Rad zu wechseln, ohne die ganze Kombination erneut eingeben zu müssen.",
        ["**Either** analog stick aims, but it's easiest if you use the hand that's not holding the shoulder button. **Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) narrows it to one stick per wheel."] =
            "**Beide** Analogsticks zielen, am einfachsten ist es aber mit der Hand, die nicht den Schulterknopf hält. **Rad ignoriert den gegenüberliegenden Stick** ([[accessibility|Einstellung unter Barrierefreiheit]]) beschränkt es auf einen Stick pro Rad.",
        ["**Tilt the stick** toward a slice and it lights up. **Release the trigger** to fire it."] =
            "**Neige den Stick** auf ein Segment, und es leuchtet auf. **Lass den Trigger los**, um es auszulösen.",
        ["**Release while centered** (stick in the deadzone) **cancels**."] =
            "**Loslassen bei zentriertem Stick** (in der Totzone) **bricht ab**.",
        ["A slice that still **needs configuring** (such as a voice-join with no URL) arms as **\"Configure in Settings\"**. Choosing it takes you to the configuration screen or the setup wizard it needs."] =
            "Ein Segment, das noch **konfiguriert werden muss** (etwa ein Sprachkanal-Beitritt ohne URL), wird als **\"In den Einstellungen einrichten\"** anvisiert. Wählst du es aus, gelangst du zum Konfigurationsbildschirm oder zum Einrichtungsassistenten, den es braucht.",
        ["Slices with **Hold to confirm** are protected from accidental triggering, which is useful for Sleep, Power Down, etc. Hold the stick on them (0.8 s) and they'll activate. You can set this on any slice in the Settings wheel editors."] =
            "Segmente mit **Zum Bestätigen halten** sind vor versehentlichem Auslösen geschützt, was für Ruhezustand, Herunterfahren usw. nützlich ist. Halte den Stick darauf (0,8 s), und sie werden aktiviert. Du kannst das in den Rad-Editoren der Einstellungen für jedes Segment festlegen.",
        ["**D-Pad 🡅 🡇** adjusts system volume."] =
            "**D-Pad 🡅 🡇** regelt die Systemlautstärke.",
        ["**D-Pad 🡄 🡆** steps the Alt-Tab window switcher by default - or [[volume-mixer|virtual desktops, track skip, or mic volume]]. Choose in **Settings ▸ Customize ▸ D-Pad 🡄 🡆**. "] =
            "**Steuerkreuz 🡄 🡆** schaltet standardmäßig durch den Alt-Tab-Fensterwechsler – oder durch [[volume-mixer|virtuelle Desktops, Titelsprung oder Mikrofonlautstärke]]. Wählbar unter **Einstellungen ▸ Anpassen ▸ Steuerkreuz 🡄 🡆**. ",
        ["You can also toggle wheels on/off from tray menu's **Disable/Enable Wheels** or add a **Disable Wheels** slice action."] =
            "Du kannst die Räder auch über den Eintrag **Räder deaktivieren/Räder aktivieren** im Tray-Menü oder über ein Segment **Räder deaktivieren** ein- und ausschalten.",
        ["Disabling the wheels changes wheel routing only. The virtual controller and cloak stay exactly where they are, basic controls keep running through that controller, and native features do **not** come back. To release capture and get your real controller, use [[passthru-mode|Passthru Mode]]. This will re-enable vendor features like special haptics and touchpads."] =
            "Das Deaktivieren der Räder ändert nur das Routing des Rads. Der virtuelle Controller und die Tarnung bleiben genau da, wo sie sind, die grundlegenden Steuerungen laufen weiter über diesen Controller, und native Funktionen kommen **nicht** zurück. Um die Erfassung freizugeben und deinen echten Controller zu bekommen, nutze den [[passthru-mode|Direktmodus]]. Dadurch werden Herstellerfunktionen wie spezielle Haptik und Touchpads wieder aktiviert.",
        ["Your current setting: toggle the wheels with **{disable}**."] =
            "Deine aktuelle Einstellung: Schalte die Räder um mit **{disable}**.",
        ["**Start editing a wheel:** with a wheel open, **click either stick** (L3/R3). The wheel centers and stays up after you release the trigger."] =
            "**Ein Rad bearbeiten:** Bei geöffnetem Rad **einen der beiden Sticks klicken** (L3/R3). Das Rad zentriert sich und bleibt nach dem Loslassen des Triggers stehen.",
        ["**Use either stick**. While you're editing, non-logo slices always show their labels regardless of the [[show-labels|Label setting]]."] =
            "**Nutze einen der beiden Sticks.** Während du bearbeitest, zeigen Segmente ohne Logo immer ihre Beschriftung, unabhängig von der [[show-labels|Beschriftungseinstellung]].",
        ["**Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) can override this."] =
            "**Rad ignoriert den gegenüberliegenden Stick** ([[accessibility|Einstellung unter Barrierefreiheit]]) kann dies überschreiben.",
        ["**Move:** **{cross}** picks up the selected slice; aim at a target slot and **{cross}** drops it."] =
            "**Verschieben:** **{cross}** hebt das ausgewählte Segment auf; ziele auf einen Zielplatz und **{cross}** legt es ab.",
        [" **D-Pad 🡄 🡆** will nudge a slice one spot left or right."] =
            " Das **Steuerkreuz 🡄 🡆** schiebt ein Segment einen Platz nach links oder rechts.",
        ["**Remove:** **hold {square}** on a slice until it disappears. Removing the **last** slice disables that wheel; see [[empty-wheel|Single-wheel mode]]."] =
            "**Entfernen:** **halte {square}** auf einem Segment, bis es verschwindet. Das Entfernen des **letzten** Segments deaktiviert dieses Rad; siehe [[empty-wheel|Einzelrad-Modus]].",
        ["A wheel holds up to **12** slices."] =
            "Ein Rad fasst bis zu **12** Segmente.",
        ["**Undo / Redo:** **L1 / R1**. You can undo/redo multiple steps while you remain in Edit mode."] =
            "**Rückgängig / Wiederholen:** **L1 / R1**. Du kannst mehrere Schritte rückgängig machen und wiederholen, solange du im Bearbeitungsmodus bleibst.",
        ["**Exit + save:** **{circle}**, click the stick again, or press an **Fn** / **L4/R4** button."] =
            "**Verlassen + speichern:** **{circle}**, den Stick erneut klicken oder eine **Fn**- / **L4/R4**-Taste drücken.",
        ["**Want only one wheel?** Delete every slice off the other one. A wheel with **no slices is disabled**. That side's chord stays fully usable in the game. Remove slices from Settings, or in [[edit-mode|edit mode]] with **hold {square}** until the last one is gone."] =
            "**Willst du nur ein Rad?** Lösche jedes Segment des anderen. Ein Rad **ohne Segmente ist deaktiviert**. Der Griff dieser Seite bleibt im Spiel voll nutzbar. Entferne Segmente in den Einstellungen oder im [[edit-mode|Bearbeitungsmodus]] mit **{square} halten**, bis das letzte weg ist.",
        ["If you accidentally empty **both** wheels, Settings opens so you can rebuild one or both of them."] =
            "Leerst du versehentlich **beide** Räder, öffnen sich die Einstellungen, damit du eines oder beide neu aufbauen kannst.",
        ["If a wheel has only one slice and it's **the Arcade Launcher**, then that wheel is replaced by the Arcade Launcher directly."] =
            "Hat ein Rad nur ein Segment und ist das **der Arcade-Launcher**, wird dieses Rad direkt durch den Arcade-Launcher ersetzt.",
        ["**To set it up:** just delete all slices on a wheel except a single **Arcade ▸ Arcade Launcher** slice. You can do that in Settings, or in [[edit-mode|edit mode]] with **hold {square}**."] =
            "**So richtest du es ein:** Lösche einfach alle Segmente eines Rads bis auf ein einziges Segment **Arcade ▸ Arcade-Launcher**. Das geht in den Einstellungen oder im [[edit-mode|Bearbeitungsmodus]] mit **{square} halten**.",
        ["**This applies to Arcade Launcher only.** A single slice with just an individual Arcade game on it still draws as a one-slice wheel."] =
            "**Das gilt nur für den Arcade-Launcher.** Ein einzelnes Segment mit nur einem einzelnen Arcade-Spiel wird weiterhin als Rad mit einem Segment gezeichnet.",
        ["**An Arcade Launcher wheel** can't be edited with R3/L3; edit it in **Settings ▸ Left/Right Wheel**, or add a second slice to get the wheel back."] =
            "**Ein Rad mit Arcade-Launcher** lässt sich nicht mit R3/L3 bearbeiten; bearbeite es unter **Einstellungen ▸ Linkes/Rechtes Rad**, oder füge ein zweites Segment hinzu, um das Rad zurückzubekommen.",
        ["If you want, you can pair this with [[empty-wheel|Single-wheel mode]]: empty the OTHER wheel and you have one gesture that opens the arcade and one that passes straight through to the game."] =
            "Wenn du möchtest, kannst du das mit dem [[empty-wheel|Einzelrad-Modus]] kombinieren: Leere das ANDERE Rad, und du hast eine Geste, die die Arcade öffnet, und eine, die direkt zum Spiel durchreicht.",
        ["**Drag an app (.exe or .lnk) from Explorer into the slice list**. A **Launch** slice lands at the drop position with its icon already extracted. Drop several at once for several slices."] =
            "**Zieh eine App (.exe oder .lnk) aus dem Explorer in die Segmentliste.** An der Ablageposition landet ein **Starten**-Segment, dessen Symbol bereits extrahiert ist. Lass mehrere auf einmal fallen, um mehrere Segmente anzulegen.",
        ["**Color swatches are paired** - the wheel shows whichever variation suits its [[customize|material]]. If you type an exact **Hex** value instead, that color is used exactly as-is, without tinting lighter or darker based on wheel Material. **Reset to Default** returns to the action-type color."] =
            "**Farbfelder sind paarweise angelegt** – das Rad zeigt jeweils die Variante, die zu seinem [[customize|Material]] passt. Gibst du stattdessen einen exakten **Hex**-Wert ein, wird genau diese Farbe verwendet, ohne je nach Material des Rads heller oder dunkler getönt zu werden. **Auf Standard zurücksetzen** kehrt zur Farbe des Aktionstyps zurück.",
        ["**Drop your own image onto the preview well** to give any slice custom artwork. It needs a **transparent background**, so a logo-style PNG is best. Something like a screenshot or a photo with no transparency would be a solid block so it's rejected. The file is **copied** into Radiata's art cache, so moving the original later won't blank the slice."] =
            "**Zieh dein eigenes Bild auf das Vorschaufeld**, um einem beliebigen Segment eine eigene Grafik zu geben. Sie braucht einen **transparenten Hintergrund**, ein PNG im Logo-Stil eignet sich also am besten. Ein Screenshot oder ein Foto ohne Transparenz wäre ein einfarbiger Block und wird deshalb abgelehnt. Die Datei wird in Radiatas Grafik-Cache **kopiert**, sodass ein späteres Verschieben des Originals das Segment nicht leert.",
        ["When you choose a game, Radiata fetches its transparent **logo** automatically. The **↻ button** restores that logo (downloading it if needed), and the **🡄 🡆** buttons cycle every logo already downloaded for that game. **Requires [[integrations|SteamGridDB]] to be configured.**"] =
            "Wenn du ein Spiel wählst, holt Radiata dessen transparentes **Logo** automatisch. Die **Schaltfläche ↻** stellt dieses Logo wieder her (und lädt es bei Bedarf), und die Schaltflächen **🡄 🡆** blättern durch alle bereits geladenen Logos dieses Spiels. **Setzt voraus, dass [[integrations|SteamGridDB]] eingerichtet ist.**",
        ["**Choosing a different icon drops the original game logo**. A slice's logo is independent of the [[cover-art|Game Grid's]]."] =
            "**Ein anderes Symbol zu wählen verwirft das ursprüngliche Spiellogo.** Das Logo eines Segments ist unabhängig von dem des [[cover-art|Spielerasters]].",
        ["**Everything auto-saves** - adds, removals, reorders, and edits to an existing slice (**Revert** undoes an in-progress edit). **A new slice created in Settings is not saved until you click Save Slice**. Entering **Ctrl+S** forces a save at any point."] =
            "**Alles wird automatisch gespeichert** – Hinzufügen, Entfernen, Umsortieren und Änderungen an einem vorhandenen Segment (**Verwerfen** macht eine laufende Bearbeitung rückgängig). **Ein in den Einstellungen erstelltes neues Segment wird erst gespeichert, wenn du auf Segment speichern klickst.** Mit **Ctrl+S** erzwingst du das Speichern jederzeit.",
        ["**Select** (Create/Share) - **cycle the selected game's cover** and save it. Your choices will cycle through default, then up to 10 top-rated SteamGridDB covers, then 5 flat colors if you just want the logo on a clean background."] =
            "**Auswählen** (Create/Share) – **die Titelbilder des ausgewählten Spiels durchschalten** und speichern. Du wechselst durch Standard, dann bis zu 10 bestbewertete SteamGridDB-Titelbilder, dann 5 einfarbige Flächen, falls du nur das Logo auf einem schlichten Hintergrund möchtest.",
        ["Covers come from [[integrations|SteamGridDB]] - add a free API key in **Settings ▸ Advanced ▸ Integrations** for portrait covers and logos across every storefront. This is the best option. However, without SteamGridDB, you still get Steam's own art, [[playnite|Playnite]]'s covers, and the flat colors."] =
            "Titelbilder kommen von [[integrations|SteamGridDB]] – trage unter **Einstellungen ▸ Erweitert ▸ Integrationen** einen kostenlosen API-Schlüssel ein, um Hochformat-Titelbilder und Logos für alle Stores zu erhalten. Das ist die beste Option. Ohne SteamGridDB bekommst du aber weiterhin Steams eigene Grafiken, die Titelbilder von [[playnite|Playnite]] und die einfarbigen Flächen.",
        ["An **Arcade** slice opens a little game in a **round window, right where the wheel was**. Play a game while you wait on a loading screen or a big lobby, no alt-tabbing required."] =
            "Ein **Arcade**-Segment öffnet ein kleines Spiel in einem **runden Fenster, genau dort, wo das Rad war**. Spiele, während du auf einem Ladebildschirm oder in einer großen Lobby wartest, ganz ohne Alt-Tab.",
        ["**Arcade Launcher** opens the whole arcade. Each game is a cabinet on a round carousel with a live screenshot of where it was left. **Left/right** on the stick or D-Pad swings the next cabinet to the front, **{cross}** plays it. It **picks up where you left off** - straight back into the game you were last playing, or at the cabinets if that's where you closed it. Each game can also be directly-launched by adding a slice for it."] =
            "**Arcade-Launcher** öffnet die ganze Arcade. Jedes Spiel ist ein Automat auf einem runden Karussell mit einem Live-Screenshot vom letzten Stand. **Links/rechts** am Stick oder Steuerkreuz schwenkt den nächsten Automaten nach vorn, **{cross}** startet ihn. Er **macht dort weiter, wo du aufgehört hast** – direkt zurück in das Spiel, das du zuletzt gespielt hast, oder bei den Automaten, wenn du ihn dort geschlossen hast. Jedes Spiel lässt sich außerdem direkt starten, indem du ein Segment dafür hinzufügst.",
        ["**A wheel with the Arcade Launcher and nothing else** skips the wheel and goes straight to the Arcade Launcher - see [[arcade-direct-launch|Arcade direct-launch]]."] =
            "**Ein Rad mit dem Arcade-Launcher und sonst nichts** überspringt das Rad und führt direkt zum Arcade-Launcher – siehe [[arcade-direct-launch|Arcade-Direktstart]].",
        ["**{circle} always backs you out**. One press closes a menu or help card, the next steps out of the game: back to the **Arcade Launcher** when that's how you got in, otherwise straight out. **The game freezes exactly as you left it**, so you can come back later and carry on. Each game stores its own state and scoreboard."] =
            "**{circle} bringt dich immer eine Ebene zurück.** Ein Druck schließt ein Menü oder eine Hilfekarte, der nächste verlässt das Spiel: zurück zum **Arcade-Launcher**, wenn du darüber hineingekommen bist, sonst direkt hinaus. **Das Spiel friert genau so ein, wie du es verlassen hast**, sodass du später weitermachen kannst. Jedes Spiel speichert seinen eigenen Zustand und seine Bestenliste.",
        ["Over a game, the arcade only plays while your controller is **isolated** from it. Otherwise a card explains why and offers **hold {triangle} to play anyway**. Passthru Mode will mean no Arcade games can be played while you're in another game. See [[input-isolation|Input isolation]]."] =
            "Über einem Spiel läuft die Arcade nur, solange dein Controller davon **isoliert** ist. Andernfalls erklärt eine Karte, warum, und bietet **{triangle} halten, um trotzdem zu spielen** an. Im Direktmodus lässt sich in einem anderen Spiel kein Arcade-Spiel spielen. Siehe [[input-isolation|Eingabe-Isolation]].",
        ["**Kabloom**: Minesweeper logic on a Floret Pentagonal Tiled field of flower petals. Move the cursor with the stick or d-pad. **{cross}** reveals a tile, **{square}** flags where you think there's a bee (or multiple bees, on later levels). Press **{square}** again to increase the flag count, or hold it for a question mark flag. Remaining bees are shown at the bottom of the screen. At the center of each floret is a nectar gem, collected when all the petals around it are cleared, which adds to your total score. Every board is solvable with no forced guesses."] =
            "**Kabloom**: Minesweeper-Logik auf einem Feld aus Blütenblättern in Floret-Fünfeck-Parkettierung. Bewege den Cursor mit dem Stick oder dem Steuerkreuz. **{cross}** deckt eine Kachel auf, **{square}** markiert eine Stelle, an der du eine Biene (oder in späteren Leveln mehrere Bienen) vermutest. Drücke **{square}** erneut, um die Zahl der Markierungen zu erhöhen, oder halte es für ein Fragezeichen. Die verbleibenden Bienen werden unten auf dem Bildschirm angezeigt. In der Mitte jeder Blüte liegt ein Nektaredelstein, der eingesammelt wird, wenn alle Blütenblätter um ihn herum geräumt sind, und der deine Gesamtpunktzahl erhöht. Jedes Feld ist ohne erzwungenes Raten lösbar.",
        ["**More about \"no forced guesses\":** Boards 1-10 are generated on the fly and validated by the Solver before play. Every board from level 11 up is baked ahead of time and certified by a complete solver before it ships. Individual petal tiles can hold up to three bees on the later levels. The solver plays the board from every zero-clue petal you could open on. It runs the human patterns first (saturation, subset difference, overlap bounds, chained constraints) and when those run dry it groups the unknown petals that share the same set of clues into boxes, enumerates every way the remaining bees can be spread over each connected group of boxes, and folds in the total bee count so the petals no clue touches get reasoned about too. A level ships only when the certified start-points cover the whole crop, so the first petal you open is always one the proof began from, ensuring every possible start guarantees a solvable board. "] =
            "**Mehr zu \"niemals raten\":** Die Felder 1 bis 10 werden spontan erzeugt und vor dem Spielen vom Löser geprüft. Jedes Feld ab Level 11 wird vorab gebacken und vor der Auslieferung von einem vollständigen Löser zertifiziert. Einzelne Blütenblatt-Kacheln fassen in späteren Leveln bis zu drei Bienen. Der Löser spielt das Feld von jedem hinweislosen Blatt aus durch, mit dem du anfangen könntest. Zuerst wendet er die menschlichen Muster an (Sättigung, Teilmengendifferenz, Überlappungsgrenzen, verkettete Bedingungen), und wenn die erschöpft sind, gruppiert er die unbekannten Blätter, die dieselbe Hinweismenge teilen, in Kästen, zählt jede Verteilung der verbleibenden Bienen über jede zusammenhängende Kastengruppe auf und bezieht die Gesamtzahl der Bienen ein, sodass auch die Blätter berücksichtigt werden, die kein Hinweis berührt. Ein Level wird nur ausgeliefert, wenn die zertifizierten Startpunkte die gesamte Fläche abdecken, sodass das erste Blatt, das du öffnest, immer eines ist, mit dem der Beweis begann. Damit garantiert jeder mögliche Start ein lösbares Feld. ",
        ["**Connate**: Your craft rides the rim around a cluster of orbs and garbage blocks. Shoot orbs to merge the numbers before the pile grows past the inner ring. **{cross}** fires your held number into the cluster. Hold to fire with more force. Star and Star-Gap pieces merge to make orbs of 3, and matching numbers from 3 up combines their values. Only **matching colors** merge, although mixed-color orbs can be created by matching stars and gaps of opposite colors; these merge with either color or with other mixed-color orbs. Combos charge up a bomb you can fire. Bomb high-value orbs to collect them to your score."] =
            "**Connate**: Dein Fahrzeug fährt am Rand entlang um einen Verbund aus Kugeln und Störblöcken. Schieße auf Kugeln, um die Zahlen zusammenzuführen, bevor der Haufen über den inneren Ring hinauswächst. **{cross}** schießt deine gehaltene Zahl in den Verbund. Halte die Taste, um mit mehr Kraft zu schießen. Stern- und Stern-Lücken-Teile verschmelzen zu Kugeln mit 3, und ab 3 addieren gleiche Zahlen ihre Werte. Nur **gleiche Farben** verschmelzen, allerdings lassen sich mehrfarbige Kugeln erzeugen, indem du Sterne und Lücken gegensätzlicher Farben zusammenbringst; diese verschmelzen mit jeder der beiden Farben oder mit anderen mehrfarbigen Kugeln. Kombos laden eine Bombe auf, die du abfeuern kannst. Bombardiere hochwertige Kugeln, um sie deinen Punkten gutzuschreiben.",
        ["**Stages**: the pace follows your score. Each time your collected total crosses 100, 250, 450, 700 and 1,000, the shot clock gets a little shorter, garbage arrives a little sooner, and a bomb takes one more combo charge to fill. The current stage is shown under the score. From stage 2, every 48 seconds of play ends with 8 seconds of relief: no garbage, and a longer shot clock."] =
            "**Abschnitte**: Das Tempo folgt deinen Punkten. Jedes Mal, wenn deine gesammelten Punkte 100, 250, 450, 700 oder 1.000 überschreiten, wird das Zeitlimit pro Schuss etwas kürzer, die Störblöcke kommen etwas früher, und eine Bombe braucht eine Kombo-Ladung mehr, bis sie voll ist. Der aktuelle Abschnitt wird unter der Punktzahl angezeigt. Ab Abschnitt 2 endet alle 48 Sekunden Spielzeit mit 8 Sekunden Verschnaufpause: keine Störblöcke und ein längeres Zeitlimit pro Schuss.",
        ["**Petalpop**: a ring of paddles around a flower of petals. Your analog stick controls every paddle together, so be careful! Hold **{cross}** to draw the paddles back like a slingshot, then release to **smash** the ball. Hit the core with a smash shot to clear the level. Pop a blue petal for multi-ball. \n\nFour sides and four levels to start, then five, six, seven and eight. You get one extra life for each size increase. Switch between spring and rail control via the **Start** menu."] =
            "**Petalpop**: ein Ring aus Schlägern um eine Blüte aus Blättern. Dein Analogstick steuert alle Schläger gemeinsam, also sei vorsichtig! Halte **{cross}**, um die Schläger wie eine Schleuder zurückzuziehen, und lass los, um den Ball zu **schmettern**. Triff den Kern mit einem Schmetterball, um das Level abzuschließen. Ein blaues Blatt bringt Multiball.\n\nZu Beginn vier Seiten und vier Level, dann fünf, sechs, sieben und acht. Mit jeder größeren Form bekommst du ein Extraleben. Wechsle über das Menü **Start** zwischen Feder- und Schienensteuerung.",
        ["**Internode**: shoot down a twisting half-pipe and collect tokens while avoiding mines and gaps. Hitting a mine will drop your tokens, and hitting one when carrying no tokens sets you back one stretch. Falling in a gap always sets you back one stretch. **{cross}** jumps. \n\nCatching a full token streak will upgrade the final token in the pattern to a gold 10x token. Reach each checkpoint with enough tokens to bank them in your score, with extra bonuses for passing a stretch on the first try and for collecting every token in a stretch. Insufficient tokens keeps you looping the same stretch until you have enough. The course twists and turns harder every stage. \n\nCamera roll can be enabled/disabled in the **START** menu."] =
            "**Internode**: Schieße durch eine gewundene Halfpipe und sammle Marken, während du Minen und Lücken meidest. Eine Mine lässt dich deine Marken verlieren, und wer eine Mine ohne Marken trifft, wird um einen Abschnitt zurückgeworfen. Ein Sturz in eine Lücke wirft dich immer um einen Abschnitt zurück. **{cross}** springt.\n\nWer eine vollständige Markenserie fängt, wertet die letzte Marke des Musters zu einer goldenen 10x-Marke auf. Erreiche jeden Kontrollpunkt mit genug Marken, um sie deinen Punkten gutzuschreiben; Extraboni gibt es für einen Abschnitt im ersten Versuch und für das Einsammeln aller Marken eines Abschnitts. Bei zu wenigen Marken wiederholst du denselben Abschnitt, bis es genug sind. Der Kurs wird mit jeder Stufe kurviger.\n\nDas Kamerarollen lässt sich im Menü **START** ein- oder ausschalten.",
        ["**Choose the app** two ways: **Browse for App…** picks an `.exe` from disk, and **Installed Apps…** lists everything with a Start-menu entry (including **Microsoft Store apps**, which have no `.exe` to browse to). Either way the icon is pulled in automatically. You can also drag an `.exe`, a shortcut, or a Start-menu app straight into the slice list."] =
            "**Wähle die App** auf zwei Arten: **App suchen…** wählt eine `.exe` von der Festplatte, und **Installierte Apps…** listet alles mit einem Startmenü-Eintrag auf (auch **Microsoft-Store-Apps**, zu denen es keine `.exe` zum Auswählen gibt). In beiden Fällen wird das Symbol automatisch übernommen. Du kannst auch eine `.exe`, eine Verknüpfung oder eine Startmenü-App direkt in die Segmentliste ziehen.",
        ["A **Store app** can't be detected as already-running, so **Run** just re-opens it and **Toggle** won't reliably close it. And an app started through an updater or launcher **stub** may run under a different name than the file you picked, so picking the app's real `.exe` is the reliable choice."] =
            "Bei einer **Store-App** lässt sich nicht erkennen, ob sie bereits läuft, deshalb öffnet **Ausführen** sie einfach erneut, und **Umschalten** schließt sie nicht zuverlässig. Und eine App, die über einen Updater oder einen Start-**Stub** gestartet wird, läuft möglicherweise unter einem anderen Namen als die Datei, die du ausgewählt hast, daher ist die echte `.exe` der App die zuverlässige Wahl.",
        ["**Installed Game** slices launch the game **directly** where possible. A GOG game runs its own exe even without GOG Galaxy installed; only stores that need their client running (like Steam) will route through the launcher."] =
            "**Installiertes Spiel**-Segmente starten das Spiel nach Möglichkeit **direkt**. Ein GOG-Spiel führt seine eigene exe aus, auch ohne installiertes GOG Galaxy; nur Stores, deren Client laufen muss (wie Steam), werden über den Launcher geleitet.",
        ["Radiata forwards a **virtual controller**, and **Controller mode** decides which kind the game sees: an **Xbox** pad or a **DualShock** pad. Two slices under **System ▸ Controller** flip it - **Toggle Xbox Mode** and **Toggle DualShock Mode**."] =
            "Radiata gibt einen **virtuellen Controller** weiter, und der **Controller-Modus** entscheidet, welche Art das Spiel sieht: ein **Xbox**- oder ein **DualShock**-Controller. Zwei Segmente unter **System ▸ Controller** schalten ihn um – **Xbox-Modus umschalten** und **DualShock-Modus umschalten**.",
        ["**What it's for:** many games only accept one class of controller. A Game Pass or Xbox-app title that refuses a DualSense controller will allow it if it's in **Xbox Mode** in Radiata. Games that want PlayStation input go the other way."] =
            "**Wofür das gut ist:** Viele Spiele akzeptieren nur eine Art von Controller. Ein Game-Pass- oder Xbox-App-Titel, der einen DualSense-Controller ablehnt, akzeptiert ihn, wenn er in Radiata im **Xbox-Modus** ist. Spiele, die PlayStation-Eingaben wollen, gehen den umgekehrten Weg.",
        ["**Button prompts follow the mode.** The glyphs a game draws come from the pad it thinks is plugged in, so Xbox Mode gets you **A B X Y** and DualShock Mode **{cross} {circle} {square} {triangle}**."] =
            "**Die Tastensymbole folgen dem Modus.** Die Zeichen, die ein Spiel zeichnet, stammen von dem Controller, den es angeschlossen glaubt: Der Xbox-Modus bringt dir **A B X Y**, der DualShock-Modus **{cross} {circle} {square} {triangle}**.",
        ["The switch is instant and stays put until you flip it back, but the game sees a controller swap at that moment. **Flip it before launching the game** for best results."] =
            "Der Wechsel ist sofort wirksam und bleibt, bis du zurückschaltest, aber das Spiel sieht in diesem Moment einen Controller-Tausch. **Schalte vor dem Start des Spiels um**, damit es am besten funktioniert.",
        ["**A wired Xbox pad stays an Xbox pad.** If your physical controller is an Xbox pad on USB, or a USB wireless dongle, the game always gets a virtual Xbox pad and DualShock Mode cannot change it."] =
            "**Ein kabelgebundener Xbox-Controller bleibt ein Xbox-Controller.** Ist dein physischer Controller ein Xbox-Controller per USB oder ein USB-Funkadapter, bekommt das Spiel immer einen virtuellen Xbox-Controller, und der DualShock-Modus kann das nicht ändern.",
        ["This requires installing the isolation drivers. There's no virtual pad to switch without them, and firing the slice puts up an on-screen notice saying so. In [[passthru-mode|Passthru Mode]] the game reads your real controller, so the mode has nothing to change. See [[input-isolation|Input isolation]]."] =
            "Dies erfordert die Installation der Isolationstreiber. Ohne sie gibt es kein virtuelles Pad zum Umschalten, und das Auslösen des Segments zeigt einen Hinweis auf dem Bildschirm, der genau das sagt. Im [[passthru-mode|Direktmodus]] liest das Spiel deinen echten Controller, der Modus hat also nichts umzuschalten. Siehe [[input-isolation|Eingabe-Isolation]].",
        ["Discord must be running. These slices talk to the Discord app on your PC, not to Discord's website. "] =
            "Discord muss laufen. Diese Segmente sprechen mit der Discord-App auf deinem PC, nicht mit der Website von Discord. ",
        ["**OBS Studio** ([obsproject.com](https://obsproject.com)) is the free, open-source program that dominates the Twitch and YouTube streaming space, and also allows you to record the screen. It builds a broadcast out of **scenes** - named layouts of game capture, camera, mic and overlays - that you switch between while live."] =
            "**OBS Studio** ([obsproject.com](https://obsproject.com)) ist das kostenlose Open-Source-Programm, das den Streaming-Bereich auf Twitch und YouTube dominiert und außerdem die Bildschirmaufnahme erlaubt. Es baut eine Sendung aus **Szenen** – benannten Layouts aus Spielaufnahme, Kamera, Mikrofon und Overlays –, zwischen denen du live umschaltest.",
        ["A **Text Chat** slice (Chat & Streaming) types a message into the running game's text chat: it presses the game's **chat-open key**, types your text, and presses Enter."] =
            "Ein **Textchat**-Segment (Chat & Streaming) tippt eine Nachricht in den Textchat des laufenden Spiels: Es drückt die **Chat-Taste** des Spiels, tippt deinen Text und drückt Enter.",
        ["**Chat Button** - **Try Game Default** looks up the chat key for whatever game is running **each time the slice fires**, from a built-in index of around 1,000 PC games, so the same slice works across multiple games. **Custom…** lets you enter the key yourself, so use it for games with remapped or unusual chat keys, or for a game the index doesn't cover."] =
            "**Chat-Taste** – **Spielstandard versuchen** schlägt die Chat-Taste des gerade laufenden Spiels **bei jedem Auslösen des Segments** in einem eingebauten Verzeichnis von rund 1.000 PC-Spielen nach, sodass dasselbe Segment bei mehreren Spielen funktioniert. Mit **Benutzerdefiniert…** gibst du die Taste selbst an, nutze es also für Spiele mit umbelegten oder ungewöhnlichen Chat-Tasten oder für ein Spiel, das das Verzeichnis nicht abdeckt.",
        ["**Message** - the line of text to send. An empty message (or Custom with no key) arms as **\"Configure in Settings\"**, and firing opens the slice's editor."] =
            "**Nachricht** – die Textzeile, die gesendet wird. Eine leere Nachricht (oder Benutzerdefiniert ohne Taste) wird als **\"In den Einstellungen einrichten\"** anvisiert, und beim Auslösen öffnet sich der Editor des Segments.",
        ["**Sends are limited to one per 15 seconds**. Hub shows the remaining cooldown. No spamming, please!"] =
            "**Es kann höchstens alle 15 Sekunden gesendet werden.** Die Nabe zeigt die verbleibende Wartezeit. Bitte kein Spam!",
        ["**Sony family** (DualSense Edge, DualSense, DualShock 4) over Bluetooth or USB: good support with clean input isolation. The **Edge's Fn buttons** are the reference trigger. Haptic triggers and touchpad will not be seen by games due to driver limitations unless you are in Passthru Mode."] =
            "**Sony-Familie** (DualSense Edge, DualSense, DualShock 4) über Bluetooth oder USB: gute Unterstützung mit sauberer Eingabe-Isolation. Die **Fn-Tasten des Edge** sind der Referenzauslöser. Haptische Trigger und das Touchpad sehen Spiele wegen Treiberbeschränkungen nur im Direktmodus.",
        ["**Pads with extra paddles or buttons** (e.g. **8BitDo Ultimate 2C**) - over **Bluetooth** the extra **L4/R4** buttons are read directly, so you can pick them in [[triggers|Settings ▸ Customize ▸ Triggers]] to summon a wheel on their own, or paired with a second button if you also use them in games. They're offered, not assumed: the default stays the standard bumper chord. Over **USB** the extra buttons are invisible to Radiata, except as mapped by the controller's own drivers. So connect over Bluetooth if you want native L4/R4 support, or map them deliberately."] =
            "**Controller mit zusätzlichen Paddles oder Tasten** (z. B. **8BitDo Ultimate 2C**) – über **Bluetooth** werden die zusätzlichen Tasten **L4/R4** direkt gelesen, sodass du sie unter [[triggers|Einstellungen ▸ Anpassen ▸ Aufrufgesten]] wählen kannst, um ein Rad allein damit aufzurufen, oder zusammen mit einer zweiten Taste, falls du sie auch im Spiel nutzt. Sie werden angeboten, nicht vorausgesetzt: Standard bleibt der übliche Bumper-Griff. Über **USB** sind die zusätzlichen Tasten für Radiata unsichtbar, außer so, wie die eigenen Treiber des Controllers sie belegen. Verbinde also über Bluetooth, wenn du native L4/R4-Unterstützung willst, oder belege sie bewusst.",
        ["**Genuine Xbox pads over Bluetooth** - isolated too, with an extra safeguard. Before presenting the stand-in pad, Radiata has a separate helper process **observe** that games really can't see the physical pad any more. If that check can't pass - or can't run - it falls back to shared-input mode instead of guessing, so a hidden pad with no stand-in won't leave you without a working controller."] =
            "**Echte Xbox-Controller über Bluetooth** – werden ebenfalls isoliert, mit einer zusätzlichen Absicherung. Bevor der Ersatz-Controller präsentiert wird, lässt Radiata einen separaten Hilfsprozess **überprüfen**, dass Spiele den physischen Controller wirklich nicht mehr sehen können. Kann diese Prüfung nicht bestehen – oder nicht laufen –, fällt Radiata auf den Modus mit geteilter Eingabe zurück, statt zu raten, sodass ein getarnter Controller ohne Ersatz dich nicht ohne funktionierenden Controller zurücklässt.",
        ["Isolation needs the drivers (ViGEm + HidHide). Without them every pad runs in shared-input mode (e.g. \"bleed-thru\"). Install from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**. Not sure which mode you're in? Hover the tray icon - see [[input-isolation|Input isolation]]."] =
            "Isolation braucht die Treiber (ViGEm + HidHide). Ohne sie läuft jeder Controller im Modus mit geteilter Eingabe (z. B. \"Bleed-thru\"). Installiere sie über **Einstellungen ▸ Erweitert ▸ Fehlerbehebung ▸ Treiber installieren/reparieren**. Nicht sicher, in welchem Modus du bist? Zeige mit der Maus auf das Symbol im Infobereich – siehe [[input-isolation|Eingabe-Isolation]].",
        ["With the optional isolation drivers installed, Radiata gives games a **virtual controller** - a DualShock 4 for Sony pads, an Xbox 360 pad for Xbox pads or in Xbox Mode - and **cloaks the physical pad** (HidHide) so the game can't see it twice. That's when capture succeeds. Other remappers, existing device access and unsupported input paths can all sabotage full isolation, and without the drivers games keep seeing your controller while a wheel is up. This is how you get bleed-thru."] =
            "Mit den optionalen Isolationstreibern gibt Radiata Spielen einen **virtuellen Controller** – einen DualShock 4 für Sony-Controller, einen Xbox 360-Controller für Xbox-Controller oder im Xbox-Modus – und **tarnt den physischen Controller** (HidHide), damit das Spiel ihn nicht doppelt sieht. Das gilt, wenn die Erfassung gelingt. Andere Remapper, ein bestehender Gerätezugriff und nicht unterstützte Eingabepfade können die vollständige Isolation verhindern, und ohne die Treiber sehen Spiele deinen Controller weiterhin, solange ein Rad offen ist. So entsteht \"Bleed-thru\".",
        ["**Which mode am I actually in? Hover the tray icon.** It reads **\"Radiata - isolated\"**, or names the reason it isn't (no drivers, Passthru Mode, a pad that can't be cloaked). When capture drops to shared-input mode you also get an on-screen notice."] =
            "**In welchem Modus bin ich wirklich? Zeige mit der Maus auf das Tray-Symbol.** Es zeigt **\"Radiata - isolated\"** oder nennt den Grund, warum nicht (keine Treiber, Direktmodus, ein nicht verbergbarer Controller). Fällt die Erfassung in den Modus mit geteilter Eingabe, erhältst du zudem einen Hinweis auf dem Bildschirm.",
        ["**Global toggle:** the tray's checkable **Passthru Mode** item, or the checkbox on **Settings ▸ Passthru Mode**. The tab appears only when the isolation drivers are installed."] =
            "**Globaler Schalter:** der ankreuzbare Eintrag **Direktmodus** im Tray oder das Kontrollkästchen unter **Einstellungen ▸ Direktmodus**. Der Reiter erscheint nur, wenn die Isolationstreiber installiert sind.",
        ["**Switch Passthru Mode on or off between play sessions, not mid-game.** Either direction swaps the controller a running game is reading - the virtual pad for your real one, or back - and most games don't go looking for a new controller once they've started, so the game loses input until it's relaunched."] =
            "**Schalte den Direktmodus zwischen Spielsitzungen ein oder aus, nicht mitten im Spiel.** In beide Richtungen wird der Controller getauscht, den ein laufendes Spiel liest – der virtuelle gegen deinen echten oder zurück –, und die meisten Spiele suchen nach dem Start nicht mehr nach einem neuen Controller, sodass das Spiel ohne Eingabe bleibt, bis es neu gestartet wird.",
        ["**Settings tabs:** Left Wheel, Right Wheel, **Customize** (look, feel, sound, triggers, the [[volume-mixer|D-Pad 🡄 🡆]] picker and the [[show-labels|Show labels on]] picker), **Passthru Mode** ([[passthru-mode|Passthru Mode]] - shown when the isolation drivers are installed), **Advanced** (a **Current Controller** readout, [[integrations|Integrations]], [[game-grid-options|Game Grid]] housekeeping, [[accessibility|Accessibility]], **Start with Windows**, and [[system-actions|System tools]] incl. backup & reset and **Quit Radiata**), **Help**, and **About**. Settings auto-save; **Ctrl+S** forces a save."] =
            "**Reiter der Einstellungen:** Linkes Rad, Rechtes Rad, **Anpassen** (Aussehen, Verhalten, Ton, Auslöser, die Auswahl für [[volume-mixer|Steuerkreuz 🡄 🡆]] und für [[show-labels|Beschriftungen anzeigen bei]]), **Direktmodus** ([[passthru-mode|Direktmodus]] – angezeigt, wenn die Isolationstreiber installiert sind), **Erweitert** (eine **Aktueller Controller**-Anzeige, [[integrations|Integrationen]], Pflege des [[game-grid-options|Spieleraster]], [[accessibility|Barrierefreiheit]], **Mit Windows starten** und die [[system-actions|Systemwerkzeuge]] inkl. Sicherung, Zurücksetzen und **Radiata beenden**), **Hilfe** und **Info**. Einstellungen speichern automatisch; mit **Ctrl+S** erzwingst du das Speichern.",
        ["**Language** - **Settings ▸ Advanced ▸ Language** sets the language for the whole app. Help language switches immediately. Everything else follows at the next launch."] =
            "**Sprache** - **Einstellungen ▸ Erweitert ▸ Sprache** legt die Sprache der ganzen App fest. Die Hilfe wechselt sofort. Alles andere folgt beim nächsten Start.",
        ["**Settings ▸ Customize** sets how the wheels look, feel and sound. Every pick applies **live**."] =
            "**Einstellungen ▸ Anpassen** legt fest, wie die Räder aussehen, sich anfühlen und klingen. Jede Wahl wirkt **sofort**.",
        ["**Material** - the resting-slice look. Eight, in two groups. **Simple** contains solid-color **Flat Light** and **Flat Dark**; **Deluxe** contains the glassy **Pearl** and **Obsidian**, plus four styled looks:"] =
            "**Material** – das Aussehen des ruhenden Segments. Acht Stück, in zwei Gruppen. **Einfach** enthält die einfarbigen **Matt hell** und **Matt dunkel**; **Deluxe** enthält die glasigen **Perle** und **Obsidian** sowie vier gestaltete Looks:",
        ["**Kawaii** - pastel wedges, each slice a different hue; firing bursts heart-and-star confetti."] =
            "**Kawaii** – pastellfarbene Segmente, jedes in einem anderen Ton; beim Auslösen platzt Konfetti aus Herzen und Sternen.",
        ["**Salvage** - charcoal slices with a rusted-metal texture, fluorescent-light highlighting, and a stamped plate edge."] =
            "**Schrott** – kohlefarbene Segmente mit rostiger Metalltextur, Leuchtstofflicht-Hervorhebung und geprägtem Plattenrand.",
        ["**Reactor** - dark hollow wedges; the armed slice lights an animated circuit-board of traces and sparks."] =
            "**Reaktor** – dunkle, hohle Segmente; das anvisierte Segment lässt eine animierte Platine aus Leiterbahnen und Funken aufleuchten.",
        ["On the Kawaii sound set, arming a slice strikes the next note of a xylophone melody, so scrubbing around the ring plays the song. There are 5 melodies... you might recognize a few of them :)"] =
            "Im Klangsatz Kawaii schlägt das Anvisieren eines Segments die nächste Note einer Xylofon-Melodie an, sodass das Kreisen im Ring das Lied spielt. Es gibt 5 Melodien ... vielleicht erkennst du ein paar davon :)",
        ["The first time Radiata sees a new **or changed** package it asks you to confirm before loading anything. Accepting will show it in **Settings ▸ Customize** in a third group, **Custom**, below Simple and Deluxe."] =
            "Beim ersten Mal, dass Radiata ein neues **oder geändertes** Paket sieht, bittet es um Bestätigung, bevor irgendetwas geladen wird. Wenn du akzeptierst, erscheint es unter **Einstellungen ▸ Anpassen** in einer dritten Gruppe, **Eigene**, unter Einfach und Deluxe.",
        ["A package is content from whoever wrote it. **Only install themes from a source you trust**. The confirmation prompt returns whenever any file in the package changes."] =
            "Ein Paket ist Inhalt von dem, der es geschrieben hat. **Installiere Themen nur aus einer Quelle, der du vertraust.** Die Bestätigungsabfrage kehrt zurück, sobald sich eine Datei im Paket ändert.",
        ["Custom themes render through Radiata's **flat** slice paths with your colors substituted, so they can't reach the built-in styled materials' procedural effects (Kawaii's confetti, Reactor's circuit board)."] =
            "Eigene Themen werden über Radiatas **flache** Segmentpfade mit deinen Farben gerendert, erreichen also nicht die prozeduralen Effekte der eingebauten gestalteten Materialien (Kawaiis Konfetti, Reaktors Leiterplatte).",
        ["Set `\"format\": 2` and add any of these optional blocks. Every number is clamped to a safe range, so an extreme value is pulled back rather than rejected."] =
            "Setze `\"format\": 2` und füge beliebige dieser optionalen Blöcke hinzu. Jede Zahl wird auf einen sicheren Bereich begrenzt, sodass ein extremer Wert zurückgeholt statt abgelehnt wird.",
        ["Custom Arcade games - build your own!"] =
            "Eigene Arcade-Spiele – bau dein eigenes!",
        ["`\"howTo\"` - optional, up to **5 lines of 80 characters**, which become the **{triangle}** help card. `{cross}` `{circle}` `{square}` `{triangle}` in a line are replaced with the player's own button glyphs. No lines means no help card."] =
            "`\"howTo\"` – optional, bis zu **5 Zeilen mit 80 Zeichen**, die zur **{triangle}**-Hilfekarte werden. `{cross}` `{circle}` `{square}` `{triangle}` in einer Zeile werden durch die Tastensymbole des Spielers ersetzt. Keine Zeilen bedeuten keine Hilfekarte.",
        ["**`kvSet(\"key\", \"value\")`** and **`kvGet(\"key\")`** are the **only** state that survives closing a game. Contains strings only, keys up to 64 characters, values up to 1024, 4 KB per game in total. Everything else resets on dismiss, so design for it."] =
            "**`kvSet(\"key\", \"value\")`** und **`kvGet(\"key\")`** sind der **einzige** Zustand, der das Schließen eines Spiels überlebt. Er enthält nur Zeichenketten, Schlüssel bis 64 Zeichen, Werte bis 1024, insgesamt 4 KB pro Spiel. Alles andere wird beim Schließen zurückgesetzt; plane entsprechend.",
        ["**Wheel ignores opposite stick** - normally **either** thumbstick aims (whichever you tilt further), and either stick's click opens [[edit-mode|edit mode]]. This option has each wheel listen to **one** stick only: the free hand's under Hold, the wheel's own side under Toggle. "] =
            "**Rad ignoriert den gegenüberliegenden Stick** – normalerweise zielen **beide** Sticks (je nachdem, welchen du weiter neigst), und der Klick auf einen der beiden öffnet den [[edit-mode|Bearbeitungsmodus]]. Mit dieser Option hört jedes Rad nur auf **einen** Stick: bei Hold den der freien Hand, bei Toggle den der eigenen Radseite. ",
        ["**Reduce motion** - stops decorative movement everywhere. No confetti or sparks, no parallax, no zooming or drifting; wheels fade in in place, edit-mode rearranging is instant, and every hold-to-confirm effect becomes the same steady progress arc. Some Arcade features are automatically disabled. Progress meters, selection highlights and state readouts all stay. This setting respects Windows' own **Animation effects** switch as well."] =
            "**Bewegung reduzieren** – stoppt dekorative Bewegung überall. Kein Konfetti und keine Funken, kein Parallaxeffekt, kein Zoomen oder Driften; Räder blenden an Ort und Stelle ein, das Umsortieren im Bearbeitungsmodus ist sofort, und jeder Zum-Bestätigen-halten-Effekt wird zum selben gleichmäßigen Fortschrittsbogen. Einige Arcade-Funktionen werden automatisch deaktiviert. Fortschrittsanzeigen, Auswahlhervorhebungen und Zustandsanzeigen bleiben alle erhalten. Diese Einstellung beachtet auch den Windows-eigenen Schalter **Animationseffekte**.",
        ["**Always show hub** - off (the default), the wheel's centre hub appears only when it has something to show. On, the hub is always drawn and also shows the controller battery: a steadier centre to read. **Reduce motion** assumes you want this checked as well, but you can set them independently too."] =
            "**Nabe immer anzeigen** – ausgeschaltet (Standard) erscheint die Nabe in der Radmitte nur, wenn sie etwas zu zeigen hat. Eingeschaltet wird die Nabe immer gezeichnet und zeigt zusätzlich den Akkustand des Controllers: eine ruhigere Mitte zum Ablesen. **Bewegung reduzieren** geht davon aus, dass du dies ebenfalls aktiviert haben möchtest, du kannst beide aber auch unabhängig voneinander einstellen.",
        ["**Icons, not Logos** (the default) - only slices showing one of Radiata's built-in icons are labelled. A slice carrying artwork (a game logo, cover art, or a PNG you added) goes unlabelled, assuming the artwork includes or replaces the name."] =
            "**Symbole, keine Logos** (Standard) – nur Segmente mit einem der eingebauten Symbole von Radiata werden beschriftet. Ein Segment mit Grafik (ein Spiellogo, ein Titelbild oder ein von dir hinzugefügtes PNG) bleibt unbeschriftet, weil angenommen wird, dass die Grafik den Namen enthält oder ersetzt.",
        ["**Slices I Choose** - each slice's own **Show Label** checkbox decides, and it's the only mode in which that checkbox appears in the slice editor (see [[editor-desktop|Slice editor tricks]]). Every slice is created with **Show Label** unchecked, so switching to this mode starts you from an unlabelled wheel: check the few slices you want named."] =
            "**Segmente meiner Wahl** – es entscheidet das Kontrollkästchen **Beschriftung anzeigen** jedes Segments, und nur in diesem Modus erscheint dieses Kästchen im Segmenteditor (siehe [[editor-desktop|Tricks im Segmenteditor]]). Jedes Segment wird mit nicht gesetztem **Beschriftung anzeigen** angelegt, dieser Modus startet also mit einem unbeschrifteten Rad: Hake die wenigen Segmente an, die benannt sein sollen.",
        ["Radiata only ever **reads** Playnite's local database. Playnite does not need to be running."] =
            "Radiata **liest** die lokale Datenbank von Playnite immer nur. Playnite muss nicht laufen.",
        ["**Recover Controller** - the ↻ button beside the **Current Controller** name. This is a soft input reset for a wedged pad. If the pad stays silent afterwards, turn it off (hold its home button until the light goes out), turn it back on, and reconnect it — a controller whose input has frozen at the device only comes back from a power-cycle."] =
            "**Controller wiederherstellen** – die Schaltfläche ↻ neben dem Namen unter **Aktueller Controller**. Das ist ein sanftes Zurücksetzen der Eingabe für einen hängenden Controller. Bleibt der Controller danach stumm, schalte ihn aus (Home-Taste gedrückt halten, bis das Licht erlischt), schalte ihn wieder ein und verbinde ihn erneut – ein Controller, dessen Eingabe im Gerät selbst eingefroren ist, kommt nur durch Aus- und Einschalten zurück.",
        ["**Battery** - beside the **Current Controller** heading, the same reading the wheel hub shows. PlayStation pads report a percentage (in 10% steps); Xbox-compatible pads only report four coarse levels. Nothing shows until the pad has reported a level."] =
            "**Akku** – neben der Überschrift **Aktueller Controller**, dieselbe Anzeige wie in der Nabe des Rades. PlayStation-Controller melden einen Prozentwert (in 10-%-Schritten); Xbox-kompatible Controller melden nur vier grobe Stufen. Bis der Controller einen Stand gemeldet hat, wird nichts angezeigt.",
        ["**Restore Settings…** - replaces settings and art picks only after validation and a successful save, then restarts Radiata. Automatic recovery backups are encrypted for your Windows account; restore them through this command. Exported ZIPs contain readable settings and artwork, but protected credentials may need re-entry on another account or PC."] =
            "**Einstellungen wiederherstellen…** – ersetzt Einstellungen und Grafikauswahl erst nach Prüfung und erfolgreichem Speichern und startet Radiata dann neu. Automatische Wiederherstellungssicherungen sind für dein Windows-Konto verschlüsselt; stelle sie über diesen Befehl wieder her. Exportierte ZIPs enthalten lesbare Einstellungen und Grafiken, geschützte Zugangsdaten müssen auf einem anderen Konto oder PC aber unter Umständen neu eingegeben werden.",
        ["**Wipe App Data and Reset…** - deletes everything in `%APPDATA%\\Radiata`, including automatic backups. Only backups saved outside that folder survive. Export a settings ZIP first if you want. Radiata restarts after a successful reset."] =
            "**App-Daten löschen und zurücksetzen…** – löscht alles in `%APPDATA%\\Radiata`, einschließlich der automatischen Sicherungen. Nur Sicherungen außerhalb dieses Ordners bleiben erhalten. Exportiere vorher bei Bedarf eine Einstellungs-ZIP. Nach einem erfolgreichen Zurücksetzen startet Radiata neu.",
        ["**Settings ▸ Advanced** shows a ↻ button beside the Current Controller name. This does a soft input reset - drops and reopens the HID stream - without restarting Radiata."] =
            "**Einstellungen ▸ Erweitert** zeigt neben dem Namen unter Aktueller Controller eine Schaltfläche ↻. Sie führt einen sanften Eingabe-Reset durch – verwirft den HID-Strom und öffnet ihn neu – ohne Radiata neu zu starten.",
    };
}
