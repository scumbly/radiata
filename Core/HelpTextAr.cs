namespace ControllerWheel;

/// <summary>ARABIC Help-tab strings. Keys are the EXACT English text authored in
/// <see cref="HelpContent"/> — copy the C# literal across unchanged (escapes included) when adding an
/// entry, and let anything not listed here fall through to English. Run
/// <c>Radiata.exe --check-help-locales</c> after editing HelpContent.cs to see what needs work, and
/// <c>Radiata.exe --dump-help-locale ar</c> to regenerate this file in source order.
/// Conventions (see docs/LOCALIZATION.md): a UI path or label reads in this language, bold, with no English gloss
/// (tools/help-flip.pl applies this; the notice sends support-seekers to English instead); markup (<c>**</c>,
/// <c>`</c>, <c>[[id|label]]</c>, <c>[label](url)</c>) and <c>{tokens}</c> are
/// preserved verbatim, a cross-link's topic id is never translated, and product names stay as they are.
/// <para>Entry ORDER follows <c>HelpLocalization.SourceStrings()</c> — the notice, the Help-pane chrome,
/// the category names, then each topic's title/keywords/blocks, then the figure labels.</para></summary>
internal static class HelpTextAr
{
    internal static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {

        // ── notice ──
        [HelpLocalization.NoticeKey] =
            "**ترجمت صفحات المساعدة هذه وواجهة Radiata بواسطة نموذج لغوي بالذكاء الاصطناعي.** قد تكون الترجمة غير دقيقة أو غير كاملة. لا يتحمل المطوّر مسؤولية الأخطاء في النص المترجم؛ النسخة الإنجليزية هي المعتمدة. لا يمكن الرد على طلبات الدعم إلا بالإنجليزية؛ ولن تتلقى الرسائل بلغات أخرى ردًا. يذكر رد الدعم الأزرار وعلامات التبويب والإعدادات بأسمائها الإنجليزية؛ بدّل Radiata إلى الإنجليزية لبرهة لتتبعه.",

        // ── chrome ──
        ["Contents"] =
            "المحتويات",
        ["No topics match."] =
            "لا توجد مواضيع مطابقة.",
        ["Language"] =
            "اللغة",
        ["Search help topics"] =
            "البحث في مواضيع المساعدة",
        ["Help topics language"] =
            "لغة مواضيع المساعدة",
        ["Back to where you were"] =
            "الرجوع إلى حيث كنت",
        ["Open this Help topic"] =
            "افتح موضوع المساعدة هذا",

        // ── category ──
        ["Welcome"] =
            "مرحبًا",
        ["Getting Around"] =
            "التنقل",
        ["Editing Wheels"] =
            "تحرير العجلات",
        ["Game Grid"] =
            "شبكة الألعاب",
        ["Actions"] =
            "الإجراءات",
        ["Controllers & Isolation"] =
            "أجهزة التحكم والعزل",
        ["Tray & Settings"] =
            "شريط النظام والإعدادات",
        ["Workshop"] =
            "الورشة",
        ["Troubleshooting"] =
            "استكشاف الأخطاء وإصلاحها",

        // ── topic:intro ──
        ["What is Radiata?"] =
            "ما هو Radiata؟",
        ["intro welcome about purpose design overview couch overlay start here"] =
            "مقدمة ترحيب حول الغرض التصميم نظرة عامة أريكة تراكب ابدأ هنا",
        ["**Configurable:** each slice's action, icon, color and position, the summon chord, the look, the sounds. Edit at the desk in Settings, or from the couch in the in-wheel editor."] =
            "**قابل للتخصيص:** إجراء كل شريحة وأيقونتها ولونها وموضعها، وتشكيلة الاستدعاء، والمظهر، والأصوات. حرّر من الإعدادات على المكتب، أو من الأريكة عبر المحرر داخل العجلة.",

        // ── topic:installing ──
        ["Installing Radiata"] =
            "تثبيت Radiata",
        ["install installing installer setup download smartscreen windows protected your pc unknown publisher unsigned signature certificate antivirus false positive virus admin administrator uac elevation drivers vigem hidhide requirements windows 10 11 x64 arm browser blocked keep discard first run update uninstall remove"] =
            "تثبيت تنصيب مثبّت إعداد تنزيل smartscreen حمى windows جهازك ناشر غير معروف غير موقّع توقيع شهادة مكافحة الفيروسات إنذار كاذب فيروس مسؤول uac رفع الصلاحيات برامج التشغيل vigem hidhide المتطلبات windows 10 11 x64 arm المتصفح محظور احتفاظ تجاهل التشغيل الأول تحديث إلغاء التثبيت إزالة",
        ["What you need"] =
            "ما تحتاج إليه",
        ["**Windows 10 or 11, 64-bit (x64)** on an Intel or AMD PC. Windows on ARM isn't supported."] =
            "**Windows 10 أو 11، 64 بت (x64)** على جهاز كمبيوتر بمعالج Intel أو AMD. لا يدعم Windows على معالجات ARM.",
        ["A supported controller - see [[supported-controllers|Supported controllers]]."] =
            "جهاز تحكم مدعوم - راجع [[supported-controllers|أجهزة التحكم المدعومة]].",
        ["\"Windows protected your PC\""] =
            "\"حمى Windows جهازك\"",
        ["**In the browser:** if the download itself is blocked, keep it (Chrome and Edge: the **⋯** menu beside the download ▸ **Keep** ▸ **Show more** ▸ **Keep anyway**)."] =
            "**في المتصفح:** إذا حظر التنزيل نفسه، احتفظ به (Chrome وEdge: قائمة **⋯** بجانب التنزيل ◂ **Keep** ▸ **Show more** ▸ **Keep anyway**).",
        ["**At the SmartScreen box:** click **More info**, then the **Run anyway** button that appears below it. If there's no **More info** link you might be seeing your browser's warning instead. See the previous step."] =
            "**عند مربع SmartScreen:** اضغط **مزيد من المعلومات**، ثم زر **التشغيل على أي حال** الذي يظهر تحته. إن لم يكن هناك رابط **مزيد من المعلومات** فربما ترى تحذير المتصفح بدلًا من ذلك. راجع الخطوة السابقة.",
        ["What the installer does"] =
            "ما يفعله المثبّت",
        ["First run"] =
            "التشغيل الأول",
        ["Setup opens by itself and walks you through the drivers, a controller check, the look, cover art, and a set of starter wheels. Re-run it any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Run First-Run Setup…** - see [[system-actions|System tools]]."] =
            "يفتح الإعداد من تلقاء نفسه ويرشدك عبر برامج التشغيل، وفحص جهاز التحكم، والمظهر، وصور الأغلفة، ومجموعة من العجلات المبدئية. أعد تشغيله في أي وقت من **الإعدادات ◂ متقدم ◂ استكشاف الأخطاء وإصلاحها ◂ تشغيل إعداد التشغيل الأول…** - راجع [[system-actions|أدوات النظام]].",
        ["Updating and uninstalling"] =
            "التحديث وإلغاء التثبيت",

        // ── topic:opening-a-wheel ──
        ["Opening a wheel"] =
            "فتح عجلة",
        ["summon invoke trigger chord fn bumper touchpad swipe hold toggle swap sides activation flip left right"] =
            "استدعاء تشغيل مشغّل مجموعة أزرار fn مصد لوحة اللمس سحب ضغط مطوّل تبديل تبادل الجانبين تنشيط قلب يسار يمين",
        ["Hold the bumper/trigger; the second button is a **tap** that brings the wheel up."] =
            "اضغط مطوّلًا على المصد/الزناد؛ الزر الثاني **نقرة** تظهر العجلة.",
        ["**Fn / L4/R4 and squeeze chords** (Bumper+Trigger, +Home, +Select/Start) - the hand you squeeze opens the **opposite** wheel, so the free hand aims (R1+R2 → Left; L1+L2 → Right). For Select/Start, either button works."] =
            "**Fn / L4/R4 وتشكيلات الضغط** (المصد+الزناد، +Home، +Select/Start) - اليد التي تضغط بها تفتح العجلة **المقابلة**، فتصوّب باليد الحرة (R1+R2 ← اليسرى؛ L1+L2 ← اليمنى). أما Select/Start فيعمل أي من الزرين.",
        ["**L3/R3** and **D-Pad L/R** - the clicked stick or the direction determines which wheel; either bumper/trigger is the hold."] =
            "**L3/R3** و**أزرار الاتجاهات يمين/يسار** - العصا المضغوطة أو الاتجاه يحدد أي عجلة تفتح؛ وأي مصد أو زناد يصلح للضغط المستمر.",
        ["**Touchpad swipe** - in from the left edge → Left wheel, right edge → Right."] =
            "**سحب لوحة اللمس** - من الحافة اليسرى إلى الداخل ← العجلة اليسرى، ومن الحافة اليمنى ← اليمنى.",
        ["**Swap left/right** ([[accessibility|Accessibility setting]]) reverses all of these."] =
            "**تبديل اليسار/اليمين** ([[accessibility|إعداد إمكانية الوصول]]) يعكس كل ما سبق.",
        ["**Activation** is **Hold** (up while held; release fires) or **Toggle** (trigger opens; **{cross} confirms, {circle} cancels**; re-trigger dismisses) - set it in [[accessibility|Accessibility]]. Touchpad swipe is always toggle-style."] =
            "**التنشيط** إما **استمرار الضغط** (تبقى مفتوحة أثناء الضغط؛ الإفلات ينفّذ) أو **Toggle** (المشغّل يفتح؛ **{cross} يؤكد، {circle} يلغي**؛ إعادة التشغيل تغلق) - اضبطه في [[accessibility|إمكانية الوصول]]. سحب لوحة اللمس دائمًا بأسلوب التبديل.",

        // ── topic:picking-an-action ──
        ["Aiming & firing"] =
            "التصويب والتنفيذ",
        ["aim arm fire cancel release deadzone sticky esc escape keyboard hub center state toggle mute hdr configure guard confirm dwell sleep reboot shutdown"] =
            "تصويب تسليح تنفيذ إلغاء إفلات منطقة ميتة لاصق esc خروج لوحة المفاتيح محور مركز حالة تبديل كتم hdr ضبط حماية تأكيد مكوث سكون إعادة تشغيل إيقاف",
        ["Center hub"] =
            "المحور المركزي",
        ["Arming a **toggle** slice (mic/volume mute, HDR, process toggle) shows its **current state** before you fire, e.g. `Mute Mic / Unmuted`. After firing, the hub shows the new state."] =
            "عند تحديد شريحة **تبديل** (كتم الميكروفون أو الصوت، HDR، تبديل عملية) تظهر **حالتها الحالية** قبل التنفيذ، مثل `Mute Mic / Unmuted`. وبعد التنفيذ يعرض المحور الحالة الجديدة.",
        ["Hold-to-confirm slices"] =
            "شرائح الضغط المطوّل للتأكيد",

        // ── topic:wheel-open-extras ──
        ["While a wheel is open"] =
            "أثناء فتح العجلة",
        ["volume dpad scrub repeat mic microphone alt-tab window switcher desktop song enable disable chord toggle wheels off keyboard arrow esc"] =
            "مستوى الصوت dpad تمرير تكرار ميكروفون alt-tab مبدّل النوافذ سطح المكتب أغنية تفعيل تعطيل مجموعة أزرار تبديل العجلات إيقاف لوحة المفاتيح سهم esc",
        ["**Enable / disable the wheels** with the \"both sides\" of your invocation chord, pressed **together**:"] =
            "**فعّل / عطّل العجلات** بـ \"الجانبين\" معًا من مجموعة أزرار الاستدعاء، مضغوطين **في الوقت نفسه**:",
        ["**Bumper/Trigger + D-Pad** is directional: **D-Pad Up = enable**, **D-Pad Down = disable**."] =
            "**Bumper/Trigger + D-Pad** اتجاهي: **D-Pad Up = تفعيل**، **D-Pad Down = تعطيل**.",

        // ── topic:edit-mode ──
        ["Edit mode (in-wheel, controller-only)"] =
            "وضع التحرير (داخل العجلة، بجهاز التحكم فقط)",
        ["edit stick click move add delete reorder undo redo picker installed game full capacity 12 limit thickness"] =
            "تحرير نقر العصا نقل إضافة حذف إعادة ترتيب تراجع إعادة منتقي لعبة مثبّتة ممتلئ سعة 12 حد سمك",
        ["**Add:** **{triangle}** opens the category→type **Add picker** ({cross} drills in, {circle} backs out). **Installed Game** opens the [[game-grid|Game Grid]] to pick a game, then returns to edit carrying the slice. Other types drop a slice immediately; free-text types (raw URL / keypress) land as placeholders you finish in Settings."] =
            "**الإضافة:** **{triangle}** يفتح **منتقي الإضافة** فئة←نوع ({cross} يتعمّق، {circle} يعود). **لعبة مثبتة** يفتح [[game-grid|شبكة الألعاب]] لاختيار لعبة، ثم يعود إلى التحرير حاملًا الشريحة. الأنواع الأخرى تضيف شريحة فورًا؛ أنواع النص الحر (رابط خام / ضغطة مفاتيح) تضاف كعناصر نائبة تكملها في الإعدادات.",

        // ── topic:empty-wheel ──
        ["Single-wheel mode"] =
            "وضع العجلة الواحدة",
        ["empty disabled free gesture resurrect rebuild"] =
            "فارغة معطّلة إيماءة حرة إحياء إعادة بناء",
        ["Emptying one wheel turns its side off: the chord that used to open it passes through to the game untouched."] =
            "إفراغ عجلة يوقف جانبها: تمر مجموعة الأزرار التي كانت تفتحها إلى اللعبة دون مساس.",
        ["To bring it back: **invoke it (hold the gesture) and click the aiming stick (L3/R3)**. The wheel opens centered, straight into the Add picker. (Toggle-style gestures have no hold, so they allow the stick click for a few seconds after the invoke.)"] =
            "لاستعادتها: **استدع العجلة (مع الاستمرار على الإيماءة) واضغط عصا التصويب (L3/R3)**. تفتح العجلة في المنتصف وتدخل مباشرة إلى منتقي الإضافة. (الإيماءات من نوع التبديل ليس فيها ضغط مستمر، لذا تقبل ضغط العصا لبضع ثوانٍ بعد الاستدعاء.)",

        // ── topic:arcade-direct-launch ──
        ["Arcade direct-launch (a wheel that is just the Arcade)"] =
            "تشغيل الأركيد المباشر (عجلة ليس فيها سوى الأركيد)",
        ["arcade direct launch shortcut lone only one single slice launcher skip wheel straight cabinets instant gesture dedicated side"] =
            "أركيد تشغيل مباشر اختصار وحيدة شريحة واحدة مشغّل تخطي عجلة مباشرة أجهزة فورية إيماءة مخصص جانب",
        ["**The arcade opens where that wheel would have been** - the left or right quarter of the screen, the same spot the wheel uses. **{circle}** closes it as usual."] =
            "**يفتح الأركيد حيث كانت ستظهر تلك العجلة** - في الربع الأيسر أو الأيمن من الشاشة، وهو الموضع نفسه الذي تستخدمه العجلة. و**{circle}** يغلقه كالمعتاد.",

        // ── topic:editor-desktop ──
        ["Slice editor tricks (Settings, mouse & keyboard)"] =
            "حيل محرر الشرائح (الإعدادات، بالفأرة ولوحة المفاتيح)",
        ["drag drop exe lnk shortcut launch slice reorder icon color color ctrl+s save autosave draft revert logo arrows cycle fetch steamgriddb"] =
            "سحب إفلات exe lnk اختصار تشغيل شريحة إعادة ترتيب أيقونة لون ctrl+s حفظ حفظ تلقائي مسودة تراجع شعار أسهم تدوير جلب steamgriddb",
        ["**Drag a slice within the list** to reorder."] =
            "**اسحب شريحة داخل القائمة** لإعادة الترتيب.",
        ["**Set a slice's icon and color** in the Icon & Color panel below the label."] =
            "**اضبط أيقونة الشريحة ولونها** في لوحة Icon & Color أسفل التسمية.",
        ["**Show Label** toggles the slice's text on the wheel. It appears only when [[show-labels|Show labels on]] is set to **Slices I Choose**."] =
            "**إظهار التسمية** يبدّل نص الشريحة على العجلة. لا يظهر إلا عندما يكون [[show-labels|إظهار التسميات على]] مضبوطًا على **الشرائح التي أختارها**.",
        ["Artwork on a slice"] =
            "عمل فني على شريحة",
        ["**A slice's action type locks once you Save it.** To change it, delete the slice and add a new one."] =
            "**يثبت نوع إجراء الشريحة بمجرد حفظها.** لتغييره، احذف الشريحة وأضف واحدة جديدة.",

        // ── topic:game-grid ──
        ["Game Grid basics"] =
            "أساسيات شبكة الألعاب",
        ["game grid browser launch navigate filter storefront chips footer add wheel pick mode installed assign favorite"] =
            "شبكة الألعاب متصفح تشغيل تنقل تصفية متجر رقائق تذييل إضافة عجلة وضع الاختيار مثبّتة تعيين مفضلة",
        ["The Game Grid is a controller-scrollable view of every installed game across your storefronts, sorted **most-recently-launched first**, favorites pinned on top. The button controls are spelled out along the bottom of the grid."] =
            "شبكة الألعاب عرض يمكن تمريره بجهاز التحكم لكل لعبة مثبّتة عبر متاجرك، مرتبة حسب **الأحدث تشغيلًا**، مع تثبيت المفضلة في الأعلى. وعناصر التحكم بالأزرار موضحة على طول أسفل الشبكة.",
        ["**D-Pad / arrow keys** or the **left stick** move the selection."] =
            "**D-Pad / مفاتيح الأسهم** أو **العصا اليسرى** تنقل التحديد.",
        ["**{cross} / Enter** or a mouse double-click - launch the selected game. **{circle} / Esc** - close the grid."] =
            "**{cross} / Enter** أو نقرة مزدوجة بالفأرة - تشغّل اللعبة المحددة. **{circle} / Esc** - يغلق الشبكة.",
        ["**{triangle}** - **favorite** the selected game. Favorites sit in their own row of larger tiles at the top of every view. Press again to un-favorite."] =
            "**{triangle}** - يجعل اللعبة المحددة **مفضلة**. تظهر المفضلة في صف خاص بها من بطاقات أكبر أعلى كل عرض. اضغط مرة أخرى لإلغاء التفضيل.",
        ["**Hold {square}** - **hide the game from the grid**. Bring hidden games back with **Settings ▸ Advanced ▸ Game Grid ▸ Reset Hidden Games**. The same hold on a storefront's **Open <store>** card hides that whole store - see [[storefronts|Hiding a storefront]]."] =
            "**اضغط مطوّلًا على {square}** - **لإخفاء اللعبة من الشبكة**. أعد الألعاب المخفية بـ **الإعدادات ◂ متقدم ◂ شبكة الألعاب ◂ إعادة ضبط الألعاب المخفية**. الضغط المطوّل نفسه على بطاقة **Open <store>** لمتجر يخفي ذلك المتجر بكامله - راجع [[storefronts|إخفاء متجر]].",
        ["**L1 / R1** (or **PgUp / PgDn**) - cycle the storefront filter. A chip appears for each store you have installed."] =
            "**L1 / R1** (أو **PgUp / PgDn**) - يتنقل بين مرشّحات المتاجر. تظهر رقاقة لكل متجر مثبّت لديك.",
        ["**Select** and **Start** - cycle the selected game's **cover** and **logo**; see [[cover-art|Cover art & logos]]."] =
            "**تحديد** و**Start** - يبدّلان **غلاف** اللعبة المحددة و**شعارها**؛ راجع [[cover-art|صور الأغلفة والشعارات]].",
        ["Add a game to a wheel"] =
            "إضافة لعبة إلى عجلة",
        ["Add games from the **in-wheel editor**: open a wheel → click the aiming stick to enter [[edit-mode|Edit]] → **{triangle} Add → Installed Game**, which opens the grid in **pick mode**. **{cross}** picks the highlighted game and carries it into the editor ({circle} cancels)."] =
            "أضف الألعاب من **المحرر داخل العجلة**: افتح عجلة ← انقر عصا التصويب للدخول إلى [[edit-mode|التحرير]] ← **{triangle} إضافة ← لعبة مثبتة**، فيفتح الشبكة في **وضع الاختيار**. **{cross}** يختار اللعبة المضاءة ويحملها إلى المحرر ({circle} يلغي).",

        // ── topic:cover-art ──
        ["Cover art & logos"] =
            "صور الأغلفة والشعارات",
        ["cover art logo cycle select start share options steamgriddb sgdb dots spinner flat colors"] =
            "غلاف فن شعار تدوير select start مشاركة خيارات steamgriddb sgdb نقاط مؤشر انتظار ألوان مسطحة",
        ["**Start** (Options/Menu) - **cycle the logo overlay** and save it: default logo → up to 2 SteamGridDB alternates → **off** (raw cover) → wrap. A game with no logo art uses its centered title text."] =
            "**Start** (Options/Menu) - **يبدّل تراكب الشعار** ويحفظه: الشعار الافتراضي ← حتى بديلين من SteamGridDB ← **إيقاف** (الغلاف الخام) ← ثم يعود. اللعبة التي لا فن شعار لها تستخدم نص عنوانها في المنتصف.",
        ["**Art is fetched once and kept on disk**, so the grid opens instantly and offline after that. A big library fills in over the first few seconds of browsing. Reopen the grid and the stragglers should be there."] =
            "**يجلب الفن مرة واحدة ويحفظ على القرص**، فتفتح الشبكة فورًا وبلا اتصال بعد ذلك. تمتلئ المكتبة الكبيرة خلال الثواني الأولى من التصفح. أعد فتح الشبكة وستكون البقية موجودة.",
        ["**A game with no art found is re-checked every couple of weeks** on its own, since art gets added over time. To recheck now, use **Retry Missing Game Art** ([[game-grid-options|Advanced ▸ Game Grid]])."] =
            "**تعاد مراجعة اللعبة التي لم يعثر لها على صور كل أسبوعين تلقائيًا**، لأن الصور تضاف مع الوقت. للمراجعة الآن، استخدم **إعادة محاولة صور الألعاب الناقصة** ([[game-grid-options|متقدم ◂ شبكة الألعاب]]).",

        // ── topic:action-types ──
        ["Action types"] =
            "أنواع الإجراءات",
        ["actions advanced launch keypress key combo volume audio display hdr sleep discord voice text chat url settings xbox mode obs mixer game bar windows"] =
            "إجراءات متقدم تشغيل ضغطة مفاتيح مجموعة مفاتيح مستوى الصوت صوت شاشة hdr سكون discord صوتي محادثة نصية رابط إعدادات وضع xbox obs مازج game bar windows",
        ["Each slice runs one action, grouped by the editor's categories:"] =
            "تنفّذ كل شريحة إجراءً واحدًا، مجمّعًا حسب فئات المحرر:",
        ["**Games & Apps** - installed game (direct launch), the Game Grid, storefront launcher (big-picture), launch/focus an app, **Exit Current App** (closes the frontmost app), and **Game Bar** (open, screenshot, start/stop recording, record the last 30 s, toggle mic - via Windows' Xbox Game Bar)."] =
            "**الألعاب والتطبيقات** - لعبة مثبّتة (تشغيل مباشر)، وشبكة الألعاب، ومشغّل المتجر (وضع الشاشة الكبيرة)، وتشغيل تطبيق أو التركيز عليه، و**إنهاء التطبيق الحالي** (يغلق التطبيق الأمامي)، و**Game Bar** (فتح، لقطة شاشة، بدء التسجيل وإيقافه، تسجيل آخر 30 ثانية، تبديل الميكروفون) عبر Xbox Game Bar في Windows.",
        ["**Chat & Streaming** - Discord (launch, join/leave a voice channel, deafen), Steam Chat (open chat) - see [[steam-xbox-voice|Steam voice chat]] - plus **Mic Mute** (the one Windows mic mute, offered in both groups and under System ▸ Audio), [[text-chat|Text Chat]], and **OBS Studio** ([[obs-studio|streaming, recording, replay, scenes, source mute]])."] =
            "**الدردشة والبث**: Discord (التشغيل، والانضمام إلى قناة صوتية أو مغادرتها، وكتم السماعات)، ودردشة Steam (فتح الدردشة)، انظر [[steam-xbox-voice|دردشة Steam الصوتية]]، إضافة إلى **كتم الميكروفون** (كتم ميكروفون Windows الوحيد، ويتوفر في المجموعتين وضمن النظام ◂ الصوت)، و[[text-chat|الدردشة النصية]]، و**OBS Studio** ([[obs-studio|البث والتسجيل والإعادة والمشاهد وكتم المصدر]]).",
        ["**System** - [[controller-mode|controller mode]] (**Xbox** / **DualShock**); audio (switch output, mute, mic mute, set volume, play/pause, next/previous); display (**Toggle Extend/Clone** and HDR toggle); **Windows** (**Show Desktop** - fire again to put the windows back - and **Empty Recycle Bin**); power (sleep/hibernate/reboot/shut down/log out/lock, and **Power Plan**, which flips between two plans you pick)."] =
            "**النظام** - [[controller-mode|وضع جهاز التحكم]] (**Xbox** / **DualShock**)؛ الصوت (تبديل الإخراج، كتم، كتم الميكروفون، ضبط مستوى الصوت، تشغيل/إيقاف مؤقت، التالي/السابق)؛ الشاشة (**تبديل التوسيع/التكرار** وتبديل HDR)؛ **Windows** (**إظهار سطح المكتب** - نفّذها مجددًا لإعادة النوافذ - و**إفراغ سلة المحذوفات**)؛ الطاقة (سكون/إسبات/إعادة تشغيل/إيقاف/تسجيل خروج/قفل، و**خطة الطاقة** الذي يبدّل بين خطتين تختارهما).",
        ["**Reboot** has a **Log In after Reboot** checkbox: checked (the default), Windows signs you back in where policy allows. Unchecked is a traditional restart, which can land at the sign-in screen."] =
            "في **إعادة التشغيل** مربع اختيار **تسجيل الدخول بعد إعادة التشغيل**: إن كان محددًا (وهو الافتراضي) أعاد Windows تسجيل دخولك حيثما تسمح السياسات. وإن كان غير محدد فهي إعادة تشغيل تقليدية قد تنتهي عند شاشة تسجيل الدخول.",
        ["**Custom** - [[open-uri|Open URI]] and [[key-combo|Key Combo]] (send a keyboard shortcut like `Win+D` or `PlayPause`)."] =
            "**مخصصة** - [[open-uri|فتح URI]] و[[key-combo|اختصار لوحة المفاتيح]] (إرسال اختصار لوحة مفاتيح مثل `Win+D` أو `PlayPause`).",
        ["**Radiata** - the Game Grid, open Settings, and disable wheels. [[passthru-mode|Passthru Mode]] is not a slice: turn it on or off from the tray or Settings ▸ Passthru Mode."] =
            "**Radiata** - شبكة الألعاب، وفتح الإعدادات، وتعطيل العجلات. [[passthru-mode|الوضع المباشر]] ليس شريحة: شغّله أو أوقفه من شريط النظام أو من الإعدادات ◂ الوضع المباشر.",
        ["A slice missing a required value arms as **\"Configure in Settings\"**. Firing it opens that slice's editor."] =
            "الشريحة التي تفتقد قيمة مطلوبة تسلّح بوصف **\"اضبطه في الإعدادات\"**. تنفيذها يفتح محرر تلك الشريحة.",

        // ── topic:arcade ──
        ["Arcade"] =
            "أركيد",
        ["arcade game games minigame mini-game kabloom connate petal pop twist breakout paddle brick pentagon square hexagon octagon smash win minesweeper merge bee flower bomb picker play waiting loading queue lobby kill time score"] =
            "أركيد لعبة ألعاب لعبة صغيرة kabloom connate petal pop التواء كسر الطوب مضرب طوبة خماسي مربع سداسي ثماني ضربة ساحقة فوز كاسحة ألغام دمج نحلة زهرة قنبلة منتقي لعب انتظار تحميل طابور ردهة تمضية الوقت نتيجة",
        ["**{cross}** acts, **{square}** is each game's second action, and the **left stick** aims. The **D-Pad** drives the menus. **{triangle} explains the game you're in**, and **START pauses** with **Resume**, **How to play**, **Reset**, and that game's own settings."] =
            "**{cross}** ينفّذ، و**{square}** هو الإجراء الثاني لكل لعبة، و**العصا اليسرى** تصوّب. **D-Pad** يتحكم في القوائم. **{triangle} يشرح اللعبة التي أنت فيها**، و**START يوقف مؤقتًا** مع **متابعة** و**طريقة اللعب** و**إعادة** وإعدادات تلك اللعبة الخاصة.",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] and [[arcade-internode|Internode]]."] =
            "**لكل لعبة صفحتها الخاصة**: [[arcade-kabloom|Kabloom]] و[[arcade-connate|Connate]] و[[arcade-petalpop|Petalpop]] و[[arcade-internode|Internode]].",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] and [[arcade-petalpop|Petalpop]]."] =
            "**لكل لعبة صفحتها الخاصة** - [[arcade-kabloom|Kabloom]] و[[arcade-connate|Connate]] و[[arcade-petalpop|Petalpop]].",
        ["**You can write your own games** and drop them in - see [[custom-arcade-games|Custom Arcade games]]."] =
            "**يمكنك كتابة ألعابك الخاصة** وإضافتها - راجع [[custom-arcade-games|ألعاب Arcade المخصصة]].",

        // ── topic:arcade-kabloom ──
        ["Arcade: Kabloom"] =
            "أركيد: Kabloom",
        ["kabloom arcade minesweeper petal petals flower tile disc bee flag mark question mark cursor reveal solvable no guessing guess solver proof certified baked stacked gem diamond level campaign"] =
            "kabloom أركيد كانسة الألغام بتلة بتلات زهرة بلاطة قرص نحلة علم علامة علامة استفهام مؤشر كشف قابل للحل بلا تخمين تخمين حلال برهان موثق محضر مسبقا متراكم جوهرة ماسة مستوى حملة",
        ["Part of the [[arcade|Arcade]]."] =
            "جزء من [[arcade|الأركيد]].",

        // ── topic:arcade-connate ──
        ["Arcade: Connate"] =
            "أركيد: Connate",
        ["connate arcade merge merging numbers number cluster pile rim ring colors colours families bomb charge fire lob orb doubling"] =
            "connate أركيد دمج أرقام رقم عنقود كومة حافة حلقة ألوان عائلات قنبلة شحن إطلاق رمي كرة مضاعفة",

        // ── topic:arcade-petalpop ──
        ["Arcade: Petalpop"] =
            "أركيد: Petalpop",
        ["petal pop petalpop arcade paddle paddles breakout brick bricks ball flower core gold split multiball smash slingshot spring rail square pentagon hexagon heptagon octagon lives win 5-8"] =
            "petal pop petalpop أركيد مضرب مضارب كسر الطوب طوبة طوب كرة زهرة قلب ذهبي انقسام كرات متعددة ضربة مقلاع نابض مسار مربع خماسي سداسي سباعي ثماني أرواح فوز من 5 إلى 8",

        // ── topic:arcade-internode ──
        ["Arcade: Internode"] =
            "أركيد: Internode",
        ["internode arcade half-pipe halfpipe pipe bike runner token tokens mine mines jump hop gate gold gate quota checkpoint stage bank score camera roll rim wall swing gap break fall"] =
            "internode أركيد نصف أنبوب أنبوب دراجة نارية عداء رمز رموز لغم ألغام قفز وثب بوابة بوابة ذهبية حصة نقطة تفتيش مرحلة إيداع نتيجة كاميرا دوران حافة جدار تأرجح فجوة كسر سقوط",

        // ── topic:app-slices ──
        ["App & launcher slices"] =
            "شرائح التطبيقات والمشغّلات",
        ["launch focus toggle kill process name override executable path storefront big picture installed apps store uwp"] =
            "تشغيل تركيز تبديل إنهاء عملية اسم تجاوز ملف تنفيذي مسار متجر صورة كبيرة تطبيقات مثبّتة store uwp",
        ["**Behavior** - **Run** launches the app, or focuses it if it's already running. **Toggle** launches the app or requests a normal close, like clicking its ✕. The hub reports **Close requested**, not a confirmed exit: unsaved-work prompts stay open until you answer them, apps may refuse to close or keep running in the tray, and Toggle leaves windowless processes running."] =
            "**السلوك** - **تشغيل** يشغّل التطبيق، أو يركّز عليه إن كان يعمل بالفعل. و**تبديل** يشغّل التطبيق أو يطلب إغلاقًا عاديًا، كالضغط على ✕. ويفيد المحور بـ**طلب الإغلاق** لا بخروج مؤكد: تبقى مربعات العمل غير المحفوظ مفتوحة حتى تجيب عنها، وقد ترفض التطبيقات الإغلاق أو تظل تعمل في شريط النظام، ويترك التبديل العمليات التي بلا نوافذ تعمل.",
        ["**Storefront** (launcher slices) - opens the store's big-picture/fullscreen mode. Only Steam and Playnite have a true one; the Xbox app is maximized; the rest just open. A storefront is offered only when its launcher app is actually installed."] =
            "**المتجر** (شرائح المشغّلات) - يفتح وضع الصورة الكبيرة/ملء الشاشة للمتجر. Steam وPlaynite وحدهما لديهما وضع حقيقي؛ يكبّر تطبيق Xbox؛ والبقية تفتح فقط. لا يعرض المتجر إلا إذا كان تطبيق مشغّله مثبّتًا فعلًا.",

        // ── topic:controller-mode ──
        ["Controller mode (Xbox / DualShock)"] =
            "وضع جهاز التحكم (Xbox / DualShock)",
        ["controller mode xbox dualshock emulation virtual pad game pass xinput button prompts glyphs playstation switch pad type unsupported controller"] =
            "وضع جهاز التحكم xbox dualshock محاكاة وحدة افتراضية game pass xinput مطالبات الأزرار رموز playstation تبديل نوع الجهاز جهاز تحكم غير مدعوم",
        ["Arming the slice shows **On** or **Off** in the hub, so you can check which mode you're in without changing it."] =
            "تسليح الشريحة يعرض **مفعّل** أو **معطّل** في المحور، فتتحقق من الوضع الذي أنت فيه دون تغييره.",

        // ── topic:switch-audio ──
        ["Switch Audio Output"] =
            "تبديل مخرج الصوت",
        ["switch audio output device mic microphone speakers headset cycle default endpoint"] =
            "تبديل إخراج الصوت جهاز ميكروفون سماعات سماعة رأس تدوير افتراضي نقطة نهاية",
        ["**Switch Audio Output** changes the Windows default audio device(s). Both fields match by **partial name**, case-insensitively - \"Speakers\" matches \"Speakers (Realtek…)\". The **▾ button** beside each field lists your connected devices, and a new slice starts pre-filled with your current defaults."] =
            "**تبديل مخرج الصوت** يغيّر جهاز (أجهزة) الصوت الافتراضية في Windows. يطابق الحقلان بـ **اسم جزئي** دون تمييز حالة الأحرف - \"Speakers\" تطابق \"Speakers (Realtek…)\". **زر ▾** بجانب كل حقل يسرد أجهزتك الموصولة، وتبدأ الشريحة الجديدة مملوءة مسبقًا بافتراضياتك الحالية.",
        ["**Output Device** - the playback device to switch to. **Blank = cycle** through your outputs on each fire (unless a Mic Device is set, which leaves the output alone)."] =
            "**جهاز الإخراج** - جهاز التشغيل الذي يبدّل إليه. **فارغ = التدوير** بين مخرجاتك في كل تنفيذ (إلا إذا ضبط Mic Device، فيترك الإخراج كما هو).",
        ["**Mic Device** - the recording device to switch to. **Blank = leave the mic unchanged.**"] =
            "**الميكروفون** - جهاز التسجيل الذي يبدّل إليه. **فارغ = ترك الميكروفون دون تغيير.**",
        ["Set both fields to switch output + mic in one slice - \"TV + no mic\", \"Headset + headset mic\". Firing shows the device switched to in the hub."] =
            "املأ الحقلين لتبديل الإخراج والميكروفون في شريحة واحدة: \"التلفاز بلا ميكروفون\"، \"سماعة الرأس مع ميكروفونها\". وعند التنفيذ يعرض المحور الجهاز الذي تم التبديل إليه.",

        // ── topic:open-uri ──
        ["Open URI"] =
            "فتح URI",
        ["uri url link scheme https steam discord ms-settings deep link protocol"] =
            "uri url رابط مخطط https steam discord ms-settings رابط عميق بروتوكول",
        ["An **Open URI** slice hands its value to Windows to open with whatever handles that scheme. That covers a lot more than web links:"] =
            "شريحة **فتح URI** تسلّم قيمتها إلى Windows ليفتحها بما يتولى ذلك المخطط. وهذا يشمل ما هو أكثر بكثير من روابط الويب:",
        ["**Web URLs** - `https://twitch.tv/yourchannel` opens in your default browser."] =
            "**روابط الويب** - `https://twitch.tv/yourchannel` يفتح في متصفحك الافتراضي.",
        ["**App deep links** - `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…` - anything an installed app registers a protocol for."] =
            "**روابط التطبيقات العميقة** - `steam://open/bigpicture`، `discord://discord.com/channels/…`، `com.epicgames.launcher://apps/…`، `spotify:playlist:…` - أي شيء يسجّل له تطبيق مثبّت بروتوكولًا.",
        ["**Windows pages** - `ms-settings:display` opens that Settings page."] =
            "**صفحات Windows** - `ms-settings:display` يفتح صفحة الإعدادات تلك.",
        ["The value must be a complete, absolute URI. A bare `twitch.tv/...` won't launch - include the `https://`."] =
            "يجب أن تكون القيمة عنوان URI كاملًا ومطلقًا. فـ `twitch.tv/...` وحده لن يعمل - ضمّنه `https://`.",
        ["Game-launch URIs (`steam://rungameid/…`) work here too, but the **Installed Game** slice type builds them for you, which is easier."] =
            "تعمل روابط تشغيل الألعاب (`steam://rungameid/…`) هنا أيضًا، لكن نوع الشريحة **لعبة مثبتة** يبنيها لك، وهو أسهل.",

        // ── topic:key-combo ──
        ["Key Combo (send a keyboard shortcut)"] =
            "Key Combo (إرسال اختصار لوحة مفاتيح)",
        ["key combo keypress keyboard shortcut hotkey send keys format grammar win ctrl alt shift del delete media volup voldown mute playpause next prev navy blue modifier plus"] =
            "مجموعة مفاتيح ضغطة مفاتيح لوحة المفاتيح اختصار مفتاح تشغيل سريع إرسال مفاتيح صيغة قواعد win ctrl alt shift del delete وسائط volup voldown mute playpause next prev كحلي أزرق معدّل زائد",
        ["A **Key Combo** slice (Custom) presses a keyboard shortcut for you. Write it as key names joined with **`+`** - `Win+D`, `Ctrl+Shift+Esc`, `Alt+F4` - or a single key like `PlayPause`. The **last** name is the key pressed; everything before it is a modifier held around it. Names aren't case-sensitive."] =
            "شريحة **تركيبة مفاتيح** (مخصص) تضغط اختصار لوحة مفاتيح نيابة عنك. اكتبها كأسماء مفاتيح موصولة بـ **`+`** مثل `Win+D` و`Ctrl+Shift+Esc` و`Alt+F4`، أو مفتاحًا واحدًا مثل `PlayPause`. الاسم **الأخير** هو المفتاح المضغوط، وكل ما قبله مفتاح تعديل يمسك حوله. والأسماء لا تفرّق بين الحروف الكبيرة والصغيرة.",
        ["In a combo, the last name is the key that actually gets pressed and everything before it is a modifier held down around it."] =
            "في المجموعة، الاسم الأخير هو المفتاح الذي يضغط فعلًا، وكل ما قبله معدّل يثبّت مضغوطًا حوله.",
        ["**Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`."] =
            "**المعدّلات:** `Ctrl`، `Alt`، `Shift`، `Win`.",
        ["**Keys:** letters and digits; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; the arrows `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` and `Slash`; and the **media keys** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop` - media keys work on their own, no modifier needed."] =
            "**المفاتيح:** الحروف والأرقام؛ `F1`-`F12`؛ `Enter`، `Esc`، `Tab`، `Space`، `Backspace`، `Del`، `Insert`؛ `Home`، `End`، `PgUp`، `PgDn`؛ الأسهم `Up` `Down` `Left` `Right`؛ `PrintScreen`، `Pause`؛ `Backtick` و`Slash`؛ و**مفاتيح الوسائط** `VolUp`، `VolDown`، `Mute`، `PlayPause`، `Next`، `Prev`، `Stop` - تعمل مفاتيح الوسائط وحدها، بلا حاجة إلى معدّل.",
        ["The combo is sent as real keystrokes. One thing can block it: an app running **as administrator** won't accept keystrokes from Radiata. This is a Windows limitation."] =
            "ترسل التركيبة كضغطات مفاتيح حقيقية. وشيء واحد قد يمنعها: التطبيق الذي يعمل **كمسؤول** لا يقبل ضغطات المفاتيح من Radiata. هذا قيد في Windows.",

        // ── topic:discord-setup ──
        ["Discord voice-channel setup"] =
            "إعداد قناة Discord الصوتية",
        ["discord credentials client id secret oauth voice join leave mute deafen keybind"] =
            "discord بيانات اعتماد معرّف العميل السر oauth صوتي انضمام مغادرة كتم إصمات مفتاح ربط",
        ["**Launch Discord** works out of the box. **Join/Leave Voice Channel**, **Deafen** and **Mute Me** talk to Discord directly, so those three need your own free Discord application credentials. Until they exist the slice editor shows a **Configure Discord Integration** button, and the same wizard sits in **Settings ▸ Advanced**; firing one of those slices from the wheel opens it too, rather than doing nothing."] =
            "**تشغيل Discord** يعمل مباشرة. أما **الانضمام إلى قناة صوتية أو مغادرتها** و**كتم السماعة** و**كتم نفسي** فتتخاطب مع Discord مباشرة، لذا تحتاج هذه الثلاثة إلى بيانات اعتماد تطبيق Discord المجانية الخاصة بك. وإلى أن توجد، يعرض محرر الشرائح زر **إعداد تكامل Discord**، والمعالج نفسه موجود في **الإعدادات ◂ متقدم**؛ وتنفيذ إحدى تلك الشرائح من العجلة يفتحه أيضًا بدل ألا يفعل شيئًا.",
        ["**Deafen** and **Mute Me** toggle Discord's own switches - the same ones the headphone and microphone buttons at the bottom-left of Discord flip - and they work while a game has focus. No keybind to set up, and the game never sees a keystroke."] =
            "**كتم السماعة** و**كتم نفسي** يبدّلان مفاتيح Discord نفسها - المفاتيح ذاتها التي يحرّكها زرا سماعة الرأس والميكروفون أسفل يسار Discord - وتعملان بينما اللعبة في المقدمة. لا اختصار مفاتيح تعدّه، واللعبة لا ترى أي ضغطة مفتاح.",
        ["To get the **channel link**: in Discord, right-click the voice channel → **Copy Link**, and paste it into the slice's **Discord URL** field (Radiata normalizes it to the `discord://` form)."] =
            "للحصول على **رابط القناة**: في Discord، انقر بالزر الأيمن على القناة الصوتية ← **Copy Link**، والصقه في حقل **عنوان Discord** في الشريحة (يحوّله Radiata إلى صيغة `discord://`).",

        // ── topic:steam-xbox-voice ──
        ["Steam voice chat"] =
            "دردشة Steam الصوتية",
        ["steam chat friends voice mic mute push to talk hotkey open limits deafen join leave"] =
            "steam دردشة أصدقاء صوت ميكروفون كتم اضغط للتحدث اختصار فتح حدود كتم السماعات انضمام مغادرة",
        ["The **Steam Chat** group does everything Steam allows a third-party app to do, which is less than Discord allows. Discord provides a local control channel that Radiata's Join/Leave slice uses; **Steam provides none**. What you can do:"] =
            "مجموعة **دردشة Steam** تفعل كل ما يسمح به Steam لتطبيق خارجي، وهو أقل مما يسمح به Discord. فـ Discord يوفر قناة تحكم محلية تستخدمها شريحة الانضمام والمغادرة في Radiata؛ أما **Steam فلا يوفر أي قناة**. وما يمكنك فعله:",
        ["**Open Steam Chat** - opens Steam's **Friends & Chat** window. Join a group's voice channel from there. Steam gives another app no way to join, leave or switch voice channels, and has no mute-incoming-voice control at all."] =
            "**فتح دردشة Steam** - يفتح نافذة **الأصدقاء والدردشة** في Steam. ومن هناك يمكنك الانضمام إلى قناة صوتية لمجموعة. ولا يمنح Steam أي تطبيق آخر وسيلة للانضمام إلى القنوات الصوتية أو مغادرتها أو التبديل بينها، وليس فيه تحكم بكتم الصوت الوارد إطلاقًا.",
        ["**Mic Mute** - in the group, and the same slice as System ▸ Audio. It mutes your **Windows microphone**, which is what Steam transmits from, so the others can't hear you. Every other app loses the mic as well, since Steam exposes no app-scoped mute. Live Muted/Unmuted state shows in the hub."] =
            "**كتم الميكروفون** - موجود في المجموعة، وهو الشريحة نفسها الموجودة ضمن النظام ◂ الصوت. يكتم **ميكروفون Windows** الذي يبث منه Steam، فلا يسمعك الآخرون. وتفقد كل التطبيقات الأخرى الميكروفون كذلك، لأن Steam لا يوفر كتمًا خاصًا بالتطبيق. وتظهر حالة الكتم أو التشغيل مباشرة في المحور.",
        ["For full voice control from a slice - join, leave, mute, deafen - Discord remains the best-supported option; see [[discord-setup|Discord voice-channel setup]]."] =
            "للتحكم الصوتي الكامل من شريحة - انضمام، مغادرة، كتم، إصمات - يبقى Discord الخيار الأفضل دعمًا؛ راجع [[discord-setup|إعداد قناة Discord الصوتية]].",

        // ── topic:obs-studio ──
        ["OBS Studio"] =
            "OBS Studio",
        ["obs studio websocket streaming recording replay buffer scene source mute setup port password"] =
            "obs studio websocket بث تسجيل مخزن إعادة مشهد مصدر كتم إعداد منفذ كلمة مرور",
        ["**OBS Studio** slices - Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute… - drive OBS over the **obs-websocket** protocol."] =
            "شرائح **OBS Studio** - Toggle Streaming، Toggle Recording، Save Replay Buffer، Switch Scene…، Toggle Source Mute… - تتحكم في OBS عبر بروتوكول **obs-websocket**.",
        ["**One-time setup:** in OBS, **Tools ▸ WebSocket Server Settings** ▸ enable the server, then copy its **port** (default `4455`) and **password** into **Settings ▸ Advanced ▸ Integrations ▸ Configure OBS Integration…**. It's one shared setting, and **Test** confirms the connection on the spot. Until it's set up, OBS slices arm as **\"Configure in Settings\"**, and an OBS slice's editor offers the same setup pane."] =
            "**إعداد لمرة واحدة:** في OBS اذهب إلى **الأدوات ◂ إعدادات خادم WebSocket** ◂ فعّل الخادم، ثم انسخ **المنفذ** (الافتراضي `4455`) و**كلمة المرور** إلى **الإعدادات ◂ متقدم ◂ التكاملات ◂ إعداد تكامل OBS…**. إنه إعداد واحد مشترك، و**اختبار** يؤكد الاتصال في الحال. وإلى أن يضبط، تحدّد شرائح OBS بعبارة **\"اضبطه في الإعدادات\"**، ويعرض محرر شريحة OBS لوحة الإعداد نفسها.",
        ["**Scene** and **audio-source** names on the slice must match OBS exactly."] =
            "يجب أن تطابق أسماء **Scene** و**مصدر الصوت** في الشريحة أسماءها في OBS تمامًا.",
        ["**What's supported:** **OBS Studio 28 or later** (the WebSocket server is built in) and **OBS 27 or earlier with the obs-websocket 5.x plugin**. Forks that speak the same protocol (**StreamElements OBS.Live**, for one) work identically. **Streamlabs Desktop does NOT** - it's a different app without obs-websocket."] =
            "**المدعوم:** **OBS Studio 28 أو أحدث** (خادم WebSocket مدمج فيه)، و**OBS 27 أو أقدم مع إضافة obs-websocket 5.x**. والنسخ المتفرعة التي تتحدث البروتوكول نفسه (مثل **StreamElements OBS.Live**) تعمل بالطريقة ذاتها. أما **Streamlabs Desktop فلا يعمل** - فهو تطبيق مختلف بلا obs-websocket.",

        // ── topic:text-chat ──
        ["Text Chat (send a message into a game)"] =
            "Text Chat (إرسال رسالة داخل لعبة)",
        ["text chat message send game keybind enter t y chat button custom quick phrase gg glhf cooldown"] =
            "محادثة نصية رسالة إرسال لعبة مفتاح ربط enter t y زر المحادثة مخصص عبارة سريعة gg glhf مهلة",
        ["**Try Game Default sends nothing unless the focused game is in the index.** When the game in front isn't covered, the hub names it and says **\"No chat key default found. Configure in Settings\"** - switch that slice to **Custom…** and set the key."] =
            "**لا يرسل خيار تجربة افتراضي اللعبة شيئًا ما لم تكن اللعبة في المقدمة مدرجة في الفهرس.** فحين لا تكون اللعبة الأمامية مغطاة، يذكر المحور اسمها ويقول **\"لم يعثر على مفتاح دردشة افتراضي. اضبطه في الإعدادات\"** - بدّل تلك الشريحة إلى **مخصص…** وحدد المفتاح.",
        ["**Limits:** the game must be focused and must accept its chat key at that moment."] =
            "**الحدود:** يجب أن تكون اللعبة في المقدمة وأن تقبل مفتاح الدردشة في تلك اللحظة.",

        // ── topic:volume-mixer ──
        ["D-Pad 🡄 🡆"] =
            "أزرار الاتجاهات 🡄 🡆",
        ["volume mixer balance dpad left right game chat discord music spotify browser desktop song track app session advanced mic microphone input alt-tab task switcher window"] =
            "مستوى الصوت مازج توازن dpad يسار يمين لعبة محادثة discord موسيقى spotify متصفح سطح المكتب أغنية مقطع تطبيق جلسة متقدم ميكروفون إدخال alt-tab مبدّل المهام نافذة",
        ["**While a wheel is open, D-Pad 🡄 🡆** does one of four things. Pick in the **Settings ▸ Customize ▸ D-Pad 🡄 🡆** section:"] =
            "**أثناء فتح عجلة، تؤدي أزرار الاتجاهات 🡄 🡆** واحدًا من أربعة أشياء. اختر من قسم **الإعدادات ◂ تخصيص ◂ أزرار الاتجاهات 🡄 🡆**:",
        ["**Cycles Windows** (default) - steps through the Alt-Tab switcher, one window per press. The switcher stays up while the wheel is open and lands on the highlighted window when the wheel closes."] =
            "**يتنقل بين النوافذ** (الافتراضي) - يتنقل عبر مبدّل Alt-Tab، نافذة واحدة لكل ضغطة. يبقى المبدّل ظاهرًا أثناء فتح العجلة ويحطّ على النافذة المضاءة عند إغلاق العجلة.",
        ["**Cycles Desktops** - switches Windows virtual desktops, the same as **Win+Ctrl+🡄 🡆**."] =
            "**تبديل أسطح المكتب** - ينتقل بين أسطح مكتب Windows الافتراضية، تمامًا مثل **Win+Ctrl+🡄 🡆**.",
        ["**Skips Songs** - the same track-skip keys as the Audio slices; each skip flashes a ⏮ / ⏭ icon in the hub."] =
            "**يتخطى الأغاني** - مفاتيح تخطي المقاطع نفسها كما في شرائح Audio؛ كل تخطٍّ يومض بأيقونة ⏮ / ⏭ في المحور.",
        ["**Mic Volume** - turns your default mic up and down in 5% steps, holding to repeat, and un-mutes it on the way up. The level shows in the hub with a **microphone** icon."] =
            "**مستوى صوت الميكروفون** - يرفع ميكروفونك الافتراضي ويخفضه بخطوات 5%، مع الاستمرار بالضغط للتكرار، ويرفع كتمه في طريق الرفع. يظهر المستوى في المحور بأيقونة **ميكروفون**.",
        ["See [[wheel-open-extras|While a wheel is open]] for everything else the D-Pad does with a wheel up."] =
            "راجع [[wheel-open-extras|أثناء فتح العجلة]] لكل ما يفعله D-Pad الآخر بينما العجلة ظاهرة.",

        // ── topic:supported-controllers ──
        ["Supported controllers"] =
            "أجهزة التحكم المدعومة",
        ["dualsense edge dualshock ds4 xbox xinput bluetooth usb bleed through shared input"] =
            "dualsense edge dualshock ds4 xbox xinput bluetooth usb تسرّب إدخال مشترك",
        ["**Third-party Xbox-style pads over Bluetooth** - most present as a DualShock 4 over BT, so they get the full isolation path too."] =
            "**أجهزة الطرف الثالث بأسلوب Xbox عبر Bluetooth** - يظهر أكثرها بوصفه DualShock 4 عبر BT، فتحصل على مسار العزل الكامل أيضًا.",
        ["**If you've remapped L4 or R4 on the pad itself** (holding L4/R4 + a button + the mapping key), that paddle now sends the button you assigned and Radiata can no longer see it - so it stops opening wheels. Clear the remap on the pad to get it back, or pick a chord in [[triggers|Settings ▸ Customize ▸ Triggers]] instead."] =
            "**إذا أعدت تعيين L4 أو R4 على الجهاز نفسه** (بالضغط المطوّل على L4/R4 + زر + مفتاح التعيين)، يرسل ذلك المجذاف الآن الزر الذي عيّنته ولا يستطيع Radiata رؤيته بعد ذلك - فيتوقف عن فتح العجلات. امسح إعادة التعيين على الجهاز لاستعادته، أو اختر مجموعة أزرار في [[triggers|الإعدادات ◂ تخصيص ◂ إيماءات الاستدعاء]] بدلًا من ذلك.",
        ["**Xbox pads over USB or wireless dongle (XInput)** - isolated with the same cloak, presenting a virtual **Xbox 360** pad to the game. If a pad can't be cloaked for any reason, Radiata falls back automatically to **shared-input mode**: the wheel still works, but the game also sees your input while a wheel is up."] =
            "**أجهزة Xbox عبر USB أو دونجل لاسلكي (XInput)** - تعزل بالإخفاء نفسه، وتقدّم للعبة كجهاز **Xbox 360** افتراضي. وإن تعذّر إخفاء جهاز لأي سبب، يعود Radiata تلقائيًا إلى **وضع الإدخال المشترك**: تبقى العجلة تعمل، لكن اللعبة ترى إدخالك أيضًا أثناء فتح العجلة.",
        ["**Two or more Xbox pads plugged in at once** - isolation switches off and both pads keep working in shared-input mode, since cloaking would make a second player's controller disappear. Unplug the second pad and isolation comes back on its own."] =
            "**جهازا Xbox أو أكثر موصولان معًا** - يتوقف العزل ويواصل كلا الجهازين العمل في وضع الإدخال المشترك، لأن الإخفاء سيجعل جهاز اللاعب الثاني يختفي. افصل الجهاز الثاني فيعود العزل من تلقاء نفسه.",

        // ── topic:input-isolation ──
        ["Input isolation (what the drivers do)"] =
            "عزل الإدخال (ما تفعله برامج التشغيل)",
        ["isolation virtual pad cloak hidhide vigem drivers double input neutral joy.cpl lag latency delay ms milliseconds input lag polling rate overhead rumble"] =
            "عزل وحدة افتراضية إخفاء hidhide vigem برامج التشغيل إدخال مزدوج ساكن joy.cpl تأخر زمن الاستجابة تأخير ms ميلي ثانية تأخر الإدخال معدل الاستقصاء عبء اهتزاز",
        ["With successful isolation, the game reads the virtual pad; in Passthru Mode or with no drivers installed, the game reads your controller directly and Radiata simply watches alongside it."] =
            "عند نجاح العزل تقرأ اللعبة الجهاز الافتراضي؛ وفي الوضع المباشر أو بدون تثبيت برامج التشغيل تقرأ اللعبة جهازك مباشرة ويكتفي Radiata بالمراقبة إلى جانبها.",
        ["Isolation forwards sticks, triggers, D-Pad and standard buttons. Sony touchpad, gyro, speaker/mic, adaptive triggers, haptics and rumble are not forwarded. Captured Xbox input supports standard two-motor rumble, but not Share or impulse-trigger motors. [[passthru-mode|Passthru Mode]] or quitting requests removal of Radiata's capture; games may need to reconnect or restart, and other remappers can still affect native features."] =
            "يمرّر العزل العصوين والزنادين وأزرار الاتجاهات والأزرار القياسية. ولا تمرّر لوحة اللمس والجيروسكوب والسماعة والميكروفون والزنادان التكيفيان والاستجابة اللمسية والاهتزاز في أجهزة Sony. ويدعم إدخال Xbox الملتقط الاهتزاز القياسي بمحركين، دون محركي Share أو الزنادين النبضيين. و[[passthru-mode|الوضع المباشر]] أو الخروج يطلب إزالة التقاط Radiata؛ وقد تحتاج الألعاب إلى إعادة الاتصال أو إعادة التشغيل، وقد تظل أدوات إعادة التعيين الأخرى تؤثر في الميزات الأصلية.",
        ["Input latency"] =
            "زمن استجابة الإدخال",
        ["**Sony pads and Xbox pads over Bluetooth** - **imperceptible**. Reports are passed straight through as they arrive."] =
            "**أجهزة Sony وأجهزة Xbox عبر Bluetooth** - **غير محسوس**. تمرّر التقارير كما هي فور وصولها.",
        ["**Xbox pads over USB or a wireless dongle (XInput)** - **a few milliseconds at most**, because these have to be polled."] =
            "**أجهزة Xbox عبر USB أو وصلة لاسلكية (XInput)** - **بضع ميلي ثوانٍ على الأكثر**، لأنها تحتاج إلى الاستقصاء.",
        ["**Passthru Mode, or no drivers installed** - **none**. The game reads your physical controller directly."] =
            "**الوضع المباشر، أو بدون برامج تشغيل مثبّتة** - **لا شيء**. تقرأ اللعبة جهازك الفعلي مباشرة.",
        ["For scale: a 60 fps game draws a frame every **16.7 ms**. Radiata never injects into or hooks a game, so it adds nothing to rendering or frame pacing."] =
            "للمقارنة: ترسم لعبة بـ 60 إطارًا في الثانية إطارًا كل **16.7 ms**. لا يحقن Radiata في أي لعبة ولا يضع خطافات فيها، فلا يضيف شيئًا إلى العرض أو توقيت الإطارات.",
        ["**While a wheel, Game Grid or editor is on screen, Radiata holds its virtual pad neutral.** If the game still reacts, another input path may be active - follow the [[controller-conflict-checklist|Controller conflict checklist]]. Passthru Mode deliberately lets controller input reach the game."] =
            "**أثناء ظهور عجلة أو شبكة الألعاب أو المحرر على الشاشة، يبقي Radiata جهازه الافتراضي محايدًا.** وإن ظلت اللعبة تستجيب فقد يكون هناك مسار إدخال آخر نشط - اتبع [[controller-conflict-checklist|قائمة فحص تعارض أجهزة التحكم]]. أما الوضع المباشر فيسمح عمدًا بوصول إدخال جهاز التحكم إلى اللعبة.",
        ["For driver repair, HP OMEN buses and version checks, see [[driver-conflicts|HP OMEN & driver version conflicts]]. For busy or hidden-device access, see [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "لإصلاح برامج التشغيل ونواقل HP OMEN وفحص الإصدارات، راجع [[driver-conflicts|تعارضات HP OMEN وإصدارات برامج التشغيل]]. ولحالات الانشغال أو الوصول إلى الأجهزة المخفية، راجع [[hidhide-troubleshooting|استكشاف أخطاء HidHide]].",

        // ── topic:passthru-mode ──
        ["Set Passthru Mode (Bypass Input Isolation)"] =
            "الوضع المباشر (تجاوز عزل الإدخال)",
        ["passthru mode passthrough safe mode bypass input isolation controller interception anticheat valorant vanguard eac battleye exceptions per game automatic capture"] =
            "passthru وضع التمرير passthrough الوضع الآمن تجاوز عزل الإدخال جهاز التحكم اعتراض مكافحة الغش valorant vanguard eac battleye استثناءات لكل لعبة تلقائي التقاط",
        ["Some competitive titles with kernel anticheat (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) could theoretically react to emulated or hidden devices. **Passthru Mode** bypasses [[input-isolation|input isolation]] entirely: Radiata removes its virtual pad and requests release of its own controller blocks. Other tools may still hide or remap the controller. The wheel still works; the trade-off is that input reaches the game while a wheel is open, which is what \"bleed-through\" or \"double input\" means. Don't confuse it with disabling the **wheels** - that keeps the virtual pad in place and only stops summons. Passthru Mode removes the virtual pad altogether."] =
            "بعض الألعاب التنافسية ذات أنظمة مكافحة الغش على مستوى النواة (Valorant وCall of Duty وFortnite وRainbow Six Siege) قد تتفاعل نظريًا مع الأجهزة المحاكاة أو المخفية. و**الوضع المباشر** يتجاوز [[input-isolation|عزل الإدخال]] كليًا: يزيل Radiata جهازه الافتراضي ويطلب تحرير حجوباته الخاصة بجهاز التحكم. وقد تظل أدوات أخرى تخفي الجهاز أو تعيد تعيينه. تبقى العجلة تعمل؛ والمقابل أن الإدخال يصل إلى اللعبة أثناء فتح العجلة، وهذا ما يسمى \"التسرب\" أو \"الإدخال المزدوج\". ولا تخلط بينه وبين تعطيل **العجلات**: فذاك يبقي الجهاز الافتراضي في مكانه ويوقف الاستدعاءات فقط. أما الوضع المباشر فيزيل الجهاز الافتراضي تمامًا.",
        ["**Automatic per-game:** add a game to the **Always use Passthru Mode for** list - from the installed-games dropdown, or **Add Application…** for a specific .exe. Radiata enters Passthru Mode while it runs and restores capture on exit; the tray shows \"(auto: <game>)\"."] =
            "**تلقائيًا لكل لعبة:** أضف لعبة إلى قائمة **استخدم الوضع المباشر دائمًا مع** - من قائمة الألعاب المثبّتة المنسدلة، أو عبر **إضافة تطبيق…** لملف .exe معيّن. يدخل Radiata الوضع المباشر ما دامت تعمل ويعيد الالتقاط عند الخروج؛ ويعرض شريط النظام \"(auto: <اللعبة>)\".",
        ["Each entry engages **While Running** (recommended - anticheat watches from launch) or **While Frontmost** (only while the game's window is focused)."] =
            "يعمل كل مدخل إما **أثناء التشغيل** (موصى به - تراقب أنظمة مكافحة الغش من الإطلاق) أو **أثناء وجوده في المقدمة** (فقط أثناء تركيز نافذة اللعبة).",
        ["Passthru Mode is **best-effort**. Per-game detection polls every ~2 seconds, so a just-launched game can briefly see normal capture. For the strictest titles, toggle Passthru Mode on manually *before* launching. The isolation drivers also stay installed system-wide either way."] =
            "الوضع المباشر **بأقصى جهد ممكن**. يستقصي الاكتشاف لكل لعبة كل ~2 ثانية، فقد ترى لعبة شغّلت للتو الالتقاط العادي لوهلة. للألعاب الأكثر صرامة، شغّل الوضع المباشر يدويًا *قبل* التشغيل. وتبقى برامج تشغيل العزل مثبّتة على مستوى النظام في الحالتين.",

        // ── topic:tray-and-settings ──
        ["Tray & Settings (mouse/keyboard, at the desk)"] =
            "شريط النظام والإعدادات (بالفأرة/لوحة المفاتيح، على المكتب)",
        ["tray icon left click right click menu settings tabs f1 f2 test"] =
            "أيقونة شريط النظام نقر أيسر نقر أيمن قائمة إعدادات علامات تبويب f1 f2 اختبار",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Show in Explorer, Start with Windows, Exit)."] =
            "**أيقونة شريط النظام:** النقر الأيسر يفتح الإعدادات؛ والنقر الأيمن يعرض القائمة (تعطيل العجلات/تفعيل العجلات، الوضع المباشر، **شبكة الألعاب**، الإعدادات، المساعدة، حول Radiata، إظهار في مستكشف الملفات، التشغيل مع Windows، خروج).",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Start with Windows, Exit)."] =
            "**أيقونة شريط النظام:** النقر الأيسر يفتح الإعدادات؛ والنقر الأيمن يعرض القائمة (تعطيل العجلات/تفعيل العجلات، الوضع المباشر، **شبكة الألعاب**، الإعدادات، المساعدة، حول Radiata، التشغيل مع Windows، خروج).",
        ["**Test Wheel** - each Wheel tab has a **Test Left/Right Wheel** button under the slice list that opens that wheel on screen (with the wheels enabled)."] =
            "**اختبار العجلة** - في كل علامة تبويب عجلة زر **اختبار العجلة اليسرى/اليمنى** أسفل قائمة الشرائح يفتح تلك العجلة على الشاشة (مع تفعيل العجلات).",

        // ── topic:customize ──
        ["Customize (look, feel & sound)"] =
            "تخصيص (المظهر والإحساس والصوت)",
        ["customize material light dark flat pearl obsidian mesa gloss terra theme slices thickness ring thick medium thin button icons glyphs best guess sound effects themed material digital physical silent none preview appearance look feel triggers accessibility"] =
            "تخصيص مادة فاتح داكن مسطح pearl obsidian mesa لمّاع terra سمة شرائح سمك حلقة سميك متوسط رفيع أيقونات الأزرار رموز أفضل تخمين مؤثرات صوتية بحسب السمة مادة رقمي مادي صامت لا شيء معاينة مظهر شكل إحساس مشغّلات إمكانية الوصول",
        ["**Mesa** - rounded cream wedges on cracked terracotta; the armed slice lifts like a 3D card."] =
            "**ميسا** - أوتاد كريمية مستديرة على طين متشقق؛ الشريحة المسلّحة ترتفع كبطاقة ثلاثية الأبعاد.",
        ["**Sound Effects** - **Themed** (the default) plays whatever sound set matches the material above. **Digital** and **Physical** are fixed sets if you'd rather pin one, and **Silent** turns the sounds off. Picking a material switches the choice back to Themed."] =
            "**المؤثرات الصوتية** - **حسب الطابع** (الافتراضي) يشغّل مجموعة الأصوات التي تناسب الخامة أعلاه. و**رقمي** و**مادي** مجموعتان ثابتتان إن فضّلت تثبيت واحدة، و**صامت** يوقف الأصوات. واختيار خامة يعيد الخيار إلى حسب الطابع.",
        ["**Slice Thickness** - the radial thickness of the slice ring. **Thick** only reads well up to **8 slices**, so a 9th slice on either wheel switches the setting to **Medium** - and it switches back on its own once you're at 8 or fewer again."] =
            "**سمك الشرائح** - السمك الشعاعي لحلقة الشرائح. لا يقرأ **سميك** جيدًا إلا حتى **8 شرائح**، فالشريحة التاسعة على أي عجلة تبدّل الإعداد إلى **متوسط** - ويعود من تلقاء نفسه ما إن تصير عند 8 أو أقل مجددًا.",
        ["**Button Icons** - the symbols in on-screen prompts: **Best Guess** (the default - follows the connected pad), **PlayStation** (✕ ○ □ △), or **Xbox** (A B X Y)."] =
            "**أيقونات الأزرار** - الرموز في المطالبات على الشاشة: **أفضل تخمين** (الافتراضي - يتبع الجهاز الموصول)، أو **PlayStation** (✕ ○ □ △)، أو **Xbox** (A B X Y).",
        ["**Triggers** - which controller gestures summon a wheel. See [[triggers|Triggers]]."] =
            "**إيماءات الاستدعاء** - أي إيماءات جهاز التحكم تستدعي عجلة. راجع [[triggers|المشغّلات]].",
        ["**D-Pad 🡄 🡆** - what left and right on the D-Pad do while a wheel is open. See [[volume-mixer|D-Pad controls]]."] =
            "**أزرار الاتجاهات 🡄 🡆** - ما يفعله اليسار واليمين على D-Pad أثناء فتح العجلة. راجع [[volume-mixer|عناصر تحكم D-Pad]].",
        ["**Show labels on** - which slices draw their text label. See [[show-labels|Show labels on]]."] =
            "**إظهار التسميات على** - أي الشرائح ترسم تسميتها النصية. راجع [[show-labels|إظهار التسميات على]].",
        ["**Accessibility** - activation, wheel sides, both-stick aiming, the hub, Reduce motion and narration - lives on **Settings ▸ Advanced**. See [[accessibility|Accessibility]]."] =
            "**إمكانية الوصول** - التنشيط، وجانبا العجلة، والتصويب بكلتا العصاتين، والمحور، وReduce motion، والسرد - في **الإعدادات ◂ متقدم**. راجع [[accessibility|إمكانية الوصول]].",
        ["**Make your own material** - drop a theme package into Radiata's Materials folder and it joins the list. See [[custom-materials|Custom materials]]."] =
            "**اصنع مادتك الخاصة** - ضع حزمة سمة في مجلد Materials الخاص بـ Radiata فتنضم إلى القائمة. راجع [[custom-materials|المواد المخصصة]].",
        ["A wheel holds up to **12** slices, but **6-8** is the sweet spot."] =
            "تحمل العجلة حتى **12** شريحة، لكن **من 6 إلى 8** هو العدد الأمثل.",

        // ── topic:workshop ──
        ["Workshop: make your own"] =
            "الورشة: اصنع بنفسك",
        ["workshop make build create author own custom package packages theme material game arcade javascript sample samples template download guide folder restart confirm share tutorial"] =
            "ورشة workshop صنع بناء إنشاء مؤلف خاص مخصص حزمة حزم سمة مادة لعبة أركيد javascript عينة عينات قالب تنزيل دليل مجلد إعادة تشغيل تأكيد مشاركة شرح",
        ["Radiata can load things you make yourself: **materials** that restyle the wheel, and **Arcade games** that play in the Arcade's round window. Each one is a **package** - a folder of plain files you can write in any text editor."] =
            "يستطيع Radiata تحميل أشياء تصنعها بنفسك: **مواد** تغير مظهر العجلة، و**ألعاب Arcade** تلعب في نافذة Arcade الدائرية. كل منها **حزمة** - مجلد ملفات نصية بسيطة تستطيع كتابتها في أي محرر نصوص.",
        ["**Materials** are data only: colors, gradients, a font name, and optionally images and sounds. See [[custom-materials|Custom materials]]."] =
            "**المواد** بيانات فقط: ألوان وتدرجات واسم خط، واختياريا صور وأصوات. راجع [[custom-materials|المواد المخصصة]].",
        ["**Arcade games** are a `game.json` and one JavaScript file, run in a sandbox. See [[custom-arcade-games|Custom Arcade games]]."] =
            "**ألعاب Arcade** ملف `game.json` وملف JavaScript واحد، يعملان داخل بيئة معزولة. راجع [[custom-arcade-games|ألعاب Arcade المخصصة]].",
        ["The **Workshop** on the Radiata website is the full guide: step-by-step walkthroughs, every setting with its range, design advice, and **sample packages to download**. Start there: [getradiata.app/workshop](https://getradiata.app/workshop/)."] =
            "**الورشة** على موقع Radiata هي الدليل الكامل: شروح خطوة بخطوة، وكل إعداد مع نطاقه، ونصائح التصميم، و**حزم عينات للتنزيل**. ابدأ من هناك: [getradiata.app/workshop](https://getradiata.app/workshop/ar.html).",
        ["How making a package works"] =
            "كيف تصنع حزمة",
        ["**1.** Make a folder for your package - or unzip a sample - inside Radiata's packages folder. Paste the path into File Explorer's address bar to open it:"] =
            "**1.** أنشئ مجلدا لحزمتك - أو فك ضغط عينة - داخل مجلد حزم Radiata. الصق المسار في شريط العنوان في مستكشف الملفات لفتحه:",
        ["a material: `%APPDATA%\\Radiata\\Packages\\Materials\\<your theme>`"] =
            "مادة: `%APPDATA%\\Radiata\\Packages\\Materials\\<سمتك>`",
        ["a game: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<your game>`"] =
            "لعبة: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<لعبتك>`",
        ["**2.** Write the manifest (`material.json` or `game.json`) and put every file it names directly inside that folder."] =
            "**2.** اكتب ملف البيان (`material.json` أو `game.json`) وضع كل ملف يذكره مباشرة داخل ذلك المجلد.",
        ["**3.** **Restart Radiata** - right-click the tray icon, choose **Exit**, then start it again. Packages are read once, at startup."] =
            "**3.** **أعد تشغيل Radiata** - انقر بزر الفأرة الأيمن على أيقونة شريط النظام، واختر **خروج**، ثم شغله من جديد. تقرأ الحزم مرة واحدة، عند بدء التشغيل.",
        ["**4.** Accept the confirmation Radiata shows for a new or changed package."] =
            "**4.** وافق على التأكيد الذي يعرضه Radiata لحزمة جديدة أو متغيرة.",
        ["**5.** Try it out: pick the material in **Settings ▸ Customize**, or open the game from the Arcade. Change something, then go back to step 3."] =
            "**5.** جربها: اختر المادة في **الإعدادات ◂ تخصيص**، أو افتح اللعبة من Arcade. غير شيئا، ثم عد إلى الخطوة 3.",
        ["Making a package is a loop: every change goes back through a restart and the confirmation before you can try it."] =
            "صنع حزمة حلقة متكررة: كل تغيير يمر من جديد بإعادة تشغيل وبالتأكيد قبل أن تتمكن من تجربته.",
        ["Keep a copy of your work somewhere else too. Radiata reads a package where it sits, but nothing backs it up for you."] =
            "احتفظ بنسخة من عملك في مكان آخر أيضا. يقرأ Radiata الحزمة حيث توجد، لكن لا شيء ينسخها احتياطيا نيابة عنك.",
        ["**Only install packages from people you trust.** Radiata asks before it loads a package and asks again whenever one changes, but it can't tell you whether a package is any good."] =
            "**لا تثبت إلا الحزم من أشخاص تثق بهم.** يسأل Radiata قبل تحميل أي حزمة ويسأل مجددا كلما تغيرت، لكنه لا يستطيع أن يخبرك إن كانت الحزمة جيدة.",
        ["When a package doesn't show up, or you want to give one to a friend, see [[workshop-sharing|Testing and sharing packages]]."] =
            "إذا لم تظهر حزمة، أو أردت إعطاءها لصديق، فراجع [[workshop-sharing|اختبار الحزم ومشاركتها]].",

        // ── topic:custom-materials ──
        ["Custom materials (build your own theme)"] =
            "المواد المخصصة (ابن سمتك الخاصة)",
        ["Beyond the eight built-in materials you can drop in **your own theme**. A theme is one folder holding a text file called `material.json` - colors, gradients, a system font name, and optionally images and sounds sitting beside it. Themes are **data only**: the format cannot express code, a network address, or a file outside the theme's own folder, so a theme can restyle the wheel and do nothing else."] =
            "إلى جانب المواد الثماني المضمّنة يمكنك إضافة **سمتك الخاصة**. السمة مجلد واحد يحوي ملفًا نصيًا باسم `material.json` - ألوان وتدرّجات واسم خط نظام، واختياريًا صور وأصوات بجانبه. السمات **بيانات فقط**: لا تستطيع الصيغة التعبير عن تعليمات برمجية أو عنوان شبكة أو ملف خارج مجلد السمة نفسه، فتستطيع السمة تغيير مظهر العجلة ولا شيء غيره.",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has two to download: **Starter**, the smallest complete theme with every line explained, and **Ember**, which uses every block below. The Workshop also covers color, contrast and texture advice this topic leaves out."] =
            "**ابدأ من عينة.** في [الورشة](https://getradiata.app/workshop/ar.html#samples) عينتان للتنزيل: **Starter**، أصغر سمة كاملة مع شرح لكل سطر، و**Ember**، التي تستخدم كل الكتل أدناه. تغطي الورشة أيضا نصائح اللون والتباين والنسيج التي يتركها هذا الموضوع.",
        ["Where it goes"] =
            "مكان الوضع",
        ["One folder per theme under `%APPDATA%\\Radiata\\Packages\\Materials` - for example `…\\Materials\\Lava\\material.json`. Paste that path into Explorer's address bar; Radiata creates the folder on first run."] =
            "مجلد واحد لكل سمة ضمن `%APPDATA%\\Radiata\\Packages\\Materials` - مثلًا `…\\Materials\\Lava\\material.json`. الصق ذلك المسار في شريط عناوين Explorer؛ ينشئ Radiata المجلد عند التشغيل الأول.",
        ["That folder also holds a **README.txt** written by Radiata, carrying a copy-paste example of every field. It's the reference; this topic is the tour."] =
            "يحوي ذلك المجلد أيضًا ملف **README.txt** يكتبه Radiata، يحمل مثالًا جاهزًا للنسخ لكل حقل. هو المرجع؛ وهذا الموضوع جولة تعريفية.",
        ["The smallest theme that works"] =
            "أصغر سمة تعمل",
        ["A **format 1** manifest is a JSON object with the keys below. `format`, `id`, `name`, `dark`, and a `colors` object holding at least `resting` and `armed` are required; everything else is optional."] =
            "بيان **format 1** كائن JSON بالمفاتيح أدناه. `format` و`id` و`name` و`dark` وكائن `colors` يحوي على الأقل `resting` و`armed` مطلوبة؛ وكل ما عداها اختياري.",
        ["`\"format\": 1` - which manifest version you wrote. Radiata reads **1 to 3**; the richer blocks further down need the higher number."] =
            "`\"format\": 1` - أي إصدار من البيان كتبت. يقرأ Radiata **من 1 إلى 3**؛ وتحتاج الكتل الأغنى أدناه إلى الرقم الأعلى.",
        ["`\"id\": \"lava\"` - 2-31 characters of lowercase a-z, 0-9 and `-`, starting with a letter or digit. This is the theme's identity: it becomes the token `custom-lava` in your config, so changing it later makes a **different** theme."] =
            "`\"id\": \"lava\"` - من 2 إلى 31 حرفًا من a-z الصغيرة و0-9 و`-`، يبدأ بحرف أو رقم. هذه هوية السمة: تصير الرمز `custom-lava` في إعداداتك، فتغييره لاحقًا يصنع سمة **مختلفة**.",
        ["`\"name\": \"Lava\"` - up to 24 characters; the label on the Customize tile. `\"author\"` (up to 64) is optional."] =
            "`\"name\": \"Lava\"` - حتى 24 حرفًا؛ التسمية على بلاطة تخصيص. `\"author\"` (حتى 64) اختياري.",
        ["`\"dark\": true` - whether the slices are dark. It flips labels and the hub to light ink and picks the dark fallbacks, so getting it wrong shows up as unreadable text rather than a wrong color."] =
            "`\"dark\": true` - هل الشرائح داكنة. يقلب التسميات والمحور إلى حبر فاتح ويختار البدائل الداكنة، فيظهر الخطأ فيه كنص غير مقروء لا كلون خاطئ.",
        ["`\"colors\"` - `\"resting\"` and `\"armed\"` are required; `\"confirm\"` (defaults to the armed color), `\"label\"` and `\"outline\"` are optional. Each is `#RRGGBB` or `#AARRGGBB`, where the leading pair is alpha - `\"#40FFFFFF\"` is a 25%-opaque white outline."] =
            "`\"colors\"` - `\"resting\"` و`\"armed\"` مطلوبان؛ و`\"confirm\"` (افتراضيًا لون التسليح) و`\"label\"` و`\"outline\"` اختيارية. كل منها `#RRGGBB` أو `#AARRGGBB`، حيث الزوج الأول هو الألفا - `\"#40FFFFFF\"` حد أبيض بعتامة 25%.",
        ["`\"labelFont\": \"Cascadia Code\"` - optional, and it must be a font **already installed on the PC**. An unknown name is ignored rather than treated as an error. Font *files* inside a package are never supported, deliberately."] =
            "`\"labelFont\": \"Cascadia Code\"` - اختياري، ويجب أن يكون خطًا **مثبّتًا على الحاسوب مسبقًا**. والاسم غير المعروف يتجاهل بدل أن يعامل كخطأ. أما *ملفات* الخطوط داخل الحزمة فغير مدعومة أبدًا، وذلك عن قصد.",
        ["`\"soundTheme\": \"physical\"` - which sounds the wheel makes on your theme. Name a sound set directly - `physical` (the default), `digital`, `kawaii`, `mesa`, `salvage`, `reactor` or `obsidian` - **or name a built-in material** and borrow whatever that one uses, so `\"pearl\"` gives you the digital set and `\"flat-dark\"` the physical set."] =
            "`\"soundTheme\": \"physical\"` - أي الأصوات تصدرها العجلة مع سمتك. سمّ مجموعة أصوات مباشرة - `physical` (الافتراضي)، `digital`، `kawaii`، `mesa`، `salvage`، `reactor` أو `obsidian` - **أو سمّ مادة مضمّنة** واستعر ما تستخدمه، فيمنحك `\"pearl\"` المجموعة الرقمية و`\"flat-dark\"` المجموعة المادية.",
        ["Naming the **material** is usually the better choice: your theme keeps sounding like the look you styled it after, even if that look's sounds are retuned in a later release. Naming a set pins it exactly."] =
            "تسمية **المادة** هي الخيار الأفضل عادةً: تظل سمتك تصوّت كالمظهر الذي صمّمتها على غراره، حتى إن أعيد ضبط أصوات ذلك المظهر في إصدار لاحق. تسمية المجموعة تثبّتها تمامًا.",
        ["`material.json` may contain `//` comments and trailing commas, so you can leave yourself notes. A color can also be written short as `#RGB`."] =
            "يمكن أن يحتوي `material.json` على تعليقات `//` وفواصل زائدة في النهاية، فتستطيع ترك ملاحظات لنفسك. ويمكن كتابة اللون مختصرا بصيغة `#RGB`.",
        ["Richer looks - format 2"] =
            "مظاهر أغنى - format 2",
        ["`\"fills\"` - a gradient per state (`resting` / `armed` / `confirm`) instead of a flat color. `\"type\"` is `solid`, `bowed` (the glassy Pearl/Obsidian ramp), or `linear` with an `\"angle\"`, plus a list of `\"stops\"` (each an `\"at\"` from 0 to 1 and a `\"color\"`)."] =
            "`\"fills\"` - تدرّج لكل حالة (`resting` / `armed` / `confirm`) بدلًا من لون مسطح. `\"type\"` هو `solid` أو `bowed` (منحدر Pearl/Obsidian الزجاجي) أو `linear` مع `\"angle\"`، إضافة إلى قائمة `\"stops\"` (كل منها `\"at\"` من 0 إلى 1 و`\"color\"`).",
        ["`\"hueWalk\"` - gives every slice its own hue around the ring, Kawaii-style, from `sat` / `light` / `armedSat` / `armedLight` (0-1) and `hueOffset`. It **overrides** the resting and armed fills."] =
            "`\"hueWalk\"` - يمنح كل شريحة درجة لونها الخاصة حول الحلقة، بأسلوب Kawaii، من `sat` / `light` / `armedSat` / `armedLight` (من 0 إلى 1) و`hueOffset`. **يتجاوز** تعبئات السكون والتسليح.",
        ["`\"outline\"` and `\"armedOutline\"` - `color`, `width`, and an optional `dash` pattern for the slice edge."] =
            "`\"outline\"` و`\"armedOutline\"` - `color` و`width` ونمط `dash` اختياري لحافة الشريحة.",
        ["`\"armed\"` - how an armed slice moves: `liftPx` (up to 24), `northPx` (±12), `scale` (1.0-1.15). It's a state treatment rather than continuous motion, so it survives [[accessibility|Reduce motion]]."] =
            "`\"armed\"` - كيف تتحرك الشريحة المسلّحة: `liftPx` (حتى 24)، `northPx` (±12)، `scale` (من 1.0 إلى 1.15). إنه معالجة حالة لا حركة مستمرة، فيبقى مع [[accessibility|تقليل الحركة]].",
        ["`\"glyph\"` - how slice icons are treated: `edge` (`inner`, `outer` or `none`) with `edgeColor` / `edgeWidth` / `edgeShadow`; a `glow` whose `color` can be the literal `\"slice\"` to take each slice's own accent, with `strength` 0-1; plus `castShadow` and `armedWash`."] =
            "`\"glyph\"` - كيف تعالج أيقونات الشرائح: `edge` (`inner` أو `outer` أو `none`) مع `edgeColor` / `edgeWidth` / `edgeShadow`؛ و`glow` يمكن أن يكون `color` فيه القيمة الحرفية `\"slice\"` لأخذ لون التمييز الخاص بكل شريحة، مع `strength` من 0 إلى 1؛ إضافة إلى `castShadow` و`armedWash`.",
        ["`\"label\"` - `case` (`upper` for stamped all-caps labels) and `sizeMul` (0.8-1.3). `\"gapPx\"` (0-14) sets the gap between slices."] =
            "`\"label\"` - `case` (`upper` للتسميات المختومة بأحرف كبيرة) و`sizeMul` (من 0.8 إلى 1.3). `\"gapPx\"` (من 0 إلى 14) يضبط الفجوة بين الشرائح.",
        ["`\"tile\"` - how the theme's swatch looks on the Customize tab: `edgeColor`, `sheen`, `lifted`, and an optional `texture`."] =
            "`\"tile\"` - كيف تبدو عيّنة السمة في علامة تبويب تخصيص: `edgeColor` و`sheen` و`lifted` و`texture` اختياري.",
        ["Images and sounds - format 3"] =
            "الصور والأصوات - format 3",
        ["Set `\"format\": 3` to reference files that live **in the theme's own folder**, by bare file name - a path isn't expressible in the format."] =
            "اضبط `\"format\": 3` للإشارة إلى ملفات تقع **في مجلد السمة نفسه**، باسم الملف المجرّد - لا يمكن التعبير عن مسار في الصيغة.",
        ["`\"textures\"` - `slice` and `hub` paint over the fill; `backdrop` draws behind the whole wheel. Each takes a `\"file\"` and an `\"opacity\"`, and slice/hub also take `\"tile\": true` to repeat the image at its natural size instead of stretching it. **PNG or JPG, up to 4 MB**; anything wider than 2048px is scaled down as it's decoded."] =
            "`\"textures\"` - `slice` و`hub` يرسمان فوق التعبئة؛ و`backdrop` يرسم خلف العجلة كلها. يأخذ كل منها `\"file\"` و`\"opacity\"`، ويأخذ slice/hub أيضًا `\"tile\": true` لتكرار الصورة بحجمها الطبيعي بدلًا من تمديدها. **PNG أو JPG، حتى 4 MB**؛ أي صورة أعرض من 2048 بكسل تصغّر أثناء فك ترميزها.",
        ["`\"sounds\"` - one file per event: `armed`, `fired`, `enableWheels`, `disableWheels`. **WAV only, up to 1 MB and 3 seconds each**; events you leave out keep the paired sound theme's own sound."] =
            "`\"sounds\"` - ملف واحد لكل حدث: `armed`، `fired`، `enableWheels`، `disableWheels`. **WAV فقط، حتى 1 MB و3 ثوانٍ لكل منها**؛ والأحداث التي تتركها تحتفظ بصوت سمة الصوت المقترنة.",
        ["A theme that ships sounds is **badged** on its Customize tile and takes over the **Sound Effects** picker - the Digital and Physical overrides go inactive, while Themed and Silent stay live."] =
            "السمة التي تشحن أصواتًا تحمل **شارة** على بلاطتها في تخصيص وتستولي على منتقي **المؤثرات الصوتية** - يصير تجاوزا Digital وPhysical غير نشطين، بينما يبقى Themed وSilent فعّالين.",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**. Changing *any* file - not just the manifest - re-asks for your confirmation on the next start."] =
            "حدود المجلد: **32 ملفًا** كحد أقصى، **4 MB لكل ملف**، **16 MB إجمالًا**. تغيير *أي* ملف - لا البيان فقط - يعيد طلب تأكيدك عند بدء التشغيل التالي.",
        ["If your theme doesn't appear"] =
            "إذا لم تظهر سمتك",
        ["**A package loads whole or not at all.** One bad value rejects the theme rather than half-applying it, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped material`."] =
            "**تحمّل الحزمة كاملةً أو لا تحمّل أبدًا.** قيمة سيئة واحدة ترفض السمة بدلًا من تطبيقها نصفيًا، ويكتب السبب في `%APPDATA%\\Radiata\\radiata-trace.log` - ابحث في ذلك الملف عن `[Packages] skipped material`.",
        ["The usual causes: a missing `dark` or `colors`, a color that isn't `#RRGGBB` / `#AARRGGBB`, an `id` with capitals or spaces, or a `format` number lower than the blocks you used."] =
            "الأسباب المعتادة: غياب `dark` أو `colors`، أو لون ليس `#RRGGBB` / `#AARRGGBB`، أو `id` بأحرف كبيرة أو مسافات، أو رقم `format` أقل من الكتل التي استخدمتها.",
        ["**A theme you had selected that stops loading** - package removed, or a change you declined - falls back to **Pearl**, quietly. Nothing else in your wheels changes."] =
            "**السمة التي كانت محددة وتوقفت عن التحميل** - حزمة أزيلت، أو تغيير رفضته - تعود إلى **لؤلؤ** بهدوء. لا يتغيّر شيء آخر في عجلاتك.",
        ["Custom themes are never offered during first-run setup, and the theme folder isn't part of a settings backup - copy the folder itself to move a theme to another PC."] =
            "لا تعرض السمات المخصصة أبدًا أثناء إعداد التشغيل الأول، وليس مجلد السمات جزءًا من نسخة الإعدادات الاحتياطية - انسخ المجلد نفسه لنقل سمة إلى جهاز آخر.",

        // ── topic:custom-arcade-games ──
        ["The Arcade also plays **games you write yourself**. One is a folder holding a `game.json` and a single **JavaScript** file. Drop it into Radiata's Arcade Games folder and it plays in the same round window as the built-in games, with the same buttons and the same best-score tracking."] =
            "يشغّل الأركيد أيضًا **ألعابًا تكتبها بنفسك**. اللعبة الواحدة مجلد يضم ملف `game.json` وملف **JavaScript** واحدًا. أفلته في مجلد Arcade Games الخاص بـ Radiata فيعمل في النافذة الدائرية نفسها التي تعمل فيها الألعاب المدمجة، بالأزرار نفسها وتتبّع أفضل نتيجة نفسه.",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has **Firefly** to download, a short but complete game with every line explained. The Workshop also walks through writing a game step by step."] =
            "**ابدأ من عينة.** في [الورشة](https://getradiata.app/workshop/ar.html#samples) يمكنك تنزيل **Firefly**، لعبة قصيرة لكنها كاملة مع شرح لكل سطر. وتشرح الورشة أيضا كتابة لعبة خطوة بخطوة.",
        ["**A game package contains code.** Radiata asks you to confirm a package before it ever runs, and again whenever any file in it changes - but the sandbox below is a limit on what a game *can* do, not a judgement about whether it's worth running. **Only install games from a source you trust.**"] =
            "**تحوي حزمة اللعبة تعليمات برمجية.** يطلب Radiata تأكيد الحزمة قبل تشغيلها لأول مرة، ومجددًا كل مرة يتغير فيها أي ملف فيها - لكن صندوق الرمل المذكور أدناه حد لما *تستطيع* اللعبة فعله، لا حكم على ما إذا كانت تستحق التشغيل. **لا تثبّت إلا الألعاب من مصدر تثق به.**",
        ["One folder per game under `%APPDATA%\\Radiata\\Packages\\Arcade Games` - for example `…\\Arcade Games\\Firefly\\game.json` beside `firefly.js`. Radiata creates the folder on first run."] =
            "مجلد واحد لكل لعبة ضمن `%APPDATA%\\Radiata\\Packages\\Arcade Games` - مثلًا `…\\Arcade Games\\Firefly\\game.json` بجانب `firefly.js`. ينشئ Radiata المجلد عند التشغيل الأول.",
        ["That folder's **README.txt** is the full API reference, kept current by Radiata itself."] =
            "ملف **README.txt** في ذلك المجلد هو مرجع الواجهة البرمجية الكامل، ويبقيه Radiata محدثًا بنفسه.",
        ["The manifest"] =
            "ملف البيان",
        ["`game.json` is a small JSON object. `format`, `id`, `title` and `entry` are required:"] =
            "`game.json` كائن JSON صغير. والحقول `format` و`id` و`title` و`entry` مطلوبة:",
        ["`\"format\": 1` - the manifest version."] =
            "`\"format\": 1` - إصدار البيان.",
        ["`\"id\": \"firefly\"` - 1-32 characters of lowercase a-z, 0-9 and `-`. The game's identity, and the `pkg-<id>` token."] =
            "`\"id\": \"firefly\"` - من 1 إلى 32 حرفًا من a-z الصغيرة و0-9 و`-`. هوية اللعبة، ورمز `pkg-<id>`.",
        ["`\"title\": \"Firefly\"` - up to 24 characters, shown in the picker."] =
            "`\"title\": \"Firefly\"` - حتى 24 حرفًا، يظهر في المنتقي.",
        ["`\"entry\": \"firefly.js\"` - the script, as a **bare file name** in the same folder (no paths), up to **256 KB**."] =
            "`\"entry\": \"firefly.js\"` - السكربت، بوصفه **اسم ملف مجرّدًا** في المجلد نفسه (بلا مسارات)، حتى **256 KB**.",
        ["`\"tint\": \"#5B8DEF\"` - optional: your cabinet's colour in the **Arcade Launcher**, as `#RGB` or `#RRGGBB`. Leave it out for the plain grey cabinet. Either way, your `title` is printed on the cabinet's nameplate."] =
            "`\"tint\": \"#5B8DEF\"` - اختياري: لون خزانتك في **مشغّل الأركيد**، بصيغة `#RGB` أو `#RRGGBB`. دونه تكون الخزانة رمادية. وفي الحالتين يطبع `title` على لوحة اسم الخزانة.",
        ["`\"preview\": \"preview.png\"` - optional: the picture on your cabinet's screen until the game has been played, as a **bare PNG or JPG file name** in the same folder. Make it square, with the round playfield filling it."] =
            "`\"preview\": \"preview.png\"` - اختياري: الصورة على شاشة خزانتك حتى تلعب اللعبة، **كاسم ملف PNG أو JPG فقط** في المجلد نفسه. اجعلها مربعة، وساحة اللعب الدائرية تملؤها.",
        ["`\"badge\": \"badge.png\"` - optional: an illustration for your cabinet's nameplate, drawn to the left of your title the way the built-in cabinets carry theirs. A **PNG with a transparent background**, in its own colours; it overflows the nameplate above and below."] =
            "`\"badge\": \"badge.png\"` - اختياري: رسم للوحة اسم خزانتك، يرسم على يسار العنوان كما تحمل الخزائن المضمنة رسومها. **ملف PNG بخلفية شفافة**، بألوانه الخاصة؛ ويبرز فوق لوحة الاسم وتحتها.",
        ["`\"glyph\": \"glyph.png\"` - optional: your game's icon on its wheel slices. A **PNG** whose transparency is the shape - the wheel colours it like every other slice icon, so draw it in one colour on a transparent background. Without one, drop-in games share a script icon."] =
            "`\"glyph\": \"glyph.png\"` - اختياري: أيقونة لعبتك على شرائح العجلة. **ملف PNG** شفافيته هي الشكل - تلونه العجلة كأي أيقونة شريحة أخرى، لذا ارسمه بلون واحد على خلفية شفافة. دونه تتشارك الألعاب المضافة أيقونة سكربت.",
        ["Once your game has been played, its cabinet shows the player's own last board instead - Radiata saves it as `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. That file is also the easiest way to make a preview: play your game, close it, and copy the file into your package as `preview.png`."] =
            "بعد أن تلعب لعبتك، تعرض خزانتها آخر لوحة للاعب بدلا من ذلك - يحفظها Radiata باسم `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. وهذا الملف هو أيضا أسهل طريقة لصنع معاينة: العب لعبتك، وأغلقها، وانسخ الملف إلى حزمتك باسم `preview.png`.",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**."] =
            "حدود المجلد: **32 ملفًا** كحد أقصى، **4 MB لكل ملف**، **16 MB إجمالًا**.",
        ["How a game runs"] =
            "كيفية عمل اللعبة",
        ["Your script runs in a **locked-down interpreter inside a separate sandboxed process**: no files, no network, no clipboard, no other programs. Only the functions below exist at all. Memory is capped by Windows at 128 MB."] =
            "يعمل سكربتك في **مفسّر مقيّد داخل عملية معزولة منفصلة**: لا ملفات ولا شبكة ولا حافظة ولا برامج أخرى. لا يوجد أصلًا سوى الدوال المذكورة أدناه. ويحدّ Windows الذاكرة بـ 128 ميجابايت.",
        ["Define **`tick(dt)`**, called for every fixed **1/120 second** step, and **`draw()`**, called once per screen frame. Issue drawing commands only from inside `draw()`."] =
            "عرّف **`tick(dt)`**، وتستدعى في كل خطوة ثابتة قدرها **1/120 ثانية**، و**`draw()`**، وتستدعى مرة لكل إطار شاشة. أصدر أوامر الرسم من داخل `draw()` فقط.",
        ["There's a hard time and instruction budget **per drawn frame**. A script that overruns is stopped and restarted (your saved data survives); one that keeps overrunning ends in a plain card you can back out of. It can slow itself down - it can't slow the PC down."] =
            "هناك ميزانية صارمة للوقت والتعليمات **لكل إطار مرسوم**. السكربت الذي يتجاوزها يوقف ويعاد تشغيله (تبقى بياناتك المحفوظة)؛ والذي يواصل التجاوز ينتهي إلى بطاقة بسيطة يمكنك الخروج منها. يمكنه إبطاء نفسه - لا إبطاء الجهاز.",
        ["**Radiata owns {circle} and {triangle}** - closing the game and the help card - so your script never sees those two buttons."] =
            "**يحتفظ Radiata بـ {circle} و{triangle}** - إغلاق اللعبة وبطاقة المساعدة - فلا يرى سكربتك هذين الزرّين أبدًا.",
        ["Drawing: the playfield is a disc"] =
            "الرسم: ساحة اللعب قرص",
        ["Everything is drawn in **polar coordinates**: `r` runs 0 at the center to 1 at the rim, angles are **degrees** with 0 at 12 o'clock, increasing clockwise. Radiata does the trigonometry and clips to the circle, so a game can't draw outside its window."] =
            "يرسم كل شيء في **إحداثيات قطبية**: `r` من 0 في المركز إلى 1 عند الحافة، والزوايا **بالدرجات** مع 0 عند اتجاه الساعة 12، متزايدة باتجاه عقارب الساعة. يقوم Radiata بحساب المثلثات ويقص على الدائرة، فلا تستطيع اللعبة الرسم خارج نافذتها.",
        ["A game places everything by radius and angle: r runs from 0 at the center to 1 at the rim, and angles are degrees clockwise from 12 o'clock."] =
            "تضع اللعبة كل شيء بنصف القطر والزاوية: r من 0 في المركز إلى 1 عند الحافة، والزوايا بالدرجات باتجاه عقارب الساعة بدءا من اتجاه الساعة 12.",
        ["Colors are numbers in **`0xAARRGGBB`** form - alpha first, so `0xFFFF0000` is opaque red."] =
            "الألوان أرقام بصيغة **`0xAARRGGBB`** - الألفا أولًا، فـ `0xFFFF0000` أحمر معتم.",
        ["The commands, up to **1024 per frame**: `arc(r0, r1, a0, a1, color)` for a ring segment, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` for 3-16 points, and `text(r, a, size, \"str\", color)` for up to 64 characters."] =
            "الأوامر، حتى **1024 لكل إطار**: `arc(r0, r1, a0, a1, color)` لجزء من حلقة، و`ring(r, width, color, edge)`، و`dot(r, a, size, color)`، و`line(r0, a0, r1, a1, w, color)`، و`poly([r,a, r,a, …], color)` من 3 إلى 16 نقطة، و`text(r, a, size, \"str\", color)` حتى 64 حرفًا.",
        ["Input"] =
            "الإدخال",
        ["Read-only globals, refreshed every frame: **`stickX`** / **`stickY`** (-1 to 1, with y positive **downward**, matching the screen), **`crossDown`** / **`squareDown`** while held, **`crossPressed`** / **`squarePressed`** true for one frame per press, and **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, also one frame per press."] =
            "متغيرات عامة للقراءة فقط، تتحدّث كل إطار: **`stickX`** / **`stickY`** (من -1 إلى 1، مع y موجب **نحو الأسفل** مطابقًا للشاشة)، و**`crossDown`** / **`squareDown`** أثناء الضغط، و**`crossPressed`** / **`squarePressed`** صحيحة لإطار واحد لكل ضغطة، و**`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**، وهي أيضًا إطار واحد لكل ضغطة.",
        ["Saving, randomness, and sound"] =
            "الحفظ والعشوائية والصوت",
        ["Write the reserved key **`hiscore`** (a whole number as text) to publish a best score to the Arcade picker."] =
            "اكتب المفتاح المحجوز **`hiscore`** (عددًا صحيحًا كنص) لنشر أفضل نتيجة في منتقي Arcade.",
        ["**`rand()`** returns 0-1 and is seeded per session, so a replay of the same inputs behaves the same way."] =
            "**`rand()`** تعيد من 0 إلى 1 وتبذر لكل جلسة، فتتصرف إعادة الإدخالات نفسها بالطريقة نفسها.",
        ["**`cue(\"name\")`** plays one of nine built-in sounds: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Any other name is silent, and there's no way to ship your own audio."] =
            "**`cue(\"name\")`** تشغّل أحد تسعة أصوات مضمّنة: `fire`، `tick`، `good`، `denied`، `kill`، `zap`، `hurt`، `clear`، `gameover`. أي اسم آخر صامت، ولا توجد طريقة لإرفاق صوتك الخاص.",
        ["Things that trip people up"] =
            "أخطاء شائعة",
        ["Scripts run in **strict mode**, so a variable you forget to declare is an error. There's no `console`, `eval`, timer or `import` - to see a value while you work, draw it with `text()`."] =
            "تعمل السكربتات في **الوضع الصارم**، لذا فالمتغير الذي تنسى تعريفه خطأ. لا يوجد `console` ولا `eval` ولا مؤقتات ولا `import` - لرؤية قيمة أثناء العمل، ارسمها باستخدام `text()`.",
        ["**Keep every drawing value in range**: radii 0-1, `dot` size up to 0.5, `line` width up to 0.1, `text` size up to 0.3, angles within ±3600. Radiata treats an out-of-range call as a broken game and restarts the script; after three restarts it shows a problem card. Clamp your numbers."] =
            "**أبق كل قيمة رسم ضمن نطاقها**: أنصاف الأقطار من 0 إلى 1، وحجم `dot` حتى 0.5، وعرض `line` حتى 0.1، وحجم `text` حتى 0.3، والزوايا ضمن ±3600. يعامل Radiata أي استدعاء خارج النطاق على أنه لعبة معطلة ويعيد تشغيل السكربت؛ وبعد ثلاث إعادات تشغيل يعرض بطاقة مشكلة. قيد أرقامك.",
        ["JavaScript's bit operators (`|`, `&`, `<<`) produce **signed** numbers, and a negative color draws as nothing. Build colors with arithmetic, or finish the expression with `>>> 0`."] =
            "معاملات البت في JavaScript (`|` و`&` و`<<`) تنتج أرقاما **ذات إشارة**، واللون السالب لا يرسم شيئا. ابن الألوان بالحساب، أو أنه التعبير بـ `>>> 0`.",
        ["Input is read **once per drawn frame**, but `tick` can run several times in that frame, and each run sees the same `crossPressed`. Make a press count once - the Firefly sample shows a way."] =
            "يقرأ الإدخال **مرة واحدة لكل إطار مرسوم**، لكن `tick` قد يعمل عدة مرات في ذلك الإطار، وكل تشغيل يرى قيمة `crossPressed` نفسها. اجعل كل ضغطة تحسب مرة واحدة - تعرض عينة Firefly طريقة لذلك.",
        ["`text()` centers the string on its point. The last argument of `ring()` is a thin edge color, or `0` for none."] =
            "`text()` يضع النص في منتصف نقطته. والوسيط الأخير في `ring()` لون حافة رفيعة، أو `0` لعدم وجود حافة.",
        ["Each drawn frame gets **2,000,000 statements and 8 ms** for all of its `tick` runs plus `draw`, at most **8** `cue` calls and **16** `kvSet` writes. `kvSet` throws an error when a key or value is too long or the 4 KB store is full."] =
            "يحصل كل إطار مرسوم على **2,000,000 تعليمة و8 مللي ثانية** لكل تشغيلات `tick` مع `draw`، وعلى **8** استدعاءات `cue` و**16** عملية كتابة `kvSet` كحد أقصى. يطلق `kvSet` خطأ إذا كان مفتاح أو قيمة أطول من اللازم أو امتلأ مخزن 4 KB.",
        ["If your game doesn't appear"] =
            "إذا لم تظهر لعبتك",
        ["**A package loads whole or not at all**, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped arcade game`."] =
            "**تحمّل الحزمة كاملةً أو لا تحمّل أبدًا**، ويكتب السبب في `%APPDATA%\\Radiata\\radiata-trace.log` - ابحث في ذلك الملف عن `[Packages] skipped arcade game`.",
        ["The usual causes: an `entry` that isn't a plain `.js` file name sitting in the same folder, a script over 256 KB, an `id` with capitals or spaces, or a confirmation that was declined (it only re-asks once the package changes)."] =
            "الأسباب المعتادة: `entry` ليس اسم ملف `.js` بسيطًا في المجلد نفسه، أو سكربت يتجاوز 256 KB، أو `id` بأحرف كبيرة أو مسافات، أو تأكيد رفض (لا يعاد الطلب إلا عند تغيّر الحزمة).",
        ["A game that loaded but misbehaves shows its card in the window rather than an error - and a package you delete simply stops being offered."] =
            "اللعبة التي حمّلت لكنها تسيء التصرف تعرض بطاقتها في النافذة بدلًا من خطأ - والحزمة التي تحذفها تتوقف ببساطة عن الظهور.",
        ["**Script errors** go to the same log: search it for `[Arcade] script` to see the error message."] =
            "**أخطاء السكربت** تذهب إلى السجل نفسه: ابحث فيه عن `[Arcade] script` لرؤية رسالة الخطأ.",

        // ── topic:workshop-sharing ──
        ["Testing and sharing packages"] =
            "اختبار الحزم ومشاركتها",
        ["Testing"] =
            "الاختبار",
        ["Radiata reads packages **only at startup**: exit from the tray and start it again after every change."] =
            "يقرأ Radiata الحزم **عند بدء التشغيل فقط**: اخرج من شريط النظام وشغله من جديد بعد كل تغيير.",
        ["A change to **any** file in a package brings the confirmation back on the next start. If you decline it, the package stays off until its files change again."] =
            "أي تغيير في **أي** ملف في الحزمة يعيد التأكيد عند التشغيل التالي. إذا رفضته، تبقى الحزمة متوقفة حتى تتغير ملفاتها مرة أخرى.",
        ["**Nothing happening?** Open `%APPDATA%\\Radiata\\radiata-trace.log` in a text editor and search for `[Packages] skipped`. Each line names the package folder and the exact problem - a missing field, a value out of range, a file that isn't there."] =
            "**لا يحدث شيء؟** افتح `%APPDATA%\\Radiata\\radiata-trace.log` في محرر نصوص وابحث عن `[Packages] skipped`. يذكر كل سطر مجلد الحزمة والمشكلة بالضبط - حقل ناقص، أو قيمة خارج النطاق، أو ملف غير موجود.",
        ["**The most common mistake is one folder too many.** Unzipping often makes `Materials\\Lava\\Lava\\material.json`; Radiata looks for the manifest directly inside `Materials\\Lava`. Move the files up a level."] =
            "**أكثر الأخطاء شيوعا مجلد زائد.** كثيرا ما ينتج فك الضغط `Materials\\Lava\\Lava\\material.json`؛ ويبحث Radiata عن ملف البيان مباشرة داخل `Materials\\Lava`. انقل الملفات مستوى واحدا إلى الأعلى.",
        ["Radiata looks for the manifest directly inside the package's own folder; the extra folder level an unzip often adds hides it."] =
            "يبحث Radiata عن ملف البيان مباشرة داخل مجلد الحزمة نفسه؛ والمستوى الزائد من المجلدات الذي يضيفه فك الضغط غالبا يخفيه.",
        ["A game that loads but misbehaves writes its script errors to the same log - search for `[Arcade] script`."] =
            "اللعبة التي تحمل لكنها تتصرف بشكل خاطئ تكتب أخطاء السكربت في السجل نفسه - ابحث عن `[Arcade] script`.",
        ["Sharing"] =
            "المشاركة",
        ["Say what the package is and what it does, and only include images, sounds and code you have the right to share."] =
            "اذكر ما هي الحزمة وما الذي تفعله، ولا تضمن إلا الصور والأصوات والتعليمات البرمجية التي يحق لك مشاركتها.",
        ["Radiata's license doesn't extend to your package: what you make in these formats is yours to license however you like."] =
            "لا يمتد ترخيص Radiata إلى حزمتك: ما تصنعه بهذه الصيغ لك أن ترخصه كما تشاء.",
        ["Moving to another PC? Settings backups don't include packages - copy the `Packages` folder across yourself."] =
            "تنتقل إلى جهاز آخر؟ النسخ الاحتياطية للإعدادات لا تشمل الحزم - انسخ مجلد `Packages` بنفسك.",

        // ── topic:triggers ──
        ["Triggers (summon chords)"] =
            "المشغّلات (مجموعات أزرار الاستدعاء)",
        ["triggers chord builder hold tap add another trigger remove row fn bumper trigger touchpad swipe select start l3 r3 dpad combined summon invoke gesture customize"] =
            "مشغّلات منشئ مجموعات ضغط مطوّل نقر إضافة مشغّل آخر إزالة صف fn مصد زناد لوحة اللمس سحب select start l3 r3 dpad مركّب استدعاء إيماءة تخصيص",
        ["**Settings ▸ Customize ▸ Triggers** is the chord builder: which controller gestures summon a wheel. Each row is one live chord - a **button** (Fn or L4/R4 / Bumper / Trigger / Touchpad) paired with how it's **combined** (Trigger, Home, L3/R3, Select/Start, D-Pad L/R, or a touchpad edge-swipe). The options adapt to the detected pad: **Fn** appears for a DualSense Edge, **L4/R4** for a pad with extra buttons on Bluetooth, and **Touchpad** only for pads that have one. Those dedicated buttons need no second button - they open a wheel on their own."] =
            "**الإعدادات ◂ تخصيص ◂ المشغّلات** هو أداة بناء التشكيلات: أي إيماءات جهاز التحكم تستدعي عجلة. كل صف تشكيلة نشطة واحدة - **زر** (Fn أو L4/R4 / المصد / الزناد / لوحة اللمس) مقترن بطريقة **الدمج** (الزناد، Home، L3/R3، Select/Start، أزرار الاتجاهات يمين/يسار، أو سحبة من حافة لوحة اللمس). وتتكيف الخيارات مع الجهاز المكتشف: يظهر **Fn** مع DualSense Edge، و**L4/R4** مع جهاز له أزرار إضافية عبر Bluetooth، و**لوحة اللمس** مع الأجهزة التي فيها واحدة فقط. وهذه الأزرار المخصصة لا تحتاج إلى زر ثانٍ - فهي تفتح عجلة بمفردها.",
        ["**+ Add Another Trigger** appends a row; a row's **✕** removes it. Every row stays live at once - up to **three** - and the set is remembered **per controller type**, so an Edge and an Xbox pad each keep their own chords."] =
            "**+ إضافة إيماءة أخرى** يضيف صفًا؛ و**✕** الصف يزيله. يبقى كل صف حيًا في الوقت نفسه - حتى **ثلاثة** - وتحفظ المجموعة **لكل نوع جهاز تحكم**، فيحتفظ كل من Edge وجهاز Xbox بمجموعاته الخاصة.",
        ["How each chord behaves (hold + tap, which wheel it opens, Hold vs Toggle) is in [[opening-a-wheel|Opening a wheel]]; the both-sides version of a chord toggles the wheels on and off, see [[wheel-open-extras|While a wheel is open]]."] =
            "كيفية تصرف كل مجموعة أزرار (ضغط مطوّل + نقر، أي عجلة تفتح، Hold مقابل Toggle) في [[opening-a-wheel|فتح عجلة]]؛ ونسخة الجانبين من المجموعة تبدّل العجلات تشغيلًا وإيقافًا، راجع [[wheel-open-extras|أثناء فتح العجلة]].",

        // ── topic:accessibility ──
        ["Accessibility (Settings ▸ Advanced)"] =
            "إمكانية الوصول (الإعدادات ◂ متقدم)",
        ["accessibility wheels toggle on off swap left right both sticks either stick one stick ignores opposite stick sidedness aim drift always show hub battery reduce motion confetti fade parallax animation effects still narration speak speech spoken screen reader narrator voice volume system-wide windows narrator settings onboarding blind low vision"] =
            "إمكانية الوصول عجلات تبديل تشغيل إيقاف تبادل يسار يمين كلتا العصاتين أي عصا عصا واحدة تجاهل العصا المقابلة جانبية تصويب انجراف إظهار المحور دائمًا بطارية تقليل الحركة قصاصات تلاشي محاذاة الحركة تحريك مؤثرات ثابت سرد نطق كلام منطوق قارئ الشاشة الراوي صوت مستوى الصوت على مستوى النظام windows الراوي إعدادات التهيئة الأولية أعمى ضعف البصر",
        ["The **Accessibility** section gathers six checkboxes, in **Settings ▸ Advanced**. The same set is offered during first-run setup from the **Accessibility…** button on the Look step:"] =
            "يجمع قسم **إمكانية الوصول** ست خانات اختيار، في **الإعدادات ◂ متقدم**. تعرض المجموعة نفسها أثناء إعداد التشغيل الأول من زر **إمكانية الوصول** في خطوة Look:",
        ["Two of them are **indented under the box that ticks them**: turning on **Wheels toggle on/off** also ticks **Swap left/right**, and turning on **Reduce motion** also ticks **Always show hub**, because each pair works best together. Both children stay yours to tick or clear on their own, and once you set one by hand it stops following its parent."] =
            "اثنتان منها **مزاحتان تحت الخانة التي تحددهما**: تشغيل **تفتح العجلات وتغلق بالتبديل** يحدد أيضًا **تبديل اليسار/اليمين**، وتشغيل **تقليل الحركة** يحدد أيضًا **إظهار المركز دائمًا**، لأن كل زوج يعمل على أفضل نحو معًا. تبقى كلتا الخانتين التابعتين لك تحددهما أو تمسحهما بنفسك، وما إن تضبط واحدة يدويًا تتوقف عن تتبّع أصلها.",
        ["**Wheels toggle on/off** and **Swap left/right** - whether an invoked wheel stays up until you dismiss it, and which wheel each hand opens (see [[opening-a-wheel|Opening a wheel]])."] =
            "**تفتح العجلات وتغلق بالتبديل** و**تبديل اليسار/اليمين** - هل تبقى العجلة المستدعاة ظاهرة حتى تغلقها، وأي عجلة تفتحها كل يد (راجع [[opening-a-wheel|فتح عجلة]]).",
        ["**Narration** - speaks what you're doing aloud: which wheel opened, the slice you arm and its current state, hold-to-confirm progress, what a fire actually did, volume levels as you scrub, edit-mode moves, and Game Grid browsing. It works alongside a screen reader."] =
            "**السرد الصوتي** - ينطق ما تفعله بصوت عالٍ: أي عجلة فتحت، والشريحة التي تسلّحها وحالتها الحالية، وتقدّم الضغط المطوّل للتأكيد، وما فعله التنفيذ فعلًا، ومستويات الصوت أثناء التمرير، وتحركات وضع التحرير، وتصفح شبكة الألعاب. يعمل جنبًا إلى جنب مع قارئ الشاشة.",
        ["**Narration covers the wheel and the Game Grid only** - the overlay surfaces a screen reader can't see. Settings, first-run setup and every other ordinary window are **Windows Narrator's** job, so run Narrator alongside Radiata if you want those read too. Ticking **Narration** offers a button to turn Narrator on; and if Narrator is running when you first set Radiata up, Narration starts on by itself."] =
            "**تغطي القراءة الصوتية العجلة وشبكة الألعاب فقط** - وهي الأسطح المتراكبة التي لا يراها قارئ الشاشة. أما الإعدادات والإعداد الأول وكل نافذة عادية أخرى فمن مهمة **الراوي في Windows**، لذا شغّل الراوي إلى جانب Radiata إن أردت قراءتها أيضًا. وتحديد **القراءة الصوتية** يعرض زرًا لتشغيل الراوي؛ وإن كان الراوي يعمل أول مرة تعدّ فيها Radiata، بدأت القراءة الصوتية مفعّلة من تلقاء نفسها.",

        // ── topic:show-labels ──
        ["Show labels on (slice text)"] =
            "إظهار التسميات على (نص الشرائح)",
        ["show labels on slice labels label text names icons not logos standard icons all slices no slices slices i choose show label checkbox unlabelled artwork logo cover png"] =
            "إظهار التسميات على تسميات الشرائح نص تسمية أسماء أيقونات لا شعارات أيقونات معيارية كل الشرائح لا شرائح شرائح أختارها خانة إظهار التسمية بلا تسمية عمل فني شعار غلاف png",
        ["**Show labels on** - which slices draw their text label on the wheel."] =
            "**إظهار التسميات على** - أي الشرائح ترسم تسميتها النصية على العجلة.",
        ["**All Slices** - every slice is labelled, artwork ones included."] =
            "**كل الشرائح** - تسمّى كل شريحة، بما فيها ذات العمل الفني.",
        ["**No Slices** - no slice is labelled."] =
            "**بلا شرائح** - لا تسمّى أي شريحة.",
        ["**Editing a wheel is exempt:** in edit mode and its Add picker, non-logo slices always show their labels whatever this is set to."] =
            "**تحرير العجلة مستثنى:** في وضع التحرير ومنتقي الإضافة فيه، تعرض الشرائح غير الشعارية تسمياتها دائمًا مهما كان هذا الإعداد.",
        ["The setting lives in **Settings ▸ Customize**, directly under the **D-Pad 🡄 🡆** selector, and applies **live** - bring up a wheel to see it."] =
            "يعيش الإعداد في **الإعدادات ◂ تخصيص**، تحت محدد **أزرار الاتجاهات 🡄 🡆** مباشرة، ويطبّق **مباشرة** - أظهر عجلة لتراه.",

        // ── topic:integrations ──
        ["Integrations (SteamGridDB, Discord & OBS)"] =
            "التكاملات (SteamGridDB وDiscord وOBS)",
        ["integrations steamgriddb sgdb api key discord obs websocket port password configure cover art logos"] =
            "تكاملات steamgriddb sgdb مفتاح api discord obs websocket منفذ كلمة مرور ضبط صور الأغلفة شعارات",
        ["**Settings ▸ Advanced ▸ Integrations** connects optional external services:"] =
            "يوصل **الإعدادات ◂ متقدم ◂ التكاملات** خدمات خارجية اختيارية:",
        ["**Configure Discord Integration…** - sets the Discord credentials (Client ID/Secret) that Join/Leave Voice Channel slices use, the same wizard the slice editor offers. Stored encrypted (Windows DPAPI) and sent only to Discord."] =
            "**إعداد تكامل Discord…** - يحدد بيانات اعتماد Discord (معرّف العميل والسر) التي تستخدمها شرائح الانضمام إلى القناة الصوتية ومغادرتها، وهو المعالج نفسه الذي يعرضه محرر الشرائح. تخزّن مشفّرة (عبر DPAPI في Windows) ولا ترسل إلا إلى Discord.",
        ["**SteamGridDB API key** - unlocks portrait cover art and logos for every storefront's games (the Game Grid's **Select**/**Start** cycling). Get a free key at [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). It's checked when you finish entering it, and entering your first key automatically fills in covers skipped while you had none."] =
            "**مفتاح API لـ SteamGridDB** - يفتح صور أغلفة عمودية وشعارات لألعاب كل متجر (تدوير **تحديد**/**Start** في شبكة الألعاب). احصل على مفتاح مجاني من [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). يفحص عند إتمام إدخاله، وإدخال مفتاحك الأول يملأ تلقائيًا الأغلفة التي تخطّيت حين لم يكن لديك مفتاح.",
        ["**OBS Studio** - **Configure OBS Integration…** sets the WebSocket port and password, one connection shared by every OBS slice; see [[obs-studio|OBS slices]]. **Test** checks it against a running OBS. The password is stored encrypted (DPAPI)."] =
            "**OBS Studio** - **إعداد تكامل OBS…** يحدد منفذ WebSocket وكلمة مروره، وهو اتصال واحد تتشاركه كل شرائح OBS؛ راجع [[obs-studio|شرائح OBS]]. و**اختبار** يتحقق منه مقابل OBS قيد التشغيل. وتخزّن كلمة المرور مشفّرة (DPAPI).",
        ["With [[playnite|Playnite]] and a SteamGridDB key both set up, a **Prefer Playnite covers** toggle appears in the **Game Grid** section. Without Playnite, an **Install Playnite…** button appears here instead."] =
            "عند إعداد [[playnite|Playnite]] ومفتاح SteamGridDB معًا، يظهر مفتاح **تفضيل أغلفة Playnite** في قسم **شبكة الألعاب**. وبدون Playnite يظهر هنا زر **تثبيت Playnite…** بدلًا منه.",

        // ── topic:playnite ──
        ["Playnite (optional library manager)"] =
            "Playnite (مدير مكتبة اختياري)",
        ["playnite library manager games covers metadata optional install third party emulators"] =
            "playnite مدير مكتبة ألعاب أغلفة بيانات وصفية اختياري تثبيت طرف ثالث محاكيات",
        ["**Playnite** is a free, open-source game-library manager for Windows ([playnite.link](https://playnite.link)) that gathers all your games - Steam, Epic, GOG, Xbox, emulators, standalone - with metadata and cover art."] =
            "**Playnite** مدير مكتبة ألعاب مجاني مفتوح المصدر لنظام Windows ([playnite.link](https://playnite.link)) يجمع كل ألعابك - Steam وEpic وGOG وXbox والمحاكيات والمستقلة - مع البيانات الوصفية وصور الأغلفة.",
        ["Radiata works fully **without** it, scanning your storefronts directly. Playnite is an optional enhancement:"] =
            "يعمل Radiata بالكامل **بدونه**، بفحص متاجرك مباشرة. Playnite تحسين اختياري:",
        ["**More games found** - Radiata reads Playnite's library, so emulated and manually-added games a raw storefront scan misses can appear in the Game Grid."] =
            "**العثور على مزيد من الألعاب** - يقرأ Radiata مكتبة Playnite، فتظهر في شبكة الألعاب ألعاب المحاكاة والمضافة يدويًا التي يفوّتها فحص المتاجر الخام.",
        ["**Curated cover art** - Playnite's own covers become an art source, with a **Prefer Playnite covers** toggle (Advanced ▸ Game Grid) to favor them over SteamGridDB."] =
            "**صور أغلفة منتقاة** - تصير أغلفة Playnite مصدرًا للفن، مع مفتاح **تفضيل أغلفة Playnite** لتفضيلها على SteamGridDB.",
        ["Not installed? **Install Playnite…** in Advanced ▸ Integrations opens its download page. Set up your libraries in Playnite and Radiata picks them up automatically."] =
            "ألم تثبّته؟ زر **تثبيت Playnite…** في متقدم ◂ التكاملات يفتح صفحة تنزيله. أعدّ مكتباتك في Playnite وسيلتقطها Radiata تلقائيًا.",

        // ── topic:system-actions ──
        ["System tools & Backup (Settings ▸ Advanced)"] =
            "أدوات النظام والنسخ الاحتياطي (الإعدادات ◂ متقدم)",
        ["run first run setup onboarding wizard reset starter slices customize recommended install repair drivers recover controller setup email log hid diagnostics quit exit backup restore reset wipe zip factory defaults undo clean install always show hub practice reduce motion check for updates automatic update skip version"] =
            "تشغيل إعداد التشغيل الأول معالج التهيئة إعادة ضبط شرائح مبدئية تخصيص موصى به تثبيت إصلاح برامج التشغيل استعادة جهاز التحكم إعداد جهاز التحكم إرسال السجل بالبريد تشخيص hid إنهاء خروج نسخ احتياطي استعادة إعادة ضبط مسح zip افتراضيات المصنع تراجع تثبيت نظيف إظهار المحور دائمًا تدريب تقليل الحركة التحقق من التحديثات تحديث تلقائي تخطي الإصدار",
        ["**Settings ▸ Advanced ▸ System** holds the update controls, the **Start with Windows** toggle, the **Troubleshooting** dropdown, and **Quit Radiata**:"] =
            "يحوي **الإعدادات ◂ متقدم ◂ النظام** عناصر تحكم التحديث، ومفتاح **التشغيل مع Windows**، وقائمة **استكشاف الأخطاء وإصلاحها** المنسدلة، و**إنهاء Radiata**:",
        ["**Quit Radiata** - releases its virtual controller and requests removal of the HidHide blocks Radiata owns. Another tool's blocks remain; see [[hidhide-troubleshooting|HidHide troubleshooting]] if the pad stays hidden."] =
            "**إنهاء Radiata** - يحرر جهاز التحكم الافتراضي ويطلب إزالة حجوبات HidHide التي يملكها Radiata. أما حجوبات أداة أخرى فتبقى؛ راجع [[hidhide-troubleshooting|استكشاف أخطاء HidHide]] إن ظل الجهاز مخفيًا.",
        ["The Troubleshooting dropdown"] =
            "قائمة استكشاف الأخطاء وإصلاحها… المنسدلة",
        ["**Run First-Run Setup…** - re-runs the setup wizard (controller check, drivers, look, cover art, starter wheels). Your customized wheels are never overwritten without asking."] =
            "**تشغيل إعداد التشغيل الأول…** - يعيد تشغيل معالج الإعداد (فحص جهاز التحكم، وبرامج التشغيل، والمظهر، وصور الأغلفة، والعجلات المبدئية). لا تستبدل عجلاتك المخصصة أبدًا دون سؤال.",
        ["**Install/Repair Drivers…** - installs or repairs the isolation drivers (ViGEmBus + HidHide). Fixes most isolation problems, and shows a result log."] =
            "**تثبيت/إصلاح برامج التشغيل…** - يثبّت برامج تشغيل العزل (ViGEmBus وHidHide) أو يصلحها. يحل معظم مشكلات العزل، ويعرض سجل نتائج.",
        ["**HID Diagnostics…** - a live view of the raw controller reports Radiata reads. Useful when support asks what your pad is actually sending."] =
            "**تشخيص HID…** - عرض مباشر لتقارير جهاز التحكم الخام التي يقرأها Radiata. مفيد حين يسألك الدعم عما يرسله جهازك فعلًا.",
        ["**Controller Setup…** - re-runs the controller detection and mapping wizard on its own, without the rest of first-run setup."] =
            "**إعداد وحدة التحكم…** - يعيد تشغيل معالج اكتشاف جهاز التحكم وتعيينه بمفرده، دون باقي إعداد التشغيل الأول.",
        ["**Email Log to Developer…** - saves a diagnostic ZIP to your Desktop and opens an addressed email. Review the ZIP before attaching and sending it: logs can include device identifiers, account names in file paths, and application or game names. Radiata does not automatically send the attachment."] =
            "**إرسال السجل إلى المطوّر…** - يحفظ ملف ZIP تشخيصيًا على سطح المكتب ويفتح رسالة بريد معنونة. راجع الملف قبل إرفاقه وإرساله: فقد تتضمن السجلات معرّفات الأجهزة وأسماء الحسابات ضمن مسارات الملفات وأسماء التطبيقات أو الألعاب. ولا يرسل Radiata المرفق تلقائيًا.",
        ["**Back Up Settings…** and **Restore Settings…** sit lower in the same dropdown, and the **resets** below them - all described under Backup & reset."] =
            "**نسخ الإعدادات احتياطيًا…** و**استعادة الإعدادات…** أسفل في القائمة المنسدلة نفسها، و**عمليات إعادة الضبط** تحتها - كلها موصوفة ضمن النسخ الاحتياطي وإعادة الضبط.",
        ["Backup & reset"] =
            "النسخ الاحتياطي وإعادة الضبط",
        ["**Back Up Settings…** - saves everything that makes Radiata yours (wheels, colors, settings, and your Game Grid cover/logo picks) to a .zip in `Documents\\Radiata Backups`."] =
            "**نسخ الإعدادات احتياطيًا…** - يحفظ كل ما يجعل Radiata خاصًا بك (العجلات والألوان والإعدادات واختياراتك لأغلفة/شعارات شبكة الألعاب) في ملف .zip في `Documents\\Radiata Backups`.",
        ["**Reset All Settings…** - factory defaults for wheels, colors and settings; first-run setup runs again on the next launch. Cached art survives."] =
            "**إعادة تعيين كل الإعدادات…** - يعيد العجلات والألوان والإعدادات إلى قيم المصنع؛ ويشغّل الإعداد الأول مرة أخرى عند التشغيل التالي. وتبقى الصور المخزنة مؤقتًا.",
        ["**Uninstall Radiata…** - removes the startup entry, the HidHide registration, and Radiata's own files, with OFF-by-default opt-ins for the shared drivers and your settings. Anything else in Radiata's folder is left alone."] =
            "**إلغاء تثبيت Radiata…** - يزيل مدخل بدء التشغيل وتسجيل HidHide وملفات Radiata الخاصة، مع خيارات إزالة غير محددة افتراضيًا لبرامج التشغيل المشتركة وإعداداتك. يترك أي شيء آخر في مجلد Radiata كما هو.",
        ["Updates"] =
            "التحديثات",
        ["**Check for Updates** - asks `getradiata.app/update` for a newer version right now."] =
            "**التحقق من التحديثات** - يسأل `getradiata.app/update` عن إصدار أحدث الآن.",
        ["**Automatic** - the checkbox beside the button: when on, the same check runs at startup and once a day. Found updates announce themselves with an on-screen notice (click it to open the update) and a line in this tab; a version you choose to **skip** stops announcing itself, though the line here still shows it."] =
            "**تلقائي** - مربع الاختيار بجوار الزر: عند تفعيله يجري الفحص نفسه عند بدء التشغيل ومرة يوميًا. وتعلن التحديثات المكتشفة عن نفسها بإشعار على الشاشة (اضغطه لفتح التحديث) وبسطر في هذه العلامة؛ والإصدار الذي تختار **تخطيه** يتوقف عن الإعلان عن نفسه، وإن ظل السطر هنا يعرضه.",
        ["Resets can't be undone - **back up first**."] =
            "لا يمكن التراجع عن إعادة الضبط - **انسخ احتياطيًا أولًا**.",
        ["The game-art buttons live in their own **Game Grid** section - see [[game-grid-options|Game Grid options]]."] =
            "تعيش أزرار فن الألعاب في قسم **شبكة الألعاب** الخاص بها - راجع [[game-grid-options|خيارات شبكة الألعاب]].",

        // ── topic:game-grid-options ──
        ["Game Grid options (Settings ▸ Advanced)"] =
            "خيارات شبكة الألعاب (الإعدادات ◂ متقدم)",
        ["game grid options clear game art cache retry missing game art reset hidden games unhide covers redownload"] =
            "خيارات شبكة الألعاب مسح ذاكرة فن الألعاب إعادة محاولة الفن المفقود إعادة ضبط الألعاب المخفية إظهار أغلفة إعادة تنزيل",
        ["**Settings ▸ Advanced ▸ Game Grid** collects the grid's housekeeping buttons:"] =
            "يجمع **الإعدادات ◂ متقدم ◂ شبكة الألعاب** أزرار تدبير الشبكة:",
        ["**Retry Missing Game Art** - re-attempts only the covers and logos that came up empty, keeping everything already downloaded and every cover you picked by hand (see [[cover-art|Cover art & logos]])."] =
            "**إعادة محاولة جلب الصور المفقودة** - يعيد محاولة الأغلفة والشعارات التي جاءت فارغة فقط، محتفظًا بكل ما نزّل بالفعل وكل غلاف اخترته يدويًا (راجع [[cover-art|صور الأغلفة والشعارات]]).",
        ["**Clear Game Art Cache** - deletes ALL cached covers, so everything re-downloads. **Images you dropped onto a slice are kept.** Try **Retry Missing Game Art** first if you only want to fill blanks."] =
            "**مسح ذاكرة صور الألعاب** - يحذف **كل** الأغلفة المخزنة مؤقتًا، فيعاد تنزيل كل شيء. **أما الصور التي أفلتها على شريحة فتبقى.** جرّب **إعادة محاولة صور الألعاب الناقصة** أولًا إن أردت ملء الفراغات فقط.",
        ["**Reset Hidden Games** - brings back every game and storefront you hid with **hold {square}** (see [[storefronts|Hiding a storefront]])."] =
            "**إعادة ضبط الألعاب المخفية** - يعيد كل لعبة ومتجر أخفيتهما بـ **الضغط المطوّل على {square}** (راجع [[storefronts|إخفاء متجر]]).",
        ["**Prefer Playnite covers** - shown when [[playnite|Playnite]] and a SteamGridDB key are both set up: favors Playnite's own cover art."] =
            "**تفضيل أغلفة Playnite** - يظهر عند إعداد [[playnite|Playnite]] ومفتاح SteamGridDB معًا: يفضّل صور أغلفة Playnite الخاصة.",

        // ── topic:storefronts ──
        ["Hiding a storefront"] =
            "إخفاء متجر",
        ["storefront steam epic gog xbox battle.net amazon itch ubisoft ea hide exclude opt out include reset hidden games launcher card"] =
            "متجر steam epic gog xbox battle.net amazon itch ubisoft ea إخفاء استبعاد إلغاء الاشتراك تضمين إعادة ضبط الألعاب المخفية بطاقة المشغّل",
        ["A whole storefront can be hidden from the [[game-grid|Game Grid]], the same way a single game can."] =
            "يمكن إخفاء متجر كامل من [[game-grid|شبكة الألعاب]]، بالطريقة نفسها التي تخفى بها لعبة واحدة.",
        ["**Filter to the store with L1 / R1**, then **hold {square}** on its **Open <store>** card. A notice asks **\"Hide <store> and all its games in Radiata?\"** - **{cross}** hides it, **{circle}** cancels."] =
            "**صفّ العرض على المتجر بـ L1 / R1**، ثم **اضغط {square} مع الاستمرار** على بطاقة **فتح <المتجر>**. يسأل إشعار: **\"إخفاء <المتجر> وكل ألعابه في Radiata؟\"** - **{cross}** يخفيه و**{circle}** يلغي.",
        ["Hiding a store **hides its games** in the Game Grid, the [[edit-mode|Add picker]], and the starter wheels the first-run wizard suggests."] =
            "إخفاء متجر **يخفي ألعابه** في شبكة الألعاب، و[[edit-mode|منتقي الإضافة]]، والعجلات المبدئية التي يقترحها معالج التشغيل الأول.",
        ["Bring it back with **Settings ▸ Advanced ▸ Game Grid ▸** [[game-grid-options|Reset Hidden Games]], which un-hides storefronts as well as games."] =
            "أعده بـ **الإعدادات ◂ متقدم ◂ شبكة الألعاب ◂** [[game-grid-options|إعادة ضبط الألعاب المخفية]]، الذي يظهر المتاجر كما يظهر الألعاب.",
        ["Newly installed storefronts appear **automatically** the next time Radiata scans."] =
            "تظهر المتاجر المثبّتة حديثًا **تلقائيًا** في المرة التالية التي يفحص فيها Radiata.",
        ["Only a store with its own **Open <store>** card can be hidden this way. A [[playnite|Playnite]] game you added by hand, or one from a third-party Playnite plugin, carries no storefront card. Hide those games individually."] =
            "لا يمكن إخفاء متجر بهذه الطريقة إلا إن كانت له بطاقة **فتح <المتجر>** خاصة به. أما لعبة [[playnite|Playnite]] التي أضفتها يدويًا، أو لعبة من إضافة Playnite خارجية، فلا تحمل بطاقة متجر. أخف تلك الألعاب فرادى.",

        // ── topic:controller-not-detected ──
        ["Controller not detected"] =
            "جهاز التحكم غير مكتشف",
        ["controller dead not detected blind hidhide lockout whitelist recover reset bluetooth radio frozen stuck wedge"] =
            "جهاز التحكم معطل غير مكتشف أعمى hidhide انغلاق قائمة السماح استعادة إعادة ضبط bluetooth راديو متجمد عالق تعطل",
        ["**Replug / re-pair first.** Bluetooth stacks occasionally wedge, and power-cycling the pad fixes most one-offs."] =
            "**أعد التوصيل أو الإقران أولًا.** تتعطل حزم Bluetooth أحيانًا، وإطفاء الجهاز وتشغيله يحل معظم الحالات العابرة.",
        ["**Bluetooth pad connected but frozen** (Windows still lists it, input never moves)? That's a Windows Bluetooth wedge that power-cycling the pad **won't** fix - toggle the PC's **Bluetooth off and on** instead. Radiata shows a \"toggle Bluetooth\" notification when it spots this."] =
            "**هل جهاز Bluetooth متصل لكنه متجمد** (ما زال Windows يسرده لكن الإدخال لا يتحرك)؟ هذا تعطل في Bluetooth الخاص بـ Windows **لا** يحله إطفاء الجهاز وتشغيله - أطفئ **Bluetooth** في الحاسوب وأعد تشغيله بدلًا من ذلك. ويعرض Radiata إشعار \"بدّل Bluetooth\" حين يرصد ذلك.",
        ["**HidHide lockout:** if the cloak hides the pad while Radiata isn't on its allow-list, Radiata goes blind. That means no input, and the Current Controller readout shows nothing even though Windows sees the pad. Radiata catches this at startup and offers a one-click fix via a clickable on-screen notice; repair can help with registration, but it does not cure every access problem. See [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "**الإقفال بسبب HidHide:** إن أخفى الإخفاء الجهاز بينما Radiata ليس في قائمة السماح، صار Radiata أعمى. أي لا إدخال، ولا تعرض قراءة \"جهاز التحكم الحالي\" شيئًا رغم أن Windows يرى الجهاز. ويرصد Radiata ذلك عند بدء التشغيل ويعرض إصلاحًا بضغطة واحدة عبر إشعار قابل للنقر على الشاشة؛ والإصلاح قد يساعد في التسجيل، لكنه لا يعالج كل مشكلات الوصول. راجع [[hidhide-troubleshooting|استكشاف أخطاء HidHide]].",
        ["**Peer controller tools** can remap or hide the controller and create additional outputs. Installed software alone does not establish a conflict; see [[controller-tool-conflicts|reWASD, DS4Windows & other tools]]."] =
            "**أدوات أجهزة التحكم الأخرى** قد تعيد تعيين الجهاز أو تخفيه وتنشئ مخرجات إضافية. ووجود البرنامج مثبّتًا وحده لا يثبت وجود تعارض؛ راجع [[controller-tool-conflicts|reWASD وDS4Windows وأدوات أخرى]].",

        // ── topic:controller-conflict-checklist ──
        ["Controller conflict checklist"] =
            "قائمة فحص تعارض أجهزة التحكم",
        ["double input duplicate bleed through wrong pad player slot remote play diagnostic log"] =
            "إدخال مزدوج مكرر تسرب جهاز خاطئ خانة لاعب لعب عن بعد سجل تشخيصي",
        ["Use this sequence when one press moves a menu twice, the game reacts beneath a wheel, the wrong controller responds, or input disappears. Change one thing at a time and test between changes."] =
            "استخدم هذا التسلسل حين تحرّك ضغطة واحدة قائمة مرتين، أو تستجيب اللعبة تحت العجلة، أو يستجيب جهاز التحكم الخاطئ، أو يختفي الإدخال. غيّر شيئًا واحدًا في كل مرة واختبر بين التغييرات.",
        ["**1. Save and exit the game.** Controller mode, Passthru Mode, remapper output changes and reconnects can all replace the device a game is using. Relaunch after the test setup is stable."] =
            "**1. احفظ واخرج من اللعبة.** فوضع جهاز التحكم، والوضع المباشر، وتغيّر مخرجات أدوات إعادة التعيين، وإعادة الاتصال، كلها قد تستبدل الجهاز الذي تستخدمه اللعبة. أعد التشغيل بعد أن تستقر بيئة الاختبار.",
        ["**2. Establish a simple baseline.** Temporarily use one controller and one connection (USB, Bluetooth or receiver). Disable other tools' remapping and automatic profile switching; close their tray agents and HidHide configuration windows. For a local test, end unused streaming sessions that create virtual pads."] =
            "**2. أنشئ حالة أساس بسيطة.** استخدم مؤقتًا جهاز تحكم واحدًا واتصالًا واحدًا (USB أو Bluetooth أو مستقبل). عطّل إعادة التعيين والتبديل التلقائي للملفات الشخصية في الأدوات الأخرى؛ وأغلق عملياتها في شريط النظام ونوافذ إعداد HidHide. وللاختبار المحلي، أنه جلسات البث غير المستخدمة التي تنشئ أجهزة افتراضية.",
        ["**3. Check Radiata first.** Read **Settings ▸ Advanced ▸ Current Controller** and hover its tray icon for isolation status. No controller points to detection or hiding; a working wheel with game input underneath points to isolation or another input path."] =
            "**3. تحقق من Radiata أولًا.** اقرأ **الإعدادات ◂ متقدم ◂ جهاز التحكم الحالي** ومرر المؤشر فوق أيقونة شريط النظام لمعرفة حالة العزل. فغياب جهاز تحكم يشير إلى مشكلة كشف أو إخفاء؛ أما عجلة تعمل مع استجابة اللعبة تحتها فتشير إلى العزل أو إلى مسار إدخال آخر.",
        ["**4. Reconnect in a controlled order.** With the game and competing readers closed, start Radiata, connect the controller, and wait for its status to settle. Start Steam or the launcher afterwards, then the game. A reader that opened the device before cloaking may retain access until it closes or the device reconnects."] =
            "**4. أعد التوصيل بترتيب محكوم.** مع إغلاق اللعبة والقارئات المنافسة، شغّل Radiata، ووصّل جهاز التحكم، وانتظر حتى تستقر حالته. ثم شغّل Steam أو المشغّل بعد ذلك، ثم اللعبة. فالقارئ الذي فتح الجهاز قبل الإخفاء قد يحتفظ بالوصول حتى يغلق أو يعاد توصيل الجهاز.",
        ["Start Radiata before anything else that reads the controller, connect the pad and let its status settle, then open Steam or your launcher, and the game last."] =
            "شغّل Radiata قبل أي شيء آخر يقرأ جهاز التحكم، ووصّل جهاز التحكم واترك حالته تستقر، ثم افتح Steam أو المشغّل، واللعبة في النهاية.",
        ["**5. Test in a safe game menu.** Opening a wheel should stop Radiata's virtual pad from driving the game until the wheel closes. Windows' `joy.cpl` can help identify extra controllers, but one entry there does not prove isolation in every game or input API."] =
            "**5. اختبر في قائمة آمنة داخل اللعبة.** فتح العجلة ينبغي أن يمنع جهاز Radiata الافتراضي من قيادة اللعبة حتى تغلق العجلة. وأداة `joy.cpl` في Windows قد تساعد في تحديد أجهزة التحكم الزائدة، لكن وجود مدخل واحد فيها لا يثبت العزل في كل لعبة أو في كل واجهة إدخال.",
        ["**6. Restore other tools one at a time.** The combination that brings back the symptom is useful evidence. Reading a controller and successfully isolating it are separate things: Radiata does not provide a general physical-device-to-XInput-slot picker."] =
            "**6. أعد الأدوات الأخرى واحدة تلو الأخرى.** فالتركيبة التي تعيد ظهور العرض دليل مفيد. وقراءة جهاز التحكم وعزله بنجاح أمران مختلفان: لا يوفر Radiata أداة عامة لربط جهاز مادي بخانة XInput.",
        ["For comparison, enable [[passthru-mode|Passthru Mode]] before launching the game. Other tools can still hide or remap the device, and wheel input reaching the game is expected in this mode. Disabling the wheels alone does not release capture."] =
            "للمقارنة، فعّل [[passthru-mode|الوضع المباشر]] قبل تشغيل اللعبة. فقد تظل الأدوات الأخرى تخفي الجهاز أو تعيد تعيينه، ووصول إدخال العجلة إلى اللعبة متوقع في هذا الوضع. وتعطيل العجلات وحده لا يحرر الالتقاط.",
        ["What to include in a support report"] =
            "ما ينبغي تضمينه في تقرير الدعم",
        ["Record Windows and Radiata versions, controller model and transport, controller mode, tray status, driver versions, other tools and active profiles, startup order, and the first step that changes the result. Include whether input works with Radiata closed and in Passthru Mode."] =
            "سجّل إصداري Windows وRadiata، وطراز جهاز التحكم وطريقة توصيله، ووضع جهاز التحكم، وحالة شريط النظام، وإصدارات برامج التشغيل، والأدوات الأخرى والملفات الشخصية النشطة، وترتيب بدء التشغيل، وأول خطوة تغيّر النتيجة. وأضف ما إذا كان الإدخال يعمل مع إغلاق Radiata وفي الوضع المباشر.",
        ["Use **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to Developer…** to prepare a diagnostic ZIP. Review it before attaching it; it may contain device identifiers and personal paths. Include the actual error text from driver setup or HID Diagnostics."] =
            "استخدم **الإعدادات ◂ متقدم ◂ استكشاف الأخطاء ◂ إرسال السجل إلى المطوّر…** لتجهيز ملف ZIP تشخيصي. راجعه قبل إرفاقه؛ فقد يحتوي على معرّفات أجهزة ومسارات شخصية. وأضف نص الخطأ الفعلي من إعداد برامج التشغيل أو من تشخيص HID.",

        // ── topic:controller-tool-conflicts ──
        ["reWASD, DS4Windows & other controller tools"] =
            "reWASD وDS4Windows وأدوات أجهزة التحكم الأخرى",
        ["rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapper conflict virtual controller duplicate xbox slot autodetect"] =
            "rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit إعادة تعيين تعارض جهاز تحكم افتراضي مكرر xbox خانة كشف تلقائي",
        ["Two tools can read one pad and produce two outputs, or one can hide the pad from the other. Start with one active remapper for that controller. Coexistence depends on versions, hiding rules, transport and game."] =
            "قد تقرأ أداتان جهازًا واحدًا فتنتجان مخرجين، أو قد تخفي إحداهما الجهاز عن الأخرى. ابدأ بأداة إعادة تعيين نشطة واحدة لذلك الجهاز. والتعايش يتوقف على الإصدارات وقواعد الإخفاء وطريقة التوصيل واللعبة.",
        ["reWASD"] =
            "reWASD",
        ["Turn **Remap OFF** for the affected device or group and pause **Autodetect** for the test. Closing the main window does not necessarily stop mappings. Check the tray agent and confirm its virtual output is gone before retesting. See [reWASD Tray Agent](https://help.rewasd.com/interface/tray-agent.html)."] =
            "أوقف **Remap** للجهاز أو المجموعة المعنية، وأوقف **Autodetect** مؤقتًا أثناء الاختبار. فإغلاق النافذة الرئيسية لا يوقف التعيينات بالضرورة. تحقق من العملية في شريط النظام وتأكد من اختفاء مخرجها الافتراضي قبل إعادة الاختبار. راجع [عملية reWASD في شريط النظام](https://help.rewasd.com/interface/tray-agent.html).",
        ["reWASD has its own virtual-device and hiding settings. Repairing ViGEmBus or adding Radiata to HidHide cannot fix every reWASD visibility rule. Record which devices remain visible; consult [reWASD troubleshooting](https://help.rewasd.com/faq/troubleshooting.html) for its own errors."] =
            "لـ reWASD إعدادات خاصة به للأجهزة الافتراضية وللإخفاء. وإصلاح ViGEmBus أو إضافة Radiata إلى HidHide لا يصلح كل قواعد الرؤية في reWASD. سجّل أي الأجهزة تبقى مرئية؛ وراجع [استكشاف أخطاء reWASD](https://help.rewasd.com/faq/troubleshooting.html) لأخطائه الخاصة.",
        ["DS4Windows, DSX and InputMapper"] =
            "DS4Windows وDSX وInputMapper",
        ["Stop controller output and fully exit the tool, including its tray process, for the baseline. Check automatic startup and profiles if it returns. Another virtual Xbox or DualShock controller can cause duplicate actions or change which device the game selects."] =
            "أوقف مخرج جهاز التحكم واخرج من الأداة تمامًا، بما في ذلك عمليتها في شريط النظام، لإنشاء حالة الأساس. وتحقق من بدء التشغيل التلقائي ومن الملفات الشخصية إن عادت. فوجود جهاز Xbox أو DualShock افتراضي آخر قد يسبب إجراءات مكررة أو يغيّر الجهاز الذي تختاره اللعبة.",
        ["If the setup uses HidHide, physical-device blocks can remain after the remapper exits. Check Radiata's access using [[hidhide-troubleshooting|HidHide troubleshooting]]. Radiata does not take ownership of another tool's existing blocks, so quitting Radiata does not clear them."] =
            "إن كان الإعداد يستخدم HidHide فقد تبقى حجوبات الأجهزة المادية بعد خروج أداة إعادة التعيين. تحقق من وصول Radiata عبر [[hidhide-troubleshooting|استكشاف أخطاء HidHide]]. ولا يتملك Radiata حجوبات أداة أخرى قائمة، لذا فإنهاء Radiata لا يزيلها.",
        ["Other sources of input"] =
            "مصادر إدخال أخرى",
        ["**JoyToKey, AntiMicroX, macros and hardware profiles** can emit keyboard or mouse events alongside gamepad input. Neutralizing Radiata's virtual pad does not neutralize those events. Disable the mapping or controller [[turbo-mode|Turbo mode]] for the test."] =
            "**JoyToKey وAntiMicroX ووحدات الماكرو وملفات الأجهزة الشخصية** قد ترسل أحداث لوحة مفاتيح أو فأرة إلى جانب إدخال جهاز اللعب. وتحييد جهاز Radiata الافتراضي لا يحيّد تلك الأحداث. عطّل التعيين أو [[turbo-mode|وضع التيربو]] في جهاز التحكم أثناء الاختبار.",
        ["**x360ce, vJoy-based tools, streaming clients and vendor utilities** can add controllers or translation layers. Check Steam Remote Play, Sunshine/Moonlight, Parsec and controller software when relevant. End only unused sessions; a remote player's virtual controller may be their only input."] =
            "**x360ce والأدوات المبنية على vJoy وعملاء البث وأدوات الشركات المصنّعة** قد تضيف أجهزة تحكم أو طبقات ترجمة. تحقق من Steam Remote Play وSunshine/Moonlight وParsec وبرامج أجهزة التحكم عند الاقتضاء. وأنه الجلسات غير المستخدمة فقط؛ فقد يكون جهاز التحكم الافتراضي للاعب بعيد هو وسيلته الوحيدة للإدخال.",
        ["**Old HidGuardian or ScpToolkit installations** can leave filtering or replacement drivers behind. Use the original project's removal guidance or support; do not delete arbitrary HID devices, Bluetooth drivers or registry filters. HidHide and HidGuardian are different components."] =
            "**عمليات التثبيت القديمة لـ HidGuardian أو ScpToolkit** قد تخلّف وراءها برامج تشغيل تصفية أو استبدال. استخدم إرشادات الإزالة أو الدعم الخاص بالمشروع الأصلي؛ ولا تحذف أجهزة HID أو برامج تشغيل Bluetooth أو مرشحات السجل اعتباطًا. فـ HidHide وHidGuardian مكوّنان مختلفان.",
        ["**Wrong player or no spare Xbox slot?** XInput exposes four slots, which may include virtual pads. Temporarily stop unused virtual outputs and reconnect in the intended order. If Radiata reports uncertainty about its own output, wait for reconnection or quit and reopen Radiata; reinstalling drivers is not the first fix."] =
            "**هل اللاعب خاطئ أو لا توجد خانة Xbox شاغرة؟** يتيح XInput أربع خانات، وقد تشمل أجهزة افتراضية. أوقف مؤقتًا المخرجات الافتراضية غير المستخدمة وأعد التوصيل بالترتيب المقصود. وإن أبلغ Radiata عن عدم يقين بشأن مخرجه الخاص، فانتظر إعادة الاتصال أو أنه Radiata وافتحه من جديد؛ وإعادة تثبيت برامج التشغيل ليست الحل الأول.",
        ["If another remapper is essential, test it with Radiata in [[passthru-mode|Passthru Mode]] first. That avoids a second Radiata stand-in, but it does not promise isolation or preservation of native controller features through the other tool."] =
            "إن كانت أداة إعادة تعيين أخرى ضرورية، فاختبرها أولًا مع Radiata في [[passthru-mode|الوضع المباشر]]. فهذا يتفادى وجود جهاز بديل ثانٍ من Radiata، لكنه لا يعد بالعزل ولا بالحفاظ على ميزات جهاز التحكم الأصلية عبر الأداة الأخرى.",

        // ── topic:hidhide-troubleshooting ──
        ["HidHide: contention, lockouts & shared settings"] =
            "HidHide: التنازع والإقفال والإعدادات المشتركة",
        ["hidhide contention busy access denied configuration client cli lockout whitelist allow list inverse cloak path moved renamed usb bluetooth shared hidden controller recovery"] =
            "hidhide تنازع مشغول رفض الوصول عميل الإعداد cli إقفال قائمة بيضاء قائمة سماح إخفاء عكسي مسار نقل إعادة تسمية usb bluetooth مشترك جهاز تحكم مخفي استرداد",
        ["Busy or access-failed status"] =
            "حالة الانشغال أو فشل الوصول",
        ["The **HidHide Configuration Client** holds the driver's exclusive configuration connection while it's open. A running or stuck **HidHideCLI** can also contend with Radiata. Close those tools completely, then allow about **15 seconds** for Radiata's retry before trying **Recover Controller**. Contention does not mean the driver needs reinstalling."] =
            "يحتفظ **عميل إعداد HidHide** باتصال الإعداد الحصري ببرنامج التشغيل ما دام مفتوحًا. كما قد يتنازع **HidHideCLI** قيد التشغيل أو المعلّق مع Radiata. أغلق تلك الأدوات تمامًا، ثم امنح إعادة محاولة Radiata نحو **15 ثانية** قبل تجربة **استرداد جهاز التحكم**. والتنازع لا يعني أن برنامج التشغيل يحتاج إلى إعادة تثبيت.",
        ["Windows sees the controller, but Radiata does not"] =
            "يرى Windows جهاز التحكم ولا يراه Radiata",
        ["Close the game and quit Radiata before inspecting HidHide. In normal mode, the **Applications** list grants access to hidden controllers. Verify the exact `Radiata.exe` you launch is listed - installed, portable, renamed and moved copies all have different paths. Radiata normally registers itself; **Install/Repair Drivers…** can repair registration, subject to its reported result."] =
            "أغلق اللعبة وأنه Radiata قبل فحص HidHide. في الوضع العادي تمنح قائمة **Applications** الوصول إلى أجهزة التحكم المخفية. تحقق من أن ملف `Radiata.exe` الذي تشغّله بالضبط مدرج فيها - فالنسخ المثبّتة والمحمولة والمعاد تسميتها والمنقولة لها كلها مسارات مختلفة. ويسجّل Radiata نفسه عادةً؛ و**تثبيت/إصلاح برامج التشغيل…** يمكنه إصلاح التسجيل، بحسب النتيجة التي يبلغ عنها.",
        ["Check the selected physical device on **Devices**. USB and Bluetooth can have separate entries. Do not hide the virtual stand-in the game needs. Use [Nefarius's setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) to identify the device, and close the client before restarting Radiata."] =
            "تحقق من الجهاز المادي المحدد في **Devices**. فقد يكون لـ USB وBluetooth مدخلان منفصلان. ولا تخف البديل الافتراضي الذي تحتاجه اللعبة. استخدم [دليل الإعداد من Nefarius](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) لتحديد الجهاز، وأغلق العميل قبل إعادة تشغيل Radiata.",
        ["**Inverse application cloak reverses the list's meaning.** Radiata preserves this shared setting and declines capture when it is enabled. Record the configuration and coordinate with the tool that needs it before choosing normal mode for Radiata; changing it affects other applications too."] =
            "**الإخفاء العكسي للتطبيقات يعكس معنى القائمة.** يحافظ Radiata على هذا الإعداد المشترك ويمتنع عن الالتقاط ما دام مفعّلًا. سجّل الإعداد ونسّق مع الأداة التي تحتاجه قبل اختيار الوضع العادي لـ Radiata؛ فتغييره يؤثر في تطبيقات أخرى أيضًا.",
        ["The game still receives input"] =
            "ما زالت اللعبة تتلقى إدخالًا",
        ["An application allowed through HidHide can still read the physical device. Review entries deliberately; do not add the game, Steam, or every executable as a general fix for double input."] =
            "التطبيق المسموح له عبر HidHide يظل قادرًا على قراءة الجهاز المادي. راجع المدخلات بتأنٍّ؛ ولا تضف اللعبة أو Steam أو كل ملف تنفيذي كحل عام للإدخال المزدوج.",
        ["After a fresh install, reconnect the controller or restart Windows if requested, so the filter can attach. Restart readers that opened the device before cloaking. Configuration readback alone does not verify what a running game receives."] =
            "بعد تثبيت جديد، أعد توصيل جهاز التحكم أو أعد تشغيل Windows إن طلب منك ذلك، كي يتمكن المرشح من الارتباط. وأعد تشغيل القارئات التي فتحت الجهاز قبل الإخفاء. وقراءة الإعداد وحدها لا تتحقق مما تتلقاه لعبة قيد التشغيل.",
        ["HidHide has limitations, including some Raw Input readers and Xbox/XInput configurations. If leakage survives a clean startup, record the game and transport rather than assuming a successful hide operation guarantees exclusive input. See the [HidHide FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/)."] =
            "لـ HidHide حدود، منها بعض قارئات Raw Input وبعض تكوينات Xbox/XInput. وإن بقي التسرب بعد بدء تشغيل نظيف، فسجّل اللعبة وطريقة التوصيل بدل افتراض أن نجاح عملية الإخفاء يضمن إدخالًا حصريًا. راجع [الأسئلة الشائعة عن HidHide](https://docs.nefarius.at/projects/HidHide/FAQ/).",
        ["The controller stays hidden after exit"] =
            "يبقى جهاز التحكم مخفيًا بعد الخروج",
        ["Radiata adopts existing hidden-device entries matching the controller model it manages, including entries from another connection method, so they can be released on exit. This can also release another tool's matching entries; you should never have two tools manage hiding for the same controller. Uninstall clears all hidden-device entries unless another Radiata copy is running. A failed release must be recovered before removal can finish."] =
            "يتبنى Radiata مدخلات الأجهزة المخفية الموجودة التي تطابق طراز جهاز التحكم الذي يديره، بما في ذلك المدخلات الناتجة عن طريقة توصيل أخرى، كي يتمكن من تحريرها عند الخروج. وقد يحرر ذلك أيضًا المدخلات المطابقة لأداة أخرى؛ ولا ينبغي أبدًا أن تدير أداتان إخفاء جهاز التحكم نفسه. وتمسح إزالة التثبيت كل مدخلات الأجهزة المخفية ما لم تكن نسخة أخرى من Radiata قيد التشغيل. وأي تحرير فاشل يجب استرداده قبل أن تكتمل الإزالة.",
        ["Keep keyboard and mouse access available while changing controller visibility. Record existing settings first, change only the identified controller or application entry, and close the configuration client before retesting."] =
            "أبق الوصول بلوحة المفاتيح والفأرة متاحًا أثناء تغيير ظهور جهاز التحكم. سجّل الإعدادات الحالية أولًا، وغيّر فقط مدخل جهاز التحكم أو التطبيق المحدد، وأغلق عميل الإعداد قبل إعادة الاختبار.",

        // ── topic:driver-conflicts ──
        ["HP OMEN & driver version conflicts"] =
            "تعارضات HP OMEN وإصدارات برامج التشغيل",
        ["hp omen gaming hub fusion vigem vigembus foreign fork driver version mismatch 10.x 1.22.0 1.5.230 oculus virtual desktop repair install bus device manager restart"] =
            "hp omen gaming hub fusion vigem vigembus أجنبي تفرع إصدار برنامج تشغيل عدم تطابق 10.x 1.22.0 1.5.230 oculus virtual desktop إصلاح تثبيت ناقل إدارة الأجهزة إعادة تشغيل",
        ["**ViGEmBus creates the virtual controller; HidHide controls access to the physical one.** A working driver of one kind does not establish that the other works. Check the driver result log and Radiata's isolation status before repeating an installer."] =
            "**ينشئ ViGEmBus جهاز التحكم الافتراضي، ويتحكم HidHide في الوصول إلى الجهاز المادي.** وعمل أحد النوعين لا يثبت عمل الآخر. تحقق من سجل نتائج برامج التشغيل ومن حالة العزل في Radiata قبل إعادة تشغيل أي مثبّت.",
        ["HP OMEN Gaming Hub / OMEN Fusion"] =
            "HP OMEN Gaming Hub / OMEN Fusion",
        ["Some HP OMEN systems have a vendor-modified ViGEmBus, and Radiata can connect to that bus instead of the correct one. A reported **10.x** version can be HP's old fork, not a newer compatible Nefarius driver."] =
            "بعض أنظمة HP OMEN فيها نسخة من ViGEmBus عدّلتها الشركة المصنّعة، وقد يتصل Radiata بذلك الناقل بدل الناقل الصحيح. والإصدار **10.x** المبلغ عنه قد يكون تفرع HP القديم، لا برنامج تشغيل أحدث ومتوافقًا من Nefarius.",
        ["Radiata names detected foreign buses and skips installing over them. Its driver removal also leaves another program's bus in place. Repeated **Install/Repair Drivers…** attempts will not switch an HP-owned bus to Nefarius's."] =
            "يسمّي Radiata النواقل الأجنبية التي يكتشفها ويتجنب التثبيت فوقها. كما أن إزالته لبرامج التشغيل تترك ناقل برنامج آخر في مكانه. وتكرار محاولات **تثبيت/إصلاح برامج التشغيل…** لن يبدّل ناقلًا تملكه HP بناقل Nefarius.",
        ["Follow [Nefarius's HP OMEN guidance](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) and its linked [HP issue and switching instructions](https://github.com/nefarius/ViGEmBus/issues/99), or contact HP. Disabling the vendor bus can break dependent OMEN features; Radiata does not perform the device or registry changes for you."] =
            "اتبع [إرشادات Nefarius بشأن HP OMEN](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) وما ترتبط به من [تفاصيل مشكلة HP وتعليمات التبديل](https://github.com/nefarius/ViGEmBus/issues/99)، أو تواصل مع HP. فتعطيل ناقل الشركة المصنّعة قد يعطّل ميزات OMEN المعتمدة عليه؛ ولا يجري Radiata تغييرات الأجهزة أو السجل نيابة عنك.",
        ["A foreign bus that refuses a virtual DualShock 4 may make Radiata fall back to an **Xbox 360 stand-in for that session**. Unexpected Xbox prompts can therefore be a driver clue rather than a changed glyph setting. This fallback is not a compatibility guarantee."] =
            "الناقل الأجنبي الذي يرفض جهاز DualShock 4 افتراضيًا قد يدفع Radiata إلى العودة إلى **بديل Xbox 360 في تلك الجلسة**. لذا فظهور رموز Xbox على غير المتوقع قد يكون دليلًا على برامج التشغيل لا تغييرًا في إعداد الرموز. وهذا التراجع ليس ضمانًا للتوافق.",
        ["Versions and duplicate buses"] =
            "الإصدارات والنواقل المكررة",
        ["This Radiata build bundles **ViGEmBus 1.22.0** and **HidHide 1.5.230**. ViGEmBus is retired; 1.22.0 is its final official release. Installer, application, client-library and driver versions are different numbers - they are not supposed to match each other."] =
            "تتضمن هذه النسخة من Radiata برنامجي **ViGEmBus 1.22.0** و**HidHide 1.5.230**. وقد توقف تطوير ViGEmBus؛ والإصدار 1.22.0 هو إصداره الرسمي الأخير. وإصدارات المثبّت والتطبيق ومكتبة العميل وبرنامج التشغيل أرقام مختلفة - وليس مفترضًا أن تتطابق.",
        ["Open **Device Manager ▸ View ▸ Devices by connection** and inspect virtual gamepad bus entries. Record each bus's name, provider, driver version and device status. Multiple buses, unexpected providers, or a version different from the one Radiata bundles all warrant investigation; a higher number alone does not prove compatibility."] =
            "افتح **إدارة الأجهزة ◂ عرض ◂ الأجهزة حسب الاتصال** وافحص مدخلات ناقل أجهزة اللعب الافتراضية. سجّل اسم كل ناقل ومزوّده وإصدار برنامج تشغيله وحالة الجهاز. فتعدد النواقل، أو وجود مزوّدين غير متوقعين، أو اختلاف الإصدار عن الإصدار الذي يرفقه Radiata، كلها أمور تستحق الفحص؛ وارتفاع الرقم وحده لا يثبت التوافق.",
        ["**Oculus and Virtual Desktop** setups can also supply their own buses. Several virtual gamepads beneath one bus are different from several competing bus drivers. Identify the owning program before changing either."] =
            "قد تجلب إعدادات **Oculus وVirtual Desktop** نواقل خاصة بها كذلك. ووجود عدة أجهزة لعب افتراضية تحت ناقل واحد يختلف عن وجود عدة برامج تشغيل نواقل متنافسة. حدد البرنامج المالك قبل تغيير أي منهما.",
        ["For an ordinary missing or older bundled driver, use **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers…**, review its result, and complete any requested restart. Close HidHide tools first. If repair fails, keep the error text and driver versions for support."] =
            "في حالة برنامج تشغيل مرفق مفقود أو قديم فحسب، استخدم **الإعدادات ◂ متقدم ◂ استكشاف الأخطاء ◂ تثبيت/إصلاح برامج التشغيل…**، وراجع نتيجته، وأكمل أي إعادة تشغيل مطلوبة. أغلق أدوات HidHide أولًا. وإن فشل الإصلاح فاحتفظ بنص الخطأ وإصدارات برامج التشغيل للدعم.",
        ["Driver removal affects every application using that shared component. Use the [official ViGEmBus install/remove guide](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) for a confirmed conflict. Its full purge is advanced recovery, not a first step for double input. Do not force-delete unrelated drivers or use unofficial download sites."] =
            "إزالة برنامج تشغيل تؤثر في كل تطبيق يستخدم ذلك المكوّن المشترك. استخدم [الدليل الرسمي لتثبيت ViGEmBus وإزالته](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) عند وجود تعارض مؤكد. فالمسح الكامل الوارد فيه إجراء استرداد متقدم، لا خطوة أولى لمعالجة الإدخال المزدوج. ولا تفرض حذف برامج تشغيل لا علاقة لها بالأمر ولا تستخدم مواقع تنزيل غير رسمية.",

        // ── topic:overlay-not-visible ──
        ["Wheel not visible over a game"] =
            "العجلة غير ظاهرة فوق لعبة",
        ["overlay fullscreen borderless windowed exclusive primary display monitor uac"] =
            "تراكب ملء الشاشة نافذة بلا حدود حصري الشاشة الرئيسية شاشة uac",
        ["**Run games Borderless Windowed**, not exclusive fullscreen. Exclusive fullscreen bypasses the compositor Radiata draws through. The setting is in most games' display options, and the performance difference on Windows 10/11 is negligible."] =
            "**شغّل الألعاب في وضع النافذة بلا حدود**، لا في وضع ملء الشاشة الحصري. فوضع ملء الشاشة الحصري يتجاوز المؤلّف الذي يرسم Radiata من خلاله. والإعداد موجود في خيارات العرض في معظم الألعاب، والفرق في الأداء على Windows 10 و11 ضئيل جدًا.",
        ["Radiata draws on the **primary display only** - on a multi-monitor rig, make your gaming display the Windows primary (Settings ▸ System ▸ Display)."] =
            "يرسم Radiata على **الشاشة الرئيسية فقط** - في جهاز متعدد الشاشات، اجعل شاشة ألعابك الرئيسية في Windows (الإعدادات ◂ النظام ◂ العرض).",
        ["Windows-secured screens (UAC prompts, the lock screen) can never be drawn over. That's a Windows limitation, and nothing can work around it."] =
            "الشاشات التي يحميها Windows (مربعات UAC وشاشة القفل) لا يمكن الرسم فوقها أبدًا. هذا قيد في Windows، ولا سبيل إلى الالتفاف عليه.",

        // ── topic:steam-conflicts ──
        ["Steam Input & Steam quirks"] =
            "Steam Input وخصوصيات Steam",
        ["steam playstation controller support big picture guide magnifier chord double input unlock controller"] =
            "steam دعم جهاز تحكم playstation الصورة الكبيرة دليل مكبّر مجموعة أزرار إدخال مزدوج فك قفل جهاز التحكم",
        ["Steam Input can translate a controller into gamepad, keyboard or mouse input. Another mapping layer can change prompts and bindings or create duplicate actions. Test per game before changing global settings."] =
            "يستطيع Steam Input ترجمة جهاز التحكم إلى إدخال جهاز لعب أو لوحة مفاتيح أو فأرة. ووجود طبقة تعيين أخرى قد يغيّر الرموز والارتباطات أو ينشئ إجراءات مكررة. اختبر لكل لعبة على حدة قبل تغيير الإعدادات العامة.",
        ["**Wrong buttons or duplicate actions?** With the game closed, open its **Steam Library ▸ Properties ▸ Controller** and try **Disable Steam Input** in the per-game override. Relaunch and compare; restore the previous setting if the game or remote setup needs Steam Input. Global options are under **Steam ▸ Settings ▸ Controller**, with names that vary by Steam version."] =
            "**هل الأزرار خاطئة أو الإجراءات مكررة؟** مع إغلاق اللعبة، افتح **مكتبة Steam ◂ خصائص ◂ جهاز التحكم** وجرّب **تعطيل Steam Input** في التجاوز الخاص باللعبة. أعد التشغيل وقارن؛ وأعد الإعداد السابق إن كانت اللعبة أو الإعداد البعيد يحتاج إلى Steam Input. والخيارات العامة موجودة ضمن **Steam ◂ الإعدادات ◂ جهاز التحكم**، بأسماء تختلف حسب إصدار Steam.",
        ["**No input with Steam Input disabled?** The game may not support Radiata's virtual DualShock controller. For a Sony pad, try [[controller-mode|Xbox Mode]] before launching, or restore Steam Input. That's a game compatibility choice, not necessarily a driver failure."] =
            "**هل اختفى الإدخال عند تعطيل Steam Input؟** قد لا تدعم اللعبة جهاز DualShock الافتراضي الذي يقدمه Radiata. مع جهاز Sony، جرّب [[controller-mode|وضع Xbox]] قبل التشغيل، أو أعد Steam Input. فهذا خيار يتعلق بتوافق اللعبة، وليس بالضرورة عطلًا في برامج التشغيل.",
        ["**Double input despite a successful cloak?** Steam may have opened the physical controller before Radiata hid it. Save and close Steam games before fully exiting Steam, then start Radiata and let capture settle before reopening Steam. Reconnecting the pad can also release stale handles. Follow any controller-unblock notice; closing Steam's window alone may leave it running."] =
            "**هل يحدث إدخال مزدوج رغم نجاح الإخفاء؟** قد يكون Steam قد فتح جهاز التحكم المادي قبل أن يخفيه Radiata. احفظ ألعاب Steam وأغلقها قبل الخروج من Steam نهائيًا، ثم شغّل Radiata ودع الالتقاط يستقر قبل فتح Steam من جديد. وإعادة توصيل الجهاز قد تحرر أيضًا المقابض القديمة. واتبع أي إشعار بإلغاء حجب جهاز التحكم؛ فإغلاق نافذة Steam وحده قد يتركه يعمل.",
        ["**Desktop keys or mouse movement?** Check Steam's **Desktop Layout** and **Guide Button Chord** layout as well as the game's layout. These can emit input outside the game. See [[controller-conflict-checklist|Controller conflict checklist]] for a controlled comparison."] =
            "**هل تظهر مفاتيح سطح المكتب أو حركة الفأرة؟** تحقق من **تخطيط سطح المكتب** في Steam ومن تخطيط **تشكيلة زر Guide**، إضافة إلى تخطيط اللعبة. فهذه قد ترسل إدخالًا خارج اللعبة. راجع [[controller-conflict-checklist|قائمة فحص تعارض أجهزة التحكم]] لإجراء مقارنة محكومة.",
        ["**Steam Remote Play may rely on Steam Input.** Keep a local fallback before changing its input path. Valve explains the translation layer in [Steam Input gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)."] =
            "**قد يعتمد Steam Remote Play على Steam Input.** أبق بديلًا محليًا جاهزًا قبل تغيير مسار إدخاله. وتشرح Valve طبقة الترجمة في [محاكاة جهاز اللعب في Steam Input](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices).",
        ["**Windows Magnifier opens by itself?** That's Steam's *Guide Button Chord* layout (Guide + face button), not Radiata - and powering a pad off by holding the PS button can leave that layout latched. One clean Guide press-and-release clears it; disable it under Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout."] =
            "**يفتح Windows Magnifier من تلقاء نفسه؟** ذاك تخطيط *Guide Button Chord* في Steam (Guide + زر وجه)، لا Radiata - وإطفاء الجهاز بالضغط المطوّل على زر PS قد يبقي ذلك التخطيط مقفلًا. ضغطة Guide واحدة نظيفة وإفلاتها تمسحه؛ عطّله في Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout.",

        // ── topic:turbo-mode ──
        ["Controller Turbo / rapid-fire mode"] =
            "وضع Turbo / الرمي السريع في جهاز التحكم",
        ["turbo rapid fire auto repeat macro cycling flicker wheel closes dismiss premature bounce double input jitter"] =
            "turbo رمي سريع تكرار تلقائي ماكرو تدوير ارتعاش تغلق العجلة إغلاق مبكر ارتداد إدخال مزدوج اهتزاز",
        ["Many third-party pads have a hardware **Turbo** / rapid-fire mode that auto-repeats a held button. An accidental button combo can toggle it on in the controller firmware, and that can look exactly like a bug:"] =
            "في كثير من الأجهزة غير الأصلية وضع **تيربو** أو إطلاق سريع في العتاد يكرر تلقائيًا الزر المضغوط باستمرار. وقد يؤدي ضغط تشكيلة أزرار بالخطأ إلى تفعيله في برنامج الجهاز الثابت، وهو ما قد يبدو تمامًا كخلل:",
        ["The wheel **flickers open and shut**, or **dismisses on its own** right after opening."] =
            "**ترتعش العجلة فتحًا وإغلاقًا**، أو **تغلق من تلقاء نفسها** بعد الفتح مباشرة.",
        ["The Game Grid's **filters cycle rapidly**, or the selection jumps on its own."] =
            "**تدور مرشّحات شبكة الألعاب بسرعة**، أو يقفز التحديد من تلقاء نفسه.",
        ["Slices **fire the instant a wheel opens**, or a hold-to-confirm slice never settles."] =
            "**تنفّذ الشرائح فور فتح العجلة**، أو لا تستقر شريحة الضغط المطوّل للتأكيد أبدًا.",
        ["Turn Turbo off on the controller itself - usually a button combo (often Home/Guide + a face or shoulder button, or a dedicated Turbo button), frequently with its own LED. Check your pad's manual for the exact combo."] =
            "أوقف Turbo على جهاز التحكم نفسه - عادةً بمجموعة أزرار (غالبًا Home/Guide + زر وجه أو كتف، أو زر Turbo مخصص)، وكثيرًا ما يكون له مصباح LED خاص. راجع دليل جهازك للمجموعة الدقيقة.",
        ["If it persists with Turbo confirmed off, it's something else - see [[opening-a-wheel|Opening a wheel]] and [[picking-an-action|Aiming & firing]]."] =
            "إذا استمر مع تأكيد إيقاف Turbo، فهو شيء آخر - راجع [[opening-a-wheel|فتح عجلة]] و[[picking-an-action|التصويب والتنفيذ]].",

        // ── topic:common-issues ──
        ["Other common issues"] =
            "مشكلات شائعة أخرى",
        ["hdr unavailable rdp remote play streaming config json double input"] =
            "hdr غير متاح rdp اللعب عن بعد بث config json إدخال مزدوج",
        ["**HDR shows `Unavailable`** - the display state can't be read in that context, such as in Remote Play or streaming."] =
            "**يعرض HDR `Unavailable`** - لا يمكن قراءة حالة الشاشة في ذلك السياق، مثل اللعب عن بعد أو البث.",
        ["**Editing `config.json` by hand** (`%APPDATA%\\Radiata`) - supported. The app hot-reloads its own writes reliably, but outside edits are occasionally missed, so restart Radiata after manual edits."] =
            "**تحرير `config.json` يدويًا** (`%APPDATA%\\Radiata`) مدعوم. فالتطبيق يعيد تحميل كتاباته الخاصة بشكل موثوق أثناء التشغيل، لكن التعديلات الخارجية تفوته أحيانًا، لذا أعد تشغيل Radiata بعد التعديل اليدوي.",

        // ── figure ──
        ["Isolated"] =
            "معزول",
        ["Your controller"] =
            "جهاز التحكم لديك",
        ["Virtual pad"] =
            "وحدة افتراضية",
        ["The game"] =
            "اللعبة",
        ["cloaked"] =
            "مخفي",
        ["Passthru Mode, or no drivers"] =
            "الوضع المباشر، أو بدون برامج تشغيل",
        ["no virtual pad"] =
            "لا وحدة افتراضية",
        ["{cross} picks the slice up"] =
            "{cross} يرفع الشريحة",
        ["Aim to the target slot"] =
            "صوّب نحو خانة الهدف",
        ["{cross} drops it — the ring reflows"] =
            "{cross} يفلتها — تعاد الحلقة ترتيبها",
        ["Its chord"] =
            "مجموعة أزرارها",
        ["Slices on it — this wheel opens."] =
            "عليها شرائح — تنفتح هذه العجلة.",
        ["No slices — this wheel draws nothing, so its chord reaches the game."] =
            "لا شرائح — لا ترسم هذه العجلة شيئًا، فتصل مجموعة أزرارها إلى اللعبة.",
        ["Modifiers — held around it"] =
            "المعدّلات — تثبّت حولها",
        ["The key that's pressed"] =
            "المفتاح المضغوط",
        ["Steam or your launcher"] =
            "Steam أو المشغّل",
        ["Game and other tools closed"] =
            "اللعبة والأدوات الأخرى مغلقة",
        ["Wait for its status to settle"] =
            "انتظر حتى تستقر حالته",
        ["Make the folder"] =
            "أنشئ المجلد",
        ["Write the manifest"] =
            "اكتب ملف البيان",
        ["Restart Radiata"] =
            "أعد تشغيل Radiata",
        ["Accept the confirmation"] =
            "وافق على التأكيد",
        ["Try it out"] =
            "جربها",
        ["Change something"] =
            "غير شيئا",
        ["clockwise"] =
            "باتجاه عقارب الساعة",
        ["Radiata finds it"] =
            "يجده Radiata",
        ["One folder too many"] =
            "مجلد زائد",
        ["Move the files up a level"] =
            "انقل الملفات مستوى واحدا إلى الأعلى",
        ["custom material theme package material.json drop in author make build own skin palette colors colours gradient fill hue walk outline glyph glow texture png jpg sound wav appdata packages folder soundtheme sound set kawaii mesa salvage reactor obsidian digital physical format token consent confirm restart trace log rejected not showing workshop sample starter ember comments drag drop zip install uninstall remove delete recycle bin right-click"] =
            "مخصصة خامة سمة حزمة material.json إسقاط إضافة مؤلف صنع بناء خاص مظهر لوحة ألوان تدرج تعبئة درجة مسار حدود رمز توهج نسيج png jpg صوت wav appdata packages مجلد soundtheme مجموعة أصوات kawaii mesa salvage reactor obsidian رقمي مادي تنسيق رمز موافقة تأكيد إعادة تشغيل سجل تتبع مرفوض لا يظهر ورشة عينة starter ember تعليقات سحب إفلات zip تثبيت إلغاء تثبيت إزالة حذف سلة المحذوفات نقر بزر الماوس الأيمن",
        ["**Restart Radiata after adding a theme to that folder by hand, or changing one.** Packages are scanned once, at startup, on purpose."] =
            "**أعد تشغيل Radiata بعد إضافة سمة إلى ذلك المجلد يدويًا أو بعد تعديل إحداها.** تفحص الحزم مرة واحدة عند بدء التشغيل، وهذا مقصود.",
        ["**Or drag and drop it.** Drop the theme's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. What you dropped stays where it was."] =
            "**أو اسحبها وأفلتها.** أفلت مجلد السمة أو ملف ZIP منها في أي مكان داخل نافذة **الإعدادات**، أو على `Radiata.exe` أو على اختصار له. ينسخها Radiata إلى مكانها ويطلب منك التأكيد، دون إعادة تشغيل. ويبقى ما أفلتّه في مكانه الأصلي.",
        ["Dropping a theme you already have asks before replacing it; the old copy goes to the **Recycle Bin**. A changed version takes effect after a restart."] =
            "عند إفلات سمة مثبتة لديك بالفعل يطلب تأكيد الاستبدال؛ وتنتقل النسخة القديمة إلى **سلة المحذوفات**. يسري الإصدار المعدّل بعد إعادة التشغيل.",
        ["**To remove a theme,** right-click its tile in **Settings ▸ Customize** and choose **Uninstall theme**. Its folder goes to the Recycle Bin; if it was your material, the wheel switches to **Pearl**. Restore the folder from the Recycle Bin and restart Radiata to get it back."] =
            "**لإزالة سمة،** انقر بزر الماوس الأيمن على بلاطتها في **الإعدادات ◂ تخصيص** واختر **إلغاء تثبيت السمة**. ينتقل مجلدها إلى سلة المحذوفات؛ وإذا كانت هي خامتك الحالية تتحول العجلة إلى **لؤلؤ**. استعد المجلد من سلة المحذوفات وأعد تشغيل Radiata لاستعادتها.",
        ["custom arcade game package game.json javascript js script write author make build own sandbox jint helper polar disc draw tick input kv hiscore cue sound appdata packages folder restart consent confirm trace log rejected not showing workshop sample firefly strict console error budget tint colour color cabinet preview screenshot nameplate launcher drag drop zip install"] =
            "أركيد لعبة مخصصة حزمة game.json javascript js نص برمجي كتابة مؤلف صنع بناء خاص صندوق رمل jint مساعد قطبي قرص رسم tick إدخال kv hiscore إشارة صوت appdata packages مجلد إعادة تشغيل موافقة تأكيد سجل تتبع مرفوض لا يظهر ورشة عينة firefly صارم وحدة تحكم خطأ ميزانية درجة لون خزانة معاينة لقطة شاشة لوحة اسم مشغّل سحب إفلات zip تثبيت",
        ["**Or drag and drop it.** Drop the game's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. A game you already have asks before replacing it, and a changed version takes effect after a restart."] =
            "**أو اسحبها وأفلتها.** أفلت مجلد اللعبة أو ملف ZIP منها في أي مكان داخل نافذة **الإعدادات**، أو على `Radiata.exe` أو على اختصار له. ينسخها Radiata إلى مكانها ويطلب منك التأكيد، دون إعادة تشغيل. وإذا كانت اللعبة مثبتة لديك بالفعل يطلب تأكيد الاستبدال، ويسري الإصدار المعدّل بعد إعادة التشغيل.",
        ["**Restart Radiata** after adding a game to that folder by hand, or changing one, then accept the confirmation. It shows up in the Arcade picker beside the built-in games, and as a choice when you add an **Arcade** slice. In your config it's the token `pkg-<id>`."] =
            "**أعد تشغيل Radiata** بعد إضافة لعبة إلى ذلك المجلد يدويًا أو بعد تعديل إحداها، ثم اقبل التأكيد. تظهر في محدد الأركيد بجانب الألعاب المدمجة، وكخيار عند إضافة شريحة **أركيد**. وهي في إعداداتك الرمز `pkg-<id>`.",
        ["workshop test testing debug debugging share sharing zip unzip send friend install license trace log skipped error not showing missing folder nested backup move another pc drag drop"] =
            "ورشة اختبار تجربة تصحيح مشاركة zip فك ضغط إرسال صديق تثبيت ترخيص سجل تتبع تخطي خطأ لا يظهر مفقود مجلد متداخل نسخة احتياطية نقل جهاز آخر سحب إفلات",
        ["To share a package, zip the files **inside** its folder (not the folder itself) and name the zip after the package. The person installing it drops the zip onto Radiata's **Settings** window and accepts the confirmation - or uses **Extract All** into their own Materials or Arcade Games folder and restarts Radiata."] =
            "لمشاركة حزمة، اضغط الملفات **الموجودة داخل** مجلدها (لا المجلد نفسه) في ملف ZIP وسمّه باسم الحزمة. يفلت من يثبّتها ملف ZIP في نافذة **الإعدادات** في Radiata ويقبل التأكيد، أو يستخدم **استخراج الكل** إلى مجلد Materials أو Arcade Games الخاص به ثم يعيد تشغيل Radiata.",
        ["Radiata is a feature-rich, controller-based radial menu utility for a Windows gaming PC. Launch a game, join the Discord call, swap to headphones, start your stream, all from a controller."] =
            "Radiata أداة قائمة دائرية غنية بالميزات تعمل بجهاز التحكم لحاسوب ألعاب يعمل بنظام Windows. شغّل لعبة، انضم إلى مكالمة Discord، انتقل إلى السماعات، ابدأ بثّك المباشر، كل ذلك من جهاز التحكم.",
        ["Try it now: open a wheel with **{invoke}**."] =
            "جرّبها الآن: افتح عجلة بـ **{invoke}**.",
        ["**Game Grid** - a universal launcher for every installed game across Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft and EA."] =
            "**شبكة الألعاب** - مشغّل شامل لكل لعبة مثبّتة عبر Steam وEpic وPlaynite وGOG وXbox وBattle.net وAmazon وitch وUbisoft وEA.",
        ["**Nothing hooked, nothing injected** - Radiata reads your controller directly and allows your input through only when a wheel isn't up, so the game underneath doesn't pick up duplicate input. For strict-anticheat titles, see [[passthru-mode|Passthru Mode]]."] =
            "**بلا خطّافات ولا حقن** - يقرأ Radiata جهاز التحكم مباشرة ويمرّر إدخالك فقط حين لا تكون العجلة مفتوحة، فلا تلتقط اللعبة تحتها إدخالًا مكررًا. للألعاب ذات أنظمة مكافحة الغش الصارمة، راجع [[passthru-mode|الوضع المباشر]].",
        ["Start with [[opening-a-wheel|Opening a wheel]]. Then open one and click the aiming stick (L3/R3) when you're ready to start editing."] =
            "ابدأ من [[opening-a-wheel|فتح عجلة]]. ثم افتح واحدة واضغط عصا التصويب (L3/R3) حين تكون مستعدًا لبدء التحرير.",
        ["Radiata ships as a single installer, **Radiata-<version>-setup.exe**. Download it from [getradiata.app](https://getradiata.app)."] =
            "يوزّع Radiata كمثبّت واحد، **Radiata-<version>-setup.exe**. نزّله من [getradiata.app](https://getradiata.app).",
        ["**Only download Radiata from known sources.** Anything else claiming to be Radiata isn't from the developer."] =
            "**نزّل Radiata من مصادر معروفة فقط.** أي شيء آخر يزعم أنه Radiata ليس من المطوّر.",
        ["**Administrator account needed** for driver installation (optional but strongly recommended)"] =
            "**حساب المسؤول مطلوب** لتثبيت برامج التشغيل (اختياري لكن يوصى به بشدة)",
        ["Windows SmartScreen may show a blue **\"Windows protected your PC\"** box the first time you run the installer, and your browser may warn that the file **\"isn't commonly downloaded\"**."] =
            "قد يعرض Windows SmartScreen مربعًا أزرق مكتوبًا عليه **\"حمى Windows جهازك\"** أول مرة تشغّل فيها المثبّت، وقد يحذّر متصفحك من أن الملف **\"لا ينزّل كثيرًا\"**.",
        ["**No Run anyway button at all?** A managed or locked-down PC can have SmartScreen set to block outright. Radiata can't work around it."] =
            "**لا يوجد زر Run anyway على الإطلاق؟** يمكن أن يكون SmartScreen على جهاز مدار أو مقيّد مضبوطًا على الحظر التام. لا يستطيع Radiata تجاوز ذلك.",
        ["You can confirm you have the genuine file before running it: every GitHub release lists the installer's **SHA-256**, and `Get-FileHash .\\Radiata-<version>-setup.exe` in PowerShell should print the same value. Radiata's updater automatically runs the same verification check on every update."] =
            "يمكنك التأكد من أن لديك الملف الأصلي قبل تشغيله: يذكر كل إصدار على GitHub قيمة **SHA-256** للمثبّت، وينبغي أن يطبع الأمر `Get-FileHash .\\Radiata-<version>-setup.exe` في PowerShell القيمة نفسها. ويجري محدّث Radiata تلقائيًا عملية التحقق نفسها مع كل تحديث.",
        ["**Antivirus false positives** happen for the same reason. If yours quarantines the installer, restore it and run it again, or download it fresh from getradiata.app."] =
            "**الإنذارات الكاذبة من برامج مكافحة الفيروسات** تحدث للسبب نفسه. إذا عزل برنامجك المثبّت في الحجر، فاستعده وشغّله مرة أخرى، أو نزّله من جديد من getradiata.app.",
        ["Installs **for your user account only**, into `%LOCALAPPDATA%\\Programs\\Radiata`. It never touches other accounts on the PC."] =
            "يثبّت **لحساب المستخدم الخاص بك فقط**، في `%LOCALAPPDATA%\\Programs\\Radiata`. ولا يمس الحسابات الأخرى على الجهاز.",
        ["Adds a **Start menu** shortcut, and on a **first** install sets Radiata to **start with Windows**. You can turn that off in the tray menu or **Settings ▸ Advanced**."] =
            "يضيف اختصارًا إلى **قائمة ابدأ**، وعند التثبيت **الأول** يضبط Radiata ليعمل **مع بدء تشغيل Windows**. يمكنك إيقاف ذلك من قائمة شريط النظام أو من **الإعدادات ◂ متقدم**.",
        ["Nothing sneaky or malicious comes with Radiata. Radiata is GPLv3 free software."] =
            "لا يأتي مع Radiata أي شيء خفي أو ضار. Radiata برنامج حر بترخيص GPLv3.",
        ["Installing over an existing copy is an **upgrade in place**. Your wheels, settings and game art are left alone."] =
            "التثبيت فوق نسخة موجودة هو **ترقية في مكانها**. تبقى عجلاتك وإعداداتك وصور الألعاب كما هي.",
        ["Driver prompts (UAC prompts)"] =
            "مطالبات برامج التشغيل (مطالبات UAC)",
        ["Leave **Install drivers (recommended)** selected and approve any necessary Windows prompts that follow. **ViGEmBus** and **HidHide** are the open-source drivers that keep duplicate controller input out of the game. See [[input-isolation|Input isolation]]."] =
            "اترك **تثبيت برامج التشغيل (موصى به)** محددًا ووافق على أي مطالبات Windows ضرورية تظهر بعد ذلك. **ViGEmBus** و**HidHide** برنامجا تشغيل مفتوحا المصدر يبقيان إدخال جهاز التحكم المكرر بعيدًا عن اللعبة. راجع [[input-isolation|عزل الإدخال]].",
        ["**Declining is safe.** Radiata still works; games just also see your controller while a wheel is open, which is annoying. Install them later any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**."] =
            "**الرفض آمن.** يظل Radiata يعمل؛ غير أن الألعاب ترى جهاز التحكم أيضًا أثناء فتح العجلة، وهو أمر مزعج. ثبّتهما لاحقًا في أي وقت من **الإعدادات ◂ متقدم ◂ استكشاف الأخطاء وإصلاحها ◂ تثبيت/إصلاح برامج التشغيل**.",
        ["The drivers are shared system components other tools may also use, so if you uninstall Radiata, removing the drivers as well is optional."] =
            "برامج التشغيل مكوّنات نظام مشتركة قد تستخدمها أدوات أخرى أيضًا، لذا فإزالتها عند إلغاء تثبيت Radiata اختيارية.",
        ["Radiata then lives in the **system tray**. Click the icon for Settings or right-click for the tray menu. Then start at [[opening-a-wheel|Opening a wheel]]."] =
            "يستقر Radiata بعد ذلك في **شريط النظام**. انقر الأيقونة لفتح الإعدادات أو انقر بالزر الأيمن لفتح قائمة شريط النظام. ثم ابدأ من [[opening-a-wheel|فتح عجلة]].",
        ["**Updates are discovered automatically** by default. You can manually check for updates at **Settings ▸ Advanced ▸ Check for Updates**. Radiata always verifies the download before running it."] =
            "**تكتشف التحديثات تلقائيًا** افتراضيًا. ويمكنك التحقق منها يدويًا من **الإعدادات ◂ متقدم ◂ التحقق من التحديثات**. يتحقق Radiata دائمًا من التنزيل قبل تشغيله.",
        ["**Uninstall** from **Settings ▸ Advanced ▸ Troubleshooting ▸ Uninstall Radiata…**, or from Windows' **Installed apps** list. Your settings and the shared drivers can be removed in the same step."] =
            "**ألغ التثبيت** من **الإعدادات ◂ متقدم ◂ استكشاف الأخطاء وإصلاحها ◂ إلغاء تثبيت Radiata…**، أو من قائمة **التطبيقات المثبّتة** في Windows. يمكن إزالة إعداداتك وبرامج التشغيل المشتركة في الخطوة نفسها.",
        ["Your current setting: open a wheel with **{invoke}**."] =
            "إعدادك الحالي: افتح عجلة بـ **{invoke}**.",
        ["Set your own chords (button combos that open a wheel) in [[triggers|Settings ▸ Customize ▸ Triggers]]."] =
            "اضبط تشكيلات الأزرار الخاصة بك (مجموعات أزرار تفتح عجلة) في [[triggers|الإعدادات ◂ تخصيص ◂ إيماءات الاستدعاء]].",
        ["**Flip mid-gesture:** while holding a chord, tap the opposite bumper or trigger to switch to the other wheel without having to re-input the entire chord."] =
            "**القلب أثناء الإيماءة:** أثناء الضغط المطوّل على مجموعة أزرار، انقر المصد أو الزناد المقابل للتبديل إلى العجلة الأخرى دون الحاجة إلى إعادة إدخال التشكيلة كاملة.",
        ["**Either** analog stick aims, but it's easiest if you use the hand that's not holding the shoulder button. **Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) narrows it to one stick per wheel."] =
            "تصوّب **أي** من العصاتين التناظريتين، لكن الأسهل أن تستخدم اليد التي لا تمسك بزر الكتف. **تتجاهل العجلة العصا المقابلة** ([[accessibility|إعداد إمكانية الوصول]]) يحصره في عصا واحدة لكل عجلة.",
        ["**Tilt the stick** toward a slice and it lights up. **Release the trigger** to fire it."] =
            "**أمل العصا** نحو شريحة فتضيء. **أفلت الزناد** لتنفيذها.",
        ["**Release while centered** (stick in the deadzone) **cancels**."] =
            "**الإفلات والعصا في المنتصف** (داخل المنطقة الميتة) **يلغي**.",
        ["A slice that still **needs configuring** (such as a voice-join with no URL) arms as **\"Configure in Settings\"**. Choosing it takes you to the configuration screen or the setup wizard it needs."] =
            "الشريحة التي ما زالت **بحاجة إلى إعداد** (مثل انضمام صوتي بلا عنوان URL) تحدّد بعبارة **\"اضبطه في الإعدادات\"**. وباختيارها تنتقل إلى شاشة الإعداد أو إلى معالج الإعداد الذي تحتاجه.",
        ["Slices with **Hold to confirm** are protected from accidental triggering, which is useful for Sleep, Power Down, etc. Hold the stick on them (0.8 s) and they'll activate. You can set this on any slice in the Settings wheel editors."] =
            "الشرائح التي عليها **اضغط مع الاستمرار للتأكيد** محمية من التنفيذ العرضي، وهذا مفيد لإجراءات مثل السكون وإيقاف التشغيل. أبق العصا عليها لمدة 0.8 ثانية فتنفّذ. ويمكنك ضبط ذلك لأي شريحة من محرري العجلات في الإعدادات.",
        ["**D-Pad 🡅 🡇** adjusts system volume."] =
            "**D-Pad 🡅 🡇** يضبط مستوى صوت النظام.",
        ["**D-Pad 🡄 🡆** steps the Alt-Tab window switcher by default - or [[volume-mixer|virtual desktops, track skip, or mic volume]]. Choose in **Settings ▸ Customize ▸ D-Pad 🡄 🡆**. "] =
            "**أزرار الاتجاهات 🡄 🡆** ينتقل عبر مبدّل نوافذ Alt-Tab افتراضيًا - أو [[volume-mixer|أسطح المكتب الافتراضية، أو تخطي المقاطع، أو مستوى الميكروفون]]. اختر في **الإعدادات ◂ تخصيص ◂ أزرار الاتجاهات 🡄 🡆**.",
        ["You can also toggle wheels on/off from tray menu's **Disable/Enable Wheels** or add a **Disable Wheels** slice action."] =
            "يمكنك أيضًا تبديل العجلات من عنصر **تعطيل العجلات/تفعيل العجلات** في قائمة شريط النظام أو بإضافة إجراء شريحة **تعطيل العجلات**.",
        ["Disabling the wheels changes wheel routing only. The virtual controller and cloak stay exactly where they are, basic controls keep running through that controller, and native features do **not** come back. To release capture and get your real controller, use [[passthru-mode|Passthru Mode]]. This will re-enable vendor features like special haptics and touchpads."] =
            "تعطيل العجلات يغيّر توجيه العجلة فقط. يبقى جهاز التحكم الافتراضي والإخفاء في مكانهما تمامًا، وتستمر عناصر التحكم الأساسية عبر ذلك الجهاز، و**لا** تعود الميزات الأصلية. لتحرير الالتقاط واستخدام جهاز التحكم الحقيقي، استخدم [[passthru-mode|الوضع المباشر]]. سيعيد هذا تفعيل ميزات الشركة المصنّعة مثل الاهتزاز الخاص ولوحات اللمس.",
        ["Your current setting: toggle the wheels with **{disable}**."] =
            "إعدادك الحالي: بدّل العجلات بـ **{disable}**.",
        ["**Start editing a wheel:** with a wheel open, **click either stick** (L3/R3). The wheel centers and stays up after you release the trigger."] =
            "**ابدأ تحرير عجلة:** بينما العجلة مفتوحة، **انقر أي من العصاتين** (L3/R3). تتمركز العجلة وتبقى ظاهرة بعد إفلات الزناد.",
        ["**Use either stick**. While you're editing, non-logo slices always show their labels regardless of the [[show-labels|Label setting]]."] =
            "**استخدم أي من العصاتين**. أثناء التحرير تعرض الشرائح التي لا تحمل شعارًا تسمياتها دائمًا، بصرف النظر عن [[show-labels|إعداد التسميات]].",
        ["**Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) can override this."] =
            "**تتجاهل العجلة العصا المقابلة** ([[accessibility|إعداد إمكانية الوصول]]) يمكنه تجاوز ذلك.",
        ["**Move:** **{cross}** picks up the selected slice; aim at a target slot and **{cross}** drops it."] =
            "**النقل:** **{cross}** يلتقط الشريحة المحددة؛ صوّب نحو الموضع المطلوب ثم **{cross}** لإفلاتها.",
        [" **D-Pad 🡄 🡆** will nudge a slice one spot left or right."] =
            "**أزرار الاتجاهات 🡄 🡆** تزحزح الشريحة موضعًا واحدًا إلى اليسار أو إلى اليمين.",
        ["**Remove:** **hold {square}** on a slice until it disappears. Removing the **last** slice disables that wheel; see [[empty-wheel|Single-wheel mode]]."] =
            "**الإزالة:** **اضغط {square} مع الاستمرار** على شريحة حتى تختفي. إزالة الشريحة **الأخيرة** تعطّل تلك العجلة؛ راجع [[empty-wheel|وضع العجلة الواحدة]].",
        ["A wheel holds up to **12** slices."] =
            "تحمل العجلة حتى **12** شريحة.",
        ["**Undo / Redo:** **L1 / R1**. You can undo/redo multiple steps while you remain in Edit mode."] =
            "**تراجع / إعادة:** **L1 / R1**. يمكنك التراجع والإعادة عدة خطوات ما دمت في وضع التحرير.",
        ["**Exit + save:** **{circle}**, click the stick again, or press an **Fn** / **L4/R4** button."] =
            "**الخروج والحفظ:** **{circle}**، أو انقر العصا مجددًا، أو اضغط زر **Fn** / **L4/R4**.",
        ["**Want only one wheel?** Delete every slice off the other one. A wheel with **no slices is disabled**. That side's chord stays fully usable in the game. Remove slices from Settings, or in [[edit-mode|edit mode]] with **hold {square}** until the last one is gone."] =
            "**هل تريد عجلة واحدة فقط؟** احذف كل الشرائح من الأخرى. العجلة **الخالية من الشرائح معطّلة**. تبقى تشكيلة ذلك الجانب صالحة تمامًا داخل اللعبة. أزل الشرائح من الإعدادات، أو من [[edit-mode|وضع التحرير]] بالضغط المستمر على **{square}** حتى تختفي آخر واحدة.",
        ["If you accidentally empty **both** wheels, Settings opens so you can rebuild one or both of them."] =
            "إن أفرغت **كلتا** العجلتين عن غير قصد، تفتح الإعدادات لتعيد بناء إحداهما أو كلتيهما.",
        ["If a wheel has only one slice and it's **the Arcade Launcher**, then that wheel is replaced by the Arcade Launcher directly."] =
            "إذا كانت في العجلة شريحة واحدة فقط وهي **مشغّل الأركيد**، فتستبدل العجلة بمشغّل الأركيد مباشرة.",
        ["**To set it up:** just delete all slices on a wheel except a single **Arcade ▸ Arcade Launcher** slice. You can do that in Settings, or in [[edit-mode|edit mode]] with **hold {square}**."] =
            "**طريقة الإعداد:** احذف كل الشرائح من العجلة ما عدا شريحة واحدة هي **الأركيد ◂ مشغّل الأركيد**. يمكنك ذلك من الإعدادات، أو من [[edit-mode|وضع التحرير]] بالضغط المستمر على **{square}**.",
        ["**This applies to Arcade Launcher only.** A single slice with just an individual Arcade game on it still draws as a one-slice wheel."] =
            "**هذا ينطبق على مشغّل الأركيد وحده.** الشريحة الواحدة التي تحمل لعبة أركيد منفردة فقط تظل ترسم كعجلة من شريحة واحدة.",
        ["**An Arcade Launcher wheel** can't be edited with R3/L3; edit it in **Settings ▸ Left/Right Wheel**, or add a second slice to get the wheel back."] =
            "**عجلة مشغّل الأركيد** لا يمكن تحريرها بـ R3/L3؛ حرّرها من **الإعدادات ◂ العجلة اليسرى/اليمنى**، أو أضف شريحة ثانية لتستعيد العجلة.",
        ["If you want, you can pair this with [[empty-wheel|Single-wheel mode]]: empty the OTHER wheel and you have one gesture that opens the arcade and one that passes straight through to the game."] =
            "إن أردت، يمكنك إقرانه مع [[empty-wheel|وضع العجلة الواحدة]]: أفرغ العجلة الأخرى فتحصل على إيماءة تفتح الأركيد وأخرى تمرّ مباشرة إلى اللعبة.",
        ["**Drag an app (.exe or .lnk) from Explorer into the slice list**. A **Launch** slice lands at the drop position with its icon already extracted. Drop several at once for several slices."] =
            "**اسحب تطبيقًا (.exe أو .lnk) من مستكشف الملفات إلى قائمة الشرائح**. تظهر شريحة **تشغيل** عند موضع الإفلات وقد استخرجت أيقونتها. أفلت عدة ملفات دفعة واحدة لإنشاء عدة شرائح.",
        ["**Color swatches are paired** - the wheel shows whichever variation suits its [[customize|material]]. If you type an exact **Hex** value instead, that color is used exactly as-is, without tinting lighter or darker based on wheel Material. **Reset to Default** returns to the action-type color."] =
            "**عيّنات الألوان مقترنة** - تعرض العجلة النسخة التي تناسب [[customize|خامتها]]. أما إن كتبت قيمة **ست عشرية** دقيقة فسيستخدم ذلك اللون كما هو، دون تفتيح أو تغميق بحسب خامة العجلة. و**إعادة التعيين إلى الافتراضي** يعيد لون نوع الإجراء.",
        ["**Drop your own image onto the preview well** to give any slice custom artwork. It needs a **transparent background**, so a logo-style PNG is best. Something like a screenshot or a photo with no transparency would be a solid block so it's rejected. The file is **copied** into Radiata's art cache, so moving the original later won't blank the slice."] =
            "**أفلت صورتك الخاصة على مربع المعاينة** لتمنح أي شريحة صورة مخصصة. تحتاج إلى **خلفية شفافة**، لذا فصيغة PNG على هيئة شعار هي الأنسب. أي صورة مثل لقطة الشاشة أو الصورة الفوتوغرافية بلا شفافية ستكون كتلة صمّاء، لذا ترفض. **ينسخ** الملف إلى ذاكرة الصور في Radiata، فلا يؤدي نقل الأصل لاحقًا إلى تفريغ الشريحة.",
        ["When you choose a game, Radiata fetches its transparent **logo** automatically. The **↻ button** restores that logo (downloading it if needed), and the **🡄 🡆** buttons cycle every logo already downloaded for that game. **Requires [[integrations|SteamGridDB]] to be configured.**"] =
            "عند اختيار لعبة يجلب Radiata **شعارها** الشفاف تلقائيًا. **زر ↻** يستعيد ذلك الشعار (ينزّله إن لزم)، وأزرار **🡄 🡆** تتنقل بين كل شعار سبق تنزيله لتلك اللعبة. **يتطلب ضبط [[integrations|SteamGridDB]].**",
        ["**Choosing a different icon drops the original game logo**. A slice's logo is independent of the [[cover-art|Game Grid's]]."] =
            "**اختيار أيقونة مختلفة يسقط شعار اللعبة الأصلي**. شعار الشريحة مستقل عن [[cover-art|شعار شبكة الألعاب]].",
        ["**Everything auto-saves** - adds, removals, reorders, and edits to an existing slice (**Revert** undoes an in-progress edit). **A new slice created in Settings is not saved until you click Save Slice**. Entering **Ctrl+S** forces a save at any point."] =
            "**كل شيء يحفظ تلقائيًا** - الإضافات والإزالات وإعادة الترتيب والتعديلات على شريحة موجودة (**تراجع** يتراجع عن تعديل جارٍ). **الشريحة الجديدة المنشأة في الإعدادات لا تحفظ حتى تنقر حفظ الشريحة**. ويفرض إدخال **Ctrl+S** الحفظ في أي لحظة.",
        ["**Select** (Create/Share) - **cycle the selected game's cover** and save it. Your choices will cycle through default, then up to 10 top-rated SteamGridDB covers, then 5 flat colors if you just want the logo on a clean background."] =
            "**تحديد** (Create/Share) - **يبدّل غلاف اللعبة المحددة** ويحفظه. تتنقل خياراتك بين الافتراضي، ثم حتى 10 أغلفة من SteamGridDB الأعلى تقييمًا، ثم 5 ألوان مسطحة إن أردت الشعار فقط على خلفية نظيفة.",
        ["Covers come from [[integrations|SteamGridDB]] - add a free API key in **Settings ▸ Advanced ▸ Integrations** for portrait covers and logos across every storefront. This is the best option. However, without SteamGridDB, you still get Steam's own art, [[playnite|Playnite]]'s covers, and the flat colors."] =
            "تأتي الأغلفة من [[integrations|SteamGridDB]] - أضف مفتاح API مجانيًا في **الإعدادات ◂ متقدم ◂ التكاملات** لأغلفة عمودية وشعارات عبر كل متجر. هذا هو الخيار الأفضل. لكن بدون SteamGridDB تحصل مع ذلك على فن Steam الخاص، وأغلفة [[playnite|Playnite]]، والألوان المسطحة.",
        ["An **Arcade** slice opens a little game in a **round window, right where the wheel was**. Play a game while you wait on a loading screen or a big lobby, no alt-tabbing required."] =
            "تفتح شريحة **الأركيد** لعبة صغيرة في **نافذة دائرية، في موضع العجلة تمامًا**. العب لعبة بينما تنتظر شاشة تحميل أو غرفة انتظار كبيرة، دون الحاجة إلى Alt-Tab.",
        ["**Arcade Launcher** opens the whole arcade. Each game is a cabinet on a round carousel with a live screenshot of where it was left. **Left/right** on the stick or D-Pad swings the next cabinet to the front, **{cross}** plays it. It **picks up where you left off** - straight back into the game you were last playing, or at the cabinets if that's where you closed it. Each game can also be directly-launched by adding a slice for it."] =
            "**مشغّل الأركيد** يفتح الأركيد كله. كل لعبة آلة على دوّار دائري مع لقطة حية من حيث تركتها. **يسار/يمين** على العصا أو أزرار الاتجاهات يدير الآلة التالية إلى المقدمة، و**{cross}** يشغّلها. وهو **يستأنف من حيث توقفت** - مباشرة إلى اللعبة التي كنت تلعبها آخر مرة، أو عند الآلات إن كنت قد أغلقته هناك. ويمكن أيضًا تشغيل كل لعبة مباشرة بإضافة شريحة لها.",
        ["**A wheel with the Arcade Launcher and nothing else** skips the wheel and goes straight to the Arcade Launcher - see [[arcade-direct-launch|Arcade direct-launch]]."] =
            "**العجلة التي فيها مشغّل الأركيد ولا شيء غيره** تتخطى العجلة وتنتقل مباشرة إلى مشغّل الأركيد - راجع [[arcade-direct-launch|تشغيل الأركيد المباشر]].",
        ["**{circle} always backs you out**. One press closes a menu or help card, the next steps out of the game: back to the **Arcade Launcher** when that's how you got in, otherwise straight out. **The game freezes exactly as you left it**, so you can come back later and carry on. Each game stores its own state and scoreboard."] =
            "**{circle} يخرجك دائمًا**. ضغطة تغلق قائمة أو بطاقة مساعدة، والتالية تخرج من اللعبة: إلى **مشغّل الأركيد** إن كنت قد دخلت منه، وإلا فإلى الخارج مباشرة. **تتجمد اللعبة تمامًا حيث تركتها**، فيمكنك العودة لاحقًا والمتابعة. تخزّن كل لعبة حالتها ولوحة نتائجها.",
        ["Over a game, the arcade only plays while your controller is **isolated** from it. Otherwise a card explains why and offers **hold {triangle} to play anyway**. Passthru Mode will mean no Arcade games can be played while you're in another game. See [[input-isolation|Input isolation]]."] =
            "فوق اللعبة، لا يعمل الأركيد إلا ما دام جهاز التحكم **معزولًا** عنها. وإلا شرحت بطاقة السبب وعرضت **الضغط المستمر على {triangle} للعب رغم ذلك**. يعني الوضع المباشر أنه لا يمكن لعب ألعاب الأركيد أثناء وجودك في لعبة أخرى. راجع [[input-isolation|عزل الإدخال]].",
        ["**Kabloom**: Minesweeper logic on a Floret Pentagonal Tiled field of flower petals. Move the cursor with the stick or d-pad. **{cross}** reveals a tile, **{square}** flags where you think there's a bee (or multiple bees, on later levels). Press **{square}** again to increase the flag count, or hold it for a question mark flag. Remaining bees are shown at the bottom of the screen. At the center of each floret is a nectar gem, collected when all the petals around it are cleared, which adds to your total score. Every board is solvable with no forced guesses."] =
            "**Kabloom**: منطق كانسة الألغام على حقل من بتلات الزهرة مبلّط بالخماسيات على شكل زهيرات. حرّك المؤشر بالعصا أو بأزرار الاتجاهات. **{cross}** يكشف بلاطة، و**{square}** يضع علامة حيث تظن أن هناك نحلة (أو عدة نحلات في المراحل المتأخرة). اضغط **{square}** مرة أخرى لزيادة عدد العلامات، أو اضغط مع الاستمرار لوضع علامة استفهام. يظهر النحل المتبقي أسفل الشاشة. في مركز كل زهيرة جوهرة رحيق تجمع عند إزالة كل البتلات المحيطة بها، وتضاف إلى نتيجتك الكلية. كل لوح قابل للحل دون تخمين مفروض.",
        ["**More about \"no forced guesses\":** Boards 1-10 are generated on the fly and validated by the Solver before play. Every board from level 11 up is baked ahead of time and certified by a complete solver before it ships. Individual petal tiles can hold up to three bees on the later levels. The solver plays the board from every zero-clue petal you could open on. It runs the human patterns first (saturation, subset difference, overlap bounds, chained constraints) and when those run dry it groups the unknown petals that share the same set of clues into boxes, enumerates every way the remaining bees can be spread over each connected group of boxes, and folds in the total bee count so the petals no clue touches get reasoned about too. A level ships only when the certified start-points cover the whole crop, so the first petal you open is always one the proof began from, ensuring every possible start guarantees a solvable board. "] =
            "**المزيد عن \"لا تخمين مفروض\":** تولّد الألواح من 1 إلى 10 فورًا ويتحقق منها الحلّال قبل اللعب. كل لوح من المرحلة 11 فصاعدًا يعدّ مسبقًا ويصادق عليه حلّال كامل قبل إصداره. وفي المراحل المتأخرة يمكن أن تضم كل بتلة حتى ثلاث نحلات. يلعب الحلّال اللوح انطلاقًا من كل بتلة بلا أدلة يمكن أن تبدأ منها. يطبّق أولًا الأنماط البشرية (الإشباع، وفرق المجموعات الجزئية، وحدود التداخل، والقيود المتسلسلة) وحين تنفد يجمع البتلات المجهولة التي تشترك في المجموعة نفسها من الأدلة داخل صناديق، ويعدّ كل توزيعات النحل المتبقي على كل مجموعة صناديق متصلة، ويدمج العدد الكلي للنحل ليستنتج أيضًا بشأن البتلات التي لا يلامسها أي دليل. ولا تصدر المرحلة إلا حين تغطي نقاط البداية المصادق عليها الحقل كله، فتكون أول بتلة تفتحها دائمًا إحدى البتلات التي بدأ منها البرهان، مما يضمن أن كل بداية ممكنة تقود إلى لوح قابل للحل.",
        ["**Connate**: Your craft rides the rim around a cluster of orbs and garbage blocks. Shoot orbs to merge the numbers before the pile grows past the inner ring. **{cross}** fires your held number into the cluster. Hold to fire with more force. Star and Star-Gap pieces merge to make orbs of 3, and matching numbers from 3 up combines their values. Only **matching colors** merge, although mixed-color orbs can be created by matching stars and gaps of opposite colors; these merge with either color or with other mixed-color orbs. Combos charge up a bomb you can fire. Bomb high-value orbs to collect them to your score."] =
            "**Connate**: تسير مركبتك على الحافة حول تجمع من الكرات وكتل النفايات. أطلق النار على الكرات لدمج الأرقام قبل أن تتجاوز الكومة الحلقة الداخلية. **{cross}** يقذف الرقم الذي تحمله داخل التجمع. اضغط مع الاستمرار للقذف بقوة أكبر. تندمج قطعتا النجمة والنجمة-الفجوة لتكوّنا كرة بقيمة 3، ومن 3 فصاعدًا يجمع تطابق الأرقام قيمها. ولا يندمج إلا ما تطابقت **ألوانه**، لكن يمكن إنشاء كرات متعددة الألوان بمطابقة نجوم وفجوات بلونين متقابلين؛ وتندمج هذه مع أي من اللونين أو مع كرات أخرى متعددة الألوان. تشحن سلاسل الدمج قنبلة يمكنك إطلاقها. فجّر الكرات عالية القيمة لتضيفها إلى نتيجتك.",
        ["**Stages**: the pace follows your score. Each time your collected total crosses 100, 250, 450, 700 and 1,000, the shot clock gets a little shorter, garbage arrives a little sooner, and a bomb takes one more combo charge to fill. The current stage is shown under the score. From stage 2, every 48 seconds of play ends with 8 seconds of relief: no garbage, and a longer shot clock."] =
            "**المراحل**: تتبع وتيرة اللعب نتيجتك. في كل مرة يتجاوز فيها مجموع ما جمعته 100 أو 250 أو 450 أو 700 أو 1,000، تقصر مهلة الإطلاق قليلًا، وتصل كتل النفايات أبكر قليلًا، وتحتاج القنبلة إلى شحنة تتابع واحدة إضافية لتمتلئ. تظهر المرحلة الحالية تحت النتيجة. ابتداءً من المرحلة 2، تنتهي كل 48 ثانية من اللعب بفترة راحة مدتها 8 ثوانٍ: بلا كتل نفايات ومع مهلة إطلاق أطول.",
        ["**Petalpop**: a ring of paddles around a flower of petals. Your analog stick controls every paddle together, so be careful! Hold **{cross}** to draw the paddles back like a slingshot, then release to **smash** the ball. Hit the core with a smash shot to clear the level. Pop a blue petal for multi-ball. \n\nFour sides and four levels to start, then five, six, seven and eight. You get one extra life for each size increase. Switch between spring and rail control via the **Start** menu."] =
            "**Petalpop**: حلقة من المضارب حول زهرة من البتلات. عصا التحكم التناظرية تحرك كل المضارب معًا، فكن حذرًا! اضغط **{cross}** مع الاستمرار لسحب المضارب إلى الخلف كالمقلاع، ثم أفلت لتوجّه **ضربة قوية** إلى الكرة. أصب النواة بضربة قوية لإنهاء المرحلة. وإزالة بتلة زرقاء تمنحك كرات متعددة.\n\nتبدأ بأربعة أضلاع وأربع مراحل، ثم خمسة وستة وسبعة وثمانية. تحصل على حياة إضافية مع كل زيادة في الحجم. بدّل بين التحكم النابضي والتحكم على المسار من قائمة **Start**.",
        ["**Internode**: shoot down a twisting half-pipe and collect tokens while avoiding mines and gaps. Hitting a mine will drop your tokens, and hitting one when carrying no tokens sets you back one stretch. Falling in a gap always sets you back one stretch. **{cross}** jumps. \n\nCatching a full token streak will upgrade the final token in the pattern to a gold 10x token. Reach each checkpoint with enough tokens to bank them in your score, with extra bonuses for passing a stretch on the first try and for collecting every token in a stretch. Insufficient tokens keeps you looping the same stretch until you have enough. The course twists and turns harder every stage. \n\nCamera roll can be enabled/disabled in the **START** menu."] =
            "**Internode**: انطلق عبر أنبوب نصفي ملتوٍ واجمع الرموز مع تفادي الألغام والفجوات. إصابة لغم تسقط رموزك، وإصابته دون حمل أي رمز تعيدك مقطعًا واحدًا إلى الوراء. السقوط في فجوة يعيدك دائمًا مقطعًا واحدًا إلى الوراء. **{cross}** للقفز.\n\nالتقاط سلسلة رموز كاملة يرقّي الرمز الأخير في النمط إلى رمز ذهبي بقيمة 10x. صل إلى كل نقطة تفتيش ومعك رموز كافية لإيداعها في نتيجتك، مع مكافآت إضافية لاجتياز مقطع من المحاولة الأولى ولجمع كل رموز المقطع. إذا لم تكن الرموز كافية فتظل تكرر المقطع نفسه حتى تجمع ما يكفي. يزداد التواء المسار وانعطافاته مع كل مرحلة.\n\nيمكن تفعيل دوران الكاميرا أو تعطيله من قائمة **START**.",
        ["**Choose the app** two ways: **Browse for App…** picks an `.exe` from disk, and **Installed Apps…** lists everything with a Start-menu entry (including **Microsoft Store apps**, which have no `.exe` to browse to). Either way the icon is pulled in automatically. You can also drag an `.exe`, a shortcut, or a Start-menu app straight into the slice list."] =
            "**اختر التطبيق** بطريقتين: **استعراض التطبيقات…** يختار ملف `.exe` من القرص، و**التطبيقات المثبّتة…** يسرد كل ما له مدخل في قائمة ابدأ (بما في ذلك **تطبيقات Microsoft Store** التي ليس لها ملف `.exe` تستعرضه). وفي الحالتين تجلب الأيقونة تلقائيًا. ويمكنك أيضًا سحب ملف `.exe` أو اختصار أو تطبيق من قائمة ابدأ مباشرة إلى قائمة الشرائح.",
        ["A **Store app** can't be detected as already-running, so **Run** just re-opens it and **Toggle** won't reliably close it. And an app started through an updater or launcher **stub** may run under a different name than the file you picked, so picking the app's real `.exe` is the reliable choice."] =
            "**تطبيق Store** لا يمكن كشف ما إذا كان يعمل بالفعل، لذا يكتفي **تشغيل** بإعادة فتحه، ولا يغلقه **تبديل** بشكل موثوق. كما أن التطبيق الذي يبدأ عبر محدّث أو **ملف وسيط** للتشغيل قد يعمل باسم يختلف عن الملف الذي اخترته، لذا فاختيار ملف `.exe` الحقيقي للتطبيق هو الخيار الموثوق.",
        ["**Installed Game** slices launch the game **directly** where possible. A GOG game runs its own exe even without GOG Galaxy installed; only stores that need their client running (like Steam) will route through the launcher."] =
            "شرائح **لعبة مثبتة** تشغّل اللعبة **مباشرة** حيث أمكن. تشغّل لعبة GOG ملفها التنفيذي حتى بدون تثبيت GOG Galaxy؛ المتاجر التي تحتاج إلى تشغيل عميلها (مثل Steam) وحدها تمر عبر المشغّل.",
        ["Radiata forwards a **virtual controller**, and **Controller mode** decides which kind the game sees: an **Xbox** pad or a **DualShock** pad. Two slices under **System ▸ Controller** flip it - **Toggle Xbox Mode** and **Toggle DualShock Mode**."] =
            "يمرّر Radiata **جهاز تحكم افتراضيًا**، و**وضع جهاز التحكم** يحدد النوع الذي تراه اللعبة: جهاز **Xbox** أو جهاز **DualShock**. وتبدّله شريحتان ضمن **النظام ◂ جهاز التحكم**: **تبديل وضع Xbox** و**تبديل وضع DualShock**.",
        ["**What it's for:** many games only accept one class of controller. A Game Pass or Xbox-app title that refuses a DualSense controller will allow it if it's in **Xbox Mode** in Radiata. Games that want PlayStation input go the other way."] =
            "**فائدة ذلك:** كثير من الألعاب لا تقبل سوى فئة واحدة من أجهزة التحكم. فلعبة من Game Pass أو تطبيق Xbox ترفض جهاز DualSense ستقبله إن كان في **وضع Xbox** في Radiata. والألعاب التي تريد إدخال PlayStation تسلك الاتجاه المعاكس.",
        ["**Button prompts follow the mode.** The glyphs a game draws come from the pad it thinks is plugged in, so Xbox Mode gets you **A B X Y** and DualShock Mode **{cross} {circle} {square} {triangle}**."] =
            "**تتبع مطالبات الأزرار الوضع.** الرموز التي ترسمها اللعبة تأتي من الجهاز الذي تعتقد أنه موصول، فيمنحك Xbox Mode **A B X Y** ويمنحك DualShock Mode **{cross} {circle} {square} {triangle}**.",
        ["The switch is instant and stays put until you flip it back, but the game sees a controller swap at that moment. **Flip it before launching the game** for best results."] =
            "التبديل فوري ويبقى حتى تعيده، لكن اللعبة ترى في تلك اللحظة تبديلًا لجهاز التحكم. **بدّله قبل تشغيل اللعبة** للحصول على أفضل النتائج.",
        ["**A wired Xbox pad stays an Xbox pad.** If your physical controller is an Xbox pad on USB, or a USB wireless dongle, the game always gets a virtual Xbox pad and DualShock Mode cannot change it."] =
            "**يبقى جهاز Xbox السلكي جهاز Xbox.** إذا كان جهاز التحكم الفعلي لديك جهاز Xbox عبر USB أو وصلة USB لاسلكية، تحصل اللعبة دائمًا على جهاز Xbox افتراضي ولا يستطيع DualShock Mode تغيير ذلك.",
        ["This requires installing the isolation drivers. There's no virtual pad to switch without them, and firing the slice puts up an on-screen notice saying so. In [[passthru-mode|Passthru Mode]] the game reads your real controller, so the mode has nothing to change. See [[input-isolation|Input isolation]]."] =
            "يتطلب هذا تثبيت برامج تشغيل العزل. لا وجود لوحدة افتراضية للتبديل بدونها، وتنفيذ الشريحة يظهر إشعارًا على الشاشة يفيد بذلك. في [[passthru-mode|الوضع المباشر]] تقرأ اللعبة جهاز التحكم الحقيقي، فليس للوضع ما يغيّره. راجع [[input-isolation|عزل الإدخال]].",
        ["Discord must be running. These slices talk to the Discord app on your PC, not to Discord's website. "] =
            "يجب أن يكون Discord قيد التشغيل. فهذه الشرائح تتخاطب مع تطبيق Discord على حاسوبك، لا مع موقع Discord.",
        ["**OBS Studio** ([obsproject.com](https://obsproject.com)) is the free, open-source program that dominates the Twitch and YouTube streaming space, and also allows you to record the screen. It builds a broadcast out of **scenes** - named layouts of game capture, camera, mic and overlays - that you switch between while live."] =
            "**OBS Studio** ([obsproject.com](https://obsproject.com)) هو البرنامج المجاني مفتوح المصدر المهيمن على مجال البث على Twitch وYouTube، ويتيح أيضًا تسجيل الشاشة. يبني البث من **مشاهد** - تخطيطات مسمّاة لالتقاط اللعبة والكاميرا والميكروفون والتراكبات - تتنقل بينها أثناء البث المباشر.",
        ["A **Text Chat** slice (Chat & Streaming) types a message into the running game's text chat: it presses the game's **chat-open key**, types your text, and presses Enter."] =
            "تكتب شريحة **الدردشة النصية** رسالة في محادثة اللعبة النصية الجارية: تضغط **مفتاح فتح المحادثة** في اللعبة، وتكتب نصك، وتضغط Enter.",
        ["**Chat Button** - **Try Game Default** looks up the chat key for whatever game is running **each time the slice fires**, from a built-in index of around 1,000 PC games, so the same slice works across multiple games. **Custom…** lets you enter the key yourself, so use it for games with remapped or unusual chat keys, or for a game the index doesn't cover."] =
            "**زر الدردشة** - **تجربة افتراضي اللعبة** يبحث عن مفتاح الدردشة للعبة التي تعمل **في كل مرة تنفّذ فيها الشريحة**، ضمن فهرس مدمج يضم نحو 1000 لعبة حاسوب، فتصلح الشريحة نفسها لعدة ألعاب. و**مخصص…** يتيح لك إدخال المفتاح بنفسك، لذا استخدمه مع الألعاب التي غيّر فيها مفتاح الدردشة أو كان غير معتاد، أو مع لعبة لا يغطيها الفهرس.",
        ["**Message** - the line of text to send. An empty message (or Custom with no key) arms as **\"Configure in Settings\"**, and firing opens the slice's editor."] =
            "**الرسالة** - سطر النص الذي يرسل. والرسالة الفارغة (أو مخصص بلا مفتاح) تحدّد بعبارة **\"اضبطه في الإعدادات\"**، وتنفيذها يفتح محرر الشريحة.",
        ["**Sends are limited to one per 15 seconds**. Hub shows the remaining cooldown. No spamming, please!"] =
            "**الإرسال محدود بواحد كل 15 ثانية**. يعرض المحور المهلة المتبقية. لا إزعاج من فضلك!",
        ["**Sony family** (DualSense Edge, DualSense, DualShock 4) over Bluetooth or USB: good support with clean input isolation. The **Edge's Fn buttons** are the reference trigger. Haptic triggers and touchpad will not be seen by games due to driver limitations unless you are in Passthru Mode."] =
            "**عائلة Sony** (DualSense Edge، DualSense، DualShock 4) عبر Bluetooth أو USB: دعم جيد مع عزل إدخال نقي. **أزرار Fn في Edge** هي المشغّل المرجعي. لن ترى الألعاب الزنادات الاهتزازية ولوحة اللمس بسبب قيود برنامج التشغيل ما لم تكن في الوضع المباشر.",
        ["**Pads with extra paddles or buttons** (e.g. **8BitDo Ultimate 2C**) - over **Bluetooth** the extra **L4/R4** buttons are read directly, so you can pick them in [[triggers|Settings ▸ Customize ▸ Triggers]] to summon a wheel on their own, or paired with a second button if you also use them in games. They're offered, not assumed: the default stays the standard bumper chord. Over **USB** the extra buttons are invisible to Radiata, except as mapped by the controller's own drivers. So connect over Bluetooth if you want native L4/R4 support, or map them deliberately."] =
            "**الأجهزة ذات المجاذيف أو الأزرار الإضافية** (مثل **8BitDo Ultimate 2C**) - عبر **Bluetooth** تقرأ أزرار **L4/R4** الإضافية مباشرة، فيمكنك اختيارها من [[triggers|الإعدادات ◂ تخصيص ◂ المشغّلات]] لاستدعاء عجلة بمفردها، أو مقترنة بزر ثانٍ إن كنت تستخدمها في الألعاب أيضًا. وهي معروضة لا مفترضة: يبقى الافتراضي هو تشكيلة المصد المعتادة. أما عبر **USB** فالأزرار الإضافية غير مرئية لـ Radiata، إلا بالصورة التي تعيّنها برامج تشغيل الجهاز نفسه. لذا اتصل عبر Bluetooth إن أردت دعمًا أصليًا لـ L4/R4، أو عيّنها عمدًا.",
        ["**Genuine Xbox pads over Bluetooth** - isolated too, with an extra safeguard. Before presenting the stand-in pad, Radiata has a separate helper process **observe** that games really can't see the physical pad any more. If that check can't pass - or can't run - it falls back to shared-input mode instead of guessing, so a hidden pad with no stand-in won't leave you without a working controller."] =
            "**أجهزة Xbox الأصلية عبر Bluetooth** - تعزل كذلك، مع ضمانة إضافية. فقبل تقديم الجهاز البديل، يجعل Radiata عملية مساعدة منفصلة **ترصد** أن الألعاب لم تعد ترى الجهاز المادي فعلًا. وإن لم يجتز ذلك الفحص - أو تعذّر تشغيله - عاد إلى وضع الإدخال المشترك بدل التخمين، فلن يتركك جهاز مخفي بلا بديل من دون جهاز تحكم عامل.",
        ["Isolation needs the drivers (ViGEm + HidHide). Without them every pad runs in shared-input mode (e.g. \"bleed-thru\"). Install from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**. Not sure which mode you're in? Hover the tray icon - see [[input-isolation|Input isolation]]."] =
            "يحتاج العزل إلى برامج التشغيل (ViGEm + HidHide). بدونها يعمل كل جهاز في وضع الإدخال المشترك (أي «تسرب الإدخال»). ثبّتها من **الإعدادات ◂ متقدم ◂ استكشاف الأخطاء وإصلاحها ◂ تثبيت/إصلاح برامج التشغيل**. لست متأكدًا من الوضع الذي أنت فيه؟ مرّر الفأرة فوق أيقونة شريط النظام - راجع [[input-isolation|عزل الإدخال]].",
        ["With the optional isolation drivers installed, Radiata gives games a **virtual controller** - a DualShock 4 for Sony pads, an Xbox 360 pad for Xbox pads or in Xbox Mode - and **cloaks the physical pad** (HidHide) so the game can't see it twice. That's when capture succeeds. Other remappers, existing device access and unsupported input paths can all sabotage full isolation, and without the drivers games keep seeing your controller while a wheel is up. This is how you get bleed-thru."] =
            "مع تثبيت برامج تشغيل العزل الاختيارية، يمنح Radiata الألعاب **جهاز تحكم افتراضيًا** - جهاز DualShock 4 لأجهزة Sony، وجهاز Xbox 360 لأجهزة Xbox أو في وضع Xbox - و**يخفي الجهاز المادي** (HidHide) كي لا تراه اللعبة مرتين. هذا حين ينجح الالتقاط. وقد تفسد أدوات إعادة التعيين الأخرى، والوصول القائم إلى الجهاز، ومسارات الإدخال غير المدعومة العزل الكامل، وبدون برامج التشغيل تظل الألعاب ترى جهاز التحكم أثناء فتح العجلة. وهكذا يحدث تسرب الإدخال.",
        ["**Which mode am I actually in? Hover the tray icon.** It reads **\"Radiata - isolated\"**, or names the reason it isn't (no drivers, Passthru Mode, a pad that can't be cloaked). When capture drops to shared-input mode you also get an on-screen notice."] =
            "**في أي وضع أنا فعلًا؟ مرّر الفأرة فوق أيقونة شريط النظام.** تقرأ **\"Radiata - isolated\"**، أو تسمّي سبب عدم العزل (لا برامج تشغيل، الوضع المباشر، جهاز لا يمكن إخفاؤه). عندما ينخفض الالتقاط إلى وضع الإدخال المشترك تحصل أيضًا على إشعار على الشاشة.",
        ["**Global toggle:** the tray's checkable **Passthru Mode** item, or the checkbox on **Settings ▸ Passthru Mode**. The tab appears only when the isolation drivers are installed."] =
            "**التبديل العام:** عنصر **الوضع المباشر** القابل للتحديد في شريط النظام، أو خانة الاختيار في **الإعدادات ◂ الوضع المباشر**. لا تظهر علامة التبويب إلا عند تثبيت برامج تشغيل العزل.",
        ["**Switch Passthru Mode on or off between play sessions, not mid-game.** Either direction swaps the controller a running game is reading - the virtual pad for your real one, or back - and most games don't go looking for a new controller once they've started, so the game loses input until it's relaunched."] =
            "**بدّل الوضع المباشر بين جلسات اللعب، لا في أثناء اللعب.** ففي كلا الاتجاهين يتبدّل جهاز التحكم الذي تقرأه لعبة قيد التشغيل - الجهاز الافتراضي بجهازك الحقيقي أو العكس - ومعظم الألعاب لا تبحث عن جهاز جديد بعد أن تبدأ، فتفقد اللعبة الإدخال حتى يعاد تشغيلها.",
        ["**Settings tabs:** Left Wheel, Right Wheel, **Customize** (look, feel, sound, triggers, the [[volume-mixer|D-Pad 🡄 🡆]] picker and the [[show-labels|Show labels on]] picker), **Passthru Mode** ([[passthru-mode|Passthru Mode]] - shown when the isolation drivers are installed), **Advanced** (a **Current Controller** readout, [[integrations|Integrations]], [[game-grid-options|Game Grid]] housekeeping, [[accessibility|Accessibility]], **Start with Windows**, and [[system-actions|System tools]] incl. backup & reset and **Quit Radiata**), **Help**, and **About**. Settings auto-save; **Ctrl+S** forces a save."] =
            "**علامات تبويب الإعدادات:** العجلة اليسرى، العجلة اليمنى، **تخصيص** (المظهر والإحساس والصوت والمشغّلات ومنتقي [[volume-mixer|أزرار الاتجاهات 🡄 🡆]] ومنتقي [[show-labels|إظهار التسميات على]])، **الوضع المباشر** ([[passthru-mode|الوضع المباشر]] - يظهر عند تثبيت برامج تشغيل العزل)، **متقدم** (قراءة **وحدة التحكم الحالية**، و[[integrations|التكاملات]]، وتدبير [[game-grid-options|شبكة الألعاب]]، و[[accessibility|إمكانية الوصول]]، و**التشغيل مع Windows**، و[[system-actions|أدوات النظام]] بما فيها النسخ الاحتياطي وإعادة الضبط و**إنهاء Radiata**)، **المساعدة**، و**حول**. تحفظ الإعدادات تلقائيًا؛ **Ctrl+S** يفرض الحفظ.",
        ["**Language** - **Settings ▸ Advanced ▸ Language** sets the language for the whole app. Help language switches immediately. Everything else follows at the next launch."] =
            "**اللغة** - يحدد **الإعدادات ◂ متقدم ◂ اللغة** لغة التطبيق كله. تتبدّل لغة المساعدة فورًا. ويتبعها كل ما عدا ذلك عند التشغيل التالي.",
        ["**Settings ▸ Customize** sets how the wheels look, feel and sound. Every pick applies **live**."] =
            "**الإعدادات ◂ تخصيص** تحدد شكل العجلات وإحساسها وصوتها. وكل اختيار يطبّق **مباشرة**.",
        ["**Material** - the resting-slice look. Eight, in two groups. **Simple** contains solid-color **Flat Light** and **Flat Dark**; **Deluxe** contains the glassy **Pearl** and **Obsidian**, plus four styled looks:"] =
            "**الخامة** - شكل الشريحة في حالة السكون. ثماني خامات في مجموعتين. تحوي **البسيطة** اللونين الصلبين **مسطح فاتح** و**مسطح داكن**؛ وتحوي **الفاخرة** الخامتين الزجاجيتين **لؤلؤ** و**سبج**، إضافة إلى أربعة أنماط مصممة:",
        ["**Kawaii** - pastel wedges, each slice a different hue; firing bursts heart-and-star confetti."] =
            "**كاواي** - أوتاد باستيلية، كل شريحة بدرجة لون مختلفة؛ التنفيذ يفجّر قصاصات قلوب ونجوم.",
        ["**Salvage** - charcoal slices with a rusted-metal texture, fluorescent-light highlighting, and a stamped plate edge."] =
            "**خردة** - شرائح فحمية بنسيج معدن صدئ وإبراز بضوء فلوري وحافة لوح مختوم.",
        ["**Reactor** - dark hollow wedges; the armed slice lights an animated circuit-board of traces and sparks."] =
            "**مفاعل** - أوتاد مجوّفة داكنة؛ الشريحة المسلّحة تضيء لوحة دوائر متحركة من المسارات والشرارات.",
        ["On the Kawaii sound set, arming a slice strikes the next note of a xylophone melody, so scrubbing around the ring plays the song. There are 5 melodies... you might recognize a few of them :)"] =
            "في مجموعة أصوات Kawaii، يعزف تحديد شريحة النغمة التالية من لحن إكسيليفون، فيؤدي التنقل حول الحلقة الأغنية كاملة. وهناك 5 ألحان... وقد تتعرف على بعضها :)",
        ["The first time Radiata sees a new **or changed** package it asks you to confirm before loading anything. Accepting will show it in **Settings ▸ Customize** in a third group, **Custom**, below Simple and Deluxe."] =
            "عند أول مرة يرى فيها Radiata حزمة جديدة **أو متغيّرة** يطلب تأكيدك قبل تحميل أي شيء. وعند القبول تظهر السمة في **الإعدادات ◂ تخصيص** في مجموعة ثالثة، **مخصصة**، أسفل Simple وDeluxe.",
        ["A package is content from whoever wrote it. **Only install themes from a source you trust**. The confirmation prompt returns whenever any file in the package changes."] =
            "الحزمة محتوى من كاتبها. **لا تثبّت إلا السمات من مصدر تثق به**. تعود مطالبة التأكيد كل مرة يتغيّر فيها أي ملف في الحزمة.",
        ["Custom themes render through Radiata's **flat** slice paths with your colors substituted, so they can't reach the built-in styled materials' procedural effects (Kawaii's confetti, Reactor's circuit board)."] =
            "تعرض السمات المخصصة عبر مسارات الشرائح **المسطحة** في Radiata مع إحلال ألوانك، فلا تصل إلى المؤثرات الإجرائية للمواد المنمّقة المضمّنة (قصاصات Kawaii، لوحة دوائر Reactor).",
        ["Set `\"format\": 2` and add any of these optional blocks. Every number is clamped to a safe range, so an extreme value is pulled back rather than rejected."] =
            "اضبط `\"format\": 2` وأضف أيًا من هذه الكتل الاختيارية. يحصر كل رقم ضمن نطاق آمن، فتردّ القيمة المتطرفة بدلًا من رفضها.",
        ["Custom Arcade games - build your own!"] =
            "ألعاب Arcade المخصصة - ابن لعبتك بنفسك!",
        ["`\"howTo\"` - optional, up to **5 lines of 80 characters**, which become the **{triangle}** help card. `{cross}` `{circle}` `{square}` `{triangle}` in a line are replaced with the player's own button glyphs. No lines means no help card."] =
            "`\"howTo\"` - اختياري، حتى **5 أسطر من 80 حرفًا**، تصير بطاقة مساعدة **{triangle}**. تستبدل `{cross}` `{circle}` `{square}` `{triangle}` في السطر برموز أزرار اللاعب. بلا أسطر تعني لا بطاقة مساعدة.",
        ["**`kvSet(\"key\", \"value\")`** and **`kvGet(\"key\")`** are the **only** state that survives closing a game. Contains strings only, keys up to 64 characters, values up to 1024, 4 KB per game in total. Everything else resets on dismiss, so design for it."] =
            "**`kvSet(\"key\", \"value\")`** و**`kvGet(\"key\")`** هما الحالة **الوحيدة** التي تبقى بعد إغلاق اللعبة. تحتوي سلاسل نصية فقط، مفاتيح حتى 64 حرفًا، قيم حتى 1024، و4 KB لكل لعبة إجمالًا. كل شيء آخر يعاد ضبطه عند الإغلاق، فصمّم على هذا الأساس.",
        ["**Wheel ignores opposite stick** - normally **either** thumbstick aims (whichever you tilt further), and either stick's click opens [[edit-mode|edit mode]]. This option has each wheel listen to **one** stick only: the free hand's under Hold, the wheel's own side under Toggle. "] =
            "**تتجاهل العجلة العصا المقابلة** - عادةً تصوّب **أي** من العصاتين (أيّهما أملتها أكثر)، ونقر أي منهما يفتح [[edit-mode|وضع التحرير]]. يجعل هذا الخيار كل عجلة تستمع إلى عصا **واحدة** فقط: عصا اليد الحرة في Hold، وعصا جانب العجلة في Toggle.",
        ["**Reduce motion** - stops decorative movement everywhere. No confetti or sparks, no parallax, no zooming or drifting; wheels fade in in place, edit-mode rearranging is instant, and every hold-to-confirm effect becomes the same steady progress arc. Some Arcade features are automatically disabled. Progress meters, selection highlights and state readouts all stay. This setting respects Windows' own **Animation effects** switch as well."] =
            "**تقليل الحركة** - يوقف الحركة الزخرفية في كل مكان. لا قصاصات ولا شرر، ولا تأثير المنظور، ولا تكبير ولا انزلاق؛ تظهر العجلات بالتلاشي في مكانها، وإعادة الترتيب في وضع التحرير فورية، وتتحول كل مؤثرات الضغط المستمر للتأكيد إلى قوس التقدم الثابت نفسه. تعطّل بعض ميزات الأركيد تلقائيًا. وتبقى مؤشرات التقدم وإبرازات التحديد وقراءات الحالة كلها. ويحترم هذا الإعداد مفتاح **مؤثرات الحركة** في Windows نفسه أيضًا.",
        ["**Always show hub** - off (the default), the wheel's centre hub appears only when it has something to show. On, the hub is always drawn and also shows the controller battery: a steadier centre to read. **Reduce motion** assumes you want this checked as well, but you can set them independently too."] =
            "**إظهار المحور دائمًا** - عند إيقافه (الافتراضي) يظهر محور مركز العجلة فقط حين يكون لديه ما يعرضه. وعند تفعيله يرسم المحور دائمًا ويعرض كذلك بطارية جهاز التحكم: مركز أثبت للقراءة. يفترض **تقليل الحركة** أنك تريد تفعيله أيضًا، لكن يمكنك ضبطهما بشكل مستقل أيضًا.",
        ["**Icons, not Logos** (the default) - only slices showing one of Radiata's built-in icons are labelled. A slice carrying artwork (a game logo, cover art, or a PNG you added) goes unlabelled, assuming the artwork includes or replaces the name."] =
            "**الأيقونات فقط دون الشعارات** (الافتراضي) - لا تسمّى إلا الشرائح التي تعرض إحدى أيقونات Radiata المضمّنة. الشريحة التي تحمل عملًا فنيًا (شعار لعبة، أو غلافًا، أو صورة PNG أضفتها) تبقى بلا تسمية، على افتراض أن العمل الفني يتضمن الاسم أو يحل محله.",
        ["**Slices I Choose** - each slice's own **Show Label** checkbox decides, and it's the only mode in which that checkbox appears in the slice editor (see [[editor-desktop|Slice editor tricks]]). Every slice is created with **Show Label** unchecked, so switching to this mode starts you from an unlabelled wheel: check the few slices you want named."] =
            "**الشرائح التي أختارها** - تقرر خانة **إظهار التسمية** الخاصة بكل شريحة، وهو الوضع الوحيد الذي تظهر فيه تلك الخانة في محرر الشريحة (راجع [[editor-desktop|حيل محرر الشرائح]]). تنشأ كل شريحة و**إظهار التسمية** غير محدد، فالتبديل إلى هذا الوضع يبدأ بك من عجلة بلا تسميات: حدد الشرائح القليلة التي تريد تسميتها.",
        ["Radiata only ever **reads** Playnite's local database. Playnite does not need to be running."] =
            "لا يفعل Radiata سوى **قراءة** قاعدة بيانات Playnite المحلية. ولا يلزم أن يكون Playnite قيد التشغيل.",
        ["**Recover Controller** - the ↻ button beside the **Current Controller** name. This is a soft input reset for a wedged pad. If the pad stays silent afterwards, turn it off (hold its home button until the light goes out), turn it back on, and reconnect it — a controller whose input has frozen at the device only comes back from a power-cycle."] =
            "**Recover Controller** - زر ↻ بجانب اسم **وحدة التحكم الحالية**. هذه إعادة ضبط إدخال خفيفة لجهاز متعطل. إن بقيت وحدة التحكم صامتة بعد ذلك، فأطفئها (اضغط مطولًا على زر الصفحة الرئيسية حتى ينطفئ الضوء)، ثم شغّلها من جديد وأعد توصيلها؛ فوحدة التحكم التي تجمّد إدخالها في الجهاز نفسه لا تعود إلا بإطفائها وتشغيلها.",
        ["**Battery** - beside the **Current Controller** heading, the same reading the wheel hub shows. PlayStation pads report a percentage (in 10% steps); Xbox-compatible pads only report four coarse levels. Nothing shows until the pad has reported a level."] =
            "**Battery** - بجانب عنوان **وحدة التحكم الحالية**، القراءة نفسها التي يعرضها محور العجلة. تعطي أجهزة PlayStation نسبة مئوية (بخطوات 10%)؛ ولا تعطي الأجهزة المتوافقة مع Xbox إلا أربعة مستويات تقريبية. لا يظهر شيء حتى يعطي الجهاز مستوى.",
        ["**Restore Settings…** - replaces settings and art picks only after validation and a successful save, then restarts Radiata. Automatic recovery backups are encrypted for your Windows account; restore them through this command. Exported ZIPs contain readable settings and artwork, but protected credentials may need re-entry on another account or PC."] =
            "**استعادة الإعدادات…** - يستبدل الإعدادات واختيارات الصور بعد التحقق والحفظ الناجح فقط، ثم يعيد تشغيل Radiata. والنسخ الاحتياطية التلقائية للاسترداد مشفّرة لحساب Windows الخاص بك؛ استعدها عبر هذا الأمر. وتحتوي ملفات ZIP المصدّرة على إعدادات وصور قابلة للقراءة، لكن بيانات الاعتماد المحمية قد تحتاج إلى إعادة إدخال على حساب أو حاسوب آخر.",
        ["**Wipe App Data and Reset…** - deletes everything in `%APPDATA%\\Radiata`, including automatic backups. Only backups saved outside that folder survive. Export a settings ZIP first if you want. Radiata restarts after a successful reset."] =
            "**مسح بيانات التطبيق وإعادة التعيين…** - يحذف كل ما في `%APPDATA%\\Radiata`، بما في ذلك النسخ الاحتياطية التلقائية. ولا يبقى إلا ما حفظ خارج ذلك المجلد. صدّر ملف ZIP للإعدادات أولًا إن أردت. ويعيد Radiata تشغيل نفسه بعد نجاح إعادة التعيين.",
        ["**Settings ▸ Advanced** shows a ↻ button beside the Current Controller name. This does a soft input reset - drops and reopens the HID stream - without restarting Radiata."] =
            "**الإعدادات ◂ متقدم** تعرض زر ↻ بجانب اسم وحدة التحكم الحالية. يجري هذا إعادة تعيين خفيفة للإدخال - يغلق تدفق HID ويعيد فتحه - دون إعادة تشغيل Radiata.",
    };
}
