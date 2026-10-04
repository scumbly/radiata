namespace ControllerWheel;

/// <summary>SPANISH Help-tab strings. Keys are the EXACT English text authored in
/// <see cref="HelpContent"/> — copy the C# literal across unchanged (escapes included) when adding an
/// entry, and let anything not listed here fall through to English. Run
/// <c>Radiata.exe --check-help-locales</c> after editing HelpContent.cs to see what needs work, and
/// <c>Radiata.exe --dump-help-locale es</c> to regenerate this file in source order.
/// Conventions (see docs/LOCALIZATION.md): a UI path or label reads in this language, bold, with no English gloss
/// (tools/help-flip.pl applies this; the notice sends support-seekers to English instead); markup (<c>**</c>,
/// <c>`</c>, <c>[[id|label]]</c>, <c>[label](url)</c>) and <c>{tokens}</c> are
/// preserved verbatim, a cross-link's topic id is never translated, and product names stay as they are.
/// <para>Entry ORDER follows <c>HelpLocalization.SourceStrings()</c> — the notice, the Help-pane chrome,
/// the category names, then each topic's title/keywords/blocks, then the figure labels.</para></summary>
internal static class HelpTextEs
{
    internal static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {

        // ── notice ──
        [HelpLocalization.NoticeKey] =
            "**Estas páginas de ayuda y la interfaz de Radiata fueron traducidas por un modelo de lenguaje de inteligencia artificial.** La traducción puede ser imperfecta o incompleta. El desarrollador no se hace responsable de los errores del texto traducido; la versión en inglés es la autoritativa. Las solicitudes de soporte solo pueden responderse en inglés; los mensajes en otros idiomas no recibirán respuesta. Una respuesta de soporte nombra los botones, pestañas y ajustes por sus etiquetas en inglés; cambia Radiata a inglés un momento para seguirla.",

        // ── chrome ──
        ["Contents"] =
            "Contenido",
        ["No topics match."] =
            "Ningún tema coincide.",
        ["Language"] =
            "Idioma",
        ["Search help topics"] =
            "Buscar en los temas de ayuda",
        ["Help topics language"] =
            "Idioma de los temas de ayuda",
        ["Back to where you were"] =
            "Volver a donde estabas",
        ["Open this Help topic"] =
            "Abrir este tema de ayuda",

        // ── category ──
        ["Welcome"] =
            "Bienvenida",
        ["Getting Around"] =
            "Uso básico",
        ["Editing Wheels"] =
            "Edición de ruedas",
        ["Game Grid"] =
            "Cuadrícula de juegos",
        ["Actions"] =
            "Acciones",
        ["Controllers & Isolation"] =
            "Mandos y aislamiento",
        ["Tray & Settings"] =
            "Bandeja del sistema y ajustes",
        ["Workshop"] =
            "Taller",
        ["Troubleshooting"] =
            "Solución de problemas",

        // ── topic:intro ──
        ["What is Radiata?"] =
            "¿Qué es Radiata?",
        ["intro welcome about purpose design overview couch overlay start here"] =
            "introducción bienvenida acerca de propósito diseño resumen sofá superposición empezar aquí",
        ["**Configurable:** each slice's action, icon, color and position, the summon chord, the look, the sounds. Edit at the desk in Settings, or from the couch in the in-wheel editor."] =
            "**Configurable:** la acción, el icono, el color y la posición de cada sector, la combinación de invocación, el aspecto, los sonidos. Edita en el escritorio desde Ajustes, o desde el sofá con el editor dentro de la rueda.",

        // ── topic:installing ──
        ["Installing Radiata"] =
            "Instalar Radiata",
        ["install installing installer setup download smartscreen windows protected your pc unknown publisher unsigned signature certificate antivirus false positive virus admin administrator uac elevation drivers vigem hidhide requirements windows 10 11 x64 arm browser blocked keep discard first run update uninstall remove"] =
            "instalar instalación instalador configuración descarga smartscreen windows protegió su pc editor desconocido sin firma firma certificado antivirus falso positivo virus administrador uac elevación controladores drivers vigem hidhide requisitos windows 10 11 x64 arm navegador bloqueado conservar descartar primer inicio actualizar desinstalar quitar",
        ["What you need"] =
            "Qué necesitas",
        ["**Windows 10 or 11, 64-bit (x64)** on an Intel or AMD PC. Windows on ARM isn't supported."] =
            "**Windows 10 u 11 de 64 bits (x64)** en un PC Intel o AMD. Windows en ARM no es compatible.",
        ["A supported controller - see [[supported-controllers|Supported controllers]]."] =
            "Un mando compatible: consulta [[supported-controllers|Mandos compatibles]].",
        ["\"Windows protected your PC\""] =
            "\"Windows protegió su PC\"",
        ["**In the browser:** if the download itself is blocked, keep it (Chrome and Edge: the **⋯** menu beside the download ▸ **Keep** ▸ **Show more** ▸ **Keep anyway**)."] =
            "**En el navegador:** si se bloquea la propia descarga, consérvala (Chrome y Edge: el menú **⋯** junto a la descarga ▸ **Conservar** ▸ **Mostrar más** ▸ **Conservar de todos modos**).",
        ["**At the SmartScreen box:** click **More info**, then the **Run anyway** button that appears below it. If there's no **More info** link you might be seeing your browser's warning instead. See the previous step."] =
            "**En el cuadro de SmartScreen:** haz clic en **Más información** y luego en el botón **Ejecutar de todas formas** que aparece debajo. Si no hay enlace **Más información**, puede que estés viendo el aviso de tu navegador. Consulta el paso anterior.",
        ["What the installer does"] =
            "Qué hace el instalador",
        ["First run"] =
            "Primer inicio",
        ["Setup opens by itself and walks you through the drivers, a controller check, the look, cover art, and a set of starter wheels. Re-run it any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Run First-Run Setup…** - see [[system-actions|System tools]]."] =
            "La configuración se abre por sí sola y te guía por los controladores, una comprobación del mando, el aspecto, las carátulas y un conjunto de ruedas iniciales. Vuelve a ejecutarla en cualquier momento desde **Ajustes ▸ Avanzado ▸ Solución de problemas ▸ Ejecutar la configuración inicial…**: consulta [[system-actions|Herramientas del sistema]].",
        ["Updating and uninstalling"] =
            "Actualizar y desinstalar",

        // ── topic:opening-a-wheel ──
        ["Opening a wheel"] =
            "Abrir una rueda",
        ["summon invoke trigger chord fn bumper touchpad swipe hold toggle swap sides activation flip left right"] =
            "invocar activar disparador combinación fn bumper touchpad deslizar mantener alternar intercambiar lados activación cambiar izquierda derecha",
        ["Hold the bumper/trigger; the second button is a **tap** that brings the wheel up."] =
            "Mantén pulsado el bumper o el gatillo; el segundo botón es un **toque** que hace aparecer la rueda.",
        ["**Fn / L4/R4 and squeeze chords** (Bumper+Trigger, +Home, +Select/Start) - the hand you squeeze opens the **opposite** wheel, so the free hand aims (R1+R2 → Left; L1+L2 → Right). For Select/Start, either button works."] =
            "**Fn / L4/R4 y combinaciones de presión** (bumper+gatillo, +Home, +Select/Start): la mano que presionas abre la rueda **contraria**, así que la mano libre apunta (R1+R2 → izquierda; L1+L2 → derecha). Para Select/Start sirve cualquiera de los dos botones.",
        ["**L3/R3** and **D-Pad L/R** - the clicked stick or the direction determines which wheel; either bumper/trigger is the hold."] =
            "**L3/R3** y **cruceta izquierda/derecha**: el stick pulsado o la dirección determinan qué rueda se abre; cualquier bumper o gatillo hace de mantenido.",
        ["**Touchpad swipe** - in from the left edge → Left wheel, right edge → Right."] =
            "**Deslizamiento en el touchpad**: desde el borde izquierdo hacia dentro → rueda izquierda; desde el borde derecho → rueda derecha.",
        ["**Swap left/right** ([[accessibility|Accessibility setting]]) reverses all of these."] =
            "**Intercambiar izquierda/derecha** (intercambiar izquierda y derecha, [[accessibility|ajuste de accesibilidad]]) invierte todo lo anterior.",
        ["**Activation** is **Hold** (up while held; release fires) or **Toggle** (trigger opens; **{cross} confirms, {circle} cancels**; re-trigger dismisses) - set it in [[accessibility|Accessibility]]. Touchpad swipe is always toggle-style."] =
            "La **activación** es **Mantener** (la rueda permanece mientras mantienes; al soltar se ejecuta) o **Toggle** (el gesto la abre; **{cross} confirma y {circle} cancela**; repetir el gesto la cierra); se elige en [[accessibility|Accesibilidad]]. El deslizamiento en el touchpad funciona siempre en modo Toggle.",

        // ── topic:picking-an-action ──
        ["Aiming & firing"] =
            "Apuntar y ejecutar",
        ["aim arm fire cancel release deadzone sticky esc escape keyboard hub center state toggle mute hdr configure guard confirm dwell sleep reboot shutdown"] =
            "apuntar preparar ejecutar cancelar soltar zona muerta esc escape teclado centro estado silenciar hdr configurar confirmar mantener suspender reiniciar apagar",
        ["Center hub"] =
            "Centro de la rueda",
        ["Arming a **toggle** slice (mic/volume mute, HDR, process toggle) shows its **current state** before you fire, e.g. `Mute Mic / Unmuted`. After firing, the hub shows the new state."] =
            "Al preparar un sector de **conmutación** (silenciar micrófono o volumen, HDR, conmutar un proceso) se muestra su **estado actual** antes de ejecutarlo, por ejemplo `Mute Mic / Unmuted`. Después de ejecutarlo, el centro muestra el estado nuevo.",
        ["Hold-to-confirm slices"] =
            "Sectores con confirmación por mantenimiento",

        // ── topic:wheel-open-extras ──
        ["While a wheel is open"] =
            "Mientras una rueda está abierta",
        ["volume dpad scrub repeat mic microphone alt-tab window switcher desktop song enable disable chord toggle wheels off keyboard arrow esc"] =
            "volumen cruceta repetir micrófono alt-tab cambiador de ventanas escritorio canción activar desactivar teclado flecha esc",
        ["**Enable / disable the wheels** with the \"both sides\" of your invocation chord, pressed **together**:"] =
            "**Activa o desactiva las ruedas** con los «dos lados» de tu combinación de invocación, pulsados **a la vez**:",
        ["**Bumper/Trigger + D-Pad** is directional: **D-Pad Up = enable**, **D-Pad Down = disable**."] =
            "**Bumper/Trigger + cruceta** es direccional: **cruceta arriba = activar**, **cruceta abajo = desactivar**.",

        // ── topic:edit-mode ──
        ["Edit mode (in-wheel, controller-only)"] =
            "Modo de edición (en la rueda, solo con el mando)",
        ["edit stick click move add delete reorder undo redo picker installed game full capacity 12 limit thickness"] =
            "editar joystick pulsar mover añadir eliminar reordenar deshacer rehacer selector juego instalado lleno capacidad 12 límite grosor",
        ["**Add:** **{triangle}** opens the category→type **Add picker** ({cross} drills in, {circle} backs out). **Installed Game** opens the [[game-grid|Game Grid]] to pick a game, then returns to edit carrying the slice. Other types drop a slice immediately; free-text types (raw URL / keypress) land as placeholders you finish in Settings."] =
            "**Añadir:** **{triangle}** abre el **Selector de añadir** de categoría→tipo ({cross} entra, {circle} retrocede). **Juego instalado** abre la [[game-grid|Cuadrícula de juegos]] para elegir un juego y vuelve a la edición con el segmento ya creado. Los demás tipos colocan el segmento de inmediato; los tipos de texto libre (URL directa o pulsación de teclas) quedan como marcadores que se completan en Ajustes.",

        // ── topic:empty-wheel ──
        ["Single-wheel mode"] =
            "Modo de una sola rueda",
        ["empty disabled free gesture resurrect rebuild"] =
            "vacía desactivada gesto libre recuperar reconstruir",
        ["Emptying one wheel turns its side off: the chord that used to open it passes through to the game untouched."] =
            "Vaciar una rueda desactiva ese lado: la combinación que antes la abría pasa al juego sin alterarse.",
        ["To bring it back: **invoke it (hold the gesture) and click the aiming stick (L3/R3)**. The wheel opens centered, straight into the Add picker. (Toggle-style gestures have no hold, so they allow the stick click for a few seconds after the invoke.)"] =
            "Para recuperarla: **invócala (mantén el gesto) y haz clic en el stick de apuntado (L3/R3)**. La rueda se abre centrada, directamente en el selector de añadir. (Los gestos de tipo conmutación no tienen mantenido, así que admiten el clic del stick durante unos segundos tras la invocación.)",

        // ── topic:arcade-direct-launch ──
        ["Arcade direct-launch (a wheel that is just the Arcade)"] =
            "Lanzamiento directo del Arcade (una rueda que es solo el Arcade)",
        ["arcade direct launch shortcut lone only one single slice launcher skip wheel straight cabinets instant gesture dedicated side"] =
            "arcade lanzamiento directo acceso directo único solo un sector lanzador saltar rueda directo máquinas instantáneo gesto dedicado lado",
        ["**The arcade opens where that wheel would have been** - the left or right quarter of the screen, the same spot the wheel uses. **{circle}** closes it as usual."] =
            "**El arcade se abre donde habría estado esa rueda**: en el cuarto izquierdo o derecho de la pantalla, el mismo sitio que usa la rueda. **{circle}** lo cierra como de costumbre.",

        // ── topic:editor-desktop ──
        ["Slice editor tricks (Settings, mouse & keyboard)"] =
            "Trucos del editor de sectores (Ajustes, con ratón y teclado)",
        ["drag drop exe lnk shortcut launch slice reorder icon color color ctrl+s save autosave draft revert logo arrows cycle fetch steamgriddb"] =
            "arrastrar soltar exe lnk acceso directo iniciar sector reordenar icono color ctrl+s guardar autoguardado borrador revertir logotipo flechas ciclo descargar steamgriddb",
        ["**Drag a slice within the list** to reorder."] =
            "**Arrastra un sector dentro de la lista** para reordenarlo.",
        ["**Set a slice's icon and color** in the Icon & Color panel below the label."] =
            "**Define el icono y el color de un sector** en el panel Icon & Color (icono y color) situado bajo la etiqueta.",
        ["**Show Label** toggles the slice's text on the wheel. It appears only when [[show-labels|Show labels on]] is set to **Slices I Choose**."] =
            "**Mostrar la etiqueta** activa o desactiva el texto del segmento en la rueda. Solo aparece cuando [[show-labels|Mostrar etiquetas en]] está en **Los sectores que yo elija**.",
        ["Artwork on a slice"] =
            "Imágenes en un segmento",
        ["**A slice's action type locks once you Save it.** To change it, delete the slice and add a new one."] =
            "**El tipo de acción de un sector queda fijado al guardarlo.** Para cambiarlo, elimina el sector y añade uno nuevo.",

        // ── topic:game-grid ──
        ["Game Grid basics"] =
            "Fundamentos de la Cuadrícula de juegos",
        ["game grid browser launch navigate filter storefront chips footer add wheel pick mode installed assign favorite"] =
            "game grid cuadrícula juegos navegador lanzar navegar filtrar tienda fichas pie añadir rueda modo selección instalados asignar favorito",
        ["The Game Grid is a controller-scrollable view of every installed game across your storefronts, sorted **most-recently-launched first**, favorites pinned on top. The button controls are spelled out along the bottom of the grid."] =
            "La Cuadrícula de juegos es una vista recorrible con el mando de todos los juegos instalados en tus tiendas, ordenados **por los abiertos más recientemente**, con los favoritos fijados arriba. Los controles de botones están indicados a lo largo de la parte inferior de la cuadrícula.",
        ["**D-Pad / arrow keys** or the **left stick** move the selection."] =
            "**D-Pad / teclas de flecha** o el **joystick izquierdo** mueven la selección.",
        ["**{cross} / Enter** or a mouse double-click - launch the selected game. **{circle} / Esc** - close the grid."] =
            "**{cross} / Enter** o un doble clic de ratón: iniciar el juego seleccionado. **{circle} / Esc**: cerrar la cuadrícula.",
        ["**{triangle}** - **favorite** the selected game. Favorites sit in their own row of larger tiles at the top of every view. Press again to un-favorite."] =
            "**{triangle}**: marca el juego seleccionado como **favorito**. Los favoritos ocupan su propia fila de fichas más grandes en la parte superior de todas las vistas. Púlsalo de nuevo para quitarlo de favoritos.",
        ["**Hold {square}** - **hide the game from the grid**. Bring hidden games back with **Settings ▸ Advanced ▸ Game Grid ▸ Reset Hidden Games**. The same hold on a storefront's **Open <store>** card hides that whole store - see [[storefronts|Hiding a storefront]]."] =
            "**Mantener {square}**: **ocultar el juego de la cuadrícula**. Recupera los juegos ocultos con **Ajustes ▸ Avanzado ▸ Cuadrícula de juegos ▸ Restablecer juegos ocultos**. La misma pulsación mantenida sobre la ficha **Open <tienda>** oculta esa tienda entera; consulta [[storefronts|Ocultar una tienda]].",
        ["**L1 / R1** (or **PgUp / PgDn**) - cycle the storefront filter. A chip appears for each store you have installed."] =
            "**L1 / R1** (o **PgUp / PgDn**): recorrer el filtro de tiendas. Aparece una ficha por cada tienda que tengas instalada.",
        ["**Select** and **Start** - cycle the selected game's **cover** and **logo**; see [[cover-art|Cover art & logos]]."] =
            "**Seleccionar** y **Start**: recorrer la **portada** y el **logotipo** del juego seleccionado; consulta [[cover-art|Portadas y logotipos]].",
        ["Add a game to a wheel"] =
            "Añadir un juego a una rueda",
        ["Add games from the **in-wheel editor**: open a wheel → click the aiming stick to enter [[edit-mode|Edit]] → **{triangle} Add → Installed Game**, which opens the grid in **pick mode**. **{cross}** picks the highlighted game and carries it into the editor ({circle} cancels)."] =
            "Añade juegos desde el **editor dentro de la rueda**: abre una rueda → pulsa el stick de apuntado para entrar en [[edit-mode|Editar]] → **{triangle} Añadir → Juego instalado**, que abre la cuadrícula en **modo de selección**. **{cross}** elige el juego resaltado y lo lleva al editor ({circle} cancela).",

        // ── topic:cover-art ──
        ["Cover art & logos"] =
            "Carátulas y logotipos",
        ["cover art logo cycle select start share options steamgriddb sgdb dots spinner flat colors"] =
            "carátula imagen logotipo ciclo select start share options steamgriddb sgdb puntos indicador colores planos",
        ["**Start** (Options/Menu) - **cycle the logo overlay** and save it: default logo → up to 2 SteamGridDB alternates → **off** (raw cover) → wrap. A game with no logo art uses its centered title text."] =
            "**Start** (Options/Menu): **recorrer el logotipo superpuesto** y guardarlo: logotipo predeterminado → hasta 2 alternativas de SteamGridDB → **desactivado** (portada sin más) → vuelta a empezar. Un juego sin logotipo usa su título centrado.",
        ["**Art is fetched once and kept on disk**, so the grid opens instantly and offline after that. A big library fills in over the first few seconds of browsing. Reopen the grid and the stragglers should be there."] =
            "**Las ilustraciones se descargan una vez y se guardan en disco**, así que a partir de entonces la cuadrícula se abre al instante y sin conexión. Una biblioteca grande se completa durante los primeros segundos de navegación. Vuelve a abrir la cuadrícula y las que faltaban deberían estar ahí.",
        ["**A game with no art found is re-checked every couple of weeks** on its own, since art gets added over time. To recheck now, use **Retry Missing Game Art** ([[game-grid-options|Advanced ▸ Game Grid]])."] =
            "**Un juego sin ilustración encontrada se vuelve a comprobar solo cada par de semanas**, ya que con el tiempo se van añadiendo ilustraciones. Para comprobarlo ahora, usa **Reintentar carátulas que faltan** ([[game-grid-options|Avanzado ▸ Cuadrícula de juegos]]).",

        // ── topic:action-types ──
        ["Action types"] =
            "Tipos de acción",
        ["actions advanced launch keypress key combo volume audio display hdr sleep discord voice text chat url settings xbox mode obs mixer game bar windows"] =
            "acciones avanzado iniciar pulsación de teclas combinación volumen audio pantalla hdr suspender discord voz chat de texto url ajustes modo xbox obs mezclador game bar windows",
        ["Each slice runs one action, grouped by the editor's categories:"] =
            "Cada sector ejecuta una acción, agrupadas según las categorías del editor:",
        ["**Games & Apps** - installed game (direct launch), the Game Grid, storefront launcher (big-picture), launch/focus an app, **Exit Current App** (closes the frontmost app), and **Game Bar** (open, screenshot, start/stop recording, record the last 30 s, toggle mic - via Windows' Xbox Game Bar)."] =
            "**Juegos y aplicaciones**: juego instalado (lanzamiento directo), la Cuadrícula de juegos, lanzador de tienda (modo big-picture), lanzar o enfocar una aplicación, **Salir de la aplicación actual** (cierra la aplicación en primer plano) y **Game Bar** (abrir, captura de pantalla, iniciar/detener grabación, grabar los últimos 30 s, silenciar el micrófono), a través de la Xbox Game Bar de Windows.",
        ["**Chat & Streaming** - Discord (launch, join/leave a voice channel, deafen), Steam Chat (open chat) - see [[steam-xbox-voice|Steam voice chat]] - plus **Mic Mute** (the one Windows mic mute, offered in both groups and under System ▸ Audio), [[text-chat|Text Chat]], and **OBS Studio** ([[obs-studio|streaming, recording, replay, scenes, source mute]])."] =
            "**Chat y emisión**: Discord (abrir, entrar/salir de un canal de voz, ensordecer), chat de Steam (abrir el chat) - consulta [[steam-xbox-voice|Chat de voz de Steam]] -, además de **Silenciar micro** (el único silencio de micrófono de Windows, ofrecido en ambos grupos y en Sistema ▸ Audio), [[text-chat|Chat de texto]] y **OBS Studio** ([[obs-studio|emisión, grabación, repetición, escenas, silenciar fuente]]).",
        ["**System** - [[controller-mode|controller mode]] (**Xbox** / **DualShock**); audio (switch output, mute, mic mute, set volume, play/pause, next/previous); display (**Toggle Extend/Clone** and HDR toggle); **Windows** (**Show Desktop** - fire again to put the windows back - and **Empty Recycle Bin**); power (sleep/hibernate/reboot/shut down/log out/lock, and **Power Plan**, which flips between two plans you pick)."] =
            "**Sistema**: [[controller-mode|modo de mando]] (**Xbox** / **DualShock**); audio (cambiar salida, silenciar, silenciar micrófono, fijar volumen, reproducir/pausar, siguiente/anterior); pantalla (**Alternar extender/duplicar** y conmutar HDR); **Windows** (**Mostrar escritorio**, que al ejecutarse de nuevo restaura las ventanas, y **Vaciar la papelera de reciclaje**); energía (suspender, hibernar, reiniciar, apagar, cerrar sesión, bloquear y **Plan de energía**, que alterna entre dos planes que tú elijas).",
        ["**Reboot** has a **Log In after Reboot** checkbox: checked (the default), Windows signs you back in where policy allows. Unchecked is a traditional restart, which can land at the sign-in screen."] =
            "**Reiniciar** tiene una casilla **Iniciar sesión tras reiniciar**: marcada (lo predeterminado), Windows vuelve a iniciar tu sesión donde las directivas lo permitan. Sin marcar es un reinicio tradicional, que puede acabar en la pantalla de inicio de sesión.",
        ["**Custom** - [[open-uri|Open URI]] and [[key-combo|Key Combo]] (send a keyboard shortcut like `Win+D` or `PlayPause`)."] =
            "**Personalizado**: [[open-uri|Abrir URI]] y [[key-combo|Combinación de teclas]] (enviar un atajo de teclado como `Win+D` o `PlayPause`).",
        ["**Radiata** - the Game Grid, open Settings, and disable wheels. [[passthru-mode|Passthru Mode]] is not a slice: turn it on or off from the tray or Settings ▸ Passthru Mode."] =
            "**Radiata**: la Cuadrícula de juegos, abrir Ajustes y desactivar las ruedas. [[passthru-mode|Modo directo]] no es una porción: actívalo o desactívalo desde la bandeja o desde Ajustes ▸ Modo directo.",
        ["A slice missing a required value arms as **\"Configure in Settings\"**. Firing it opens that slice's editor."] =
            "Un sector al que le falte un valor obligatorio se prepara como **\"Configurar en Ajustes\"**. Al ejecutarlo se abre el editor de ese sector.",

        // ── topic:arcade ──
        ["Arcade"] =
            "Arcade",
        ["arcade game games minigame mini-game kabloom connate petal pop twist breakout paddle brick pentagon square hexagon octagon smash win minesweeper merge bee flower bomb picker play waiting loading queue lobby kill time score"] =
            "arcade juego juegos minijuego mini-juego kabloom connate petal pop giro romper ladrillos pala ladrillo pentágono cuadrado hexágono octógono remate ganar buscaminas fusionar abeja flor bomba selector jugar esperar carga cola sala matar el tiempo puntuación",
        ["**{cross}** acts, **{square}** is each game's second action, and the **left stick** aims. The **D-Pad** drives the menus. **{triangle} explains the game you're in**, and **START pauses** with **Resume**, **How to play**, **Reset**, and that game's own settings."] =
            "**{cross}** actúa, **{square}** es la segunda acción de cada juego y el **joystick izquierdo** apunta. El **D-Pad** maneja los menús. **{triangle} explica el juego en el que estás** y **START pausa**, con **Continuar**, **Cómo jugar**, **Reiniciar** y los ajustes propios de ese juego.",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] and [[arcade-internode|Internode]]."] =
            "**Cada juego tiene su propia página**: [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] e [[arcade-internode|Internode]].",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] and [[arcade-petalpop|Petalpop]]."] =
            "**Cada juego tiene su propia página**: [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] y [[arcade-petalpop|Petalpop]].",
        ["**You can write your own games** and drop them in - see [[custom-arcade-games|Custom Arcade games]]."] =
            "**Puedes escribir tus propios juegos** y añadirlos: consulta [[custom-arcade-games|Juegos Arcade personalizados]].",

        // ── topic:arcade-kabloom ──
        ["Arcade: Kabloom"] =
            "Arcade: Kabloom",
        ["kabloom arcade minesweeper petal petals flower tile disc bee flag mark question mark cursor reveal solvable no guessing guess solver proof certified baked stacked gem diamond level campaign"] =
            "kabloom arcade buscaminas pétalo pétalos flor casilla disco abeja bandera marca interrogación cursor descubrir resoluble sin adivinar adivinanza solucionador demostración certificado precalculado apiladas gema diamante nivel campaña",
        ["Part of the [[arcade|Arcade]]."] =
            "Parte del [[arcade|Arcade]].",

        // ── topic:arcade-connate ──
        ["Arcade: Connate"] =
            "Arcade: Connate",
        ["connate arcade merge merging numbers number cluster pile rim ring colors colours families bomb charge fire lob orb doubling"] =
            "connate arcade fusión fusionar números número grupo montón borde anillo colores familias bomba carga disparar lanzar orbe duplicar",

        // ── topic:arcade-petalpop ──
        ["Arcade: Petalpop"] =
            "Arcade: Petalpop",
        ["petal pop petalpop arcade paddle paddles breakout brick bricks ball flower core gold split multiball smash slingshot spring rail square pentagon hexagon heptagon octagon lives win 5-8"] =
            "petal pop petalpop arcade pala palas arkanoid ladrillo ladrillos bola flor núcleo dorado dividir multibola golpe tirachinas muelle raíl cuadrado pentágono hexágono heptágono octógono vidas victoria 5-8",

        // ── topic:arcade-internode ──
        ["Arcade: Internode"] =
            "Arcade: Internode",
        ["internode arcade half-pipe halfpipe pipe bike runner token tokens mine mines jump hop gate gold gate quota checkpoint stage bank score camera roll rim wall swing gap break fall"] =
            "internode arcade half-pipe tubo moto corredor ficha fichas mina minas saltar salto puerta puerta dorada cuota punto de control fase banco puntuación cámara giro borde pared balanceo hueco rotura caída",

        // ── topic:app-slices ──
        ["App & launcher slices"] =
            "Sectores de aplicaciones y lanzadores",
        ["launch focus toggle kill process name override executable path storefront big picture installed apps store uwp"] =
            "iniciar enfocar alternar cerrar nombre de proceso ruta del ejecutable tienda pantalla grande aplicaciones instaladas microsoft store uwp",
        ["**Behavior** - **Run** launches the app, or focuses it if it's already running. **Toggle** launches the app or requests a normal close, like clicking its ✕. The hub reports **Close requested**, not a confirmed exit: unsaved-work prompts stay open until you answer them, apps may refuse to close or keep running in the tray, and Toggle leaves windowless processes running."] =
            "**Comportamiento**: **Ejecutar** lanza la aplicación, o la enfoca si ya se está ejecutando. **Conmutar** la lanza o solicita un cierre normal, como si pulsaras su ✕. El centro informa de **Cierre solicitado**, no de una salida confirmada: los avisos de trabajo sin guardar siguen abiertos hasta que respondas, las aplicaciones pueden negarse a cerrarse o seguir en la bandeja, y Conmutar deja en marcha los procesos sin ventana.",
        ["**Storefront** (launcher slices) - opens the store's big-picture/fullscreen mode. Only Steam and Playnite have a true one; the Xbox app is maximized; the rest just open. A storefront is offered only when its launcher app is actually installed."] =
            "**Tienda** (sectores de lanzador): abre el modo de pantalla grande o pantalla completa de la tienda. Solo Steam y Playnite tienen uno de verdad; la aplicación de Xbox se maximiza y el resto simplemente se abre. Una tienda solo se ofrece cuando su aplicación está realmente instalada.",

        // ── topic:controller-mode ──
        ["Controller mode (Xbox / DualShock)"] =
            "Modo de mando (Xbox / DualShock)",
        ["controller mode xbox dualshock emulation virtual pad game pass xinput button prompts glyphs playstation switch pad type unsupported controller"] =
            "modo mando xbox dualshock emulación mando virtual game pass xinput indicaciones botones símbolos playstation cambiar tipo mando no compatible",
        ["Arming the slice shows **On** or **Off** in the hub, so you can check which mode you're in without changing it."] =
            "Al preparar el sector se muestra **Activado** u **Desactivado** en el centro de la rueda, de modo que puedes comprobar en qué modo estás sin cambiarlo.",

        // ── topic:switch-audio ──
        ["Switch Audio Output"] =
            "Cambiar la salida de audio",
        ["switch audio output device mic microphone speakers headset cycle default endpoint"] =
            "cambiar salida audio dispositivo micrófono altavoces auriculares ciclo predeterminado",
        ["**Switch Audio Output** changes the Windows default audio device(s). Both fields match by **partial name**, case-insensitively - \"Speakers\" matches \"Speakers (Realtek…)\". The **▾ button** beside each field lists your connected devices, and a new slice starts pre-filled with your current defaults."] =
            "**Cambiar la salida de audio** cambia los dispositivos de audio predeterminados de Windows. Ambos campos coinciden por **nombre parcial**, sin distinguir mayúsculas: \"Speakers\" coincide con \"Speakers (Realtek…)\". El **botón ▾** junto a cada campo enumera tus dispositivos conectados, y un sector nuevo se rellena previamente con tus predeterminados actuales.",
        ["**Output Device** - the playback device to switch to. **Blank = cycle** through your outputs on each fire (unless a Mic Device is set, which leaves the output alone)."] =
            "**Dispositivo de salida**: el dispositivo de reproducción al que cambiar. **En blanco = recorrer** tus salidas en cada ejecución (salvo que haya un Mic Device definido, en cuyo caso la salida no se toca).",
        ["**Mic Device** - the recording device to switch to. **Blank = leave the mic unchanged.**"] =
            "**Micrófono** (dispositivo de micrófono): el dispositivo de grabación al que cambiar. **En blanco = dejar el micrófono sin cambios.**",
        ["Set both fields to switch output + mic in one slice - \"TV + no mic\", \"Headset + headset mic\". Firing shows the device switched to in the hub."] =
            "Rellena ambos campos para cambiar salida y micrófono en un solo sector: \"TV sin micrófono\", \"Auriculares con micrófono de auriculares\". Al ejecutarlo, el centro muestra el dispositivo al que se ha cambiado.",

        // ── topic:open-uri ──
        ["Open URI"] =
            "Abrir URI",
        ["uri url link scheme https steam discord ms-settings deep link protocol"] =
            "uri url enlace esquema https steam discord ms-settings enlace profundo protocolo",
        ["An **Open URI** slice hands its value to Windows to open with whatever handles that scheme. That covers a lot more than web links:"] =
            "Un sector **Abrir URI** entrega su valor a Windows para que lo abra con lo que gestione ese esquema. Eso abarca mucho más que enlaces web:",
        ["**Web URLs** - `https://twitch.tv/yourchannel` opens in your default browser."] =
            "**Direcciones web**: `https://twitch.tv/tucanal` se abre en tu navegador predeterminado.",
        ["**App deep links** - `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…` - anything an installed app registers a protocol for."] =
            "**Enlaces profundos de aplicaciones**: `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…`, cualquier cosa para la que una aplicación instalada registre un protocolo.",
        ["**Windows pages** - `ms-settings:display` opens that Settings page."] =
            "**Páginas de Windows**: `ms-settings:display` abre esa página de Configuración.",
        ["The value must be a complete, absolute URI. A bare `twitch.tv/...` won't launch - include the `https://`."] =
            "El valor debe ser un URI completo y absoluto. Un `twitch.tv/...` a secas no se abrirá: incluye el `https://`.",
        ["Game-launch URIs (`steam://rungameid/…`) work here too, but the **Installed Game** slice type builds them for you, which is easier."] =
            "Los URI de inicio de juegos (`steam://rungameid/…`) también funcionan aquí, pero el tipo de sector **Juego instalado** los construye por ti, lo cual es más cómodo.",

        // ── topic:key-combo ──
        ["Key Combo (send a keyboard shortcut)"] =
            "Key Combo (enviar un atajo de teclado)",
        ["key combo keypress keyboard shortcut hotkey send keys format grammar win ctrl alt shift del delete media volup voldown mute playpause next prev navy blue modifier plus"] =
            "key combo combinación pulsación teclado atajo tecla rápida enviar teclas formato sintaxis win ctrl alt shift del suprimir supr multimedia volup voldown silenciar playpause siguiente anterior azul marino modificador más",
        ["A **Key Combo** slice (Custom) presses a keyboard shortcut for you. Write it as key names joined with **`+`** - `Win+D`, `Ctrl+Shift+Esc`, `Alt+F4` - or a single key like `PlayPause`. The **last** name is the key pressed; everything before it is a modifier held around it. Names aren't case-sensitive."] =
            "Un sector **Combinación de teclas** (Personalizado) pulsa un atajo de teclado por ti. Escríbelo como nombres de tecla unidos con **`+`** (`Win+D`, `Ctrl+Shift+Esc`, `Alt+F4`) o como una sola tecla, por ejemplo `PlayPause`. El **último** nombre es la tecla que se pulsa; todo lo anterior es un modificador que se mantiene a su alrededor. Los nombres no distinguen mayúsculas de minúsculas.",
        ["In a combo, the last name is the key that actually gets pressed and everything before it is a modifier held down around it."] =
            "En una combinación, el último nombre es la tecla que realmente se pulsa y todo lo anterior son modificadores que se mantienen pulsados a su alrededor.",
        ["**Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`."] =
            "**Modificadores:** `Ctrl`, `Alt`, `Shift`, `Win`.",
        ["**Keys:** letters and digits; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; the arrows `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` and `Slash`; and the **media keys** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop` - media keys work on their own, no modifier needed."] =
            "**Teclas:** letras y dígitos; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; las flechas `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` y `Slash`; y las **teclas multimedia** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop`, que funcionan por sí solas, sin modificador.",
        ["The combo is sent as real keystrokes. One thing can block it: an app running **as administrator** won't accept keystrokes from Radiata. This is a Windows limitation."] =
            "La combinación se envía como pulsaciones reales. Solo una cosa puede bloquearla: una aplicación que se ejecuta **como administrador** no acepta pulsaciones de Radiata. Es una limitación de Windows.",

        // ── topic:discord-setup ──
        ["Discord voice-channel setup"] =
            "Configuración de canales de voz de Discord",
        ["discord credentials client id secret oauth voice join leave mute deafen keybind"] =
            "discord credenciales client id secreto oauth voz entrar salir silenciar ensordecer atajo",
        ["**Launch Discord** works out of the box. **Join/Leave Voice Channel**, **Deafen** and **Mute Me** talk to Discord directly, so those three need your own free Discord application credentials. Until they exist the slice editor shows a **Configure Discord Integration** button, and the same wizard sits in **Settings ▸ Advanced**; firing one of those slices from the wheel opens it too, rather than doing nothing."] =
            "**Iniciar Discord** funciona sin más. **Entrar/salir del canal de voz**, **Ensordecer** y **Silenciarme** hablan directamente con Discord, así que esos tres necesitan tus propias credenciales gratuitas de aplicación de Discord. Hasta que existan, el editor de sectores muestra un botón **Configurar la integración con Discord**, y el mismo asistente está en **Ajustes ▸ Avanzado**; ejecutar uno de esos sectores desde la rueda también lo abre, en lugar de no hacer nada.",
        ["**Deafen** and **Mute Me** toggle Discord's own switches - the same ones the headphone and microphone buttons at the bottom-left of Discord flip - and they work while a game has focus. No keybind to set up, and the game never sees a keystroke."] =
            "**Ensordecer** y **Silenciarme** conmutan los propios interruptores de Discord, los mismos que accionan los botones de auriculares y micrófono de la esquina inferior izquierda de Discord, y funcionan mientras el juego tiene el foco. No hay ninguna combinación de teclas que configurar, y el juego nunca ve una pulsación.",
        ["To get the **channel link**: in Discord, right-click the voice channel → **Copy Link**, and paste it into the slice's **Discord URL** field (Radiata normalizes it to the `discord://` form)."] =
            "Para obtener el **enlace del canal**: en Discord, haz clic con el botón derecho en el canal de voz → **Copy Link** (copiar enlace) y pégalo en el campo **URL de Discord** del sector (Radiata lo convierte al formato `discord://`).",

        // ── topic:steam-xbox-voice ──
        ["Steam voice chat"] =
            "Chat de voz de Steam",
        ["steam chat friends voice mic mute push to talk hotkey open limits deafen join leave"] =
            "steam chat amigos voz micrófono silenciar pulsar para hablar atajo abrir límites ensordecer entrar salir",
        ["The **Steam Chat** group does everything Steam allows a third-party app to do, which is less than Discord allows. Discord provides a local control channel that Radiata's Join/Leave slice uses; **Steam provides none**. What you can do:"] =
            "El grupo **Chat de Steam** hace todo lo que Steam permite hacer a una aplicación de terceros, que es menos de lo que permite Discord. Discord ofrece un canal de control local que usa el sector Entrar/salir de Radiata; **Steam no ofrece ninguno**. Lo que sí puedes hacer:",
        ["**Open Steam Chat** - opens Steam's **Friends & Chat** window. Join a group's voice channel from there. Steam gives another app no way to join, leave or switch voice channels, and has no mute-incoming-voice control at all."] =
            "**Abrir el chat de Steam**: abre la ventana **Amigos y chat** de Steam. Desde ahí puedes entrar en el canal de voz de un grupo. Steam no da a otra aplicación ninguna forma de entrar, salir ni cambiar de canal de voz, y no tiene ningún control para silenciar la voz entrante.",
        ["**Mic Mute** - in the group, and the same slice as System ▸ Audio. It mutes your **Windows microphone**, which is what Steam transmits from, so the others can't hear you. Every other app loses the mic as well, since Steam exposes no app-scoped mute. Live Muted/Unmuted state shows in the hub."] =
            "**Silenciar micrófono**: está en el grupo, y es el mismo sector que Sistema ▸ Audio. Silencia tu **micrófono de Windows**, que es desde donde transmite Steam, así que los demás no te oyen. Todas las demás aplicaciones pierden también el micrófono, ya que Steam no expone ningún silencio limitado a la aplicación. El estado Silenciado/Activo se muestra en vivo en el centro.",
        ["For full voice control from a slice - join, leave, mute, deafen - Discord remains the best-supported option; see [[discord-setup|Discord voice-channel setup]]."] =
            "Para un control de voz completo desde un sector (entrar, salir, silenciar, ensordecer), Discord sigue siendo la opción mejor soportada; consulta [[discord-setup|Configuración del canal de voz de Discord]].",

        // ── topic:obs-studio ──
        ["OBS Studio"] =
            "OBS Studio",
        ["obs studio websocket streaming recording replay buffer scene source mute setup port password"] =
            "obs studio websocket emisión grabación búfer de repetición escena fuente silenciar configuración puerto contraseña",
        ["**OBS Studio** slices - Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute… - drive OBS over the **obs-websocket** protocol."] =
            "Las porciones de **OBS Studio** (Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute…) controlan OBS mediante el protocolo **obs-websocket**.",
        ["**One-time setup:** in OBS, **Tools ▸ WebSocket Server Settings** ▸ enable the server, then copy its **port** (default `4455`) and **password** into **Settings ▸ Advanced ▸ Integrations ▸ Configure OBS Integration…**. It's one shared setting, and **Test** confirms the connection on the spot. Until it's set up, OBS slices arm as **\"Configure in Settings\"**, and an OBS slice's editor offers the same setup pane."] =
            "**Configuración inicial:** en OBS, **Herramientas ▸ Configuración del servidor WebSocket** ▸ activa el servidor y copia su **puerto** (`4455` de forma predeterminada) y su **contraseña** en **Ajustes ▸ Avanzado ▸ Integraciones ▸ Configurar la integración con OBS…**. Es un único ajuste compartido, y **Probar** confirma la conexión al momento. Hasta que esté configurado, los sectores de OBS se preparan como **\"Configurar en Ajustes\"**, y el editor de un sector de OBS ofrece el mismo panel de configuración.",
        ["**Scene** and **audio-source** names on the slice must match OBS exactly."] =
            "Los nombres de **escena** y de **fuente de audio** indicados en el sector deben coincidir exactamente con los de OBS.",
        ["**What's supported:** **OBS Studio 28 or later** (the WebSocket server is built in) and **OBS 27 or earlier with the obs-websocket 5.x plugin**. Forks that speak the same protocol (**StreamElements OBS.Live**, for one) work identically. **Streamlabs Desktop does NOT** - it's a different app without obs-websocket."] =
            "**Qué es compatible:** **OBS Studio 28 o posterior** (el servidor WebSocket viene incluido) y **OBS 27 o anterior con el complemento obs-websocket 5.x**. Las bifurcaciones que hablan el mismo protocolo (**StreamElements OBS.Live**, por ejemplo) funcionan igual. **Streamlabs Desktop NO**: es otra aplicación, sin obs-websocket.",

        // ── topic:text-chat ──
        ["Text Chat (send a message into a game)"] =
            "Text Chat (enviar un mensaje a un juego)",
        ["text chat message send game keybind enter t y chat button custom quick phrase gg glhf cooldown"] =
            "chat de texto mensaje enviar juego atajo enter t y botón chat personalizado frase rápida gg glhf tiempo de espera",
        ["**Try Game Default sends nothing unless the focused game is in the index.** When the game in front isn't covered, the hub names it and says **\"No chat key default found. Configure in Settings\"** - switch that slice to **Custom…** and set the key."] =
            "**Probar el valor del juego no envía nada si el juego en primer plano no está en el índice.** Cuando el juego de delante no está cubierto, el centro lo nombra y dice **\"No hay tecla de chat predeterminada. Configúrala en Ajustes\"**: cambia ese sector a **Personalizado…** y define la tecla.",
        ["**Limits:** the game must be focused and must accept its chat key at that moment."] =
            "**Límites:** el juego debe tener el foco y debe aceptar su tecla de chat en ese momento.",

        // ── topic:volume-mixer ──
        ["D-Pad 🡄 🡆"] =
            "Cruceta 🡄 🡆",
        ["volume mixer balance dpad left right game chat discord music spotify browser desktop song track app session advanced mic microphone input alt-tab task switcher window"] =
            "mezclador de volumen balance cruceta izquierda derecha juego chat discord música spotify navegador escritorio canción pista sesión avanzado micrófono entrada alt-tab cambiador de ventanas",
        ["**While a wheel is open, D-Pad 🡄 🡆** does one of four things. Pick in the **Settings ▸ Customize ▸ D-Pad 🡄 🡆** section:"] =
            "**Mientras hay una rueda abierta, la cruceta 🡄 🡆** hace una de estas cuatro cosas. Elige en la sección **Ajustes ▸ Personalizar ▸ Cruceta 🡄 🡆**:",
        ["**Cycles Windows** (default) - steps through the Alt-Tab switcher, one window per press. The switcher stays up while the wheel is open and lands on the highlighted window when the wheel closes."] =
            "**Cambia de ventana** (recorrer ventanas, predeterminado): avanza por el conmutador Alt-Tab, una ventana por pulsación. El conmutador permanece visible mientras la rueda está abierta y se queda en la ventana resaltada cuando la rueda se cierra.",
        ["**Cycles Desktops** - switches Windows virtual desktops, the same as **Win+Ctrl+🡄 🡆**."] =
            "**Cambia de escritorio**: cambia entre los escritorios virtuales de Windows, igual que **Win+Ctrl+🡄 🡆**.",
        ["**Skips Songs** - the same track-skip keys as the Audio slices; each skip flashes a ⏮ / ⏭ icon in the hub."] =
            "**Salta canciones** (saltar canciones): las mismas teclas de cambio de pista que los sectores de audio; cada salto muestra brevemente un icono ⏮ / ⏭ en el centro de la rueda.",
        ["**Mic Volume** - turns your default mic up and down in 5% steps, holding to repeat, and un-mutes it on the way up. The level shows in the hub with a **microphone** icon."] =
            "**Volumen del micrófono**: sube y baja el micrófono predeterminado en pasos del 5 %, repitiendo si mantienes, y lo reactiva al subir. El nivel se muestra en el centro con un icono de **micrófono**.",
        ["See [[wheel-open-extras|While a wheel is open]] for everything else the D-Pad does with a wheel up."] =
            "Consulta [[wheel-open-extras|Con una rueda abierta]] para todo lo demás que hace el D-Pad con una rueda desplegada.",

        // ── topic:supported-controllers ──
        ["Supported controllers"] =
            "Mandos compatibles",
        ["dualsense edge dualshock ds4 xbox xinput bluetooth usb bleed through shared input"] =
            "dualsense edge dualshock ds4 xbox xinput bluetooth usb filtración entrada compartida",
        ["**Third-party Xbox-style pads over Bluetooth** - most present as a DualShock 4 over BT, so they get the full isolation path too."] =
            "**Mandos de estilo Xbox de terceros por Bluetooth**: la mayoría se presentan como un DualShock 4 por BT, así que también obtienen la ruta de aislamiento completa.",
        ["**If you've remapped L4 or R4 on the pad itself** (holding L4/R4 + a button + the mapping key), that paddle now sends the button you assigned and Radiata can no longer see it - so it stops opening wheels. Clear the remap on the pad to get it back, or pick a chord in [[triggers|Settings ▸ Customize ▸ Triggers]] instead."] =
            "**Si has reasignado L4 o R4 en el propio mando** (manteniendo L4/R4 + un botón + la tecla de asignación), esa paleta envía ahora el botón que le asignaste y Radiata ya no puede verla, así que deja de abrir ruedas. Borra la reasignación en el mando para recuperarla, o elige en su lugar un acorde en [[triggers|Ajustes ▸ Personalizar ▸ Gestos de invocación]].",
        ["**Xbox pads over USB or wireless dongle (XInput)** - isolated with the same cloak, presenting a virtual **Xbox 360** pad to the game. If a pad can't be cloaked for any reason, Radiata falls back automatically to **shared-input mode**: the wheel still works, but the game also sees your input while a wheel is up."] =
            "**Mandos Xbox por USB o receptor inalámbrico (XInput)**: se aíslan con el mismo ocultamiento y presentan al juego un mando **Xbox 360** virtual. Si por cualquier motivo no se puede ocultar un mando, Radiata recurre automáticamente al **modo de entrada compartida**: la rueda sigue funcionando, pero el juego también ve tu entrada mientras hay una rueda abierta.",
        ["**Two or more Xbox pads plugged in at once** - isolation switches off and both pads keep working in shared-input mode, since cloaking would make a second player's controller disappear. Unplug the second pad and isolation comes back on its own."] =
            "**Dos o más mandos Xbox conectados a la vez**: el aislamiento se desactiva y ambos mandos siguen funcionando en modo de entrada compartida, ya que ocultarlos haría desaparecer el mando de un segundo jugador. Desconecta el segundo mando y el aislamiento vuelve por sí solo.",

        // ── topic:input-isolation ──
        ["Input isolation (what the drivers do)"] =
            "Aislamiento de entrada (qué hacen los controladores)",
        ["isolation virtual pad cloak hidhide vigem drivers double input neutral joy.cpl lag latency delay ms milliseconds input lag polling rate overhead rumble"] =
            "aislamiento mando virtual ocultar hidhide vigem controladores entrada doble neutral joy.cpl retardo latencia ms milisegundos frecuencia de sondeo sobrecarga vibración",
        ["With successful isolation, the game reads the virtual pad; in Passthru Mode or with no drivers installed, the game reads your controller directly and Radiata simply watches alongside it."] =
            "Con el aislamiento correcto, el juego lee el mando virtual; en Modo directo o sin controladores instalados, el juego lee tu mando directamente y Radiata se limita a observar en paralelo.",
        ["Isolation forwards sticks, triggers, D-Pad and standard buttons. Sony touchpad, gyro, speaker/mic, adaptive triggers, haptics and rumble are not forwarded. Captured Xbox input supports standard two-motor rumble, but not Share or impulse-trigger motors. [[passthru-mode|Passthru Mode]] or quitting requests removal of Radiata's capture; games may need to reconnect or restart, and other remappers can still affect native features."] =
            "El aislamiento transmite sticks, gatillos, cruceta y botones estándar. El panel táctil, el giroscopio, el altavoz y micrófono, los gatillos adaptativos, la respuesta háptica y la vibración de los mandos Sony no se transmiten. La entrada Xbox capturada admite la vibración estándar de dos motores, pero no los motores de Share ni de los gatillos de impulso. El [[passthru-mode|Modo directo]] o salir de la aplicación solicitan la retirada de la captura de Radiata; puede que los juegos tengan que reconectarse o reiniciarse, y otros remapeadores pueden seguir afectando a las funciones nativas.",
        ["Input latency"] =
            "Latencia de entrada",
        ["**Sony pads and Xbox pads over Bluetooth** - **imperceptible**. Reports are passed straight through as they arrive."] =
            "**Mandos Sony y mandos Xbox por Bluetooth**: **imperceptible**. Los informes se transmiten tal cual a medida que llegan.",
        ["**Xbox pads over USB or a wireless dongle (XInput)** - **a few milliseconds at most**, because these have to be polled."] =
            "**Mandos Xbox por USB o receptor inalámbrico (XInput)**: **unos pocos milisegundos como mucho**, porque hay que sondearlos.",
        ["**Passthru Mode, or no drivers installed** - **none**. The game reads your physical controller directly."] =
            "**Modo directo, o sin controladores instalados**: **ninguna**. El juego lee tu mando físico directamente.",
        ["For scale: a 60 fps game draws a frame every **16.7 ms**. Radiata never injects into or hooks a game, so it adds nothing to rendering or frame pacing."] =
            "Para hacerte una idea: un juego a 60 fps dibuja un fotograma cada **16,7 ms**. Radiata nunca inyecta código ni engancha un juego, así que no añade nada al renderizado ni al ritmo de fotogramas.",
        ["**While a wheel, Game Grid or editor is on screen, Radiata holds its virtual pad neutral.** If the game still reacts, another input path may be active - follow the [[controller-conflict-checklist|Controller conflict checklist]]. Passthru Mode deliberately lets controller input reach the game."] =
            "**Mientras hay una rueda, la Cuadrícula de juegos o el editor en pantalla, Radiata mantiene su mando virtual en reposo.** Si aun así el juego reacciona, puede haber otra ruta de entrada activa: sigue la [[controller-conflict-checklist|Lista de comprobación de conflictos de mando]]. El Modo directo deja deliberadamente que la entrada del mando llegue al juego.",
        ["For driver repair, HP OMEN buses and version checks, see [[driver-conflicts|HP OMEN & driver version conflicts]]. For busy or hidden-device access, see [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "Para reparar controladores, buses de HP OMEN y comprobaciones de versión, consulta [[driver-conflicts|Conflictos con HP OMEN y versiones de controladores]]. Para acceso ocupado o a dispositivos ocultos, consulta [[hidhide-troubleshooting|Solución de problemas de HidHide]].",

        // ── topic:passthru-mode ──
        ["Set Passthru Mode (Bypass Input Isolation)"] =
            "Modo directo (omitir el aislamiento de entrada)",
        ["passthru mode passthrough safe mode bypass input isolation controller interception anticheat valorant vanguard eac battleye exceptions per game automatic capture"] =
            "passthru modo paso directo passthrough modo seguro omitir aislamiento entrada intercepción mando anticheat valorant vanguard eac battleye excepciones por juego automático captura",
        ["Some competitive titles with kernel anticheat (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) could theoretically react to emulated or hidden devices. **Passthru Mode** bypasses [[input-isolation|input isolation]] entirely: Radiata removes its virtual pad and requests release of its own controller blocks. Other tools may still hide or remap the controller. The wheel still works; the trade-off is that input reaches the game while a wheel is open, which is what \"bleed-through\" or \"double input\" means. Don't confuse it with disabling the **wheels** - that keeps the virtual pad in place and only stops summons. Passthru Mode removes the virtual pad altogether."] =
            "Algunos títulos competitivos con anti-cheat de kernel (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) podrían teóricamente reaccionar ante dispositivos emulados u ocultos. El **Modo directo** omite por completo el [[input-isolation|aislamiento de entrada]]: Radiata retira su mando virtual y solicita liberar sus propios bloqueos de mando. Otras herramientas pueden seguir ocultando o remapeando el mando. La rueda sigue funcionando; la contrapartida es que la entrada llega al juego mientras hay una rueda abierta, que es lo que significa \"filtrado\" o \"entrada duplicada\". No lo confundas con desactivar las **ruedas**: eso mantiene el mando virtual en su sitio y solo detiene las invocaciones. El Modo directo retira el mando virtual por completo.",
        ["**Automatic per-game:** add a game to the **Always use Passthru Mode for** list - from the installed-games dropdown, or **Add Application…** for a specific .exe. Radiata enters Passthru Mode while it runs and restores capture on exit; the tray shows \"(auto: <game>)\"."] =
            "**Automático por juego:** añade un juego a la lista **Usar siempre el Modo directo con**, desde el desplegable de juegos instalados o con **Añadir aplicación…** para un .exe concreto. Radiata entra en Modo directo mientras se ejecuta y restablece la captura al salir; la bandeja muestra \"(auto: <juego>)\".",
        ["Each entry engages **While Running** (recommended - anticheat watches from launch) or **While Frontmost** (only while the game's window is focused)."] =
            "Cada entrada se activa **Mientras se ejecuta** (mientras se ejecuta, recomendado, porque el anticheat vigila desde el inicio) o **Mientras está en primer plano** (solo mientras la ventana del juego tiene el foco).",
        ["Passthru Mode is **best-effort**. Per-game detection polls every ~2 seconds, so a just-launched game can briefly see normal capture. For the strictest titles, toggle Passthru Mode on manually *before* launching. The isolation drivers also stay installed system-wide either way."] =
            "Modo directo es **el mejor esfuerzo posible**. La detección por juego consulta cada ~2 segundos, así que un juego recién lanzado puede ver brevemente la captura normal. Para los títulos más estrictos, activa Modo directo manualmente *antes* de lanzarlos. En cualquier caso, los controladores de aislamiento permanecen instalados en todo el sistema.",

        // ── topic:tray-and-settings ──
        ["Tray & Settings (mouse/keyboard, at the desk)"] =
            "Bandeja del sistema y Ajustes (con ratón y teclado, en el escritorio)",
        ["tray icon left click right click menu settings tabs f1 f2 test"] =
            "bandeja icono clic izquierdo clic derecho menú ajustes pestañas f1 f2 probar",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Show in Explorer, Start with Windows, Exit)."] =
            "**Icono de la bandeja:** clic izquierdo abre Ajustes; clic derecho muestra el menú (Desactivar las ruedas/Activar las ruedas, Modo directo, **Cuadrícula de juegos**, Ajustes, Ayuda, Acerca de Radiata, Mostrar en el Explorador, Iniciar con Windows, Salir).",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Start with Windows, Exit)."] =
            "**Icono de la bandeja:** clic izquierdo abre Ajustes; clic derecho muestra el menú (Desactivar las ruedas/Activar las ruedas, Modo directo, **Cuadrícula de juegos**, Ajustes, Ayuda, Acerca de Radiata, Iniciar con Windows, Salir).",
        ["**Test Wheel** - each Wheel tab has a **Test Left/Right Wheel** button under the slice list that opens that wheel on screen (with the wheels enabled)."] =
            "**Probar la rueda**: cada pestaña de rueda tiene un botón **Probar la rueda izquierda/derecha** bajo la lista de sectores que abre esa rueda en pantalla (con las ruedas activadas).",

        // ── topic:customize ──
        ["Customize (look, feel & sound)"] =
            "Personalizar (aspecto, comportamiento y sonido)",
        ["customize material light dark flat pearl obsidian mesa gloss terra theme slices thickness ring thick medium thin button icons glyphs best guess sound effects themed material digital physical silent none preview appearance look feel triggers accessibility"] =
            "personalizar material claro oscuro plano pearl obsidian mesa brillo terra tema porciones grosor anillo grueso medio fino iconos botones glifos mejor estimación efectos de sonido temático material digital físico silencio ninguno vista previa apariencia aspecto sensación disparadores accesibilidad",
        ["**Mesa** - rounded cream wedges on cracked terracotta; the armed slice lifts like a 3D card."] =
            "**Mesa**: sectores redondeados color crema sobre terracota agrietada; el sector preparado se eleva como una tarjeta en 3D.",
        ["**Sound Effects** - **Themed** (the default) plays whatever sound set matches the material above. **Digital** and **Physical** are fixed sets if you'd rather pin one, and **Silent** turns the sounds off. Picking a material switches the choice back to Themed."] =
            "**Efectos de sonido**: **Temático** (lo predeterminado) reproduce el conjunto de sonidos que corresponda al material de arriba. **Digital** y **Físico** son conjuntos fijos si prefieres fijar uno, y **Silencioso** desactiva los sonidos. Al elegir un material, la opción vuelve a Temático.",
        ["**Slice Thickness** - the radial thickness of the slice ring. **Thick** only reads well up to **8 slices**, so a 9th slice on either wheel switches the setting to **Medium** - and it switches back on its own once you're at 8 or fewer again."] =
            "**Grosor de los sectores**: el grosor radial del anillo. **Grueso** solo se lee bien hasta **8 sectores**, así que un noveno sector en cualquiera de las ruedas cambia el ajuste a **Medio**, y vuelve solo cuando haya 8 o menos.",
        ["**Button Icons** - the symbols in on-screen prompts: **Best Guess** (the default - follows the connected pad), **PlayStation** (✕ ○ □ △), or **Xbox** (A B X Y)."] =
            "**Iconos de botones**: los símbolos de las indicaciones en pantalla: **Mejor estimación** (predeterminado, sigue al mando conectado), **PlayStation** (✕ ○ □ △) o **Xbox** (A B X Y).",
        ["**Triggers** - which controller gestures summon a wheel. See [[triggers|Triggers]]."] =
            "**Gestos de invocación** (disparadores): qué gestos del mando invocan una rueda. Consulta [[triggers|Gestos de invocación]].",
        ["**D-Pad 🡄 🡆** - what left and right on the D-Pad do while a wheel is open. See [[volume-mixer|D-Pad controls]]."] =
            "**Cruceta 🡄 🡆**: qué hacen izquierda y derecha del D-Pad mientras una rueda está abierta. Consulta [[volume-mixer|Controles del D-Pad]].",
        ["**Show labels on** - which slices draw their text label. See [[show-labels|Show labels on]]."] =
            "**Mostrar etiquetas en**: qué sectores dibujan su etiqueta de texto. Consulta [[show-labels|Mostrar etiquetas en]].",
        ["**Accessibility** - activation, wheel sides, both-stick aiming, the hub, Reduce motion and narration - lives on **Settings ▸ Advanced**. See [[accessibility|Accessibility]]."] =
            "**Accesibilidad**: activación, lados de las ruedas, apuntado con ambos sticks, el centro, Reduce motion y narración; está en **Ajustes ▸ Avanzado**. Consulta [[accessibility|Accesibilidad]].",
        ["**Make your own material** - drop a theme package into Radiata's Materials folder and it joins the list. See [[custom-materials|Custom materials]]."] =
            "**Crea tu propio material**: deja un paquete de tema en la carpeta Materials de Radiata y se sumará a la lista. Consulta [[custom-materials|Materiales personalizados]].",
        ["A wheel holds up to **12** slices, but **6-8** is the sweet spot."] =
            "Una rueda admite hasta **12** sectores, pero **entre 6 y 8** es lo ideal.",

        // ── topic:workshop ──
        ["Workshop: make your own"] =
            "Taller: crea lo tuyo",
        ["workshop make build create author own custom package packages theme material game arcade javascript sample samples template download guide folder restart confirm share tutorial"] =
            "taller workshop crear construir autor propio personalizado paquete paquetes tema material juego arcade javascript muestra muestras ejemplo plantilla descargar guía carpeta reiniciar confirmar compartir tutorial",
        ["Radiata can load things you make yourself: **materials** that restyle the wheel, and **Arcade games** that play in the Arcade's round window. Each one is a **package** - a folder of plain files you can write in any text editor."] =
            "Radiata puede cargar cosas que haces tú: **materiales** que cambian el estilo de la rueda y **juegos Arcade** que se juegan en la ventana redonda del Arcade. Cada uno es un **paquete**: una carpeta de archivos de texto que puedes escribir con cualquier editor.",
        ["**Materials** are data only: colors, gradients, a font name, and optionally images and sounds. See [[custom-materials|Custom materials]]."] =
            "Los **materiales** son solo datos: colores, degradados, el nombre de una fuente y, opcionalmente, imágenes y sonidos. Consulta [[custom-materials|Materiales personalizados]].",
        ["**Arcade games** are a `game.json` and one JavaScript file, run in a sandbox. See [[custom-arcade-games|Custom Arcade games]]."] =
            "Los **juegos Arcade** son un `game.json` y un archivo JavaScript, que se ejecutan en un entorno aislado. Consulta [[custom-arcade-games|Juegos Arcade personalizados]].",
        ["The **Workshop** on the Radiata website is the full guide: step-by-step walkthroughs, every setting with its range, design advice, and **sample packages to download**. Start there: [getradiata.app/workshop](https://getradiata.app/workshop/)."] =
            "El **Taller** del sitio web de Radiata es la guía completa: recorridos paso a paso, cada ajuste con su rango, consejos de diseño y **paquetes de muestra para descargar**. Empieza por ahí: [getradiata.app/workshop](https://getradiata.app/workshop/es.html).",
        ["How making a package works"] =
            "Cómo se hace un paquete",
        ["**1.** Make a folder for your package - or unzip a sample - inside Radiata's packages folder. Paste the path into File Explorer's address bar to open it:"] =
            "**1.** Crea una carpeta para tu paquete (o descomprime una muestra) dentro de la carpeta de paquetes de Radiata. Pega la ruta en la barra de direcciones del Explorador de archivos para abrirla:",
        ["a material: `%APPDATA%\\Radiata\\Packages\\Materials\\<your theme>`"] =
            "un material: `%APPDATA%\\Radiata\\Packages\\Materials\\<tu tema>`",
        ["a game: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<your game>`"] =
            "un juego: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<tu juego>`",
        ["**2.** Write the manifest (`material.json` or `game.json`) and put every file it names directly inside that folder."] =
            "**2.** Escribe el manifiesto (`material.json` o `game.json`) y pon cada archivo que nombre directamente dentro de esa carpeta.",
        ["**3.** **Restart Radiata** - right-click the tray icon, choose **Exit**, then start it again. Packages are read once, at startup."] =
            "**3.** **Reinicia Radiata**: haz clic derecho en el icono de la bandeja, elige **Salir** y vuelve a iniciarlo. Los paquetes se leen una vez, al iniciar.",
        ["**4.** Accept the confirmation Radiata shows for a new or changed package."] =
            "**4.** Acepta la confirmación que Radiata muestra para un paquete nuevo o modificado.",
        ["**5.** Try it out: pick the material in **Settings ▸ Customize**, or open the game from the Arcade. Change something, then go back to step 3."] =
            "**5.** Pruébalo: elige el material en **Ajustes ▸ Personalizar** o abre el juego desde el Arcade. Cambia algo y vuelve al paso 3.",
        ["Making a package is a loop: every change goes back through a restart and the confirmation before you can try it."] =
            "Crear un paquete es un ciclo: cada cambio vuelve a pasar por un reinicio y la confirmación antes de que puedas probarlo.",
        ["Keep a copy of your work somewhere else too. Radiata reads a package where it sits, but nothing backs it up for you."] =
            "Guarda también una copia de tu trabajo en otro sitio. Radiata lee el paquete donde está, pero nada hace una copia de seguridad por ti.",
        ["**Only install packages from people you trust.** Radiata asks before it loads a package and asks again whenever one changes, but it can't tell you whether a package is any good."] =
            "**Instala solo paquetes de personas en las que confíes.** Radiata pregunta antes de cargar un paquete y vuelve a preguntar cada vez que cambia, pero no puede decirte si un paquete es bueno.",
        ["When a package doesn't show up, or you want to give one to a friend, see [[workshop-sharing|Testing and sharing packages]]."] =
            "Si un paquete no aparece, o quieres dárselo a alguien, consulta [[workshop-sharing|Probar y compartir paquetes]].",

        // ── topic:custom-materials ──
        ["Custom materials (build your own theme)"] =
            "Materiales personalizados (crea tu propio tema)",
        ["Beyond the eight built-in materials you can drop in **your own theme**. A theme is one folder holding a text file called `material.json` - colors, gradients, a system font name, and optionally images and sounds sitting beside it. Themes are **data only**: the format cannot express code, a network address, or a file outside the theme's own folder, so a theme can restyle the wheel and do nothing else."] =
            "Más allá de los ocho materiales integrados puedes añadir **tu propio tema**. Un tema es una carpeta con un archivo de texto llamado `material.json` (colores, degradados, el nombre de una fuente del sistema y, opcionalmente, imágenes y sonidos a su lado). Los temas son **solo datos**: el formato no puede expresar código, una dirección de red ni un archivo fuera de la carpeta del propio tema, así que un tema puede cambiar el estilo de la rueda y nada más.",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has two to download: **Starter**, the smallest complete theme with every line explained, and **Ember**, which uses every block below. The Workshop also covers color, contrast and texture advice this topic leaves out."] =
            "**Empieza por una muestra.** El [Taller](https://getradiata.app/workshop/es.html#samples) tiene dos para descargar: **Starter**, el tema completo más pequeño con cada línea explicada, y **Ember**, que usa todos los bloques de abajo. El Taller también incluye consejos sobre color, contraste y texturas que este tema deja fuera.",
        ["Where it goes"] =
            "Dónde va",
        ["One folder per theme under `%APPDATA%\\Radiata\\Packages\\Materials` - for example `…\\Materials\\Lava\\material.json`. Paste that path into Explorer's address bar; Radiata creates the folder on first run."] =
            "Una carpeta por tema dentro de `%APPDATA%\\Radiata\\Packages\\Materials`, por ejemplo `…\\Materials\\Lava\\material.json`. Pega esa ruta en la barra de direcciones del Explorador; Radiata crea la carpeta en el primer inicio.",
        ["That folder also holds a **README.txt** written by Radiata, carrying a copy-paste example of every field. It's the reference; this topic is the tour."] =
            "Esa carpeta también contiene un **README.txt** escrito por Radiata, con un ejemplo listo para copiar de cada campo. Es la referencia; este tema es el recorrido.",
        ["The smallest theme that works"] =
            "El tema más pequeño que funciona",
        ["A **format 1** manifest is a JSON object with the keys below. `format`, `id`, `name`, `dark`, and a `colors` object holding at least `resting` and `armed` are required; everything else is optional."] =
            "Un manifiesto de **formato 1** es un objeto JSON con las claves siguientes. `format`, `id`, `name`, `dark` y un objeto `colors` con al menos `resting` y `armed` son obligatorios; todo lo demás es opcional.",
        ["`\"format\": 1` - which manifest version you wrote. Radiata reads **1 to 3**; the richer blocks further down need the higher number."] =
            "`\"format\": 1`: qué versión del manifiesto has escrito. Radiata lee de **1 a 3**; los bloques más completos de más abajo necesitan el número más alto.",
        ["`\"id\": \"lava\"` - 2-31 characters of lowercase a-z, 0-9 and `-`, starting with a letter or digit. This is the theme's identity: it becomes the token `custom-lava` in your config, so changing it later makes a **different** theme."] =
            "`\"id\": \"lava\"`: de 2 a 31 caracteres en minúsculas a-z, 0-9 y `-`, empezando por letra o dígito. Es la identidad del tema: se convierte en el token `custom-lava` en tu configuración, así que cambiarlo después crea un tema **distinto**.",
        ["`\"name\": \"Lava\"` - up to 24 characters; the label on the Customize tile. `\"author\"` (up to 64) is optional."] =
            "`\"name\": \"Lava\"`: hasta 24 caracteres; la etiqueta de la tarjeta en Personalizar. `\"author\"` (hasta 64) es opcional.",
        ["`\"dark\": true` - whether the slices are dark. It flips labels and the hub to light ink and picks the dark fallbacks, so getting it wrong shows up as unreadable text rather than a wrong color."] =
            "`\"dark\": true`: si las porciones son oscuras. Cambia las etiquetas y el centro a tinta clara y elige las alternativas oscuras, así que equivocarse se nota como texto ilegible y no como un color incorrecto.",
        ["`\"colors\"` - `\"resting\"` and `\"armed\"` are required; `\"confirm\"` (defaults to the armed color), `\"label\"` and `\"outline\"` are optional. Each is `#RRGGBB` or `#AARRGGBB`, where the leading pair is alpha - `\"#40FFFFFF\"` is a 25%-opaque white outline."] =
            "`\"colors\"`: `\"resting\"` y `\"armed\"` son obligatorios; `\"confirm\"` (de forma predeterminada, el color de armado), `\"label\"` y `\"outline\"` son opcionales. Cada uno es `#RRGGBB` o `#AARRGGBB`, donde el primer par es el alfa: `\"#40FFFFFF\"` es un contorno blanco con un 25 % de opacidad.",
        ["`\"labelFont\": \"Cascadia Code\"` - optional, and it must be a font **already installed on the PC**. An unknown name is ignored rather than treated as an error. Font *files* inside a package are never supported, deliberately."] =
            "`\"labelFont\": \"Cascadia Code\"`: opcional, y debe ser una fuente **ya instalada en el PC**. Un nombre desconocido se ignora en lugar de tratarse como un error. Los *archivos* de fuente dentro de un paquete nunca son compatibles, a propósito.",
        ["`\"soundTheme\": \"physical\"` - which sounds the wheel makes on your theme. Name a sound set directly - `physical` (the default), `digital`, `kawaii`, `mesa`, `salvage`, `reactor` or `obsidian` - **or name a built-in material** and borrow whatever that one uses, so `\"pearl\"` gives you the digital set and `\"flat-dark\"` the physical set."] =
            "`\"soundTheme\": \"physical\"`: qué sonidos hace la rueda con tu tema. Nombra directamente un conjunto de sonidos (`physical`, el predeterminado, `digital`, `kawaii`, `mesa`, `salvage`, `reactor` u `obsidian`) **o nombra un material integrado** y toma prestado lo que ese use, de modo que `\"pearl\"` te da el conjunto digital y `\"flat-dark\"` el físico.",
        ["Naming the **material** is usually the better choice: your theme keeps sounding like the look you styled it after, even if that look's sounds are retuned in a later release. Naming a set pins it exactly."] =
            "Nombrar el **material** suele ser la mejor opción: tu tema sigue sonando como el aspecto en el que se inspiró, aunque los sonidos de ese aspecto se reajusten en una versión posterior. Nombrar un conjunto lo fija exactamente.",
        ["`material.json` may contain `//` comments and trailing commas, so you can leave yourself notes. A color can also be written short as `#RGB`."] =
            "`material.json` puede contener comentarios `//` y comas finales, así que puedes dejarte notas. Un color también se puede escribir abreviado como `#RGB`.",
        ["Richer looks - format 2"] =
            "Aspectos más ricos: formato 2",
        ["`\"fills\"` - a gradient per state (`resting` / `armed` / `confirm`) instead of a flat color. `\"type\"` is `solid`, `bowed` (the glassy Pearl/Obsidian ramp), or `linear` with an `\"angle\"`, plus a list of `\"stops\"` (each an `\"at\"` from 0 to 1 and a `\"color\"`)."] =
            "`\"fills\"`: un degradado por estado (`resting` / `armed` / `confirm`) en lugar de un color plano. `\"type\"` es `solid`, `bowed` (la rampa cristalina de Pearl/Obsidian) o `linear` con un `\"angle\"`, más una lista de `\"stops\"` (cada uno con un `\"at\"` de 0 a 1 y un `\"color\"`).",
        ["`\"hueWalk\"` - gives every slice its own hue around the ring, Kawaii-style, from `sat` / `light` / `armedSat` / `armedLight` (0-1) and `hueOffset`. It **overrides** the resting and armed fills."] =
            "`\"hueWalk\"`: da a cada porción su propio tono alrededor del anillo, al estilo Kawaii, a partir de `sat` / `light` / `armedSat` / `armedLight` (0-1) y `hueOffset`. **Anula** los rellenos de reposo y armado.",
        ["`\"outline\"` and `\"armedOutline\"` - `color`, `width`, and an optional `dash` pattern for the slice edge."] =
            "`\"outline\"` y `\"armedOutline\"`: `color`, `width` y un patrón `dash` opcional para el borde de la porción.",
        ["`\"armed\"` - how an armed slice moves: `liftPx` (up to 24), `northPx` (±12), `scale` (1.0-1.15). It's a state treatment rather than continuous motion, so it survives [[accessibility|Reduce motion]]."] =
            "`\"armed\"`: cómo se mueve una porción armada: `liftPx` (hasta 24), `northPx` (±12), `scale` (1.0-1.15). Es un tratamiento de estado y no un movimiento continuo, así que sobrevive a [[accessibility|Reducir el movimiento]].",
        ["`\"glyph\"` - how slice icons are treated: `edge` (`inner`, `outer` or `none`) with `edgeColor` / `edgeWidth` / `edgeShadow`; a `glow` whose `color` can be the literal `\"slice\"` to take each slice's own accent, with `strength` 0-1; plus `castShadow` and `armedWash`."] =
            "`\"glyph\"`: cómo se tratan los iconos de las porciones: `edge` (`inner`, `outer` o `none`) con `edgeColor` / `edgeWidth` / `edgeShadow`; un `glow` cuyo `color` puede ser el literal `\"slice\"` para tomar el acento propio de cada porción, con `strength` de 0 a 1; además de `castShadow` y `armedWash`.",
        ["`\"label\"` - `case` (`upper` for stamped all-caps labels) and `sizeMul` (0.8-1.3). `\"gapPx\"` (0-14) sets the gap between slices."] =
            "`\"label\"`: `case` (`upper` para etiquetas estampadas en mayúsculas) y `sizeMul` (0.8-1.3). `\"gapPx\"` (0-14) fija el hueco entre porciones.",
        ["`\"tile\"` - how the theme's swatch looks on the Customize tab: `edgeColor`, `sheen`, `lifted`, and an optional `texture`."] =
            "`\"tile\"`: cómo se ve la muestra del tema en la pestaña Personalizar: `edgeColor`, `sheen`, `lifted` y una `texture` opcional.",
        ["Images and sounds - format 3"] =
            "Imágenes y sonidos: formato 3",
        ["Set `\"format\": 3` to reference files that live **in the theme's own folder**, by bare file name - a path isn't expressible in the format."] =
            "Establece `\"format\": 3` para hacer referencia a archivos que estén **en la propia carpeta del tema**, por nombre de archivo sin ruta; una ruta no puede expresarse en el formato.",
        ["`\"textures\"` - `slice` and `hub` paint over the fill; `backdrop` draws behind the whole wheel. Each takes a `\"file\"` and an `\"opacity\"`, and slice/hub also take `\"tile\": true` to repeat the image at its natural size instead of stretching it. **PNG or JPG, up to 4 MB**; anything wider than 2048px is scaled down as it's decoded."] =
            "`\"textures\"`: `slice` y `hub` se pintan sobre el relleno; `backdrop` se dibuja detrás de toda la rueda. Cada uno toma un `\"file\"` y una `\"opacity\"`, y slice/hub también aceptan `\"tile\": true` para repetir la imagen a su tamaño natural en lugar de estirarla. **PNG o JPG, hasta 4 MB**; cualquier imagen de más de 2048 px de ancho se reduce al decodificarla.",
        ["`\"sounds\"` - one file per event: `armed`, `fired`, `enableWheels`, `disableWheels`. **WAV only, up to 1 MB and 3 seconds each**; events you leave out keep the paired sound theme's own sound."] =
            "`\"sounds\"`: un archivo por evento: `armed`, `fired`, `enableWheels`, `disableWheels`. **Solo WAV, hasta 1 MB y 3 segundos cada uno**; los eventos que omitas conservan el sonido del tema de sonido emparejado.",
        ["A theme that ships sounds is **badged** on its Customize tile and takes over the **Sound Effects** picker - the Digital and Physical overrides go inactive, while Themed and Silent stay live."] =
            "Un tema que incluye sonidos lleva una **insignia** en su tarjeta de Personalizar y toma el control del selector **Efectos de sonido**: las anulaciones Digital y Physical quedan inactivas, mientras que Themed y Silent siguen activas.",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**. Changing *any* file - not just the manifest - re-asks for your confirmation on the next start."] =
            "Límites de la carpeta: como máximo **32 archivos**, **4 MB por archivo**, **16 MB en total**. Cambiar *cualquier* archivo, no solo el manifiesto, vuelve a pedir tu confirmación en el siguiente inicio.",
        ["If your theme doesn't appear"] =
            "Si tu tema no aparece",
        ["**A package loads whole or not at all.** One bad value rejects the theme rather than half-applying it, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped material`."] =
            "**Un paquete se carga entero o no se carga.** Un solo valor incorrecto rechaza el tema en lugar de aplicarlo a medias, y el motivo se escribe en `%APPDATA%\\Radiata\\radiata-trace.log`: busca en ese archivo `[Packages] skipped material`.",
        ["The usual causes: a missing `dark` or `colors`, a color that isn't `#RRGGBB` / `#AARRGGBB`, an `id` with capitals or spaces, or a `format` number lower than the blocks you used."] =
            "Las causas habituales: falta `dark` o `colors`, un color que no es `#RRGGBB` / `#AARRGGBB`, un `id` con mayúsculas o espacios, o un número de `format` inferior a los bloques que has usado.",
        ["**A theme you had selected that stops loading** - package removed, or a change you declined - falls back to **Pearl**, quietly. Nothing else in your wheels changes."] =
            "**Un tema que tenías seleccionado y deja de cargarse** (paquete eliminado o un cambio que rechazaste) vuelve a **Perla** discretamente. Nada más cambia en tus ruedas.",
        ["Custom themes are never offered during first-run setup, and the theme folder isn't part of a settings backup - copy the folder itself to move a theme to another PC."] =
            "Los temas personalizados nunca se ofrecen durante la configuración inicial, y la carpeta de temas no forma parte de una copia de seguridad de ajustes: copia la propia carpeta para llevar un tema a otro PC.",

        // ── topic:custom-arcade-games ──
        ["The Arcade also plays **games you write yourself**. One is a folder holding a `game.json` and a single **JavaScript** file. Drop it into Radiata's Arcade Games folder and it plays in the same round window as the built-in games, with the same buttons and the same best-score tracking."] =
            "El Arcade también ejecuta **juegos que escribas tú**. Cada uno es una carpeta con un `game.json` y un único archivo **JavaScript**. Suéltala en la carpeta Arcade Games de Radiata y se juega en la misma ventana redonda que los juegos integrados, con los mismos botones y el mismo registro de mejor puntuación.",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has **Firefly** to download, a short but complete game with every line explained. The Workshop also walks through writing a game step by step."] =
            "**Empieza por una muestra.** En el [Taller](https://getradiata.app/workshop/es.html#samples) puedes descargar **Firefly**, un juego corto pero completo con cada línea explicada. El Taller también explica paso a paso cómo escribir un juego.",
        ["**A game package contains code.** Radiata asks you to confirm a package before it ever runs, and again whenever any file in it changes - but the sandbox below is a limit on what a game *can* do, not a judgement about whether it's worth running. **Only install games from a source you trust.**"] =
            "**Un paquete de juego contiene código.** Radiata te pide confirmar un paquete antes de ejecutarlo por primera vez, y de nuevo cada vez que cambia cualquier archivo suyo; pero el sandbox descrito más abajo es un límite a lo que un juego *puede* hacer, no un juicio sobre si merece ejecutarse. **Instala únicamente juegos de una fuente en la que confíes.**",
        ["One folder per game under `%APPDATA%\\Radiata\\Packages\\Arcade Games` - for example `…\\Arcade Games\\Firefly\\game.json` beside `firefly.js`. Radiata creates the folder on first run."] =
            "Una carpeta por juego dentro de `%APPDATA%\\Radiata\\Packages\\Arcade Games`, por ejemplo `…\\Arcade Games\\Firefly\\game.json` junto a `firefly.js`. Radiata crea la carpeta en el primer inicio.",
        ["That folder's **README.txt** is the full API reference, kept current by Radiata itself."] =
            "El **README.txt** de esa carpeta es la referencia completa de la API, y Radiata lo mantiene actualizado.",
        ["The manifest"] =
            "El manifiesto",
        ["`game.json` is a small JSON object. `format`, `id`, `title` and `entry` are required:"] =
            "`game.json` es un objeto JSON pequeño. `format`, `id`, `title` y `entry` son obligatorios:",
        ["`\"format\": 1` - the manifest version."] =
            "`\"format\": 1`: la versión del manifiesto.",
        ["`\"id\": \"firefly\"` - 1-32 characters of lowercase a-z, 0-9 and `-`. The game's identity, and the `pkg-<id>` token."] =
            "`\"id\": \"firefly\"`: de 1 a 32 caracteres en minúsculas a-z, 0-9 y `-`. La identidad del juego y el token `pkg-<id>`.",
        ["`\"title\": \"Firefly\"` - up to 24 characters, shown in the picker."] =
            "`\"title\": \"Firefly\"`: hasta 24 caracteres, mostrado en el selector.",
        ["`\"entry\": \"firefly.js\"` - the script, as a **bare file name** in the same folder (no paths), up to **256 KB**."] =
            "`\"entry\": \"firefly.js\"`: el script, como **nombre de archivo sin ruta** en la misma carpeta, de hasta **256 KB**.",
        ["`\"tint\": \"#5B8DEF\"` - optional: your cabinet's colour in the **Arcade Launcher**, as `#RGB` or `#RRGGBB`. Leave it out for the plain grey cabinet. Either way, your `title` is printed on the cabinet's nameplate."] =
            "`\"tint\": \"#5B8DEF\"`: opcional, el color de tu máquina en el **Lanzador Arcade**, como `#RGB` o `#RRGGBB`. Sin él, la máquina es gris. En ambos casos, tu `title` se imprime en la placa de la máquina.",
        ["`\"preview\": \"preview.png\"` - optional: the picture on your cabinet's screen until the game has been played, as a **bare PNG or JPG file name** in the same folder. Make it square, with the round playfield filling it."] =
            "`\"preview\": \"preview.png\"`: opcional, la imagen de la pantalla de tu máquina hasta que alguien juegue, como **nombre de archivo PNG o JPG a secas** en la misma carpeta. Hazla cuadrada, con el área de juego redonda llenándola.",
        ["`\"badge\": \"badge.png\"` - optional: an illustration for your cabinet's nameplate, drawn to the left of your title the way the built-in cabinets carry theirs. A **PNG with a transparent background**, in its own colours; it overflows the nameplate above and below."] =
            "`\"badge\": \"badge.png\"`: opcional, una ilustración para la placa de tu máquina, dibujada a la izquierda del título como la llevan las máquinas integradas. Un **PNG con fondo transparente**, con sus propios colores; sobresale de la placa por arriba y por abajo.",
        ["`\"glyph\": \"glyph.png\"` - optional: your game's icon on its wheel slices. A **PNG** whose transparency is the shape - the wheel colours it like every other slice icon, so draw it in one colour on a transparent background. Without one, drop-in games share a script icon."] =
            "`\"glyph\": \"glyph.png\"`: opcional, el icono de tu juego en sus porciones de la rueda. Un **PNG** cuya transparencia es la forma: la rueda lo colorea como cualquier otro icono de porción, así que dibújalo en un solo color sobre fondo transparente. Sin él, los juegos añadidos comparten un icono de script.",
        ["Once your game has been played, its cabinet shows the player's own last board instead - Radiata saves it as `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. That file is also the easiest way to make a preview: play your game, close it, and copy the file into your package as `preview.png`."] =
            "Cuando alguien ya ha jugado, la máquina muestra en su lugar el último tablero de ese jugador: Radiata lo guarda como `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. Ese archivo es también la forma más fácil de hacer una vista previa: juega tu juego, ciérralo y copia el archivo a tu paquete como `preview.png`.",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**."] =
            "Límites de la carpeta: como máximo **32 archivos**, **4 MB por archivo**, **16 MB en total**.",
        ["How a game runs"] =
            "Cómo se ejecuta un juego",
        ["Your script runs in a **locked-down interpreter inside a separate sandboxed process**: no files, no network, no clipboard, no other programs. Only the functions below exist at all. Memory is capped by Windows at 128 MB."] =
            "Tu script se ejecuta en un **intérprete restringido dentro de un proceso aislado aparte**: sin archivos, sin red, sin portapapeles, sin otros programas. Solo existen las funciones que se indican abajo. Windows limita la memoria a 128 MB.",
        ["Define **`tick(dt)`**, called for every fixed **1/120 second** step, and **`draw()`**, called once per screen frame. Issue drawing commands only from inside `draw()`."] =
            "Define **`tick(dt)`**, que se llama en cada paso fijo de **1/120 de segundo**, y **`draw()`**, que se llama una vez por fotograma de pantalla. Emite órdenes de dibujo solo desde dentro de `draw()`.",
        ["There's a hard time and instruction budget **per drawn frame**. A script that overruns is stopped and restarted (your saved data survives); one that keeps overrunning ends in a plain card you can back out of. It can slow itself down - it can't slow the PC down."] =
            "Hay un presupuesto estricto de tiempo e instrucciones **por fotograma dibujado**. Un script que lo sobrepasa se detiene y se reinicia (tus datos guardados sobreviven); uno que lo sobrepasa repetidamente termina en una tarjeta sencilla de la que puedes salir. Puede ralentizarse a sí mismo; no puede ralentizar el PC.",
        ["**Radiata owns {circle} and {triangle}** - closing the game and the help card - so your script never sees those two buttons."] =
            "**Radiata se reserva {circle} y {triangle}** (cerrar el juego y la tarjeta de ayuda), así que tu script nunca ve esos dos botones.",
        ["Drawing: the playfield is a disc"] =
            "Dibujo: el campo de juego es un disco",
        ["Everything is drawn in **polar coordinates**: `r` runs 0 at the center to 1 at the rim, angles are **degrees** with 0 at 12 o'clock, increasing clockwise. Radiata does the trigonometry and clips to the circle, so a game can't draw outside its window."] =
            "Todo se dibuja en **coordenadas polares**: `r` va de 0 en el centro a 1 en el borde, y los ángulos son **grados** con 0 a las 12 en punto, creciendo en sentido horario. Radiata hace la trigonometría y recorta al círculo, así que un juego no puede dibujar fuera de su ventana.",
        ["A game places everything by radius and angle: r runs from 0 at the center to 1 at the rim, and angles are degrees clockwise from 12 o'clock."] =
            "Un juego lo sitúa todo por radio y ángulo: r va de 0 en el centro a 1 en el borde, y los ángulos son grados en sentido horario desde las 12 en punto.",
        ["Colors are numbers in **`0xAARRGGBB`** form - alpha first, so `0xFFFF0000` is opaque red."] =
            "Los colores son números en forma **`0xAARRGGBB`**, con el alfa primero, de modo que `0xFFFF0000` es rojo opaco.",
        ["The commands, up to **1024 per frame**: `arc(r0, r1, a0, a1, color)` for a ring segment, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` for 3-16 points, and `text(r, a, size, \"str\", color)` for up to 64 characters."] =
            "Las órdenes, hasta **1024 por fotograma**: `arc(r0, r1, a0, a1, color)` para un segmento de anillo, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` para 3-16 puntos y `text(r, a, size, \"str\", color)` para hasta 64 caracteres.",
        ["Input"] =
            "Entrada",
        ["Read-only globals, refreshed every frame: **`stickX`** / **`stickY`** (-1 to 1, with y positive **downward**, matching the screen), **`crossDown`** / **`squareDown`** while held, **`crossPressed`** / **`squarePressed`** true for one frame per press, and **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, also one frame per press."] =
            "Variables globales de solo lectura, actualizadas en cada fotograma: **`stickX`** / **`stickY`** (de -1 a 1, con y positiva **hacia abajo**, como en la pantalla), **`crossDown`** / **`squareDown`** mientras se mantienen pulsados, **`crossPressed`** / **`squarePressed`** verdaderos durante un fotograma por pulsación, y **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, también un fotograma por pulsación.",
        ["Saving, randomness, and sound"] =
            "Guardado, aleatoriedad y sonido",
        ["Write the reserved key **`hiscore`** (a whole number as text) to publish a best score to the Arcade picker."] =
            "Escribe la clave reservada **`hiscore`** (un número entero como texto) para publicar una mejor puntuación en el selector de Arcade.",
        ["**`rand()`** returns 0-1 and is seeded per session, so a replay of the same inputs behaves the same way."] =
            "**`rand()`** devuelve un valor de 0 a 1 y se inicializa por sesión, de modo que una repetición de las mismas entradas se comporta igual.",
        ["**`cue(\"name\")`** plays one of nine built-in sounds: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Any other name is silent, and there's no way to ship your own audio."] =
            "**`cue(\"name\")`** reproduce uno de nueve sonidos integrados: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Cualquier otro nombre es silencio, y no hay forma de incluir audio propio.",
        ["Things that trip people up"] =
            "Errores frecuentes",
        ["Scripts run in **strict mode**, so a variable you forget to declare is an error. There's no `console`, `eval`, timer or `import` - to see a value while you work, draw it with `text()`."] =
            "Los scripts se ejecutan en **modo estricto**, así que una variable que olvides declarar es un error. No hay `console`, `eval`, temporizadores ni `import`: para ver un valor mientras trabajas, dibújalo con `text()`.",
        ["**Keep every drawing value in range**: radii 0-1, `dot` size up to 0.5, `line` width up to 0.1, `text` size up to 0.3, angles within ±3600. Radiata treats an out-of-range call as a broken game and restarts the script; after three restarts it shows a problem card. Clamp your numbers."] =
            "**Mantén cada valor de dibujo dentro de su rango**: radios de 0 a 1, tamaño de `dot` hasta 0.5, ancho de `line` hasta 0.1, tamaño de `text` hasta 0.3, ángulos dentro de ±3600. Radiata trata una llamada fuera de rango como un juego roto y reinicia el script; tras tres reinicios muestra una tarjeta de problema. Limita tus números.",
        ["JavaScript's bit operators (`|`, `&`, `<<`) produce **signed** numbers, and a negative color draws as nothing. Build colors with arithmetic, or finish the expression with `>>> 0`."] =
            "Los operadores de bits de JavaScript (`|`, `&`, `<<`) producen números **con signo**, y un color negativo no dibuja nada. Construye los colores con aritmética o termina la expresión con `>>> 0`.",
        ["Input is read **once per drawn frame**, but `tick` can run several times in that frame, and each run sees the same `crossPressed`. Make a press count once - the Firefly sample shows a way."] =
            "La entrada se lee **una vez por fotograma dibujado**, pero `tick` puede ejecutarse varias veces en ese fotograma, y cada ejecución ve el mismo `crossPressed`. Haz que cada pulsación cuente una sola vez: la muestra Firefly enseña una forma.",
        ["`text()` centers the string on its point. The last argument of `ring()` is a thin edge color, or `0` for none."] =
            "`text()` centra el texto en su punto. El último argumento de `ring()` es un color de borde fino, o `0` para ninguno.",
        ["Each drawn frame gets **2,000,000 statements and 8 ms** for all of its `tick` runs plus `draw`, at most **8** `cue` calls and **16** `kvSet` writes. `kvSet` throws an error when a key or value is too long or the 4 KB store is full."] =
            "Cada fotograma dibujado tiene **2.000.000 de instrucciones y 8 ms** para todas sus ejecuciones de `tick` más `draw`, como máximo **8** llamadas a `cue` y **16** escrituras de `kvSet`. `kvSet` lanza un error si una clave o un valor es demasiado largo o si el almacén de 4 KB está lleno.",
        ["If your game doesn't appear"] =
            "Si tu juego no aparece",
        ["**A package loads whole or not at all**, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped arcade game`."] =
            "**Un paquete se carga entero o no se carga**, y el motivo se escribe en `%APPDATA%\\Radiata\\radiata-trace.log`: busca en ese archivo `[Packages] skipped arcade game`.",
        ["The usual causes: an `entry` that isn't a plain `.js` file name sitting in the same folder, a script over 256 KB, an `id` with capitals or spaces, or a confirmation that was declined (it only re-asks once the package changes)."] =
            "Las causas habituales: un `entry` que no es un simple nombre de archivo `.js` situado en la misma carpeta, un script de más de 256 KB, un `id` con mayúsculas o espacios, o una confirmación rechazada (solo vuelve a preguntar cuando el paquete cambia).",
        ["A game that loaded but misbehaves shows its card in the window rather than an error - and a package you delete simply stops being offered."] =
            "Un juego que se cargó pero se comporta mal muestra su tarjeta en la ventana en lugar de un error, y un paquete que elimines simplemente deja de ofrecerse.",
        ["**Script errors** go to the same log: search it for `[Arcade] script` to see the error message."] =
            "Los **errores de script** van al mismo registro: busca en él `[Arcade] script` para ver el mensaje de error.",

        // ── topic:workshop-sharing ──
        ["Testing and sharing packages"] =
            "Probar y compartir paquetes",
        ["Testing"] =
            "Probar",
        ["Radiata reads packages **only at startup**: exit from the tray and start it again after every change."] =
            "Radiata lee los paquetes **solo al iniciar**: sal desde la bandeja y vuelve a iniciarlo después de cada cambio.",
        ["A change to **any** file in a package brings the confirmation back on the next start. If you decline it, the package stays off until its files change again."] =
            "Un cambio en **cualquier** archivo de un paquete hace que la confirmación vuelva en el siguiente inicio. Si la rechazas, el paquete queda desactivado hasta que sus archivos vuelvan a cambiar.",
        ["**Nothing happening?** Open `%APPDATA%\\Radiata\\radiata-trace.log` in a text editor and search for `[Packages] skipped`. Each line names the package folder and the exact problem - a missing field, a value out of range, a file that isn't there."] =
            "**¿No pasa nada?** Abre `%APPDATA%\\Radiata\\radiata-trace.log` en un editor de texto y busca `[Packages] skipped`. Cada línea nombra la carpeta del paquete y el problema exacto: un campo que falta, un valor fuera de rango, un archivo que no está.",
        ["**The most common mistake is one folder too many.** Unzipping often makes `Materials\\Lava\\Lava\\material.json`; Radiata looks for the manifest directly inside `Materials\\Lava`. Move the files up a level."] =
            "**El error más común es una carpeta de más.** Al descomprimir suele quedar `Materials\\Lava\\Lava\\material.json`; Radiata busca el manifiesto directamente dentro de `Materials\\Lava`. Sube los archivos un nivel.",
        ["Radiata looks for the manifest directly inside the package's own folder; the extra folder level an unzip often adds hides it."] =
            "Radiata busca el manifiesto directamente dentro de la carpeta del propio paquete; el nivel de carpeta de más que suele añadir la descompresión lo esconde.",
        ["A game that loads but misbehaves writes its script errors to the same log - search for `[Arcade] script`."] =
            "Un juego que carga pero se porta mal escribe sus errores de script en el mismo registro: busca `[Arcade] script`.",
        ["Sharing"] =
            "Compartir",
        ["Say what the package is and what it does, and only include images, sounds and code you have the right to share."] =
            "Explica qué es el paquete y qué hace, e incluye solo imágenes, sonidos y código que tengas derecho a compartir.",
        ["Radiata's license doesn't extend to your package: what you make in these formats is yours to license however you like."] =
            "La licencia de Radiata no se extiende a tu paquete: lo que crees en estos formatos puedes licenciarlo como quieras.",
        ["Moving to another PC? Settings backups don't include packages - copy the `Packages` folder across yourself."] =
            "¿Cambias de PC? Las copias de seguridad de ajustes no incluyen los paquetes: copia tú mismo la carpeta `Packages`.",

        // ── topic:triggers ──
        ["Triggers (summon chords)"] =
            "Gestos de invocación (combinaciones de invocación)",
        ["triggers chord builder hold tap add another trigger remove row fn bumper trigger touchpad swipe select start l3 r3 dpad combined summon invoke gesture customize"] =
            "triggers disparadores combinación creador mantener pulsar añadir otro disparador quitar fila fn bumper gatillo panel táctil deslizar select start l3 r3 dpad combinado invocar llamar gesto personalizar",
        ["**Settings ▸ Customize ▸ Triggers** is the chord builder: which controller gestures summon a wheel. Each row is one live chord - a **button** (Fn or L4/R4 / Bumper / Trigger / Touchpad) paired with how it's **combined** (Trigger, Home, L3/R3, Select/Start, D-Pad L/R, or a touchpad edge-swipe). The options adapt to the detected pad: **Fn** appears for a DualSense Edge, **L4/R4** for a pad with extra buttons on Bluetooth, and **Touchpad** only for pads that have one. Those dedicated buttons need no second button - they open a wheel on their own."] =
            "**Ajustes ▸ Personalizar ▸ Disparadores** es el constructor de combinaciones: qué gestos del mando invocan una rueda. Cada fila es una combinación activa: un **botón** (Fn o L4/R4 / bumper / gatillo / panel táctil) emparejado con la forma de **combinarlo** (gatillo, Home, L3/R3, Select/Start, cruceta izquierda/derecha o un deslizamiento desde el borde del panel táctil). Las opciones se adaptan al mando detectado: **Fn** aparece con un DualSense Edge, **L4/R4** con un mando con botones adicionales por Bluetooth, y **Panel táctil** solo con los mandos que tienen uno. Esos botones dedicados no necesitan un segundo botón: abren una rueda por sí solos.",
        ["**+ Add Another Trigger** appends a row; a row's **✕** removes it. Every row stays live at once - up to **three** - and the set is remembered **per controller type**, so an Edge and an Xbox pad each keep their own chords."] =
            "**+ Añadir otro gesto** añade una fila; la **✕** de una fila la elimina. Todas las filas están activas a la vez, hasta **tres**, y el conjunto se recuerda **por tipo de mando**, así que un Edge y un mando Xbox conservan cada uno sus propias combinaciones.",
        ["How each chord behaves (hold + tap, which wheel it opens, Hold vs Toggle) is in [[opening-a-wheel|Opening a wheel]]; the both-sides version of a chord toggles the wheels on and off, see [[wheel-open-extras|While a wheel is open]]."] =
            "Cómo se comporta cada combinación (mantener y tocar, qué rueda abre, Hold frente a Toggle) se explica en [[opening-a-wheel|Abrir una rueda]]; la versión con ambos lados de una combinación activa y desactiva las ruedas, consulta [[wheel-open-extras|Con una rueda abierta]].",

        // ── topic:accessibility ──
        ["Accessibility (Settings ▸ Advanced)"] =
            "Accesibilidad (Ajustes ▸ Avanzado)",
        ["accessibility wheels toggle on off swap left right both sticks either stick one stick ignores opposite stick sidedness aim drift always show hub battery reduce motion confetti fade parallax animation effects still narration speak speech spoken screen reader narrator voice volume system-wide windows narrator settings onboarding blind low vision"] =
            "accesibilidad ruedas alternar activar desactivar intercambiar izquierda derecha ambos sticks cualquier stick un solo stick ignora el stick opuesto lateralidad apuntar deriva mostrar siempre el centro batería reducir movimiento confeti fundido paralaje animación efectos quieto narración hablar voz habla lector de pantalla narrador voz volumen todo el sistema windows narrador ajustes configuración inicial ciego baja visión",
        ["The **Accessibility** section gathers six checkboxes, in **Settings ▸ Advanced**. The same set is offered during first-run setup from the **Accessibility…** button on the Look step:"] =
            "La sección **Accesibilidad** reúne seis casillas, en **Ajustes ▸ Avanzado**. El mismo conjunto se ofrece durante la configuración inicial desde el botón **Accesibilidad** del paso Look:",
        ["Two of them are **indented under the box that ticks them**: turning on **Wheels toggle on/off** also ticks **Swap left/right**, and turning on **Reduce motion** also ticks **Always show hub**, because each pair works best together. Both children stay yours to tick or clear on their own, and once you set one by hand it stops following its parent."] =
            "Dos de ellas están **sangradas bajo la casilla que las marca**: activar **Las ruedas se abren y cierran por pulsación** también marca **Intercambiar izquierda/derecha**, y activar **Reducir el movimiento** también marca **Mostrar siempre el centro**, porque cada pareja funciona mejor junta. Ambas hijas siguen siendo tuyas para marcar o desmarcar por separado, y una vez que ajustas una a mano deja de seguir a su padre.",
        ["**Wheels toggle on/off** and **Swap left/right** - whether an invoked wheel stays up until you dismiss it, and which wheel each hand opens (see [[opening-a-wheel|Opening a wheel]])."] =
            "**Las ruedas se abren y cierran por pulsación** y **Intercambiar izquierda/derecha**: si una rueda invocada permanece en pantalla hasta que la descartas, y qué rueda abre cada mano (consulta [[opening-a-wheel|Abrir una rueda]]).",
        ["**Narration** - speaks what you're doing aloud: which wheel opened, the slice you arm and its current state, hold-to-confirm progress, what a fire actually did, volume levels as you scrub, edit-mode moves, and Game Grid browsing. It works alongside a screen reader."] =
            "**Narración**: dice en voz alta lo que estás haciendo: qué rueda se ha abierto, el sector que preparas y su estado actual, el progreso de mantener para confirmar, lo que ha hecho realmente una ejecución, los niveles de volumen mientras los ajustas, los movimientos en modo edición y la navegación por la Cuadrícula de juegos. Funciona junto a un lector de pantalla.",
        ["**Narration covers the wheel and the Game Grid only** - the overlay surfaces a screen reader can't see. Settings, first-run setup and every other ordinary window are **Windows Narrator's** job, so run Narrator alongside Radiata if you want those read too. Ticking **Narration** offers a button to turn Narrator on; and if Narrator is running when you first set Radiata up, Narration starts on by itself."] =
            "**La narración cubre solo la rueda y la Cuadrícula de juegos**, las superficies superpuestas que un lector de pantalla no puede ver. De Ajustes, la configuración inicial y cualquier otra ventana corriente se encarga el **Narrador de Windows**, así que ejecuta el Narrador junto a Radiata si quieres que también se lean. Al marcar **Narración** se ofrece un botón para activar el Narrador; y si el Narrador está en marcha la primera vez que configuras Radiata, la narración se activa sola.",

        // ── topic:show-labels ──
        ["Show labels on (slice text)"] =
            "Mostrar etiquetas en (texto de los sectores)",
        ["show labels on slice labels label text names icons not logos standard icons all slices no slices slices i choose show label checkbox unlabelled artwork logo cover png"] =
            "mostrar etiquetas etiquetas de segmento texto nombres iconos no logotipos iconos estándar todos los segmentos ningún segmento los que yo elija casilla show label sin etiqueta imagen logotipo carátula png",
        ["**Show labels on** - which slices draw their text label on the wheel."] =
            "**Mostrar etiquetas en**: qué sectores dibujan su etiqueta de texto en la rueda.",
        ["**All Slices** - every slice is labelled, artwork ones included."] =
            "**Todos los sectores**: se etiquetan todos, incluidos los que tienen ilustración.",
        ["**No Slices** - no slice is labelled."] =
            "**Ningún sector**: no se etiqueta ninguno.",
        ["**Editing a wheel is exempt:** in edit mode and its Add picker, non-logo slices always show their labels whatever this is set to."] =
            "**La edición de una rueda queda exenta:** en el modo de edición y en su selector de añadir, los segmentos sin logotipo muestran siempre su etiqueta, sea cual sea este ajuste.",
        ["The setting lives in **Settings ▸ Customize**, directly under the **D-Pad 🡄 🡆** selector, and applies **live** - bring up a wheel to see it."] =
            "El ajuste está en **Ajustes ▸ Personalizar**, justo debajo del selector **Cruceta 🡄 🡆**, y se aplica **en vivo**: abre una rueda para verlo.",

        // ── topic:integrations ──
        ["Integrations (SteamGridDB, Discord & OBS)"] =
            "Integraciones (SteamGridDB, Discord y OBS)",
        ["integrations steamgriddb sgdb api key discord obs websocket port password configure cover art logos"] =
            "integraciones steamgriddb sgdb clave api discord obs websocket puerto contraseña configurar carátulas logotipos",
        ["**Settings ▸ Advanced ▸ Integrations** connects optional external services:"] =
            "**Ajustes ▸ Avanzado ▸ Integraciones** conecta servicios externos opcionales:",
        ["**Configure Discord Integration…** - sets the Discord credentials (Client ID/Secret) that Join/Leave Voice Channel slices use, the same wizard the slice editor offers. Stored encrypted (Windows DPAPI) and sent only to Discord."] =
            "**Configurar la integración con Discord…**: define las credenciales de Discord (ID de cliente y secreto) que usan los sectores de entrar/salir del canal de voz, el mismo asistente que ofrece el editor de sectores. Se almacenan cifradas (DPAPI de Windows) y solo se envían a Discord.",
        ["**SteamGridDB API key** - unlocks portrait cover art and logos for every storefront's games (the Game Grid's **Select**/**Start** cycling). Get a free key at [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). It's checked when you finish entering it, and entering your first key automatically fills in covers skipped while you had none."] =
            "**Clave de API de SteamGridDB**: desbloquea las portadas verticales y los logotipos de los juegos de todas las tiendas (el recorrido con **Seleccionar**/**Start** en la Cuadrícula de juegos). Consigue una clave gratuita en [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). Se comprueba en cuanto terminas de introducirla, y al introducir tu primera clave se rellenan automáticamente las portadas que se omitieron mientras no tenías ninguna.",
        ["**OBS Studio** - **Configure OBS Integration…** sets the WebSocket port and password, one connection shared by every OBS slice; see [[obs-studio|OBS slices]]. **Test** checks it against a running OBS. The password is stored encrypted (DPAPI)."] =
            "**OBS Studio**: **Configurar la integración con OBS…** define el puerto y la contraseña de WebSocket, una única conexión compartida por todos los sectores de OBS; consulta [[obs-studio|los sectores de OBS]]. **Probar** lo comprueba contra un OBS en marcha. La contraseña se almacena cifrada (DPAPI).",
        ["With [[playnite|Playnite]] and a SteamGridDB key both set up, a **Prefer Playnite covers** toggle appears in the **Game Grid** section. Without Playnite, an **Install Playnite…** button appears here instead."] =
            "Con [[playnite|Playnite]] y una clave de SteamGridDB configuradas, aparece un conmutador **Preferir las carátulas de Playnite** en la sección **Cuadrícula de juegos**. Sin Playnite, aparece aquí un botón **Instalar Playnite…** en su lugar.",

        // ── topic:playnite ──
        ["Playnite (optional library manager)"] =
            "Playnite (gestor de bibliotecas opcional)",
        ["playnite library manager games covers metadata optional install third party emulators"] =
            "playnite gestor de bibliotecas juegos carátulas metadatos opcional instalar terceros emuladores",
        ["**Playnite** is a free, open-source game-library manager for Windows ([playnite.link](https://playnite.link)) that gathers all your games - Steam, Epic, GOG, Xbox, emulators, standalone - with metadata and cover art."] =
            "**Playnite** es un gestor de bibliotecas de juegos gratuito y de código abierto para Windows ([playnite.link](https://playnite.link)) que reúne todos tus juegos (Steam, Epic, GOG, Xbox, emuladores, independientes) con metadatos y portadas.",
        ["Radiata works fully **without** it, scanning your storefronts directly. Playnite is an optional enhancement:"] =
            "Radiata funciona perfectamente **sin** él, analizando tus tiendas directamente. Playnite es una mejora opcional:",
        ["**More games found** - Radiata reads Playnite's library, so emulated and manually-added games a raw storefront scan misses can appear in the Game Grid."] =
            "**Más juegos encontrados**: Radiata lee la biblioteca de Playnite, así que los juegos emulados y los añadidos a mano que un análisis de tiendas no detecta pueden aparecer en Cuadrícula de juegos.",
        ["**Curated cover art** - Playnite's own covers become an art source, with a **Prefer Playnite covers** toggle (Advanced ▸ Game Grid) to favor them over SteamGridDB."] =
            "**Portadas seleccionadas**: las propias portadas de Playnite se convierten en una fuente de ilustraciones, con un conmutador **Preferir las portadas de Playnite** para darles prioridad sobre SteamGridDB.",
        ["Not installed? **Install Playnite…** in Advanced ▸ Integrations opens its download page. Set up your libraries in Playnite and Radiata picks them up automatically."] =
            "¿No lo tienes instalado? **Instalar Playnite…**, en Avanzado ▸ Integraciones, abre su página de descarga. Configura tus bibliotecas en Playnite y Radiata las recoge automáticamente.",

        // ── topic:system-actions ──
        ["System tools & Backup (Settings ▸ Advanced)"] =
            "Herramientas del sistema y copia de seguridad (Ajustes ▸ Avanzado)",
        ["run first run setup onboarding wizard reset starter slices customize recommended install repair drivers recover controller setup email log hid diagnostics quit exit backup restore reset wipe zip factory defaults undo clean install always show hub practice reduce motion check for updates automatic update skip version"] =
            "ejecutar configuración inicial asistente restablecer sectores iniciales personalizar recomendado instalar reparar controladores recuperar mando configuración correo registro hid diagnóstico salir copia de seguridad restaurar restablecer borrar zip valores de fábrica deshacer instalación limpia mostrar siempre centro práctica reducir movimiento buscar actualizaciones actualización automática omitir versión",
        ["**Settings ▸ Advanced ▸ System** holds the update controls, the **Start with Windows** toggle, the **Troubleshooting** dropdown, and **Quit Radiata**:"] =
            "**Ajustes ▸ Avanzado ▸ Sistema** contiene los controles de actualización, el conmutador **Iniciar con Windows**, el desplegable **Solución de problemas** y **Salir de Radiata**:",
        ["**Quit Radiata** - releases its virtual controller and requests removal of the HidHide blocks Radiata owns. Another tool's blocks remain; see [[hidhide-troubleshooting|HidHide troubleshooting]] if the pad stays hidden."] =
            "**Salir de Radiata**: libera su mando virtual y solicita la retirada de los bloqueos de HidHide que son suyos. Los bloqueos de otra herramienta permanecen; consulta [[hidhide-troubleshooting|Solución de problemas de HidHide]] si el mando sigue oculto.",
        ["The Troubleshooting dropdown"] =
            "La lista desplegable Solución de problemas…",
        ["**Run First-Run Setup…** - re-runs the setup wizard (controller check, drivers, look, cover art, starter wheels). Your customized wheels are never overwritten without asking."] =
            "**Ejecutar la configuración inicial…**: vuelve a ejecutar el asistente de configuración (comprobación del mando, controladores, aspecto, portadas, ruedas iniciales). Tus ruedas personalizadas nunca se sobrescriben sin preguntar.",
        ["**Install/Repair Drivers…** - installs or repairs the isolation drivers (ViGEmBus + HidHide). Fixes most isolation problems, and shows a result log."] =
            "**Instalar/reparar controladores…**: instala o repara los controladores de aislamiento (ViGEmBus y HidHide). Soluciona la mayoría de los problemas de aislamiento y muestra un registro de resultados.",
        ["**HID Diagnostics…** - a live view of the raw controller reports Radiata reads. Useful when support asks what your pad is actually sending."] =
            "**Diagnóstico HID…**: una vista en vivo de los informes en bruto del mando que lee Radiata. Útil cuando el soporte pregunta qué está enviando realmente tu mando.",
        ["**Controller Setup…** - re-runs the controller detection and mapping wizard on its own, without the rest of first-run setup."] =
            "**Configuración del mando…**: vuelve a ejecutar por separado el asistente de detección y asignación del mando, sin el resto de la configuración inicial.",
        ["**Email Log to Developer…** - saves a diagnostic ZIP to your Desktop and opens an addressed email. Review the ZIP before attaching and sending it: logs can include device identifiers, account names in file paths, and application or game names. Radiata does not automatically send the attachment."] =
            "**Enviar el registro al desarrollador…**: guarda un ZIP de diagnóstico en tu escritorio y abre un correo ya dirigido. Revisa el ZIP antes de adjuntarlo y enviarlo: los registros pueden incluir identificadores de dispositivo, nombres de cuenta en las rutas de archivo y nombres de aplicaciones o juegos. Radiata no envía el adjunto automáticamente.",
        ["**Back Up Settings…** and **Restore Settings…** sit lower in the same dropdown, and the **resets** below them - all described under Backup & reset."] =
            "**Hacer copia de seguridad de los ajustes…** y **Restaurar ajustes…** están más abajo en el mismo desplegable, junto con los **restablecimientos** que hay debajo; todo ello se describe en Copia de seguridad y restablecimiento.",
        ["Backup & reset"] =
            "Copia de seguridad y restablecimiento",
        ["**Back Up Settings…** - saves everything that makes Radiata yours (wheels, colors, settings, and your Game Grid cover/logo picks) to a .zip in `Documents\\Radiata Backups`."] =
            "**Hacer copia de seguridad de los ajustes…**: guarda todo lo que hace tuya a Radiata (ruedas, colores, ajustes y tus elecciones de portadas y logotipos de la Cuadrícula de juegos) en un .zip dentro de `Documents\\Radiata Backups`.",
        ["**Reset All Settings…** - factory defaults for wheels, colors and settings; first-run setup runs again on the next launch. Cached art survives."] =
            "**Restablecer todos los ajustes…**: valores de fábrica para ruedas, colores y ajustes; la configuración inicial se ejecuta de nuevo en el siguiente inicio. Las ilustraciones en caché se conservan.",
        ["**Uninstall Radiata…** - removes the startup entry, the HidHide registration, and Radiata's own files, with OFF-by-default opt-ins for the shared drivers and your settings. Anything else in Radiata's folder is left alone."] =
            "**Desinstalar Radiata…**: elimina la entrada de inicio, el registro en HidHide y los archivos propios de Radiata, con casillas DESACTIVADAS de forma predeterminada para los controladores compartidos y tus ajustes. Cualquier otra cosa en la carpeta de Radiata se deja intacta.",
        ["Updates"] =
            "Actualizaciones",
        ["**Check for Updates** - asks `getradiata.app/update` for a newer version right now."] =
            "**Buscar actualizaciones**: pregunta ahora mismo a `getradiata.app/update` si hay una versión más reciente.",
        ["**Automatic** - the checkbox beside the button: when on, the same check runs at startup and once a day. Found updates announce themselves with an on-screen notice (click it to open the update) and a line in this tab; a version you choose to **skip** stops announcing itself, though the line here still shows it."] =
            "**Automático**: la casilla junto al botón. Cuando está activada, la misma comprobación se ejecuta al iniciar y una vez al día. Las actualizaciones encontradas se anuncian con un aviso en pantalla (haz clic en él para abrir la actualización) y con una línea en esta pestaña; una versión que elijas **omitir** deja de anunciarse, aunque la línea de aquí siga mostrándola.",
        ["Resets can't be undone - **back up first**."] =
            "Los restablecimientos no se pueden deshacer: **haz antes una copia de seguridad**.",
        ["The game-art buttons live in their own **Game Grid** section - see [[game-grid-options|Game Grid options]]."] =
            "Los botones de ilustraciones de juegos están en su propia sección **Cuadrícula de juegos**; consulta [[game-grid-options|Opciones de la Cuadrícula de juegos]].",

        // ── topic:game-grid-options ──
        ["Game Grid options (Settings ▸ Advanced)"] =
            "Opciones de la Cuadrícula de juegos (Ajustes ▸ Avanzado)",
        ["game grid options clear game art cache retry missing game art reset hidden games unhide covers redownload"] =
            "opciones cuadrícula juegos borrar caché imágenes reintentar carátulas restablecer juegos ocultos mostrar de nuevo descargar",
        ["**Settings ▸ Advanced ▸ Game Grid** collects the grid's housekeeping buttons:"] =
            "**Ajustes ▸ Avanzado ▸ Cuadrícula de juegos** reúne los botones de mantenimiento de la cuadrícula:",
        ["**Retry Missing Game Art** - re-attempts only the covers and logos that came up empty, keeping everything already downloaded and every cover you picked by hand (see [[cover-art|Cover art & logos]])."] =
            "**Reintentar las imágenes que faltan**: reintenta únicamente las portadas y logotipos que quedaron vacíos, conservando todo lo ya descargado y todas las portadas que hayas elegido a mano (consulta [[cover-art|Portadas y logotipos]]).",
        ["**Clear Game Art Cache** - deletes ALL cached covers, so everything re-downloads. **Images you dropped onto a slice are kept.** Try **Retry Missing Game Art** first if you only want to fill blanks."] =
            "**Vaciar la caché de carátulas**: elimina TODAS las carátulas en caché, de modo que todo se vuelve a descargar. **Las imágenes que soltaste sobre un sector se conservan.** Prueba antes **Reintentar carátulas que faltan** si solo quieres rellenar los huecos.",
        ["**Reset Hidden Games** - brings back every game and storefront you hid with **hold {square}** (see [[storefronts|Hiding a storefront]])."] =
            "**Restablecer juegos ocultos**: recupera todos los juegos y tiendas que hayas ocultado con **mantener {square}** (consulta [[storefronts|Ocultar una tienda]]).",
        ["**Prefer Playnite covers** - shown when [[playnite|Playnite]] and a SteamGridDB key are both set up: favors Playnite's own cover art."] =
            "**Preferir las portadas de Playnite**: aparece cuando [[playnite|Playnite]] y una clave de SteamGridDB están configurados; da prioridad a las portadas propias de Playnite.",

        // ── topic:storefronts ──
        ["Hiding a storefront"] =
            "Ocultar una tienda",
        ["storefront steam epic gog xbox battle.net amazon itch ubisoft ea hide exclude opt out include reset hidden games launcher card"] =
            "tienda storefront steam epic gog xbox battle.net amazon itch ubisoft ea ocultar excluir descartar incluir restablecer juegos ocultos lanzador tarjeta",
        ["A whole storefront can be hidden from the [[game-grid|Game Grid]], the same way a single game can."] =
            "Se puede ocultar una tienda entera de la [[game-grid|Cuadrícula de juegos]], igual que se oculta un solo juego.",
        ["**Filter to the store with L1 / R1**, then **hold {square}** on its **Open <store>** card. A notice asks **\"Hide <store> and all its games in Radiata?\"** - **{cross}** hides it, **{circle}** cancels."] =
            "**Filtra a la tienda con L1 / R1** y luego **mantén {square}** sobre su tarjeta **Abrir <tienda>**. Un aviso pregunta **\"¿Ocultar <tienda> y todos sus juegos en Radiata?\"**: **{cross}** la oculta, **{circle}** cancela.",
        ["Hiding a store **hides its games** in the Game Grid, the [[edit-mode|Add picker]], and the starter wheels the first-run wizard suggests."] =
            "Ocultar una tienda **oculta sus juegos** en la Cuadrícula de juegos, en el [[edit-mode|selector de añadir]] y en las ruedas iniciales que sugiere el asistente de primer arranque.",
        ["Bring it back with **Settings ▸ Advanced ▸ Game Grid ▸** [[game-grid-options|Reset Hidden Games]], which un-hides storefronts as well as games."] =
            "Recupérala con **Ajustes ▸ Avanzado ▸ Cuadrícula de juegos ▸** [[game-grid-options|Restablecer juegos ocultos]], que vuelve a mostrar tanto las tiendas como los juegos.",
        ["Newly installed storefronts appear **automatically** the next time Radiata scans."] =
            "Las tiendas recién instaladas aparecen **automáticamente** la próxima vez que Radiata analiza el sistema.",
        ["Only a store with its own **Open <store>** card can be hidden this way. A [[playnite|Playnite]] game you added by hand, or one from a third-party Playnite plugin, carries no storefront card. Hide those games individually."] =
            "Solo se puede ocultar así una tienda que tenga su propia tarjeta **Abrir <tienda>**. Un juego de [[playnite|Playnite]] que hayas añadido a mano, o uno procedente de un complemento de Playnite de terceros, no lleva tarjeta de tienda. Esos juegos se ocultan individualmente.",

        // ── topic:controller-not-detected ──
        ["Controller not detected"] =
            "El mando no se detecta",
        ["controller dead not detected blind hidhide lockout whitelist recover reset bluetooth radio frozen stuck wedge"] =
            "mando muerto no detectado ciego hidhide bloqueo lista blanca recuperar reiniciar bluetooth radio congelado atascado bloqueado",
        ["**Replug / re-pair first.** Bluetooth stacks occasionally wedge, and power-cycling the pad fixes most one-offs."] =
            "**Vuelve a conectarlo o a emparejarlo primero.** Las pilas Bluetooth se atascan de vez en cuando, y apagar y encender el mando resuelve la mayoría de los casos puntuales.",
        ["**Bluetooth pad connected but frozen** (Windows still lists it, input never moves)? That's a Windows Bluetooth wedge that power-cycling the pad **won't** fix - toggle the PC's **Bluetooth off and on** instead. Radiata shows a \"toggle Bluetooth\" notification when it spots this."] =
            "**¿Mando Bluetooth conectado pero congelado** (Windows lo sigue listando, pero la entrada no se mueve)? Es un atasco del Bluetooth de Windows que apagar y encender el mando **no** resuelve: apaga y enciende el **Bluetooth** del PC. Radiata muestra una notificación de \"conmuta el Bluetooth\" cuando lo detecta.",
        ["**HidHide lockout:** if the cloak hides the pad while Radiata isn't on its allow-list, Radiata goes blind. That means no input, and the Current Controller readout shows nothing even though Windows sees the pad. Radiata catches this at startup and offers a one-click fix via a clickable on-screen notice; repair can help with registration, but it does not cure every access problem. See [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "**Bloqueo de HidHide:** si el ocultamiento esconde el mando mientras Radiata no está en su lista de permitidos, Radiata se queda ciego. Eso significa que no hay entrada, y la lectura de Mando actual no muestra nada aunque Windows sí vea el mando. Radiata lo detecta al iniciarse y ofrece una solución de un clic mediante un aviso en pantalla en el que se puede hacer clic; la reparación puede ayudar con el registro, pero no cura todos los problemas de acceso. Consulta [[hidhide-troubleshooting|Solución de problemas de HidHide]].",
        ["**Peer controller tools** can remap or hide the controller and create additional outputs. Installed software alone does not establish a conflict; see [[controller-tool-conflicts|reWASD, DS4Windows & other tools]]."] =
            "**Otras herramientas de mando** pueden remapear u ocultar el mando y crear salidas adicionales. El mero hecho de tener software instalado no demuestra que haya un conflicto; consulta [[controller-tool-conflicts|reWASD, DS4Windows y otras herramientas]].",

        // ── topic:controller-conflict-checklist ──
        ["Controller conflict checklist"] =
            "Lista de comprobación de conflictos de mando",
        ["double input duplicate bleed through wrong pad player slot remote play diagnostic log"] =
            "entrada doble duplicada filtrado mando equivocado ranura de jugador juego remoto registro de diagnóstico",
        ["Use this sequence when one press moves a menu twice, the game reacts beneath a wheel, the wrong controller responds, or input disappears. Change one thing at a time and test between changes."] =
            "Usa esta secuencia cuando una sola pulsación mueva un menú dos veces, el juego reaccione por debajo de una rueda, responda el mando equivocado o desaparezca la entrada. Cambia una cosa cada vez y prueba entre cambio y cambio.",
        ["**1. Save and exit the game.** Controller mode, Passthru Mode, remapper output changes and reconnects can all replace the device a game is using. Relaunch after the test setup is stable."] =
            "**1. Guarda y sal del juego.** El modo de mando, el Modo directo, los cambios de salida de un remapeador y las reconexiones pueden sustituir el dispositivo que está usando un juego. Relanza el juego cuando la configuración de prueba sea estable.",
        ["**2. Establish a simple baseline.** Temporarily use one controller and one connection (USB, Bluetooth or receiver). Disable other tools' remapping and automatic profile switching; close their tray agents and HidHide configuration windows. For a local test, end unused streaming sessions that create virtual pads."] =
            "**2. Establece una base sencilla.** Usa temporalmente un solo mando y una sola conexión (USB, Bluetooth o receptor). Desactiva el remapeo y el cambio automático de perfiles de otras herramientas; cierra sus agentes de bandeja y las ventanas de configuración de HidHide. Para una prueba local, finaliza las sesiones de streaming sin usar que creen mandos virtuales.",
        ["**3. Check Radiata first.** Read **Settings ▸ Advanced ▸ Current Controller** and hover its tray icon for isolation status. No controller points to detection or hiding; a working wheel with game input underneath points to isolation or another input path."] =
            "**3. Comprueba Radiata primero.** Lee **Ajustes ▸ Avanzado ▸ Mando actual** y pasa el ratón por su icono de bandeja para ver el estado del aislamiento. Que no haya ningún mando apunta a la detección o al ocultamiento; una rueda que funciona con entrada del juego por debajo apunta al aislamiento o a otra ruta de entrada.",
        ["**4. Reconnect in a controlled order.** With the game and competing readers closed, start Radiata, connect the controller, and wait for its status to settle. Start Steam or the launcher afterwards, then the game. A reader that opened the device before cloaking may retain access until it closes or the device reconnects."] =
            "**4. Vuelve a conectar en un orden controlado.** Con el juego y los demás lectores cerrados, inicia Radiata, conecta el mando y espera a que su estado se asiente. Inicia después Steam o el lanzador, y luego el juego. Un lector que abrió el dispositivo antes del ocultamiento puede conservar el acceso hasta que se cierre o el dispositivo se reconecte.",
        ["Start Radiata before anything else that reads the controller, connect the pad and let its status settle, then open Steam or your launcher, and the game last."] =
            "Inicia Radiata antes que cualquier otra cosa que lea el mando, conecta el mando y deja que su estado se asiente; luego abre Steam o tu lanzador, y el juego al final.",
        ["**5. Test in a safe game menu.** Opening a wheel should stop Radiata's virtual pad from driving the game until the wheel closes. Windows' `joy.cpl` can help identify extra controllers, but one entry there does not prove isolation in every game or input API."] =
            "**5. Prueba en un menú de juego seguro.** Al abrir una rueda, el mando virtual de Radiata debería dejar de accionar el juego hasta que la rueda se cierre. El `joy.cpl` de Windows puede ayudar a identificar mandos de más, pero una entrada ahí no demuestra que haya aislamiento en todos los juegos ni en todas las API de entrada.",
        ["**6. Restore other tools one at a time.** The combination that brings back the symptom is useful evidence. Reading a controller and successfully isolating it are separate things: Radiata does not provide a general physical-device-to-XInput-slot picker."] =
            "**6. Restablece las demás herramientas de una en una.** La combinación que hace volver el síntoma es una prueba útil. Leer un mando y aislarlo correctamente son cosas distintas: Radiata no ofrece un selector general de dispositivo físico a ranura XInput.",
        ["For comparison, enable [[passthru-mode|Passthru Mode]] before launching the game. Other tools can still hide or remap the device, and wheel input reaching the game is expected in this mode. Disabling the wheels alone does not release capture."] =
            "Para comparar, activa el [[passthru-mode|Modo directo]] antes de lanzar el juego. Otras herramientas pueden seguir ocultando o remapeando el dispositivo, y en este modo es normal que la entrada de la rueda llegue al juego. Desactivar las ruedas por sí solo no libera la captura.",
        ["What to include in a support report"] =
            "Qué incluir en un informe de soporte",
        ["Record Windows and Radiata versions, controller model and transport, controller mode, tray status, driver versions, other tools and active profiles, startup order, and the first step that changes the result. Include whether input works with Radiata closed and in Passthru Mode."] =
            "Anota las versiones de Windows y de Radiata, el modelo y la conexión del mando, el modo de mando, el estado de la bandeja, las versiones de los controladores, las demás herramientas y los perfiles activos, el orden de inicio y el primer paso que cambia el resultado. Indica también si la entrada funciona con Radiata cerrado y en Modo directo.",
        ["Use **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to Developer…** to prepare a diagnostic ZIP. Review it before attaching it; it may contain device identifiers and personal paths. Include the actual error text from driver setup or HID Diagnostics."] =
            "Usa **Ajustes ▸ Avanzado ▸ Solución de problemas ▸ Enviar el registro al desarrollador…** para preparar un ZIP de diagnóstico. Revísalo antes de adjuntarlo; puede contener identificadores de dispositivo y rutas personales. Incluye el texto exacto del error de la instalación de controladores o del Diagnóstico HID.",

        // ── topic:controller-tool-conflicts ──
        ["reWASD, DS4Windows & other controller tools"] =
            "reWASD, DS4Windows y otras herramientas de mando",
        ["rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapper conflict virtual controller duplicate xbox slot autodetect"] =
            "rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapeador conflicto mando virtual duplicado ranura xbox detección automática",
        ["Two tools can read one pad and produce two outputs, or one can hide the pad from the other. Start with one active remapper for that controller. Coexistence depends on versions, hiding rules, transport and game."] =
            "Dos herramientas pueden leer un mismo mando y producir dos salidas, o una puede ocultar el mando a la otra. Empieza con un único remapeador activo para ese mando. La convivencia depende de las versiones, las reglas de ocultamiento, la conexión y el juego.",
        ["reWASD"] =
            "reWASD",
        ["Turn **Remap OFF** for the affected device or group and pause **Autodetect** for the test. Closing the main window does not necessarily stop mappings. Check the tray agent and confirm its virtual output is gone before retesting. See [reWASD Tray Agent](https://help.rewasd.com/interface/tray-agent.html)."] =
            "Desactiva **Remap** para el dispositivo o grupo afectado y pausa **Autodetect** durante la prueba. Cerrar la ventana principal no detiene necesariamente las asignaciones. Comprueba el agente de bandeja y confirma que su salida virtual ha desaparecido antes de volver a probar. Consulta [el agente de bandeja de reWASD](https://help.rewasd.com/interface/tray-agent.html).",
        ["reWASD has its own virtual-device and hiding settings. Repairing ViGEmBus or adding Radiata to HidHide cannot fix every reWASD visibility rule. Record which devices remain visible; consult [reWASD troubleshooting](https://help.rewasd.com/faq/troubleshooting.html) for its own errors."] =
            "reWASD tiene sus propios ajustes de dispositivo virtual y de ocultamiento. Reparar ViGEmBus o añadir Radiata a HidHide no puede corregir todas las reglas de visibilidad de reWASD. Anota qué dispositivos siguen siendo visibles; consulta [la solución de problemas de reWASD](https://help.rewasd.com/faq/troubleshooting.html) para sus propios errores.",
        ["DS4Windows, DSX and InputMapper"] =
            "DS4Windows, DSX e InputMapper",
        ["Stop controller output and fully exit the tool, including its tray process, for the baseline. Check automatic startup and profiles if it returns. Another virtual Xbox or DualShock controller can cause duplicate actions or change which device the game selects."] =
            "Detén la salida del mando y sal por completo de la herramienta, incluido su proceso de bandeja, para establecer la base. Comprueba el inicio automático y los perfiles si vuelve a aparecer. Otro mando virtual Xbox o DualShock puede provocar acciones duplicadas o cambiar qué dispositivo elige el juego.",
        ["If the setup uses HidHide, physical-device blocks can remain after the remapper exits. Check Radiata's access using [[hidhide-troubleshooting|HidHide troubleshooting]]. Radiata does not take ownership of another tool's existing blocks, so quitting Radiata does not clear them."] =
            "Si la configuración usa HidHide, los bloqueos del dispositivo físico pueden permanecer después de salir del remapeador. Comprueba el acceso de Radiata siguiendo la [[hidhide-troubleshooting|Solución de problemas de HidHide]]. Radiata no se apropia de los bloqueos existentes de otra herramienta, así que salir de Radiata no los elimina.",
        ["Other sources of input"] =
            "Otras fuentes de entrada",
        ["**JoyToKey, AntiMicroX, macros and hardware profiles** can emit keyboard or mouse events alongside gamepad input. Neutralizing Radiata's virtual pad does not neutralize those events. Disable the mapping or controller [[turbo-mode|Turbo mode]] for the test."] =
            "**JoyToKey, AntiMicroX, las macros y los perfiles de hardware** pueden emitir eventos de teclado o ratón junto con la entrada del mando. Neutralizar el mando virtual de Radiata no neutraliza esos eventos. Desactiva la asignación o el [[turbo-mode|modo Turbo]] del mando durante la prueba.",
        ["**x360ce, vJoy-based tools, streaming clients and vendor utilities** can add controllers or translation layers. Check Steam Remote Play, Sunshine/Moonlight, Parsec and controller software when relevant. End only unused sessions; a remote player's virtual controller may be their only input."] =
            "**x360ce, las herramientas basadas en vJoy, los clientes de streaming y las utilidades de fabricante** pueden añadir mandos o capas de traducción. Comprueba Steam Remote Play, Sunshine/Moonlight, Parsec y el software del mando cuando corresponda. Finaliza solo las sesiones sin usar; el mando virtual de un jugador remoto puede ser su única entrada.",
        ["**Old HidGuardian or ScpToolkit installations** can leave filtering or replacement drivers behind. Use the original project's removal guidance or support; do not delete arbitrary HID devices, Bluetooth drivers or registry filters. HidHide and HidGuardian are different components."] =
            "**Las instalaciones antiguas de HidGuardian o ScpToolkit** pueden dejar atrás controladores de filtrado o de sustitución. Usa las instrucciones de desinstalación o el soporte del proyecto original; no elimines dispositivos HID, controladores de Bluetooth ni filtros del registro al azar. HidHide y HidGuardian son componentes distintos.",
        ["**Wrong player or no spare Xbox slot?** XInput exposes four slots, which may include virtual pads. Temporarily stop unused virtual outputs and reconnect in the intended order. If Radiata reports uncertainty about its own output, wait for reconnection or quit and reopen Radiata; reinstalling drivers is not the first fix."] =
            "**¿Jugador equivocado o sin ranura Xbox libre?** XInput expone cuatro ranuras, que pueden incluir mandos virtuales. Detén temporalmente las salidas virtuales que no uses y vuelve a conectar en el orden previsto. Si Radiata informa de incertidumbre sobre su propia salida, espera a que se reconecte o sal y vuelve a abrir Radiata; reinstalar los controladores no es la primera solución.",
        ["If another remapper is essential, test it with Radiata in [[passthru-mode|Passthru Mode]] first. That avoids a second Radiata stand-in, but it does not promise isolation or preservation of native controller features through the other tool."] =
            "Si otro remapeador te resulta imprescindible, pruébalo primero con Radiata en [[passthru-mode|Modo directo]]. Eso evita un segundo suplente de Radiata, pero no garantiza el aislamiento ni la conservación de las funciones nativas del mando a través de la otra herramienta.",

        // ── topic:hidhide-troubleshooting ──
        ["HidHide: contention, lockouts & shared settings"] =
            "HidHide: contención, bloqueos y ajustes compartidos",
        ["hidhide contention busy access denied configuration client cli lockout whitelist allow list inverse cloak path moved renamed usb bluetooth shared hidden controller recovery"] =
            "hidhide contención ocupado acceso denegado cliente de configuración cli bloqueo lista blanca lista de permitidos ocultamiento inverso ruta movido renombrado usb bluetooth compartido mando oculto recuperación",
        ["Busy or access-failed status"] =
            "Estado de ocupado o de fallo de acceso",
        ["The **HidHide Configuration Client** holds the driver's exclusive configuration connection while it's open. A running or stuck **HidHideCLI** can also contend with Radiata. Close those tools completely, then allow about **15 seconds** for Radiata's retry before trying **Recover Controller**. Contention does not mean the driver needs reinstalling."] =
            "El **cliente de configuración de HidHide** mantiene la conexión de configuración exclusiva del controlador mientras está abierto. Un **HidHideCLI** en marcha o atascado también puede competir con Radiata. Cierra esas herramientas por completo y deja unos **15 segundos** para el reintento de Radiata antes de probar **Recuperar mando**. Que haya contención no significa que haya que reinstalar el controlador.",
        ["Windows sees the controller, but Radiata does not"] =
            "Windows ve el mando, pero Radiata no",
        ["Close the game and quit Radiata before inspecting HidHide. In normal mode, the **Applications** list grants access to hidden controllers. Verify the exact `Radiata.exe` you launch is listed - installed, portable, renamed and moved copies all have different paths. Radiata normally registers itself; **Install/Repair Drivers…** can repair registration, subject to its reported result."] =
            "Cierra el juego y sal de Radiata antes de inspeccionar HidHide. En modo normal, la lista **Applications** concede acceso a los mandos ocultos. Comprueba que esté listado exactamente el `Radiata.exe` que lanzas: las copias instaladas, portátiles, renombradas y movidas tienen rutas distintas. Radiata se registra normalmente por sí solo; **Instalar/reparar controladores…** puede reparar el registro, según el resultado que indique.",
        ["Check the selected physical device on **Devices**. USB and Bluetooth can have separate entries. Do not hide the virtual stand-in the game needs. Use [Nefarius's setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) to identify the device, and close the client before restarting Radiata."] =
            "Comprueba el dispositivo físico seleccionado en **Devices**. USB y Bluetooth pueden tener entradas separadas. No ocultes el suplente virtual que el juego necesita. Usa la [guía de configuración de Nefarius](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) para identificar el dispositivo, y cierra el cliente antes de reiniciar Radiata.",
        ["**Inverse application cloak reverses the list's meaning.** Radiata preserves this shared setting and declines capture when it is enabled. Record the configuration and coordinate with the tool that needs it before choosing normal mode for Radiata; changing it affects other applications too."] =
            "**El ocultamiento inverso de aplicaciones invierte el significado de la lista.** Radiata conserva este ajuste compartido y renuncia a la captura cuando está activado. Anota la configuración y coordínate con la herramienta que lo necesita antes de elegir el modo normal para Radiata; cambiarlo afecta también a otras aplicaciones.",
        ["The game still receives input"] =
            "El juego sigue recibiendo entrada",
        ["An application allowed through HidHide can still read the physical device. Review entries deliberately; do not add the game, Steam, or every executable as a general fix for double input."] =
            "Una aplicación permitida a través de HidHide todavía puede leer el dispositivo físico. Revisa las entradas con criterio; no añadas el juego, Steam ni todos los ejecutables como solución general para la entrada duplicada.",
        ["After a fresh install, reconnect the controller or restart Windows if requested, so the filter can attach. Restart readers that opened the device before cloaking. Configuration readback alone does not verify what a running game receives."] =
            "Tras una instalación nueva, vuelve a conectar el mando o reinicia Windows si se te pide, para que el filtro pueda engancharse. Reinicia los lectores que abrieran el dispositivo antes del ocultamiento. Releer la configuración por sí solo no verifica qué recibe un juego en marcha.",
        ["HidHide has limitations, including some Raw Input readers and Xbox/XInput configurations. If leakage survives a clean startup, record the game and transport rather than assuming a successful hide operation guarantees exclusive input. See the [HidHide FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/)."] =
            "HidHide tiene limitaciones, entre ellas algunos lectores de Raw Input y ciertas configuraciones de Xbox/XInput. Si la filtración sobrevive a un arranque limpio, anota el juego y la conexión en lugar de dar por hecho que una operación de ocultamiento correcta garantiza entrada exclusiva. Consulta las [preguntas frecuentes de HidHide](https://docs.nefarius.at/projects/HidHide/FAQ/).",
        ["The controller stays hidden after exit"] =
            "El mando sigue oculto después de salir",
        ["Radiata adopts existing hidden-device entries matching the controller model it manages, including entries from another connection method, so they can be released on exit. This can also release another tool's matching entries; you should never have two tools manage hiding for the same controller. Uninstall clears all hidden-device entries unless another Radiata copy is running. A failed release must be recovered before removal can finish."] =
            "Radiata adopta las entradas de dispositivo oculto existentes que coinciden con el modelo de mando que gestiona, incluidas las de otro método de conexión, para poder liberarlas al salir. Eso puede liberar también las entradas coincidentes de otra herramienta; nunca deberías tener dos herramientas gestionando el ocultamiento del mismo mando. La desinstalación borra todas las entradas de dispositivo oculto salvo que haya otra copia de Radiata en marcha. Una liberación fallida debe recuperarse antes de que la eliminación pueda terminar.",
        ["Keep keyboard and mouse access available while changing controller visibility. Record existing settings first, change only the identified controller or application entry, and close the configuration client before retesting."] =
            "Mantén disponible el acceso con teclado y ratón mientras cambias la visibilidad del mando. Anota primero los ajustes existentes, cambia solo la entrada del mando o de la aplicación identificada, y cierra el cliente de configuración antes de volver a probar.",

        // ── topic:driver-conflicts ──
        ["HP OMEN & driver version conflicts"] =
            "Conflictos con HP OMEN y versiones de controladores",
        ["hp omen gaming hub fusion vigem vigembus foreign fork driver version mismatch 10.x 1.22.0 1.5.230 oculus virtual desktop repair install bus device manager restart"] =
            "hp omen gaming hub fusion vigem vigembus ajeno bifurcación versión de controlador discrepancia 10.x 1.22.0 1.5.230 oculus virtual desktop reparar instalar bus administrador de dispositivos reiniciar",
        ["**ViGEmBus creates the virtual controller; HidHide controls access to the physical one.** A working driver of one kind does not establish that the other works. Check the driver result log and Radiata's isolation status before repeating an installer."] =
            "**ViGEmBus crea el mando virtual; HidHide controla el acceso al físico.** Que funcione un controlador de un tipo no demuestra que funcione el otro. Comprueba el registro de resultados de los controladores y el estado de aislamiento de Radiata antes de repetir un instalador.",
        ["HP OMEN Gaming Hub / OMEN Fusion"] =
            "HP OMEN Gaming Hub / OMEN Fusion",
        ["Some HP OMEN systems have a vendor-modified ViGEmBus, and Radiata can connect to that bus instead of the correct one. A reported **10.x** version can be HP's old fork, not a newer compatible Nefarius driver."] =
            "Algunos sistemas HP OMEN tienen un ViGEmBus modificado por el fabricante, y Radiata puede conectarse a ese bus en lugar de al correcto. Una versión **10.x** indicada puede ser la bifurcación antigua de HP, no un controlador de Nefarius más reciente y compatible.",
        ["Radiata names detected foreign buses and skips installing over them. Its driver removal also leaves another program's bus in place. Repeated **Install/Repair Drivers…** attempts will not switch an HP-owned bus to Nefarius's."] =
            "Radiata nombra los buses ajenos que detecta y evita instalar por encima de ellos. Su eliminación de controladores también deja intacto el bus de otro programa. Repetir los intentos de **Instalar/reparar controladores…** no cambiará un bus propiedad de HP por el de Nefarius.",
        ["Follow [Nefarius's HP OMEN guidance](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) and its linked [HP issue and switching instructions](https://github.com/nefarius/ViGEmBus/issues/99), or contact HP. Disabling the vendor bus can break dependent OMEN features; Radiata does not perform the device or registry changes for you."] =
            "Sigue [las indicaciones de Nefarius sobre HP OMEN](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) y las [instrucciones enlazadas sobre el problema con HP y cómo cambiarlo](https://github.com/nefarius/ViGEmBus/issues/99), o ponte en contacto con HP. Desactivar el bus del fabricante puede estropear funciones de OMEN que dependan de él; Radiata no realiza por ti los cambios de dispositivo ni de registro.",
        ["A foreign bus that refuses a virtual DualShock 4 may make Radiata fall back to an **Xbox 360 stand-in for that session**. Unexpected Xbox prompts can therefore be a driver clue rather than a changed glyph setting. This fallback is not a compatibility guarantee."] =
            "Un bus ajeno que rechace un DualShock 4 virtual puede hacer que Radiata recurra a un **suplente Xbox 360 durante esa sesión**. Por eso, unos iconos de Xbox inesperados pueden ser una pista sobre los controladores y no un cambio en el ajuste de glifos. Esta alternativa no es ninguna garantía de compatibilidad.",
        ["Versions and duplicate buses"] =
            "Versiones y buses duplicados",
        ["This Radiata build bundles **ViGEmBus 1.22.0** and **HidHide 1.5.230**. ViGEmBus is retired; 1.22.0 is its final official release. Installer, application, client-library and driver versions are different numbers - they are not supposed to match each other."] =
            "Esta versión de Radiata incluye **ViGEmBus 1.22.0** y **HidHide 1.5.230**. ViGEmBus está retirado; 1.22.0 es su última versión oficial. Las versiones del instalador, de la aplicación, de la biblioteca cliente y del controlador son números distintos: no tienen por qué coincidir entre sí.",
        ["Open **Device Manager ▸ View ▸ Devices by connection** and inspect virtual gamepad bus entries. Record each bus's name, provider, driver version and device status. Multiple buses, unexpected providers, or a version different from the one Radiata bundles all warrant investigation; a higher number alone does not prove compatibility."] =
            "Abre **Administrador de dispositivos ▸ Ver ▸ Dispositivos por conexión** e inspecciona las entradas de bus de gamepad virtual. Anota el nombre, el proveedor, la versión del controlador y el estado de cada bus. Varios buses, proveedores inesperados o una versión distinta de la que incluye Radiata merecen investigación; un número más alto por sí solo no demuestra compatibilidad.",
        ["**Oculus and Virtual Desktop** setups can also supply their own buses. Several virtual gamepads beneath one bus are different from several competing bus drivers. Identify the owning program before changing either."] =
            "Las instalaciones de **Oculus y Virtual Desktop** también pueden aportar sus propios buses. Varios gamepads virtuales bajo un mismo bus no es lo mismo que varios controladores de bus compitiendo. Identifica el programa propietario antes de cambiar ninguno de los dos.",
        ["For an ordinary missing or older bundled driver, use **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers…**, review its result, and complete any requested restart. Close HidHide tools first. If repair fails, keep the error text and driver versions for support."] =
            "Para un controlador incluido que falte o esté anticuado sin más, usa **Ajustes ▸ Avanzado ▸ Solución de problemas ▸ Instalar/reparar controladores…**, revisa su resultado y completa cualquier reinicio que se pida. Cierra antes las herramientas de HidHide. Si la reparación falla, guarda el texto del error y las versiones de los controladores para el soporte.",
        ["Driver removal affects every application using that shared component. Use the [official ViGEmBus install/remove guide](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) for a confirmed conflict. Its full purge is advanced recovery, not a first step for double input. Do not force-delete unrelated drivers or use unofficial download sites."] =
            "Eliminar un controlador afecta a todas las aplicaciones que usan ese componente compartido. Usa la [guía oficial de instalación y eliminación de ViGEmBus](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) cuando haya un conflicto confirmado. Su purga completa es una recuperación avanzada, no un primer paso ante la entrada duplicada. No fuerces la eliminación de controladores ajenos al problema ni uses sitios de descarga no oficiales.",

        // ── topic:overlay-not-visible ──
        ["Wheel not visible over a game"] =
            "La rueda no se ve sobre el juego",
        ["overlay fullscreen borderless windowed exclusive primary display monitor uac"] =
            "superposición pantalla completa sin bordes en ventana exclusiva pantalla principal monitor uac",
        ["**Run games Borderless Windowed**, not exclusive fullscreen. Exclusive fullscreen bypasses the compositor Radiata draws through. The setting is in most games' display options, and the performance difference on Windows 10/11 is negligible."] =
            "**Ejecuta los juegos en modo ventana sin bordes**, no en pantalla completa exclusiva. La pantalla completa exclusiva se salta el compositor a través del cual dibuja Radiata. El ajuste está en las opciones de pantalla de casi todos los juegos, y la diferencia de rendimiento en Windows 10 y 11 es insignificante.",
        ["Radiata draws on the **primary display only** - on a multi-monitor rig, make your gaming display the Windows primary (Settings ▸ System ▸ Display)."] =
            "Radiata dibuja **solo en la pantalla principal**: en un equipo con varios monitores, haz que tu pantalla de juego sea la principal de Windows (Configuración ▸ Sistema ▸ Pantalla).",
        ["Windows-secured screens (UAC prompts, the lock screen) can never be drawn over. That's a Windows limitation, and nothing can work around it."] =
            "Sobre las pantallas protegidas de Windows (los avisos de UAC, la pantalla de bloqueo) nunca se puede dibujar. Es una limitación de Windows, y no hay forma de sortearla.",

        // ── topic:steam-conflicts ──
        ["Steam Input & Steam quirks"] =
            "Steam Input y peculiaridades de Steam",
        ["steam playstation controller support big picture guide magnifier chord double input unlock controller"] =
            "steam soporte mando playstation big picture guide lupa combinación entrada doble desbloquear mando",
        ["Steam Input can translate a controller into gamepad, keyboard or mouse input. Another mapping layer can change prompts and bindings or create duplicate actions. Test per game before changing global settings."] =
            "Steam Input puede traducir un mando en entrada de gamepad, de teclado o de ratón. Otra capa de asignación puede cambiar los iconos y las asignaciones o crear acciones duplicadas. Haz la prueba juego por juego antes de cambiar los ajustes globales.",
        ["**Wrong buttons or duplicate actions?** With the game closed, open its **Steam Library ▸ Properties ▸ Controller** and try **Disable Steam Input** in the per-game override. Relaunch and compare; restore the previous setting if the game or remote setup needs Steam Input. Global options are under **Steam ▸ Settings ▸ Controller**, with names that vary by Steam version."] =
            "**¿Botones equivocados o acciones duplicadas?** Con el juego cerrado, abre su **Biblioteca de Steam ▸ Propiedades ▸ Mando** y prueba **Desactivar Steam Input** en la anulación por juego. Relanza y compara; restablece el ajuste anterior si el juego o la configuración remota necesitan Steam Input. Las opciones globales están en **Steam ▸ Configuración ▸ Mando**, con nombres que varían según la versión de Steam.",
        ["**No input with Steam Input disabled?** The game may not support Radiata's virtual DualShock controller. For a Sony pad, try [[controller-mode|Xbox Mode]] before launching, or restore Steam Input. That's a game compatibility choice, not necessarily a driver failure."] =
            "**¿Sin entrada con Steam Input desactivado?** Puede que el juego no admita el mando DualShock virtual de Radiata. Con un mando de Sony, prueba el [[controller-mode|modo Xbox]] antes de lanzarlo, o restablece Steam Input. Es una cuestión de compatibilidad del juego, no necesariamente un fallo de los controladores.",
        ["**Double input despite a successful cloak?** Steam may have opened the physical controller before Radiata hid it. Save and close Steam games before fully exiting Steam, then start Radiata and let capture settle before reopening Steam. Reconnecting the pad can also release stale handles. Follow any controller-unblock notice; closing Steam's window alone may leave it running."] =
            "**¿Entrada duplicada pese a un ocultamiento correcto?** Puede que Steam abriera el mando físico antes de que Radiata lo ocultara. Guarda y cierra los juegos de Steam antes de salir del todo de Steam, luego inicia Radiata y deja que la captura se asiente antes de volver a abrir Steam. Reconectar el mando también puede liberar identificadores obsoletos. Sigue cualquier aviso de desbloqueo del mando; cerrar la ventana de Steam por sí solo puede dejarlo en marcha.",
        ["**Desktop keys or mouse movement?** Check Steam's **Desktop Layout** and **Guide Button Chord** layout as well as the game's layout. These can emit input outside the game. See [[controller-conflict-checklist|Controller conflict checklist]] for a controlled comparison."] =
            "**¿Teclas del escritorio o movimiento del ratón?** Revisa la **Distribución de escritorio** y la distribución de **Combinación con el botón Guía** de Steam, además de la distribución del juego. Pueden emitir entrada fuera del juego. Consulta la [[controller-conflict-checklist|Lista de comprobación de conflictos de mando]] para hacer una comparación controlada.",
        ["**Steam Remote Play may rely on Steam Input.** Keep a local fallback before changing its input path. Valve explains the translation layer in [Steam Input gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)."] =
            "**Steam Remote Play puede depender de Steam Input.** Ten preparada una alternativa local antes de cambiar su ruta de entrada. Valve explica la capa de traducción en [la emulación de gamepad de Steam Input](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices).",
        ["**Windows Magnifier opens by itself?** That's Steam's *Guide Button Chord* layout (Guide + face button), not Radiata - and powering a pad off by holding the PS button can leave that layout latched. One clean Guide press-and-release clears it; disable it under Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout."] =
            "**¿La Lupa de Windows se abre sola?** Es la disposición *Guide Button Chord* de Steam (Guide + un botón frontal), no Radiata, y apagar un mando manteniendo el botón PS puede dejar esa disposición enganchada. Una pulsación limpia del botón Guide la libera; desactívala en Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout.",

        // ── topic:turbo-mode ──
        ["Controller Turbo / rapid-fire mode"] =
            "Modo Turbo o de disparo rápido del mando",
        ["turbo rapid fire auto repeat macro cycling flicker wheel closes dismiss premature bounce double input jitter"] =
            "turbo disparo rápido repetición automática macro parpadeo la rueda se cierra prematuro rebote entrada doble inestabilidad",
        ["Many third-party pads have a hardware **Turbo** / rapid-fire mode that auto-repeats a held button. An accidental button combo can toggle it on in the controller firmware, and that can look exactly like a bug:"] =
            "Muchos mandos de terceros tienen un modo **Turbo** o de disparo rápido por hardware que repite automáticamente un botón mantenido. Una combinación de botones accidental puede activarlo en el firmware del mando, y eso puede parecer exactamente un fallo:",
        ["The wheel **flickers open and shut**, or **dismisses on its own** right after opening."] =
            "La rueda **parpadea abriéndose y cerrándose** o **se cierra sola** justo después de abrirse.",
        ["The Game Grid's **filters cycle rapidly**, or the selection jumps on its own."] =
            "Los **filtros de la Cuadrícula de juegos cambian a gran velocidad** o la selección se mueve sola.",
        ["Slices **fire the instant a wheel opens**, or a hold-to-confirm slice never settles."] =
            "Los sectores **se ejecutan en el instante en que se abre la rueda**, o un sector con confirmación por mantenimiento nunca llega a completarse.",
        ["Turn Turbo off on the controller itself - usually a button combo (often Home/Guide + a face or shoulder button, or a dedicated Turbo button), frequently with its own LED. Check your pad's manual for the exact combo."] =
            "Desactiva el Turbo en el propio mando, normalmente con una combinación de botones (a menudo Home/Guide más un botón frontal o superior, o un botón Turbo específico), con frecuencia acompañada de su propio LED. Consulta el manual de tu mando para la combinación exacta.",
        ["If it persists with Turbo confirmed off, it's something else - see [[opening-a-wheel|Opening a wheel]] and [[picking-an-action|Aiming & firing]]."] =
            "Si persiste con el Turbo confirmado como desactivado, se trata de otra cosa: consulta [[opening-a-wheel|Abrir una rueda]] y [[picking-an-action|Apuntar y ejecutar]].",

        // ── topic:common-issues ──
        ["Other common issues"] =
            "Otros problemas frecuentes",
        ["hdr unavailable rdp remote play streaming config json double input"] =
            "hdr no disponible rdp remote play retransmisión config json entrada doble",
        ["**HDR shows `Unavailable`** - the display state can't be read in that context, such as in Remote Play or streaming."] =
            "**HDR muestra `Unavailable`**: el estado de la pantalla no se puede leer en ese contexto, por ejemplo en Remote Play o en una retransmisión.",
        ["**Editing `config.json` by hand** (`%APPDATA%\\Radiata`) - supported. The app hot-reloads its own writes reliably, but outside edits are occasionally missed, so restart Radiata after manual edits."] =
            "**Editar `config.json` a mano** (`%APPDATA%\\Radiata`): está admitido. La aplicación recarga en caliente sus propias escrituras de forma fiable, pero las ediciones externas se pasan por alto de vez en cuando, así que reinicia Radiata después de editar a mano.",

        // ── figure ──
        ["Isolated"] =
            "Con aislamiento",
        ["Your controller"] =
            "Tu mando",
        ["Virtual pad"] =
            "Mando virtual",
        ["The game"] =
            "El juego",
        ["cloaked"] =
            "oculto",
        ["Passthru Mode, or no drivers"] =
            "Modo directo, o sin controladores",
        ["no virtual pad"] =
            "sin mando virtual",
        ["{cross} picks the slice up"] =
            "{cross} recoge el segmento",
        ["Aim to the target slot"] =
            "Apunta a la posición de destino",
        ["{cross} drops it — the ring reflows"] =
            "{cross} lo suelta y el anillo se reorganiza",
        ["Its chord"] =
            "Su combinación",
        ["Slices on it — this wheel opens."] =
            "Con segmentos: esta rueda se abre.",
        ["No slices — this wheel draws nothing, so its chord reaches the game."] =
            "Sin segmentos: esta rueda no dibuja nada, así que su combinación llega al juego.",
        ["Modifiers — held around it"] =
            "Modificadores: se mantienen a su alrededor",
        ["The key that's pressed"] =
            "La tecla que se pulsa",
        ["Steam or your launcher"] =
            "Steam o tu lanzador",
        ["Game and other tools closed"] =
            "Juego y otras herramientas cerrados",
        ["Wait for its status to settle"] =
            "Espera a que su estado se asiente",
        ["Make the folder"] =
            "Crea la carpeta",
        ["Write the manifest"] =
            "Escribe el manifiesto",
        ["Restart Radiata"] =
            "Reinicia Radiata",
        ["Accept the confirmation"] =
            "Acepta la confirmación",
        ["Try it out"] =
            "Pruébalo",
        ["Change something"] =
            "Cambia algo",
        ["clockwise"] =
            "sentido horario",
        ["Radiata finds it"] =
            "Radiata lo encuentra",
        ["One folder too many"] =
            "Una carpeta de más",
        ["Move the files up a level"] =
            "Sube los archivos un nivel",
        ["custom material theme package material.json drop in author make build own skin palette colors colours gradient fill hue walk outline glyph glow texture png jpg sound wav appdata packages folder soundtheme sound set kawaii mesa salvage reactor obsidian digital physical format token consent confirm restart trace log rejected not showing workshop sample starter ember comments drag drop zip install uninstall remove delete recycle bin right-click"] =
            "personalizado material tema paquete material.json soltar añadir autor crear construir propio piel paleta colores degradado relleno tono recorrido contorno glifo brillo textura png jpg sonido wav appdata packages carpeta soundtheme conjunto de sonidos kawaii mesa salvage reactor obsidian digital físico formato token consentimiento confirmar reiniciar traza registro rechazado no aparece taller muestra starter ember comentarios arrastrar soltar zip instalar desinstalar quitar eliminar papelera de reciclaje clic derecho",
        ["**Restart Radiata after adding a theme to that folder by hand, or changing one.** Packages are scanned once, at startup, on purpose."] =
            "**Reinicia Radiata después de añadir un tema a esa carpeta a mano, o de modificar uno.** Los paquetes se examinan una sola vez, al iniciar, a propósito.",
        ["**Or drag and drop it.** Drop the theme's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. What you dropped stays where it was."] =
            "**O arrástralo y suéltalo.** Suelta la carpeta del tema, o un ZIP con ella, en cualquier parte de la ventana de **Ajustes**, o sobre `Radiata.exe` o un acceso directo a él. Radiata lo copia a su sitio y te pide confirmar, sin reiniciar. Lo que soltaste se queda donde estaba.",
        ["Dropping a theme you already have asks before replacing it; the old copy goes to the **Recycle Bin**. A changed version takes effect after a restart."] =
            "Si sueltas un tema que ya tienes, se te pregunta antes de reemplazarlo; la copia anterior va a la **Papelera de reciclaje**. Una versión modificada surte efecto tras reiniciar.",
        ["**To remove a theme,** right-click its tile in **Settings ▸ Customize** and choose **Uninstall theme**. Its folder goes to the Recycle Bin; if it was your material, the wheel switches to **Pearl**. Restore the folder from the Recycle Bin and restart Radiata to get it back."] =
            "**Para quitar un tema,** haz clic derecho en su mosaico en **Ajustes ▸ Personalizar** y elige **Desinstalar tema**. Su carpeta va a la Papelera de reciclaje; si era tu material, la rueda cambia a **Perla**. Restaura la carpeta desde la Papelera de reciclaje y reinicia Radiata para recuperarlo.",
        ["custom arcade game package game.json javascript js script write author make build own sandbox jint helper polar disc draw tick input kv hiscore cue sound appdata packages folder restart consent confirm trace log rejected not showing workshop sample firefly strict console error budget tint colour color cabinet preview screenshot nameplate launcher drag drop zip install"] =
            "juego arcade personalizado paquete game.json javascript js script escribir autor crear construir propio sandbox jint auxiliar polar disco dibujar tick entrada kv hiscore señal sonido appdata packages carpeta reiniciar consentimiento confirmar traza registro rechazado no aparece taller muestra firefly estricto consola error presupuesto tinte color recreativa vista previa captura placa de nombre lanzador arrastrar soltar zip instalar",
        ["**Or drag and drop it.** Drop the game's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. A game you already have asks before replacing it, and a changed version takes effect after a restart."] =
            "**O arrástralo y suéltalo.** Suelta la carpeta del juego, o un ZIP con ella, en cualquier parte de la ventana de **Ajustes**, o sobre `Radiata.exe` o un acceso directo a él. Radiata lo copia a su sitio y te pide confirmar, sin reiniciar. Si ya tienes ese juego, se te pregunta antes de reemplazarlo, y una versión modificada surte efecto tras reiniciar.",
        ["**Restart Radiata** after adding a game to that folder by hand, or changing one, then accept the confirmation. It shows up in the Arcade picker beside the built-in games, and as a choice when you add an **Arcade** slice. In your config it's the token `pkg-<id>`."] =
            "**Reinicia Radiata** después de añadir un juego a esa carpeta a mano, o de modificar uno, y acepta la confirmación. Aparece en el selector de Arcade junto a los juegos integrados, y como opción al añadir un sector de **Arcade**. En tu configuración es el token `pkg-<id>`.",
        ["workshop test testing debug debugging share sharing zip unzip send friend install license trace log skipped error not showing missing folder nested backup move another pc drag drop"] =
            "taller probar prueba depurar depuración compartir zip descomprimir enviar amigo instalar licencia traza registro omitido error no aparece falta carpeta anidada copia de seguridad mover otro pc arrastrar soltar",
        ["To share a package, zip the files **inside** its folder (not the folder itself) and name the zip after the package. The person installing it drops the zip onto Radiata's **Settings** window and accepts the confirmation - or uses **Extract All** into their own Materials or Arcade Games folder and restarts Radiata."] =
            "Para compartir un paquete, comprime los archivos que hay **dentro** de su carpeta (no la carpeta en sí) y ponle al ZIP el nombre del paquete. Quien lo instale suelta el ZIP en la ventana de **Ajustes** de Radiata y acepta la confirmación, o usa **Extraer todo** en su propia carpeta de Materials o Arcade Games y reinicia Radiata.",
        ["Radiata is a feature-rich, controller-based radial menu utility for a Windows gaming PC. Launch a game, join the Discord call, swap to headphones, start your stream, all from a controller."] =
            "Radiata es una utilidad de menú radial para mando, con muchas funciones, para un PC gaming con Windows. Lanza un juego, únete a la llamada de Discord, cambia a los auriculares, inicia tu emisión, todo desde un mando.",
        ["Try it now: open a wheel with **{invoke}**."] =
            "Pruébalo ahora: abre una rueda con **{invoke}**.",
        ["**Game Grid** - a universal launcher for every installed game across Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft and EA."] =
            "**Cuadrícula de juegos**: un lanzador universal para todos los juegos instalados en Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft y EA.",
        ["**Nothing hooked, nothing injected** - Radiata reads your controller directly and allows your input through only when a wheel isn't up, so the game underneath doesn't pick up duplicate input. For strict-anticheat titles, see [[passthru-mode|Passthru Mode]]."] =
            "**Sin ganchos ni inyección**: Radiata lee tu mando directamente y deja pasar tu entrada solo cuando no hay una rueda abierta, de modo que el juego de debajo no recibe entradas duplicadas. Para títulos con anti-cheat estricto, consulta [[passthru-mode|Modo directo]].",
        ["Start with [[opening-a-wheel|Opening a wheel]]. Then open one and click the aiming stick (L3/R3) when you're ready to start editing."] =
            "Empieza por [[opening-a-wheel|Abrir una rueda]]. Luego abre una y haz clic en el stick de apuntado (L3/R3) cuando estés listo para empezar a editar.",
        ["Radiata ships as a single installer, **Radiata-<version>-setup.exe**. Download it from [getradiata.app](https://getradiata.app)."] =
            "Radiata se distribuye como un único instalador, **Radiata-<version>-setup.exe**. Descárgalo desde [getradiata.app](https://getradiata.app).",
        ["**Only download Radiata from known sources.** Anything else claiming to be Radiata isn't from the developer."] =
            "**Descarga Radiata únicamente desde fuentes conocidas.** Cualquier otra cosa que diga ser Radiata no procede del desarrollador.",
        ["**Administrator account needed** for driver installation (optional but strongly recommended)"] =
            "**Hace falta una cuenta de administrador** para instalar los controladores (opcional, pero muy recomendable)",
        ["Windows SmartScreen may show a blue **\"Windows protected your PC\"** box the first time you run the installer, and your browser may warn that the file **\"isn't commonly downloaded\"**."] =
            "Windows SmartScreen puede mostrar un cuadro azul de **\"Windows protegió su PC\"** la primera vez que ejecutas el instalador, y tu navegador puede avisar de que el archivo **\"no se descarga habitualmente\"**.",
        ["**No Run anyway button at all?** A managed or locked-down PC can have SmartScreen set to block outright. Radiata can't work around it."] =
            "**¿No aparece ningún botón Ejecutar de todas formas?** Un PC administrado o restringido puede tener SmartScreen configurado para bloquear directamente. Radiata no puede saltárselo.",
        ["You can confirm you have the genuine file before running it: every GitHub release lists the installer's **SHA-256**, and `Get-FileHash .\\Radiata-<version>-setup.exe` in PowerShell should print the same value. Radiata's updater automatically runs the same verification check on every update."] =
            "Puedes confirmar que tienes el archivo auténtico antes de ejecutarlo: cada versión de GitHub indica el **SHA-256** del instalador, y `Get-FileHash .\\Radiata-<version>-setup.exe` en PowerShell debería imprimir el mismo valor. El actualizador de Radiata ejecuta automáticamente la misma comprobación en cada actualización.",
        ["**Antivirus false positives** happen for the same reason. If yours quarantines the installer, restore it and run it again, or download it fresh from getradiata.app."] =
            "**Los falsos positivos de antivirus** ocurren por el mismo motivo. Si el tuyo pone en cuarentena el instalador, restáuralo y ejecútalo de nuevo, o descárgalo otra vez desde getradiata.app.",
        ["Installs **for your user account only**, into `%LOCALAPPDATA%\\Programs\\Radiata`. It never touches other accounts on the PC."] =
            "Instala **solo para tu cuenta de usuario**, en `%LOCALAPPDATA%\\Programs\\Radiata`. Nunca toca otras cuentas del PC.",
        ["Adds a **Start menu** shortcut, and on a **first** install sets Radiata to **start with Windows**. You can turn that off in the tray menu or **Settings ▸ Advanced**."] =
            "Añade un acceso directo al **menú Inicio** y, en una **primera** instalación, configura Radiata para **iniciarse con Windows**. Puedes desactivarlo en el menú de la bandeja o en **Ajustes ▸ Avanzado**.",
        ["Nothing sneaky or malicious comes with Radiata. Radiata is GPLv3 free software."] =
            "Radiata no incluye nada sospechoso ni malicioso. Radiata es software libre GPLv3.",
        ["Installing over an existing copy is an **upgrade in place**. Your wheels, settings and game art are left alone."] =
            "Instalar sobre una copia existente es una **actualización in situ**. Tus ruedas, ajustes y carátulas se conservan intactos.",
        ["Driver prompts (UAC prompts)"] =
            "Avisos de los controladores (avisos de UAC)",
        ["Leave **Install drivers (recommended)** selected and approve any necessary Windows prompts that follow. **ViGEmBus** and **HidHide** are the open-source drivers that keep duplicate controller input out of the game. See [[input-isolation|Input isolation]]."] =
            "Deja **Instalar controladores (recomendado)** seleccionado y aprueba los avisos de Windows que aparezcan a continuación. **ViGEmBus** y **HidHide** son los controladores de código abierto que mantienen la entrada duplicada del mando fuera del juego. Consulta [[input-isolation|Aislamiento de entrada]].",
        ["**Declining is safe.** Radiata still works; games just also see your controller while a wheel is open, which is annoying. Install them later any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**."] =
            "**Rechazarlos es seguro.** Radiata sigue funcionando; simplemente los juegos también ven el mando mientras una rueda está abierta, lo cual resulta molesto. Instálalos más tarde en cualquier momento desde **Ajustes ▸ Avanzado ▸ Solución de problemas ▸ Instalar/reparar controladores**.",
        ["The drivers are shared system components other tools may also use, so if you uninstall Radiata, removing the drivers as well is optional."] =
            "Los controladores son componentes compartidos del sistema que otras herramientas también pueden usar, así que, si desinstalas Radiata, eliminar también los controladores es opcional.",
        ["Radiata then lives in the **system tray**. Click the icon for Settings or right-click for the tray menu. Then start at [[opening-a-wheel|Opening a wheel]]."] =
            "Radiata vive después en la **bandeja del sistema**. Haz clic en el icono para abrir Ajustes o clic derecho para el menú de la bandeja. A continuación, empieza por [[opening-a-wheel|Abrir una rueda]].",
        ["**Updates are discovered automatically** by default. You can manually check for updates at **Settings ▸ Advanced ▸ Check for Updates**. Radiata always verifies the download before running it."] =
            "**Las actualizaciones se detectan automáticamente** de forma predeterminada. Puedes buscar actualizaciones manualmente en **Ajustes ▸ Avanzado ▸ Buscar actualizaciones**. Radiata siempre verifica la descarga antes de ejecutarla.",
        ["**Uninstall** from **Settings ▸ Advanced ▸ Troubleshooting ▸ Uninstall Radiata…**, or from Windows' **Installed apps** list. Your settings and the shared drivers can be removed in the same step."] =
            "**Desinstala** desde **Ajustes ▸ Avanzado ▸ Solución de problemas ▸ Desinstalar Radiata…** o desde la lista de **Aplicaciones instaladas** de Windows. Tus ajustes y los controladores compartidos se pueden eliminar en el mismo paso.",
        ["Your current setting: open a wheel with **{invoke}**."] =
            "Tu configuración actual: abre una rueda con **{invoke}**.",
        ["Set your own chords (button combos that open a wheel) in [[triggers|Settings ▸ Customize ▸ Triggers]]."] =
            "Define tus propias combinaciones (combinaciones de botones que abren una rueda) en [[triggers|Ajustes ▸ Personalizar ▸ Gestos de invocación]].",
        ["**Flip mid-gesture:** while holding a chord, tap the opposite bumper or trigger to switch to the other wheel without having to re-input the entire chord."] =
            "**Cambio a mitad del gesto:** mientras mantienes una combinación, toca el bumper o el gatillo opuesto para pasar a la otra rueda sin tener que volver a introducir toda la combinación.",
        ["**Either** analog stick aims, but it's easiest if you use the hand that's not holding the shoulder button. **Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) narrows it to one stick per wheel."] =
            "Apunta **cualquiera** de los dos sticks analógicos, pero es más fácil si usas la mano que no sostiene el botón superior. **La rueda ignora el stick contrario** ([[accessibility|ajuste de Accesibilidad]]) lo limita a un stick por rueda.",
        ["**Tilt the stick** toward a slice and it lights up. **Release the trigger** to fire it."] =
            "**Inclina el stick** hacia un sector y se ilumina. **Suelta el gatillo** para ejecutarlo.",
        ["**Release while centered** (stick in the deadzone) **cancels**."] =
            "**Soltar con el stick centrado** (dentro de la zona muerta) **cancela**.",
        ["A slice that still **needs configuring** (such as a voice-join with no URL) arms as **\"Configure in Settings\"**. Choosing it takes you to the configuration screen or the setup wizard it needs."] =
            "Un sector que todavía **necesita configurarse** (por ejemplo, una unión a voz sin URL) se prepara como **\"Configurar en Ajustes\"**. Al elegirlo, te lleva a la pantalla de configuración o al asistente de configuración que necesita.",
        ["Slices with **Hold to confirm** are protected from accidental triggering, which is useful for Sleep, Power Down, etc. Hold the stick on them (0.8 s) and they'll activate. You can set this on any slice in the Settings wheel editors."] =
            "Los sectores con **Mantener para confirmar** están protegidos contra activaciones accidentales, algo útil para Suspender, Apagar, etc. Mantén el stick sobre ellos (0,8 s) y se activarán. Puedes activarlo en cualquier sector desde los editores de ruedas de Ajustes.",
        ["**D-Pad 🡅 🡇** adjusts system volume."] =
            "**D-Pad 🡅 🡇** ajusta el volumen del sistema.",
        ["**D-Pad 🡄 🡆** steps the Alt-Tab window switcher by default - or [[volume-mixer|virtual desktops, track skip, or mic volume]]. Choose in **Settings ▸ Customize ▸ D-Pad 🡄 🡆**. "] =
            "**Cruceta 🡄 🡆** recorre el conmutador de ventanas Alt-Tab de forma predeterminada, o bien [[volume-mixer|escritorios virtuales, cambio de pista o volumen del micrófono]]. Elígelo en **Ajustes ▸ Personalizar ▸ Cruceta 🡄 🡆**.",
        ["You can also toggle wheels on/off from tray menu's **Disable/Enable Wheels** or add a **Disable Wheels** slice action."] =
            "También puedes activar o desactivar las ruedas desde la opción **Desactivar las ruedas/Activar las ruedas** del menú de la bandeja, o añadir un sector de acción **Desactivar las ruedas**.",
        ["Disabling the wheels changes wheel routing only. The virtual controller and cloak stay exactly where they are, basic controls keep running through that controller, and native features do **not** come back. To release capture and get your real controller, use [[passthru-mode|Passthru Mode]]. This will re-enable vendor features like special haptics and touchpads."] =
            "Desactivar las ruedas solo cambia el enrutado de la rueda. El mando virtual y el ocultamiento siguen exactamente donde estaban, los controles básicos siguen pasando por ese mando y las funciones nativas **no** vuelven. Para liberar la captura y recuperar tu mando real, usa el [[passthru-mode|Modo directo]]. Esto reactivará funciones del fabricante, como la háptica especial y los paneles táctiles.",
        ["Your current setting: toggle the wheels with **{disable}**."] =
            "Tu configuración actual: activa o desactiva las ruedas con **{disable}**.",
        ["**Start editing a wheel:** with a wheel open, **click either stick** (L3/R3). The wheel centers and stays up after you release the trigger."] =
            "**Empezar a editar una rueda:** con una rueda abierta, **haz clic en cualquiera de los sticks** (L3/R3). La rueda se centra y permanece en pantalla tras soltar el gatillo.",
        ["**Use either stick**. While you're editing, non-logo slices always show their labels regardless of the [[show-labels|Label setting]]."] =
            "**Usa cualquiera de los dos sticks**. Mientras editas, los sectores sin logotipo siempre muestran su etiqueta, independientemente del [[show-labels|ajuste de etiquetas]].",
        ["**Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) can override this."] =
            "**La rueda ignora el stick contrario** ([[accessibility|ajuste de Accesibilidad]]) puede anular esto.",
        ["**Move:** **{cross}** picks up the selected slice; aim at a target slot and **{cross}** drops it."] =
            "**Mover:** **{cross}** recoge el sector seleccionado; apunta a la posición de destino y **{cross}** lo suelta.",
        [" **D-Pad 🡄 🡆** will nudge a slice one spot left or right."] =
            " La **cruceta 🡄 🡆** desplaza un sector una posición a la izquierda o a la derecha.",
        ["**Remove:** **hold {square}** on a slice until it disappears. Removing the **last** slice disables that wheel; see [[empty-wheel|Single-wheel mode]]."] =
            "**Quitar:** **mantén {square}** sobre un sector hasta que desaparezca. Quitar el **último** sector desactiva esa rueda; consulta [[empty-wheel|Modo de una sola rueda]].",
        ["A wheel holds up to **12** slices."] =
            "Una rueda admite hasta **12** sectores.",
        ["**Undo / Redo:** **L1 / R1**. You can undo/redo multiple steps while you remain in Edit mode."] =
            "**Deshacer / Rehacer:** **L1 / R1**. Puedes deshacer y rehacer varios pasos mientras permanezcas en el modo de edición.",
        ["**Exit + save:** **{circle}**, click the stick again, or press an **Fn** / **L4/R4** button."] =
            "**Salir y guardar:** **{circle}**, volver a hacer clic en el stick o pulsar un botón **Fn** / **L4/R4**.",
        ["**Want only one wheel?** Delete every slice off the other one. A wheel with **no slices is disabled**. That side's chord stays fully usable in the game. Remove slices from Settings, or in [[edit-mode|edit mode]] with **hold {square}** until the last one is gone."] =
            "**¿Solo quieres una rueda?** Elimina todos los sectores de la otra. Una rueda **sin sectores está desactivada**. La combinación de ese lado sigue siendo plenamente utilizable en el juego. Quita sectores desde Ajustes, o en [[edit-mode|modo de edición]] con **mantener {square}** hasta que no quede ninguno.",
        ["If you accidentally empty **both** wheels, Settings opens so you can rebuild one or both of them."] =
            "Si vacías **ambas** ruedas por accidente, se abre Ajustes para que reconstruyas una de ellas o las dos.",
        ["If a wheel has only one slice and it's **the Arcade Launcher**, then that wheel is replaced by the Arcade Launcher directly."] =
            "Si una rueda tiene un solo sector y es **el lanzador del Arcade**, esa rueda se sustituye directamente por el lanzador del Arcade.",
        ["**To set it up:** just delete all slices on a wheel except a single **Arcade ▸ Arcade Launcher** slice. You can do that in Settings, or in [[edit-mode|edit mode]] with **hold {square}**."] =
            "**Para configurarlo:** elimina todos los sectores de una rueda excepto un único sector **Arcade ▸ Lanzador del Arcade**. Puedes hacerlo en Ajustes o en [[edit-mode|modo de edición]] con **mantener {square}**.",
        ["**This applies to Arcade Launcher only.** A single slice with just an individual Arcade game on it still draws as a one-slice wheel."] =
            "**Esto se aplica solo al lanzador del Arcade.** Un único sector con un juego individual del Arcade sigue dibujándose como una rueda de un solo sector.",
        ["**An Arcade Launcher wheel** can't be edited with R3/L3; edit it in **Settings ▸ Left/Right Wheel**, or add a second slice to get the wheel back."] =
            "**Una rueda del lanzador del Arcade** no se puede editar con R3/L3; edítala en **Ajustes ▸ Rueda izquierda/derecha**, o añade un segundo sector para recuperar la rueda.",
        ["If you want, you can pair this with [[empty-wheel|Single-wheel mode]]: empty the OTHER wheel and you have one gesture that opens the arcade and one that passes straight through to the game."] =
            "Si quieres, puedes combinarlo con el [[empty-wheel|Modo de una sola rueda]]: vacía la OTRA rueda y tendrás un gesto que abre el arcade y otro que pasa directamente al juego.",
        ["**Drag an app (.exe or .lnk) from Explorer into the slice list**. A **Launch** slice lands at the drop position with its icon already extracted. Drop several at once for several slices."] =
            "**Arrastra una aplicación (.exe o .lnk) desde el Explorador a la lista de sectores**. Aparece un sector **Lanzar** en la posición donde lo sueltes, con su icono ya extraído. Suelta varias a la vez para crear varios sectores.",
        ["**Color swatches are paired** - the wheel shows whichever variation suits its [[customize|material]]. If you type an exact **Hex** value instead, that color is used exactly as-is, without tinting lighter or darker based on wheel Material. **Reset to Default** returns to the action-type color."] =
            "**Las muestras de color van emparejadas**: la rueda muestra la variación que mejor encaje con su [[customize|material]]. Si escribes un valor **hexadecimal** exacto, ese color se usa tal cual, sin aclararlo ni oscurecerlo según el material de la rueda. **Restablecer el valor predeterminado** vuelve al color del tipo de acción.",
        ["**Drop your own image onto the preview well** to give any slice custom artwork. It needs a **transparent background**, so a logo-style PNG is best. Something like a screenshot or a photo with no transparency would be a solid block so it's rejected. The file is **copied** into Radiata's art cache, so moving the original later won't blank the slice."] =
            "**Suelta tu propia imagen sobre el recuadro de vista previa** para dar a cualquier sector una ilustración personalizada. Necesita **fondo transparente**, así que lo mejor es un PNG de tipo logotipo. Algo como una captura de pantalla o una foto sin transparencia sería un bloque sólido, así que se rechaza. El archivo se **copia** a la caché de ilustraciones de Radiata, de modo que mover el original después no deja el sector en blanco.",
        ["When you choose a game, Radiata fetches its transparent **logo** automatically. The **↻ button** restores that logo (downloading it if needed), and the **🡄 🡆** buttons cycle every logo already downloaded for that game. **Requires [[integrations|SteamGridDB]] to be configured.**"] =
            "Cuando eliges un juego, Radiata descarga automáticamente su **logotipo** transparente. El **botón ↻** restaura ese logotipo (descargándolo si hace falta), y los botones **🡄 🡆** recorren todos los logotipos ya descargados para ese juego. **Requiere tener configurado [[integrations|SteamGridDB]].**",
        ["**Choosing a different icon drops the original game logo**. A slice's logo is independent of the [[cover-art|Game Grid's]]."] =
            "**Elegir un icono distinto descarta el logotipo original del juego**. El logotipo de un sector es independiente del de la [[cover-art|Cuadrícula de juegos]].",
        ["**Everything auto-saves** - adds, removals, reorders, and edits to an existing slice (**Revert** undoes an in-progress edit). **A new slice created in Settings is not saved until you click Save Slice**. Entering **Ctrl+S** forces a save at any point."] =
            "**Todo se guarda automáticamente**: altas, bajas, reordenaciones y modificaciones de un sector existente (**Revertir** deshace una edición en curso). **Un sector nuevo creado en Ajustes no se guarda hasta que haces clic en Guardar sector**. Al pulsar **Ctrl+S** se fuerza el guardado en cualquier momento.",
        ["**Select** (Create/Share) - **cycle the selected game's cover** and save it. Your choices will cycle through default, then up to 10 top-rated SteamGridDB covers, then 5 flat colors if you just want the logo on a clean background."] =
            "**Seleccionar** (Create/Share): **recorrer las portadas del juego seleccionado** y guardarla. Las opciones recorren la predeterminada, luego hasta 10 portadas mejor valoradas de SteamGridDB y después 5 colores planos, por si solo quieres el logotipo sobre un fondo limpio.",
        ["Covers come from [[integrations|SteamGridDB]] - add a free API key in **Settings ▸ Advanced ▸ Integrations** for portrait covers and logos across every storefront. This is the best option. However, without SteamGridDB, you still get Steam's own art, [[playnite|Playnite]]'s covers, and the flat colors."] =
            "Las portadas provienen de [[integrations|SteamGridDB]]; añade una clave de API gratuita en **Ajustes ▸ Avanzado ▸ Integraciones** para obtener portadas verticales y logotipos de todas las tiendas. Es la mejor opción. Aun así, sin SteamGridDB seguirás teniendo las ilustraciones propias de Steam, las portadas de [[playnite|Playnite]] y los colores planos.",
        ["An **Arcade** slice opens a little game in a **round window, right where the wheel was**. Play a game while you wait on a loading screen or a big lobby, no alt-tabbing required."] =
            "Un sector de **Arcade** abre un pequeño juego en una **ventana redonda, justo donde estaba la rueda**. Juega mientras esperas en una pantalla de carga o en una sala grande, sin necesidad de hacer alt-tab.",
        ["**Arcade Launcher** opens the whole arcade. Each game is a cabinet on a round carousel with a live screenshot of where it was left. **Left/right** on the stick or D-Pad swings the next cabinet to the front, **{cross}** plays it. It **picks up where you left off** - straight back into the game you were last playing, or at the cabinets if that's where you closed it. Each game can also be directly-launched by adding a slice for it."] =
            "**Lanzador Arcade** abre todo el arcade. Cada juego es una máquina en un carrusel redondo, con una captura en vivo del punto donde se dejó. **Izquierda/derecha** en el joystick o la cruceta trae la siguiente máquina al frente, y **{cross}** la pone en marcha. **Retoma donde lo dejaste**: directamente en el juego al que estabas jugando, o en las máquinas si fue ahí donde lo cerraste. Cada juego también se puede iniciar directamente añadiendo un sector para él.",
        ["**A wheel with the Arcade Launcher and nothing else** skips the wheel and goes straight to the Arcade Launcher - see [[arcade-direct-launch|Arcade direct-launch]]."] =
            "**Una rueda con el lanzador del Arcade y nada más** se salta la rueda y va directamente al lanzador del Arcade. Consulta [[arcade-direct-launch|Lanzamiento directo del Arcade]].",
        ["**{circle} always backs you out**. One press closes a menu or help card, the next steps out of the game: back to the **Arcade Launcher** when that's how you got in, otherwise straight out. **The game freezes exactly as you left it**, so you can come back later and carry on. Each game stores its own state and scoreboard."] =
            "**{circle} siempre te hace retroceder**. Una pulsación cierra un menú o una tarjeta de ayuda, la siguiente sale del juego: de vuelta al **Lanzador Arcade** si entraste por él, o directamente fuera si no. **El juego se congela exactamente como lo dejaste**, de modo que puedes volver más tarde y continuar. Cada juego guarda su propio estado y su marcador.",
        ["Over a game, the arcade only plays while your controller is **isolated** from it. Otherwise a card explains why and offers **hold {triangle} to play anyway**. Passthru Mode will mean no Arcade games can be played while you're in another game. See [[input-isolation|Input isolation]]."] =
            "Sobre un juego, el arcade solo funciona mientras tu mando está **aislado** de él. Si no, una tarjeta explica por qué y ofrece **mantener {triangle} para jugar igualmente**. El Modo directo implica que no se puede jugar a ningún juego del Arcade mientras estás en otro juego. Consulta [[input-isolation|Aislamiento de entrada]].",
        ["**Kabloom**: Minesweeper logic on a Floret Pentagonal Tiled field of flower petals. Move the cursor with the stick or d-pad. **{cross}** reveals a tile, **{square}** flags where you think there's a bee (or multiple bees, on later levels). Press **{square}** again to increase the flag count, or hold it for a question mark flag. Remaining bees are shown at the bottom of the screen. At the center of each floret is a nectar gem, collected when all the petals around it are cleared, which adds to your total score. Every board is solvable with no forced guesses."] =
            "**Kabloom**: lógica de buscaminas sobre un campo de pétalos de flor con teselado pentagonal en flor. Mueve el cursor con el stick o la cruceta. **{cross}** descubre una casilla y **{square}** marca donde crees que hay una abeja (o varias, en los niveles avanzados). Pulsa **{square}** otra vez para aumentar el número de marcas, o mantenlo para poner un signo de interrogación. Las abejas restantes se muestran en la parte inferior de la pantalla. En el centro de cada flor hay una gema de néctar, que se recoge cuando se despejan todos los pétalos que la rodean y suma a tu puntuación total. Todos los tableros tienen solución sin necesidad de adivinar.",
        ["**More about \"no forced guesses\":** Boards 1-10 are generated on the fly and validated by the Solver before play. Every board from level 11 up is baked ahead of time and certified by a complete solver before it ships. Individual petal tiles can hold up to three bees on the later levels. The solver plays the board from every zero-clue petal you could open on. It runs the human patterns first (saturation, subset difference, overlap bounds, chained constraints) and when those run dry it groups the unknown petals that share the same set of clues into boxes, enumerates every way the remaining bees can be spread over each connected group of boxes, and folds in the total bee count so the petals no clue touches get reasoned about too. A level ships only when the certified start-points cover the whole crop, so the first petal you open is always one the proof began from, ensuring every possible start guarantees a solvable board. "] =
            "**Más sobre \"nunca hay que adivinar\":** los tableros 1-10 se generan al momento y el solucionador los valida antes de jugar. Todos los tableros a partir del nivel 11 se generan de antemano y los certifica un solucionador completo antes de distribuirlos. Cada pétalo puede contener hasta tres abejas en los niveles avanzados. El solucionador juega el tablero desde cada pétalo sin pistas por el que podrías empezar. Primero aplica los patrones humanos (saturación, diferencia de subconjuntos, límites de solapamiento, restricciones encadenadas) y, cuando esos se agotan, agrupa en cajas los pétalos desconocidos que comparten el mismo conjunto de pistas, enumera todas las formas de repartir las abejas restantes por cada grupo conexo de cajas e incorpora el total de abejas para razonar también sobre los pétalos que ninguna pista toca. Un nivel solo se distribuye cuando los puntos de inicio certificados cubren todo el cultivo, así que el primer pétalo que abres siempre es uno de los que partió la demostración, lo que garantiza que cualquier inicio posible lleve a un tablero con solución.",
        ["**Connate**: Your craft rides the rim around a cluster of orbs and garbage blocks. Shoot orbs to merge the numbers before the pile grows past the inner ring. **{cross}** fires your held number into the cluster. Hold to fire with more force. Star and Star-Gap pieces merge to make orbs of 3, and matching numbers from 3 up combines their values. Only **matching colors** merge, although mixed-color orbs can be created by matching stars and gaps of opposite colors; these merge with either color or with other mixed-color orbs. Combos charge up a bomb you can fire. Bomb high-value orbs to collect them to your score."] =
            "**Connate**: tu nave recorre el borde alrededor de un grupo de orbes y bloques de basura. Dispara a los orbes para fusionar los números antes de que el montón crezca más allá del anillo interior. **{cross}** lanza el número que llevas hacia el grupo. Mantenlo pulsado para disparar con más fuerza. Las piezas Estrella y Estrella-hueco se fusionan para formar orbes de 3, y a partir de 3 los números iguales combinan sus valores. Solo se fusionan los **colores iguales**, aunque se pueden crear orbes de color mixto uniendo estrellas y huecos de colores opuestos; estos se fusionan con cualquiera de los dos colores o con otros orbes de color mixto. Los combos cargan una bomba que puedes lanzar. Bombardea los orbes de mayor valor para sumarlos a tu puntuación.",
        ["**Stages**: the pace follows your score. Each time your collected total crosses 100, 250, 450, 700 and 1,000, the shot clock gets a little shorter, garbage arrives a little sooner, and a bomb takes one more combo charge to fill. The current stage is shown under the score. From stage 2, every 48 seconds of play ends with 8 seconds of relief: no garbage, and a longer shot clock."] =
            "**Fases**: el ritmo sigue tu puntuación. Cada vez que tu total recogido supera 100, 250, 450, 700 y 1.000, el tiempo límite de disparo se acorta un poco, la basura llega un poco antes y una bomba necesita una carga de combo más para llenarse. La fase actual se muestra bajo la puntuación. A partir de la fase 2, cada 48 segundos de juego terminan con 8 segundos de respiro: sin basura y con un tiempo límite de disparo más largo.",
        ["**Petalpop**: a ring of paddles around a flower of petals. Your analog stick controls every paddle together, so be careful! Hold **{cross}** to draw the paddles back like a slingshot, then release to **smash** the ball. Hit the core with a smash shot to clear the level. Pop a blue petal for multi-ball. \n\nFour sides and four levels to start, then five, six, seven and eight. You get one extra life for each size increase. Switch between spring and rail control via the **Start** menu."] =
            "**Petalpop**: un anillo de palas alrededor de una flor de pétalos. Tu stick analógico controla todas las palas a la vez, ¡así que ten cuidado! Mantén **{cross}** para echar las palas hacia atrás como un tirachinas y suelta para **golpear** la bola. Golpea el núcleo con un disparo potente para completar el nivel. Rompe un pétalo azul para conseguir multibola.\n\nEmpiezas con cuatro lados y cuatro niveles, luego cinco, seis, siete y ocho. Obtienes una vida extra por cada aumento de tamaño. Alterna entre control de muelle y de raíl desde el menú **Start**.",
        ["**Internode**: shoot down a twisting half-pipe and collect tokens while avoiding mines and gaps. Hitting a mine will drop your tokens, and hitting one when carrying no tokens sets you back one stretch. Falling in a gap always sets you back one stretch. **{cross}** jumps. \n\nCatching a full token streak will upgrade the final token in the pattern to a gold 10x token. Reach each checkpoint with enough tokens to bank them in your score, with extra bonuses for passing a stretch on the first try and for collecting every token in a stretch. Insufficient tokens keeps you looping the same stretch until you have enough. The course twists and turns harder every stage. \n\nCamera roll can be enabled/disabled in the **START** menu."] =
            "**Internode**: dispara por un half-pipe sinuoso y recoge fichas evitando minas y huecos. Si chocas con una mina, sueltas tus fichas, y si chocas con una sin llevar ninguna, retrocedes un tramo. Caer en un hueco siempre te hace retroceder un tramo. **{cross}** salta.\n\nSi consigues una racha completa de fichas, la última ficha del patrón se convierte en una ficha dorada de 10x. Llega a cada punto de control con suficientes fichas para depositarlas en tu puntuación, con bonificaciones extra por superar un tramo al primer intento y por recoger todas las fichas de un tramo. Si no tienes suficientes fichas, sigues repitiendo el mismo tramo hasta que las consigas. El recorrido se retuerce más en cada fase.\n\nEl giro de cámara se puede activar o desactivar en el menú **START**.",
        ["**Choose the app** two ways: **Browse for App…** picks an `.exe` from disk, and **Installed Apps…** lists everything with a Start-menu entry (including **Microsoft Store apps**, which have no `.exe` to browse to). Either way the icon is pulled in automatically. You can also drag an `.exe`, a shortcut, or a Start-menu app straight into the slice list."] =
            "**Elige la aplicación** de dos maneras: **Buscar aplicación…** escoge un `.exe` del disco, y **Aplicaciones instaladas…** lista todo lo que tenga entrada en el menú Inicio (incluidas las **aplicaciones de Microsoft Store**, que no tienen ningún `.exe` que buscar). En ambos casos el icono se extrae automáticamente. También puedes arrastrar un `.exe`, un acceso directo o una aplicación del menú Inicio directamente a la lista de sectores.",
        ["A **Store app** can't be detected as already-running, so **Run** just re-opens it and **Toggle** won't reliably close it. And an app started through an updater or launcher **stub** may run under a different name than the file you picked, so picking the app's real `.exe` is the reliable choice."] =
            "De una **aplicación de Store** no se puede detectar si ya se está ejecutando, así que **Ejecutar** simplemente la vuelve a abrir y **Conmutar** no la cerrará de forma fiable. Y una aplicación iniciada mediante un actualizador o un **stub** de lanzamiento puede ejecutarse con un nombre distinto del archivo que elegiste, así que lo fiable es elegir el `.exe` real de la aplicación.",
        ["**Installed Game** slices launch the game **directly** where possible. A GOG game runs its own exe even without GOG Galaxy installed; only stores that need their client running (like Steam) will route through the launcher."] =
            "Los sectores **Juego instalado** inician el juego **directamente** siempre que es posible. Un juego de GOG ejecuta su propio exe aunque no tenga GOG Galaxy instalado; solo las tiendas que necesitan su cliente en ejecución (como Steam) pasan por el lanzador.",
        ["Radiata forwards a **virtual controller**, and **Controller mode** decides which kind the game sees: an **Xbox** pad or a **DualShock** pad. Two slices under **System ▸ Controller** flip it - **Toggle Xbox Mode** and **Toggle DualShock Mode**."] =
            "Radiata reenvía un **mando virtual**, y el **modo de mando** decide de qué tipo lo ve el juego: un mando **Xbox** o un mando **DualShock**. Dos sectores en **Sistema ▸ Mando** lo cambian: **Activar modo Xbox** y **Activar modo DualShock**.",
        ["**What it's for:** many games only accept one class of controller. A Game Pass or Xbox-app title that refuses a DualSense controller will allow it if it's in **Xbox Mode** in Radiata. Games that want PlayStation input go the other way."] =
            "**Para qué sirve:** muchos juegos solo aceptan una clase de mando. Un título de Game Pass o de la aplicación Xbox que rechaza un mando DualSense lo aceptará si está en **modo Xbox** en Radiata. Los juegos que quieren entrada de PlayStation van al revés.",
        ["**Button prompts follow the mode.** The glyphs a game draws come from the pad it thinks is plugged in, so Xbox Mode gets you **A B X Y** and DualShock Mode **{cross} {circle} {square} {triangle}**."] =
            "**Las indicaciones de botones siguen al modo.** Los símbolos que dibuja un juego provienen del mando que cree tener conectado, así que Xbox Mode te da **A B X Y** y DualShock Mode **{cross} {circle} {square} {triangle}**.",
        ["The switch is instant and stays put until you flip it back, but the game sees a controller swap at that moment. **Flip it before launching the game** for best results."] =
            "El cambio es instantáneo y se mantiene hasta que lo vuelvas a cambiar, pero en ese momento el juego ve un cambio de mando. **Cámbialo antes de lanzar el juego** para obtener los mejores resultados.",
        ["**A wired Xbox pad stays an Xbox pad.** If your physical controller is an Xbox pad on USB, or a USB wireless dongle, the game always gets a virtual Xbox pad and DualShock Mode cannot change it."] =
            "**Un mando Xbox con cable sigue siendo un mando Xbox.** Si tu mando físico es un mando Xbox por USB, o por un receptor inalámbrico USB, el juego recibirá siempre un mando Xbox virtual y DualShock Mode no puede cambiarlo.",
        ["This requires installing the isolation drivers. There's no virtual pad to switch without them, and firing the slice puts up an on-screen notice saying so. In [[passthru-mode|Passthru Mode]] the game reads your real controller, so the mode has nothing to change. See [[input-isolation|Input isolation]]."] =
            "Esto requiere instalar los controladores de aislamiento. Sin ellos no hay mando virtual que cambiar, y ejecutar el sector muestra un aviso en pantalla que lo indica. En [[passthru-mode|Modo directo]] el juego lee tu mando real, así que el modo no tiene nada que cambiar. Consulta [[input-isolation|Aislamiento de entrada]].",
        ["Discord must be running. These slices talk to the Discord app on your PC, not to Discord's website. "] =
            "Discord tiene que estar en marcha. Estos sectores hablan con la aplicación de Discord de tu PC, no con la web de Discord.",
        ["**OBS Studio** ([obsproject.com](https://obsproject.com)) is the free, open-source program that dominates the Twitch and YouTube streaming space, and also allows you to record the screen. It builds a broadcast out of **scenes** - named layouts of game capture, camera, mic and overlays - that you switch between while live."] =
            "**OBS Studio** ([obsproject.com](https://obsproject.com)) es el programa gratuito y de código abierto que domina el mundo de las emisiones en Twitch y YouTube, y que también permite grabar la pantalla. Construye una emisión a partir de **escenas** (disposiciones con nombre de captura de juego, cámara, micrófono y superposiciones) entre las que cambias mientras estás en directo.",
        ["A **Text Chat** slice (Chat & Streaming) types a message into the running game's text chat: it presses the game's **chat-open key**, types your text, and presses Enter."] =
            "Un sector **Chat de texto** (Chat y emisión) escribe un mensaje en el chat de texto del juego en ejecución: pulsa la **tecla que abre el chat** del juego, escribe tu texto y pulsa Enter.",
        ["**Chat Button** - **Try Game Default** looks up the chat key for whatever game is running **each time the slice fires**, from a built-in index of around 1,000 PC games, so the same slice works across multiple games. **Custom…** lets you enter the key yourself, so use it for games with remapped or unusual chat keys, or for a game the index doesn't cover."] =
            "**Botón de chat**: **Probar el valor del juego** busca la tecla de chat del juego que esté en marcha **cada vez que se ejecuta el sector**, en un índice integrado de unos 1.000 juegos de PC, así que el mismo sector sirve para varios juegos. **Personalizado…** te deja introducir la tecla tú mismo, así que úsalo con juegos que tengan teclas de chat reasignadas o poco habituales, o con un juego que el índice no cubra.",
        ["**Message** - the line of text to send. An empty message (or Custom with no key) arms as **\"Configure in Settings\"**, and firing opens the slice's editor."] =
            "**Mensaje**: la línea de texto que se envía. Un mensaje vacío (o Personalizado sin tecla) se prepara como **\"Configurar en Ajustes\"**, y ejecutarlo abre el editor del sector.",
        ["**Sends are limited to one per 15 seconds**. Hub shows the remaining cooldown. No spamming, please!"] =
            "**Los envíos están limitados a uno cada 15 segundos**. El centro de la rueda muestra el tiempo restante. Nada de spam, por favor.",
        ["**Sony family** (DualSense Edge, DualSense, DualShock 4) over Bluetooth or USB: good support with clean input isolation. The **Edge's Fn buttons** are the reference trigger. Haptic triggers and touchpad will not be seen by games due to driver limitations unless you are in Passthru Mode."] =
            "**Familia Sony** (DualSense Edge, DualSense, DualShock 4) por Bluetooth o USB: buena compatibilidad con aislamiento de entrada limpio. Los **botones Fn del Edge** son el disparador de referencia. Los gatillos hápticos y el panel táctil no los verán los juegos por limitaciones del controlador, salvo que estés en Modo directo.",
        ["**Pads with extra paddles or buttons** (e.g. **8BitDo Ultimate 2C**) - over **Bluetooth** the extra **L4/R4** buttons are read directly, so you can pick them in [[triggers|Settings ▸ Customize ▸ Triggers]] to summon a wheel on their own, or paired with a second button if you also use them in games. They're offered, not assumed: the default stays the standard bumper chord. Over **USB** the extra buttons are invisible to Radiata, except as mapped by the controller's own drivers. So connect over Bluetooth if you want native L4/R4 support, or map them deliberately."] =
            "**Mandos con palancas o botones adicionales** (p. ej., el **8BitDo Ultimate 2C**): por **Bluetooth**, los botones **L4/R4** adicionales se leen directamente, así que puedes elegirlos en [[triggers|Ajustes ▸ Personalizar ▸ Disparadores]] para invocar una rueda por sí solos, o emparejados con un segundo botón si también los usas en los juegos. Se ofrecen, no se dan por supuestos: lo predeterminado sigue siendo la combinación de bumper estándar. Por **USB**, los botones adicionales son invisibles para Radiata, salvo tal como los asignen los propios controladores del mando. Así que conéctalo por Bluetooth si quieres compatibilidad nativa con L4/R4, o asígnalos a propósito.",
        ["**Genuine Xbox pads over Bluetooth** - isolated too, with an extra safeguard. Before presenting the stand-in pad, Radiata has a separate helper process **observe** that games really can't see the physical pad any more. If that check can't pass - or can't run - it falls back to shared-input mode instead of guessing, so a hidden pad with no stand-in won't leave you without a working controller."] =
            "**Mandos Xbox originales por Bluetooth**: también se aíslan, con una salvaguarda adicional. Antes de presentar el mando suplente, Radiata hace que un proceso auxiliar independiente **compruebe** que los juegos ya no pueden ver realmente el mando físico. Si esa comprobación no puede superarse (o no puede ejecutarse), recurre al modo de entrada compartida en lugar de suponerlo, de modo que un mando oculto sin suplente no te dejará sin un mando que funcione.",
        ["Isolation needs the drivers (ViGEm + HidHide). Without them every pad runs in shared-input mode (e.g. \"bleed-thru\"). Install from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**. Not sure which mode you're in? Hover the tray icon - see [[input-isolation|Input isolation]]."] =
            "El aislamiento necesita los controladores (ViGEm + HidHide). Sin ellos, todos los mandos funcionan en modo de entrada compartida (por ejemplo, \"bleed-thru\"). Instálalos desde **Ajustes ▸ Avanzado ▸ Solución de problemas ▸ Instalar/reparar controladores**. ¿No sabes en qué modo estás? Pasa el cursor sobre el icono de la bandeja; consulta [[input-isolation|Aislamiento de entrada]].",
        ["With the optional isolation drivers installed, Radiata gives games a **virtual controller** - a DualShock 4 for Sony pads, an Xbox 360 pad for Xbox pads or in Xbox Mode - and **cloaks the physical pad** (HidHide) so the game can't see it twice. That's when capture succeeds. Other remappers, existing device access and unsupported input paths can all sabotage full isolation, and without the drivers games keep seeing your controller while a wheel is up. This is how you get bleed-thru."] =
            "Con los controladores de aislamiento opcionales instalados, Radiata da a los juegos un **mando virtual** (un DualShock 4 para los mandos de Sony, un mando Xbox 360 para los mandos Xbox o en modo Xbox) y **oculta el mando físico** (HidHide) para que el juego no pueda verlo dos veces. Eso es cuando la captura funciona. Otros remapeadores, un acceso al dispositivo ya existente y las rutas de entrada no compatibles pueden sabotear el aislamiento completo, y sin los controladores los juegos siguen viendo tu mando mientras hay una rueda abierta. Así se produce el bleed-thru.",
        ["**Which mode am I actually in? Hover the tray icon.** It reads **\"Radiata - isolated\"**, or names the reason it isn't (no drivers, Passthru Mode, a pad that can't be cloaked). When capture drops to shared-input mode you also get an on-screen notice."] =
            "**¿En qué modo estoy realmente? Pasa el ratón sobre el icono de la bandeja.** Indica **\"Radiata - isolated\"** o nombra el motivo por el que no lo está (sin controladores, Modo directo, un mando que no puede ocultarse). Cuando la captura cae al modo de entrada compartida también recibes un aviso en pantalla.",
        ["**Global toggle:** the tray's checkable **Passthru Mode** item, or the checkbox on **Settings ▸ Passthru Mode**. The tab appears only when the isolation drivers are installed."] =
            "**Interruptor global:** el elemento marcable **Modo directo** de la bandeja, o la casilla de **Ajustes ▸ Modo directo**. La pestaña solo aparece cuando los controladores de aislamiento están instalados.",
        ["**Switch Passthru Mode on or off between play sessions, not mid-game.** Either direction swaps the controller a running game is reading - the virtual pad for your real one, or back - and most games don't go looking for a new controller once they've started, so the game loses input until it's relaunched."] =
            "**Activa o desactiva el Modo directo entre sesiones de juego, no a mitad de partida.** En cualquiera de los dos sentidos se cambia el mando que está leyendo un juego en marcha (el mando virtual por el real, o al revés) y la mayoría de los juegos no buscan un mando nuevo una vez iniciados, así que el juego se queda sin entrada hasta que se relanza.",
        ["**Settings tabs:** Left Wheel, Right Wheel, **Customize** (look, feel, sound, triggers, the [[volume-mixer|D-Pad 🡄 🡆]] picker and the [[show-labels|Show labels on]] picker), **Passthru Mode** ([[passthru-mode|Passthru Mode]] - shown when the isolation drivers are installed), **Advanced** (a **Current Controller** readout, [[integrations|Integrations]], [[game-grid-options|Game Grid]] housekeeping, [[accessibility|Accessibility]], **Start with Windows**, and [[system-actions|System tools]] incl. backup & reset and **Quit Radiata**), **Help**, and **About**. Settings auto-save; **Ctrl+S** forces a save."] =
            "**Pestañas de Ajustes:** Rueda izquierda, Rueda derecha, **Personalizar** (aspecto, comportamiento, sonido, disparadores, el selector de [[volume-mixer|Cruceta 🡄 🡆]] y el selector de [[show-labels|Mostrar etiquetas en]]), **Modo directo** ([[passthru-mode|Modo directo]], visible cuando los controladores de aislamiento están instalados), **Avanzado** (una lectura de **Mando actual**, [[integrations|Integraciones]], mantenimiento de la [[game-grid-options|Cuadrícula de juegos]], [[accessibility|Accesibilidad]], **Iniciar con Windows** y las [[system-actions|Herramientas del sistema]], incluidas copia de seguridad y restablecimiento y **Salir de Radiata**), **Ayuda** y **Acerca de**. Los ajustes se guardan solos; **Ctrl+S** fuerza el guardado.",
        ["**Language** - **Settings ▸ Advanced ▸ Language** sets the language for the whole app. Help language switches immediately. Everything else follows at the next launch."] =
            "**Idioma** - **Ajustes ▸ Avanzado ▸ Idioma** establece el idioma de toda la aplicación. El idioma de la Ayuda cambia al instante. Todo lo demás cambia en el siguiente inicio.",
        ["**Settings ▸ Customize** sets how the wheels look, feel and sound. Every pick applies **live**."] =
            "**Ajustes ▸ Personalizar** define el aspecto, el tacto y el sonido de las ruedas. Cada elección se aplica **en vivo**.",
        ["**Material** - the resting-slice look. Eight, in two groups. **Simple** contains solid-color **Flat Light** and **Flat Dark**; **Deluxe** contains the glassy **Pearl** and **Obsidian**, plus four styled looks:"] =
            "**Material**: el aspecto del sector en reposo. Son ocho, en dos grupos. **Sencillo** contiene los colores sólidos **Plano claro** y **Plano oscuro**; **Deluxe** contiene los acristalados **Perla** y **Obsidiana**, más cuatro estilos:",
        ["**Kawaii** - pastel wedges, each slice a different hue; firing bursts heart-and-star confetti."] =
            "**Kawaii**: sectores en pastel, cada uno de un tono distinto; al ejecutar, estalla confeti de corazones y estrellas.",
        ["**Salvage** - charcoal slices with a rusted-metal texture, fluorescent-light highlighting, and a stamped plate edge."] =
            "**Chatarra**: sectores color carbón con textura de metal oxidado, resaltado de luz fluorescente y un borde de placa estampada.",
        ["**Reactor** - dark hollow wedges; the armed slice lights an animated circuit-board of traces and sparks."] =
            "**Reactor**: sectores oscuros y huecos; el sector preparado enciende un circuito animado de pistas y chispas.",
        ["On the Kawaii sound set, arming a slice strikes the next note of a xylophone melody, so scrubbing around the ring plays the song. There are 5 melodies... you might recognize a few of them :)"] =
            "Con el conjunto de sonidos Kawaii, preparar un sector toca la siguiente nota de una melodía de xilófono, así que recorrer el anillo interpreta la canción. Hay 5 melodías... puede que reconozcas alguna :)",
        ["The first time Radiata sees a new **or changed** package it asks you to confirm before loading anything. Accepting will show it in **Settings ▸ Customize** in a third group, **Custom**, below Simple and Deluxe."] =
            "La primera vez que Radiata ve un paquete nuevo **o modificado** te pide confirmar antes de cargar nada. Al aceptar, aparecerá en **Ajustes ▸ Personalizar** en un tercer grupo, **Personalizado**, debajo de Simple y Deluxe.",
        ["A package is content from whoever wrote it. **Only install themes from a source you trust**. The confirmation prompt returns whenever any file in the package changes."] =
            "Un paquete es contenido de quien lo escribió. **Instala únicamente temas de una fuente en la que confíes**. El aviso de confirmación vuelve cada vez que cambia cualquier archivo del paquete.",
        ["Custom themes render through Radiata's **flat** slice paths with your colors substituted, so they can't reach the built-in styled materials' procedural effects (Kawaii's confetti, Reactor's circuit board)."] =
            "Los temas personalizados se dibujan con las rutas de sector **planas** de Radiata con tus colores sustituidos, así que no alcanzan los efectos procedurales de los materiales estilizados integrados (el confeti de Kawaii, la placa de circuito de Reactor).",
        ["Set `\"format\": 2` and add any of these optional blocks. Every number is clamped to a safe range, so an extreme value is pulled back rather than rejected."] =
            "Establece `\"format\": 2` y añade cualquiera de estos bloques opcionales. Cada número se limita a un intervalo seguro, así que un valor extremo se corrige en lugar de rechazarse.",
        ["Custom Arcade games - build your own!"] =
            "Juegos Arcade personalizados: ¡crea los tuyos!",
        ["`\"howTo\"` - optional, up to **5 lines of 80 characters**, which become the **{triangle}** help card. `{cross}` `{circle}` `{square}` `{triangle}` in a line are replaced with the player's own button glyphs. No lines means no help card."] =
            "`\"howTo\"`: opcional, hasta **5 líneas de 80 caracteres**, que se convierten en la tarjeta de ayuda de **{triangle}**. `{cross}` `{circle}` `{square}` `{triangle}` dentro de una línea se sustituyen por los glifos de botón del propio jugador. Sin líneas, no hay tarjeta de ayuda.",
        ["**`kvSet(\"key\", \"value\")`** and **`kvGet(\"key\")`** are the **only** state that survives closing a game. Contains strings only, keys up to 64 characters, values up to 1024, 4 KB per game in total. Everything else resets on dismiss, so design for it."] =
            "**`kvSet(\"key\", \"value\")`** y **`kvGet(\"key\")`** son el **único** estado que sobrevive al cerrar un juego. Solo admite cadenas, claves de hasta 64 caracteres, valores de hasta 1024, 4 KB por juego en total. Todo lo demás se reinicia al cerrar, así que diseña con eso en mente.",
        ["**Wheel ignores opposite stick** - normally **either** thumbstick aims (whichever you tilt further), and either stick's click opens [[edit-mode|edit mode]]. This option has each wheel listen to **one** stick only: the free hand's under Hold, the wheel's own side under Toggle. "] =
            "**La rueda ignora el stick contrario**: normalmente apunta **cualquiera** de los dos sticks (el que inclines más), y el clic de cualquiera de ellos abre el [[edit-mode|modo de edición]]. Con esta opción, cada rueda escucha **un solo** stick: el de la mano libre en Hold, el del propio lado de la rueda en Toggle.",
        ["**Reduce motion** - stops decorative movement everywhere. No confetti or sparks, no parallax, no zooming or drifting; wheels fade in in place, edit-mode rearranging is instant, and every hold-to-confirm effect becomes the same steady progress arc. Some Arcade features are automatically disabled. Progress meters, selection highlights and state readouts all stay. This setting respects Windows' own **Animation effects** switch as well."] =
            "**Reducir el movimiento**: detiene el movimiento decorativo en todas partes. Sin confeti ni chispas, sin paralaje, sin zoom ni desplazamientos; las ruedas aparecen fundiéndose en su sitio, la reordenación en modo de edición es instantánea y todos los efectos de mantener para confirmar se convierten en el mismo arco de progreso constante. Algunas funciones del Arcade se desactivan automáticamente. Los medidores de progreso, los resaltados de selección y las lecturas de estado se mantienen. Este ajuste también respeta el interruptor **Efectos de animación** de Windows.",
        ["**Always show hub** - off (the default), the wheel's centre hub appears only when it has something to show. On, the hub is always drawn and also shows the controller battery: a steadier centre to read. **Reduce motion** assumes you want this checked as well, but you can set them independently too."] =
            "**Mostrar siempre el centro**: desactivado (lo predeterminado), el centro de la rueda solo aparece cuando tiene algo que mostrar. Activado, el centro se dibuja siempre y muestra además la batería del mando: un centro más estable de leer. **Reducir el movimiento** supone que también quieres tenerlo activado, pero también puedes ajustarlos por separado.",
        ["**Icons, not Logos** (the default) - only slices showing one of Radiata's built-in icons are labelled. A slice carrying artwork (a game logo, cover art, or a PNG you added) goes unlabelled, assuming the artwork includes or replaces the name."] =
            "**Iconos, no logotipos** (predeterminado): solo se etiquetan los sectores que muestran uno de los iconos integrados de Radiata. Un sector con ilustración (un logotipo de juego, una portada o un PNG que hayas añadido) queda sin etiqueta, suponiendo que la ilustración incluye o sustituye el nombre.",
        ["**Slices I Choose** - each slice's own **Show Label** checkbox decides, and it's the only mode in which that checkbox appears in the slice editor (see [[editor-desktop|Slice editor tricks]]). Every slice is created with **Show Label** unchecked, so switching to this mode starts you from an unlabelled wheel: check the few slices you want named."] =
            "**Los sectores que yo elija**: decide la casilla **Mostrar la etiqueta** de cada sector, y es el único modo en el que esa casilla aparece en el editor de sectores (consulta [[editor-desktop|Trucos del editor de sectores]]). Todos los sectores se crean con **Mostrar la etiqueta** desmarcado, así que al cambiar a este modo partes de una rueda sin etiquetas: marca los pocos que quieras nombrar.",
        ["Radiata only ever **reads** Playnite's local database. Playnite does not need to be running."] =
            "Radiata solo **lee** la base de datos local de Playnite. No hace falta que Playnite esté en ejecución.",
        ["**Recover Controller** - the ↻ button beside the **Current Controller** name. This is a soft input reset for a wedged pad. If the pad stays silent afterwards, turn it off (hold its home button until the light goes out), turn it back on, and reconnect it — a controller whose input has frozen at the device only comes back from a power-cycle."] =
            "**Recover Controller** (recuperar mando): el botón ↻ junto al nombre de **Mando actual**. Es un reinicio suave de la entrada para un mando bloqueado. Si después el mando sigue sin responder, apágalo (mantén pulsado su botón de inicio hasta que se apague la luz), vuelve a encenderlo y conéctalo de nuevo: un mando cuya entrada se ha congelado en el propio dispositivo solo vuelve tras apagarlo y encenderlo.",
        ["**Battery** - beside the **Current Controller** heading, the same reading the wheel hub shows. PlayStation pads report a percentage (in 10% steps); Xbox-compatible pads only report four coarse levels. Nothing shows until the pad has reported a level."] =
            "**Batería**: junto al encabezado **Mando actual**, la misma lectura que muestra el centro de la rueda. Los mandos de PlayStation informan de un porcentaje (en pasos del 10 %); los mandos compatibles con Xbox solo informan de cuatro niveles aproximados. No aparece nada hasta que el mando ha informado de un nivel.",
        ["**Restore Settings…** - replaces settings and art picks only after validation and a successful save, then restarts Radiata. Automatic recovery backups are encrypted for your Windows account; restore them through this command. Exported ZIPs contain readable settings and artwork, but protected credentials may need re-entry on another account or PC."] =
            "**Restaurar los ajustes…**: sustituye los ajustes y las carátulas elegidas solo tras validarlos y guardarlos correctamente, y después reinicia Radiata. Las copias de seguridad de recuperación automáticas están cifradas para tu cuenta de Windows; restáuralas con este comando. Los ZIP exportados contienen ajustes e ilustraciones legibles, pero puede que las credenciales protegidas haya que volver a introducirlas en otra cuenta u otro PC.",
        ["**Wipe App Data and Reset…** - deletes everything in `%APPDATA%\\Radiata`, including automatic backups. Only backups saved outside that folder survive. Export a settings ZIP first if you want. Radiata restarts after a successful reset."] =
            "**Borrar los datos de la aplicación y restablecer…**: elimina todo lo que hay en `%APPDATA%\\Radiata`, incluidas las copias de seguridad automáticas. Solo sobreviven las copias guardadas fuera de esa carpeta. Exporta antes un ZIP de ajustes si quieres. Radiata se reinicia tras un restablecimiento correcto.",
        ["**Settings ▸ Advanced** shows a ↻ button beside the Current Controller name. This does a soft input reset - drops and reopens the HID stream - without restarting Radiata."] =
            "**Ajustes ▸ Avanzado** muestra un botón ↻ junto al nombre del Mando actual. Hace un reinicio suave de la entrada (cierra y vuelve a abrir el flujo HID) sin reiniciar Radiata.",
    };
}
