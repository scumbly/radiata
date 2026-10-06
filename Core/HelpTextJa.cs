namespace ControllerWheel;

/// <summary>JAPANESE Help-tab strings. Keys are the EXACT English text authored in
/// <see cref="HelpContent"/> — copy the C# literal across unchanged (escapes included) when adding an
/// entry, and let anything not listed here fall through to English. Run
/// <c>Radiata.exe --check-help-locales</c> after editing HelpContent.cs to see what needs work, and
/// <c>Radiata.exe --dump-help-locale ja</c> to regenerate this file in source order.
/// Conventions (see docs/LOCALIZATION.md): a UI path or label reads in this language, bold, with no English gloss
/// (tools/help-flip.pl applies this; the notice sends support-seekers to English instead); markup (<c>**</c>,
/// <c>`</c>, <c>[[id|label]]</c>, <c>[label](url)</c>) and <c>{tokens}</c> are
/// preserved verbatim, a cross-link's topic id is never translated, and product names stay as they are.
/// <para>Entry ORDER follows <c>HelpLocalization.SourceStrings()</c> — the notice, the Help-pane chrome,
/// the category names, then each topic's title/keywords/blocks, then the figure labels.</para></summary>
internal static class HelpTextJa
{
    internal static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {

        // ── notice ──
        [HelpLocalization.NoticeKey] =
            "**このヘルプページと Radiata のインターフェイスは AI 言語モデルによって翻訳されました。** 翻訳には不正確または不完全な箇所があるかもしれません。開発者は翻訳文の誤りについて責任を負いません。英語版が正式なものです。サポートへの問い合わせには英語でのみ回答でき、他の言語のメッセージには返信しません。サポートの回答ではボタン、タブ、設定を英語の表記で呼びます。回答に沿って操作するときは、Radiata を一時的に英語に切り替えてください。",

        // ── chrome ──
        ["Contents"] =
            "目次",
        ["No topics match."] =
            "該当する項目はありません。",
        ["Language"] =
            "言語",
        ["Search help topics"] =
            "ヘルプの項目を検索",
        ["Help topics language"] =
            "ヘルプトピックの言語",
        ["Back to where you were"] =
            "前の場所に戻る",
        ["Open this Help topic"] =
            "このヘルプ項目を開く",

        // ── category ──
        ["Welcome"] =
            "はじめに",
        ["Getting Around"] =
            "基本操作",
        ["Editing Wheels"] =
            "ホイールの編集",
        ["Game Grid"] =
            "ゲームグリッド（ゲーム一覧）",
        ["Actions"] =
            "アクション",
        ["Controllers & Isolation"] =
            "コントローラーと入力の分離",
        ["Tray & Settings"] =
            "通知領域と設定",
        ["Workshop"] =
            "ワークショップ",
        ["Troubleshooting"] =
            "トラブルシューティング",

        // ── topic:intro ──
        ["What is Radiata?"] =
            "Radiata とは",
        ["intro welcome about purpose design overview couch overlay start here"] =
            "はじめに 概要 目的 設計 ソファ オーバーレイ 最初に読む",
        ["**Configurable:** each slice's action, icon, color and position, the summon chord, the look, the sounds. Edit at the desk in Settings, or from the couch in the in-wheel editor."] =
            "**設定できるもの:** 各スライスのアクション、アイコン、色、位置、呼び出しコード、見た目、サウンド。机の上では設定から、ソファからはホイール内エディターで編集できます。",

        // ── topic:installing ──
        ["Installing Radiata"] =
            "Radiata のインストール",
        ["install installing installer setup download smartscreen windows protected your pc unknown publisher unsigned signature certificate antivirus false positive virus admin administrator uac elevation drivers vigem hidhide requirements windows 10 11 x64 arm browser blocked keep discard first run update uninstall remove"] =
            "インストール インストーラー セットアップ ダウンロード smartscreen windows によって pc が保護されました 発行元不明 未署名 署名 証明書 ウイルス対策 誤検知 ウイルス 管理者 uac 昇格 ドライバー vigem hidhide 要件 windows 10 11 x64 arm ブラウザー ブロック 保持 破棄 初回起動 更新 アンインストール 削除",
        ["What you need"] =
            "必要なもの",
        ["**Windows 10 or 11, 64-bit (x64)** on an Intel or AMD PC. Windows on ARM isn't supported."] =
            "Intel または AMD の PC 上の **Windows 10 または 11、64 ビット (x64)**。Windows on ARM には対応していません。",
        ["A supported controller - see [[supported-controllers|Supported controllers]]."] =
            "対応コントローラー。[[supported-controllers|対応コントローラー]] を参照してください。",
        ["\"Windows protected your PC\""] =
            "「Windows によって PC が保護されました」",
        ["**In the browser:** if the download itself is blocked, keep it (Chrome and Edge: the **⋯** menu beside the download ▸ **Keep** ▸ **Show more** ▸ **Keep anyway**)."] =
            "**ブラウザーで:** ダウンロード自体がブロックされた場合は保持してください（Chrome と Edge: ダウンロードの横の **⋯** メニュー ▸ **保持** ▸ **詳細表示** ▸ **保持する**）。",
        ["**At the SmartScreen box:** click **More info**, then the **Run anyway** button that appears below it. If there's no **More info** link you might be seeing your browser's warning instead. See the previous step."] =
            "**SmartScreen のボックスで:** **詳細情報**をクリックし、その下に現れる**実行**ボタンを押します。**詳細情報**のリンクが見当たらない場合は、ブラウザー側の警告を見ている可能性があります。前の手順をご覧ください。",
        ["What the installer does"] =
            "インストーラーが行うこと",
        ["First run"] =
            "初回起動",
        ["Setup opens by itself and walks you through the drivers, a controller check, the look, cover art, and a set of starter wheels. Re-run it any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Run First-Run Setup…** - see [[system-actions|System tools]]."] =
            "セットアップが自動的に開き、ドライバー、コントローラーの確認、外観、カバーアート、初期ホイールのセットを順に案内します。**設定 ▸ 詳細 ▸ トラブルシューティング ▸ 初回セットアップを実行…** からいつでも再実行できます。[[system-actions|システムツール]] を参照してください。",
        ["Updating and uninstalling"] =
            "更新とアンインストール",

        // ── topic:opening-a-wheel ──
        ["Opening a wheel"] =
            "ホイールを開く",
        ["summon invoke trigger chord fn bumper touchpad swipe hold toggle swap sides activation flip left right"] =
            "呼び出し 起動 トリガー コード 組み合わせ fn バンパー タッチパッド スワイプ ホールド トグル 左右入れ替え 有効化 切り替え 左 右",
        ["Hold the bumper/trigger; the second button is a **tap** that brings the wheel up."] =
            "バンパーまたはトリガーを押し続け、2 つ目のボタンを**タップ**するとホイールが表示されます。",
        ["**Fn / L4/R4 and squeeze chords** (Bumper+Trigger, +Home, +Select/Start) - the hand you squeeze opens the **opposite** wheel, so the free hand aims (R1+R2 → Left; L1+L2 → Right). For Select/Start, either button works."] =
            "**Fn / L4/R4 と握り込みコード**（バンパー+トリガー、+Home、+Select/Start）— 握った側の手が**反対側**のホイールを開くので、空いた手で照準できます（R1+R2 → 左、L1+L2 → 右）。Select/Start はどちらのボタンでも動作します。",
        ["**L3/R3** and **D-Pad L/R** - the clicked stick or the direction determines which wheel; either bumper/trigger is the hold."] =
            "**L3/R3** と**十字キーの左右** — クリックしたスティック、または方向がどちらのホイールかを決めます。長押しはバンパーでもトリガーでもかまいません。",
        ["**Touchpad swipe** - in from the left edge → Left wheel, right edge → Right."] =
            "**タッチパッドのスワイプ**: 左端から内側へ → 左ホイール、右端から → 右ホイール。",
        ["**Swap left/right** ([[accessibility|Accessibility setting]]) reverses all of these."] =
            "**左右を入れ替え**（左右入れ替え、[[accessibility|アクセシビリティ設定]]）を使うと、以上のすべてが反転します。",
        ["**Activation** is **Hold** (up while held; release fires) or **Toggle** (trigger opens; **{cross} confirms, {circle} cancels**; re-trigger dismisses) - set it in [[accessibility|Accessibility]]. Touchpad swipe is always toggle-style."] =
            "**有効化方式**は **長押し**（押している間だけ表示され、離すと実行）または **Toggle**（操作で開き、**{cross} で確定、{circle} で取り消し**、もう一度操作すると閉じる）のいずれかで、[[accessibility|アクセシビリティ]]で設定します。タッチパッドのスワイプは常に Toggle 方式です。",

        // ── topic:picking-an-action ──
        ["Aiming & firing"] =
            "照準と実行",
        ["aim arm fire cancel release deadzone sticky esc escape keyboard hub center state toggle mute hdr configure guard confirm dwell sleep reboot shutdown"] =
            "照準 選択 実行 取り消し 離す 無反応領域 esc キーボード 中央 状態 切り替え ミュート hdr 設定 確認 長押し スリープ 再起動 シャットダウン",
        ["Center hub"] =
            "ホイール中央の表示",
        ["Arming a **toggle** slice (mic/volume mute, HDR, process toggle) shows its **current state** before you fire, e.g. `Mute Mic / Unmuted`. After firing, the hub shows the new state."] =
            "**切り替え**タイプのスライス（マイクや音量のミュート、HDR、プロセスの切り替え）を選択すると、実行前に**現在の状態**が表示されます。たとえば `Mute Mic / Unmuted` のように表示されます。実行後は中央に新しい状態が表示されます。",
        ["Hold-to-confirm slices"] =
            "長押しで確認する扇形項目",

        // ── topic:wheel-open-extras ──
        ["While a wheel is open"] =
            "ホイールが開いている間の操作",
        ["volume dpad scrub repeat mic microphone alt-tab window switcher desktop song enable disable chord toggle wheels off keyboard arrow esc"] =
            "音量 方向キー 連続 マイク alt-tab ウィンドウ切り替え デスクトップ 曲 有効 無効 切り替え キーボード 矢印 esc",
        ["**Enable / disable the wheels** with the \"both sides\" of your invocation chord, pressed **together**:"] =
            "呼び出し用の組み合わせの「両側」を**同時に**押すと、**ホイールの有効／無効を切り替え**られます。",
        ["**Bumper/Trigger + D-Pad** is directional: **D-Pad Up = enable**, **D-Pad Down = disable**."] =
            "**Bumper/Trigger + D-Pad** は方向で決まります。**D-Pad 上 = 有効**、**D-Pad 下 = 無効** です。",

        // ── topic:edit-mode ──
        ["Edit mode (in-wheel, controller-only)"] =
            "編集モード（ホイール内、コントローラーのみで操作）",
        ["edit stick click move add delete reorder undo redo picker installed game full capacity 12 limit thickness"] =
            "編集 スティック 押し込み 移動 追加 削除 並べ替え 元に戻す やり直し 選択 インストール済みゲーム 上限 12 太さ",
        ["**Add:** **{triangle}** opens the category→type **Add picker** ({cross} drills in, {circle} backs out). **Installed Game** opens the [[game-grid|Game Grid]] to pick a game, then returns to edit carrying the slice. Other types drop a slice immediately; free-text types (raw URL / keypress) land as placeholders you finish in Settings."] =
            "**追加:** **{triangle}** でカテゴリー→種類の **追加ピッカー** が開きます（{cross} で進み、{circle} で戻ります）。**インストール済みゲーム** を選ぶと [[game-grid|ゲームグリッド]] が開き、ゲームを選ぶとスライスを持って編集画面に戻ります。ほかの種類はすぐにスライスが追加され、自由入力の種類（URL 直接指定やキー入力）はプレースホルダーとして追加されるので、設定で仕上げてください。",

        // ── topic:empty-wheel ──
        ["Single-wheel mode"] =
            "ホイールを 1 つだけ使う",
        ["empty disabled free gesture resurrect rebuild"] =
            "空 無効 操作を空ける 復元 作り直す",
        ["Emptying one wheel turns its side off: the chord that used to open it passes through to the game untouched."] =
            "片方のホイールを空にすると、その側は無効になります。以前そのホイールを開いていた組み合わせは、そのままゲームに渡ります。",
        ["To bring it back: **invoke it (hold the gesture) and click the aiming stick (L3/R3)**. The wheel opens centered, straight into the Add picker. (Toggle-style gestures have no hold, so they allow the stick click for a few seconds after the invoke.)"] =
            "戻すには、**ホイールを呼び出し（ジェスチャーを長押しし）、照準スティック（L3/R3）をクリック**します。ホイールは中央に寄った状態で、そのまま追加メニューが開きます。（切り替え式のジェスチャーには長押しがないため、呼び出しから数秒間はスティックのクリックを受け付けます。）",

        // ── topic:arcade-direct-launch ──
        ["Arcade direct-launch (a wheel that is just the Arcade)"] =
            "アーケードの直接起動（アーケードだけのホイール）",
        ["arcade direct launch shortcut lone only one single slice launcher skip wheel straight cabinets instant gesture dedicated side"] =
            "アーケード 直接 起動 ショートカット 単一 スライス ランチャー スキップ ホイール 直行 筐体 即座 ジェスチャー 専用 サイド",
        ["**The arcade opens where that wheel would have been** - the left or right quarter of the screen, the same spot the wheel uses. **{circle}** closes it as usual."] =
            "**アーケードはそのホイールがあったはずの場所に開きます。** 画面の左または右の 4 分の 1、ホイールと同じ位置です。**{circle}** でいつものように閉じられます。",

        // ── topic:editor-desktop ──
        ["Slice editor tricks (Settings, mouse & keyboard)"] =
            "項目エディターの便利な操作（設定、マウスとキーボード）",
        ["drag drop exe lnk shortcut launch slice reorder icon color color ctrl+s save autosave draft revert logo arrows cycle fetch steamgriddb"] =
            "ドラッグ ドロップ exe lnk ショートカット 起動 項目 並べ替え アイコン 色 ctrl+s 保存 自動保存 下書き 元に戻す ロゴ 矢印 切り替え 取得 steamgriddb",
        ["**Drag a slice within the list** to reorder."] =
            "**一覧の中でセクターをドラッグ**すると並び順を変更できます。",
        ["**Set a slice's icon and color** in the Icon & Color panel below the label."] =
            "**セクターのアイコンと色**は、ラベルの下にある Icon & Color パネルで設定します。",
        ["**Show Label** toggles the slice's text on the wheel. It appears only when [[show-labels|Show labels on]] is set to **Slices I Choose**."] =
            "**ラベルを表示** は、そのスライスの文字をホイールに表示するかどうかを切り替えます。このチェックボックスは [[show-labels|ラベルを表示するスライス]] が **選んだスライスのみ** のときだけ表示されます。",
        ["Artwork on a slice"] =
            "スライスの画像",
        ["**A slice's action type locks once you Save it.** To change it, delete the slice and add a new one."] =
            "**スライスのアクションタイプは保存すると固定されます。** 変更するには、そのスライスを削除して新しく追加してください。",

        // ── topic:game-grid ──
        ["Game Grid basics"] =
            "ゲームグリッドの基本",
        ["game grid browser launch navigate filter storefront chips footer add wheel pick mode installed assign favorite"] =
            "game grid ゲーム一覧 ブラウザー 起動 移動 絞り込み ストア チップ フッター 追加 ホイール 選択モード インストール済み 割り当て お気に入り",
        ["The Game Grid is a controller-scrollable view of every installed game across your storefronts, sorted **most-recently-launched first**, favorites pinned on top. The button controls are spelled out along the bottom of the grid."] =
            "ゲームグリッドは、各ストアにインストール済みのすべてのゲームをコントローラーでスクロールできる一覧です。**最近起動した順**に並び、お気に入りが上部に固定されます。ボタン操作はグリッド下部に明示されています。",
        ["**D-Pad / arrow keys** or the **left stick** move the selection."] =
            "**D-Pad ／方向キー**または**左スティック**で選択を移動します。",
        ["**{cross} / Enter** or a mouse double-click - launch the selected game. **{circle} / Esc** - close the grid."] =
            "**{cross} / Enter** またはマウスのダブルクリック: 選択中のゲームを起動します。**{circle} / Esc**: 一覧を閉じます。",
        ["**{triangle}** - **favorite** the selected game. Favorites sit in their own row of larger tiles at the top of every view. Press again to un-favorite."] =
            "**{triangle}** — 選択中のゲームを**お気に入り**にします。お気に入りはどの表示でも、上部の大きめのタイルの専用行に並びます。もう一度押すと解除されます。",
        ["**Hold {square}** - **hide the game from the grid**. Bring hidden games back with **Settings ▸ Advanced ▸ Game Grid ▸ Reset Hidden Games**. The same hold on a storefront's **Open <store>** card hides that whole store - see [[storefronts|Hiding a storefront]]."] =
            "**{square} を押し続ける**: **そのゲームを一覧から隠します**。隠したゲームは **設定 ▸ 詳細 ▸ ゲームグリッド ▸ 非表示のゲームをリセット** で戻せます。ストアの **Open <ストア>** カードで同じ操作を行うと、そのストア全体を隠せます。[[storefronts|ストアを隠す]] をご覧ください。",
        ["**L1 / R1** (or **PgUp / PgDn**) - cycle the storefront filter. A chip appears for each store you have installed."] =
            "**L1 / R1**（または **PgUp / PgDn**）: ストアの絞り込みを切り替えます。インストール済みのストアごとにチップが表示されます。",
        ["**Select** and **Start** - cycle the selected game's **cover** and **logo**; see [[cover-art|Cover art & logos]]."] =
            "**選択** と **Start**: 選択中のゲームの**カバー**と**ロゴ**を順に切り替えます。[[cover-art|カバーアートとロゴ]] をご覧ください。",
        ["Add a game to a wheel"] =
            "ホイールにゲームを追加する",
        ["Add games from the **in-wheel editor**: open a wheel → click the aiming stick to enter [[edit-mode|Edit]] → **{triangle} Add → Installed Game**, which opens the grid in **pick mode**. **{cross}** picks the highlighted game and carries it into the editor ({circle} cancels)."] =
            "ゲームの追加は**ホイール内エディター**から行います。ホイールを開き、狙いに使うスティックを押し込んで [[edit-mode|編集]] に入り、**{triangle} 追加 → インストール済みゲーム** を選ぶと、グリッドが**選択モード**で開きます。**{cross}** で強調表示中のゲームを選び、そのままエディターに持ち帰ります（{circle} で取り消し）。",

        // ── topic:cover-art ──
        ["Cover art & logos"] =
            "カバー画像とロゴ",
        ["cover art logo cycle select start share options steamgriddb sgdb dots spinner flat colors"] =
            "カバー画像 ロゴ 切り替え select start share options steamgriddb sgdb ドット 進行表示 単色",
        ["**Start** (Options/Menu) - **cycle the logo overlay** and save it: default logo → up to 2 SteamGridDB alternates → **off** (raw cover) → wrap. A game with no logo art uses its centered title text."] =
            "**Start**（Options/Menu）: **重ねて表示するロゴを順に切り替えて**保存します。既定のロゴ → SteamGridDB の代替ロゴ最大 2 点 → **オフ**（カバーのみ）→ 最初に戻る。ロゴ画像がないゲームは中央にタイトル文字を表示します。",
        ["**Art is fetched once and kept on disk**, so the grid opens instantly and offline after that. A big library fills in over the first few seconds of browsing. Reopen the grid and the stragglers should be there."] =
            "**アートは一度取得するとディスクに保存されます。** そのため 2 回目以降は一覧が瞬時に、しかもオフラインでも開きます。大きなライブラリは、閲覧を始めてから数秒かけて埋まっていきます。一覧を開き直せば、残っていた分もそろっているはずです。",
        ["**A game with no art found is re-checked every couple of weeks** on its own, since art gets added over time. To recheck now, use **Retry Missing Game Art** ([[game-grid-options|Advanced ▸ Game Grid]])."] =
            "**アートが見つからなかったゲームは数週間ごとに自動で再確認されます。** アートは時間とともに追加されていくためです。今すぐ確認するには **不足しているゲームアートを再取得** （[[game-grid-options|詳細 ▸ ゲームグリッド]]）を使ってください。",

        // ── topic:action-types ──
        ["Action types"] =
            "アクションの種類",
        ["actions advanced launch keypress key combo volume audio display hdr sleep discord voice text chat url settings xbox mode obs mixer game bar windows"] =
            "アクション 詳細 起動 キー入力 キーの組み合わせ 音量 オーディオ ディスプレイ hdr スリープ discord 音声 テキストチャット url 設定 xbox モード obs ミキサー game bar windows",
        ["Each slice runs one action, grouped by the editor's categories:"] =
            "各扇形項目は 1 つのアクションを実行します。エディターでは次のカテゴリーに分類されています。",
        ["**Games & Apps** - installed game (direct launch), the Game Grid, storefront launcher (big-picture), launch/focus an app, **Exit Current App** (closes the frontmost app), and **Game Bar** (open, screenshot, start/stop recording, record the last 30 s, toggle mic - via Windows' Xbox Game Bar)."] =
            "**ゲームとアプリ** — インストール済みゲーム（直接起動）、ゲームグリッド、ストアのランチャー（ビッグピクチャー）、アプリの起動やフォーカス、**現在のアプリを終了**（最前面のアプリを閉じます）、そして **Game Bar**（起動、スクリーンショット、録画の開始と停止、直近 30 秒の録画、マイクの切り替え）— Windows の Xbox Game Bar を利用します。",
        ["**Chat & Streaming** - Discord (launch, join/leave a voice channel, deafen), Steam Chat (open chat) - see [[steam-xbox-voice|Steam voice chat]] - plus **Mic Mute** (the one Windows mic mute, offered in both groups and under System ▸ Audio), [[text-chat|Text Chat]], and **OBS Studio** ([[obs-studio|streaming, recording, replay, scenes, source mute]])."] =
            "**チャットと配信**: Discord（起動、ボイスチャンネルへの参加/退出、スピーカーミュート）、Steam チャット（チャットを開く）。[[steam-xbox-voice|Steam のボイスチャット]] を参照してください。さらに **マイクミュート**（Windows で唯一のマイクミュートで、どちらのグループでも システム ▸ オーディオ でも使えます）、[[text-chat|テキストチャット]]、**OBS Studio**（[[obs-studio|配信、録画、リプレイ、シーン、ソースのミュート]]）があります。",
        ["**System** - [[controller-mode|controller mode]] (**Xbox** / **DualShock**); audio (switch output, mute, mic mute, set volume, play/pause, next/previous); display (**Toggle Extend/Clone** and HDR toggle); **Windows** (**Show Desktop** - fire again to put the windows back - and **Empty Recycle Bin**); power (sleep/hibernate/reboot/shut down/log out/lock, and **Power Plan**, which flips between two plans you pick)."] =
            "**システム**（システム）: [[controller-mode|コントローラーモード]]（**Xbox** / **DualShock**）、オーディオ（出力の切り替え、ミュート、マイクミュート、音量指定、再生／一時停止、次へ／前へ）、ディスプレイ（**拡張/複製を切り替え** と HDR の切り替え）、**Windows**（**デスクトップを表示**。もう一度実行するとウィンドウが戻ります。および **ごみ箱を空にする**）、電源（スリープ、休止状態、再起動、シャットダウン、サインアウト、ロック、および指定した 2 つのプランを切り替える **電源プラン**）。",
        ["**Reboot** has a **Log In after Reboot** checkbox: checked (the default), Windows signs you back in where policy allows. Unchecked is a traditional restart, which can land at the sign-in screen."] =
            "**再起動**には**再起動後にサインインする**のチェックボックスがあります。チェックあり（既定）ならポリシーが許す範囲で Windows が再びサインインします。チェックなしは従来どおりの再起動で、サインイン画面で止まることがあります。",
        ["**Custom** - [[open-uri|Open URI]] and [[key-combo|Key Combo]] (send a keyboard shortcut like `Win+D` or `PlayPause`)."] =
            "**カスタム**（カスタム）: [[open-uri|URI を開く]] と [[key-combo|キーの組み合わせ]]（`Win+D` や `PlayPause` のようなキーボードショートカットを送ります）。",
        ["**Radiata** - the Game Grid, open Settings, and disable wheels. [[passthru-mode|Passthru Mode]] is not a slice: turn it on or off from the tray or Settings ▸ Passthru Mode."] =
            "**Radiata** - ゲームグリッド、設定を開く、ホイールの無効化。[[passthru-mode|パススルーモード]] はスライスではなく、トレイまたは設定 ▸ パススルーモード からオン・オフを切り替えます。",
        ["A slice missing a required value arms as **\"Configure in Settings\"**. Firing it opens that slice's editor."] =
            "必須の値が欠けているセクターは **\"設定で構成\"** と表示されます。実行するとそのセクターのエディターが開きます。",

        // ── topic:arcade ──
        ["Arcade"] =
            "アーケード",
        ["arcade game games minigame mini-game kabloom connate petal pop twist breakout paddle brick pentagon square hexagon octagon smash win minesweeper merge bee flower bomb picker play waiting loading queue lobby kill time score"] =
            "アーケード ゲーム ミニゲーム kabloom connate petal pop ひねり ブロック崩し パドル ブロック 五角形 四角形 六角形 八角形 スマッシュ 勝利 マインスイーパ 合体 マージ ハチ 花 爆弾 ピッカー 遊ぶ 待ち時間 ロード中 待機列 ロビー 暇つぶし スコア",
        ["**{cross}** acts, **{square}** is each game's second action, and the **left stick** aims. The **D-Pad** drives the menus. **{triangle} explains the game you're in**, and **START pauses** with **Resume**, **How to play**, **Reset**, and that game's own settings."] =
            "**{cross}** が主操作、**{square}** が各ゲームの 2 つ目の操作、**左スティック**で照準します。メニューは **D-Pad** で操作します。**{triangle} は今遊んでいるゲームの説明を表示**し、**START で一時停止**して **再開**、**遊び方**、**リセット**、そのゲーム固有の設定を表示します。",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]], [[arcade-petalpop|Petalpop]] and [[arcade-internode|Internode]]."] =
            "**各ゲームに専用のページがあります**: [[arcade-kabloom|Kabloom]]、[[arcade-connate|Connate]]、[[arcade-petalpop|Petalpop]]、[[arcade-internode|Internode]]。",
        ["**Each game has its own page** - [[arcade-kabloom|Kabloom]], [[arcade-connate|Connate]] and [[arcade-petalpop|Petalpop]]."] =
            "**各ゲームに専用のページがあります** — [[arcade-kabloom|Kabloom]]、[[arcade-connate|Connate]]、[[arcade-petalpop|Petalpop]]。",
        ["**You can write your own games** and drop them in - see [[custom-arcade-games|Custom Arcade games]]."] =
            "**自分でゲームを作って**追加することもできます。[[custom-arcade-games|カスタムアーケードゲーム]] を参照してください。",

        // ── topic:arcade-kabloom ──
        ["Arcade: Kabloom"] =
            "アーケード: Kabloom",
        ["kabloom arcade minesweeper petal petals flower tile disc bee flag mark question mark cursor reveal solvable no guessing guess solver proof certified baked stacked gem diamond level campaign"] =
            "kabloom アーケード マインスイーパ 花びら 花 タイル 円盤 ハチ 旗 マーク はてなマーク カーソル 開く 解ける 運任せなし 推測 ソルバー 証明 保証済み 事前生成 積み重ね 宝石 ダイヤ レベル キャンペーン",
        ["Part of the [[arcade|Arcade]]."] =
            "[[arcade|アーケード]] の一部です。",

        // ── topic:arcade-connate ──
        ["Arcade: Connate"] =
            "アーケード: Connate",
        ["connate arcade merge merging numbers number cluster pile rim ring colors colours families bomb charge fire lob orb doubling"] =
            "connate アーケード 合体 マージ 数字 数 かたまり 山 縁 リング 色 系統 爆弾 チャージ 発射 放る オーブ 倍",

        // ── topic:arcade-petalpop ──
        ["Arcade: Petalpop"] =
            "アーケード: Petalpop",
        ["petal pop petalpop arcade paddle paddles breakout brick bricks ball flower core gold split multiball smash slingshot spring rail square pentagon hexagon heptagon octagon lives win 5-8"] =
            "petal pop petalpop アーケード パドル ブロック崩し ブロック ボール 花 中心 金色 分裂 マルチボール スマッシュ パチンコ ばね レール 四角形 五角形 六角形 七角形 八角形 残機 勝利 5-8",

        // ── topic:arcade-internode ──
        ["Arcade: Internode"] =
            "アーケード: Internode",
        ["internode arcade half-pipe halfpipe pipe bike runner token tokens mine mines jump hop gate gold gate quota checkpoint stage bank score camera roll rim wall swing gap break fall"] =
            "internode アーケード ハーフパイプ パイプ バイク ランナー トークン 地雷 ジャンプ ゲート ゴールドゲート ノルマ チェックポイント ステージ 預ける スコア カメラ ロール 縁 壁 スイング 隙間 割れ目 落下",

        // ── topic:app-slices ──
        ["App & launcher slices"] =
            "アプリとランチャーの項目",
        ["launch focus toggle kill process name override executable path storefront big picture installed apps store uwp"] =
            "起動 前面 切り替え 終了 プロセス名 実行ファイルのパス ストア 大画面 インストール済みアプリ microsoft store uwp",
        ["**Behavior** - **Run** launches the app, or focuses it if it's already running. **Toggle** launches the app or requests a normal close, like clicking its ✕. The hub reports **Close requested**, not a confirmed exit: unsaved-work prompts stay open until you answer them, apps may refuse to close or keep running in the tray, and Toggle leaves windowless processes running."] =
            "**動作** — **実行**はアプリを起動し、すでに起動していればフォーカスします。**切り替え**はアプリを起動するか、✕ をクリックしたときと同じ通常の終了を要求します。中央には**終了を要求**と表示され、終了の確定ではありません。未保存の作業を尋ねるダイアログは答えるまで開いたままですし、アプリが終了を拒んだりトレイに残り続けたりすることもあり、ウィンドウを持たないプロセスは切り替えでも動き続けます。",
        ["**Storefront** (launcher slices) - opens the store's big-picture/fullscreen mode. Only Steam and Playnite have a true one; the Xbox app is maximized; the rest just open. A storefront is offered only when its launcher app is actually installed."] =
            "**ストア**（ランチャーのセクター）: ストアのビッグピクチャー／全画面モードを開きます。本来の全画面モードを備えているのは Steam と Playnite だけで、Xbox アプリは最大化され、その他は通常どおり開きます。ストアは、そのランチャーアプリが実際にインストールされている場合にのみ表示されます。",

        // ── topic:controller-mode ──
        ["Controller mode (Xbox / DualShock)"] =
            "コントローラーモード（Xbox / DualShock）",
        ["controller mode xbox dualshock emulation virtual pad game pass xinput button prompts glyphs playstation switch pad type unsupported controller"] =
            "コントローラーモード xbox dualshock エミュレーション 仮想コントローラー game pass xinput ボタン表示 記号 playstation パッド種別 切り替え 非対応コントローラー",
        ["Arming the slice shows **On** or **Off** in the hub, so you can check which mode you're in without changing it."] =
            "セクターを選択するとハブに **オン** または **オフ** が表示されるため、変更せずに現在のモードを確認できます。",

        // ── topic:switch-audio ──
        ["Switch Audio Output"] =
            "オーディオ出力を切り替え",
        ["switch audio output device mic microphone speakers headset cycle default endpoint"] =
            "切り替え 音声 出力 デバイス マイク スピーカー ヘッドセット 順次切り替え 既定",
        ["**Switch Audio Output** changes the Windows default audio device(s). Both fields match by **partial name**, case-insensitively - \"Speakers\" matches \"Speakers (Realtek…)\". The **▾ button** beside each field lists your connected devices, and a new slice starts pre-filled with your current defaults."] =
            "**オーディオ出力を切り替え** は Windows の既定のオーディオデバイスを切り替えます。どちらの欄も**名前の一部**で大文字小文字を区別せずに一致します。\"Speakers\" は \"Speakers (Realtek…)\" に一致します。各欄の横の **▾ ボタン**で接続中のデバイスを一覧表示でき、新しいセクターには現在の既定デバイスがあらかじめ入力されます。",
        ["**Output Device** - the playback device to switch to. **Blank = cycle** through your outputs on each fire (unless a Mic Device is set, which leaves the output alone)."] =
            "**出力デバイス**（出力デバイス）: 切り替え先の再生デバイスです。**空欄にすると**実行のたびに出力を**順に切り替えます**（Mic Device が設定されている場合は出力に手を触れません）。",
        ["**Mic Device** - the recording device to switch to. **Blank = leave the mic unchanged.**"] =
            "**マイクデバイス**（マイクデバイス）: 切り替え先の録音デバイスです。**空欄の場合、マイクは変更されません。**",
        ["Set both fields to switch output + mic in one slice - \"TV + no mic\", \"Headset + headset mic\". Firing shows the device switched to in the hub."] =
            "両方の欄を埋めると、1 つのスライスで出力とマイクをまとめて切り替えられます。「テレビ、マイクなし」「ヘッドセットとヘッドセットのマイク」のように使えます。実行すると、切り替え先のデバイスが中央に表示されます。",

        // ── topic:open-uri ──
        ["Open URI"] =
            "URI を開く",
        ["uri url link scheme https steam discord ms-settings deep link protocol"] =
            "uri url リンク スキーム https steam discord ms-settings ディープリンク プロトコル",
        ["An **Open URI** slice hands its value to Windows to open with whatever handles that scheme. That covers a lot more than web links:"] =
            "**URI を開く**スライスは、その値を Windows に渡し、そのスキームを扱うアプリで開かせます。ウェブのリンクだけにとどまりません。",
        ["**Web URLs** - `https://twitch.tv/yourchannel` opens in your default browser."] =
            "**Web アドレス**: `https://twitch.tv/yourchannel` は既定のブラウザーで開きます。",
        ["**App deep links** - `steam://open/bigpicture`, `discord://discord.com/channels/…`, `com.epicgames.launcher://apps/…`, `spotify:playlist:…` - anything an installed app registers a protocol for."] =
            "**アプリのディープリンク**: `steam://open/bigpicture`、`discord://discord.com/channels/…`、`com.epicgames.launcher://apps/…`、`spotify:playlist:…` など、インストール済みアプリがプロトコルを登録しているものすべて。",
        ["**Windows pages** - `ms-settings:display` opens that Settings page."] =
            "**Windows の設定ページ**: `ms-settings:display` はその設定ページを開きます。",
        ["The value must be a complete, absolute URI. A bare `twitch.tv/...` won't launch - include the `https://`."] =
            "値は完全な絶対 URI である必要があります。`twitch.tv/...` だけでは起動しません。`https://` を含めてください。",
        ["Game-launch URIs (`steam://rungameid/…`) work here too, but the **Installed Game** slice type builds them for you, which is easier."] =
            "ゲーム起動用の URI（`steam://rungameid/…`）もここで使えますが、**インストール済みゲーム** セクター種別が自動で組み立ててくれるため、そちらの方が簡単です。",

        // ── topic:key-combo ──
        ["Key Combo (send a keyboard shortcut)"] =
            "Key Combo（キーボードショートカットを送る）",
        ["key combo keypress keyboard shortcut hotkey send keys format grammar win ctrl alt shift del delete media volup voldown mute playpause next prev navy blue modifier plus"] =
            "key combo キー入力 キーボード ショートカット ホットキー 送信 書式 文法 win ctrl alt shift del delete メディアキー volup voldown ミュート playpause 次へ 前へ 紺色 修飾キー プラス",
        ["A **Key Combo** slice (Custom) presses a keyboard shortcut for you. Write it as key names joined with **`+`** - `Win+D`, `Ctrl+Shift+Esc`, `Alt+F4` - or a single key like `PlayPause`. The **last** name is the key pressed; everything before it is a modifier held around it. Names aren't case-sensitive."] =
            "**キーの組み合わせ**スライス（カスタム）は、キーボードショートカットを代わりに押します。キー名を **`+`** でつないで書きます（`Win+D`、`Ctrl+Shift+Esc`、`Alt+F4`）。`PlayPause` のような単独のキーでもかまいません。**最後**の名前が押されるキーで、その前にあるものはその間押し続ける修飾キーです。大文字と小文字は区別されません。",
        ["In a combo, the last name is the key that actually gets pressed and everything before it is a modifier held down around it."] =
            "組み合わせの中で、最後の名前が実際に押されるキーであり、その前にあるものはすべて、その間押し続けられる修飾キーです。",
        ["**Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`."] =
            "**修飾キー:** `Ctrl`、`Alt`、`Shift`、`Win`。",
        ["**Keys:** letters and digits; `F1`-`F12`; `Enter`, `Esc`, `Tab`, `Space`, `Backspace`, `Del`, `Insert`; `Home`, `End`, `PgUp`, `PgDn`; the arrows `Up` `Down` `Left` `Right`; `PrintScreen`, `Pause`; `Backtick` and `Slash`; and the **media keys** `VolUp`, `VolDown`, `Mute`, `PlayPause`, `Next`, `Prev`, `Stop` - media keys work on their own, no modifier needed."] =
            "**キー:** 英字と数字、`F1`-`F12`、`Enter`、`Esc`、`Tab`、`Space`、`Backspace`、`Del`、`Insert`、`Home`、`End`、`PgUp`、`PgDn`、矢印の `Up` `Down` `Left` `Right`、`PrintScreen`、`Pause`、`Backtick` と `Slash`、および**メディアキー** `VolUp`、`VolDown`、`Mute`、`PlayPause`、`Next`、`Prev`、`Stop`。メディアキーは修飾キーなしで単独で動作します。",
        ["The combo is sent as real keystrokes. One thing can block it: an app running **as administrator** won't accept keystrokes from Radiata. This is a Windows limitation."] =
            "組み合わせは実際のキー入力として送られます。妨げになるものが 1 つだけあります。**管理者として**実行中のアプリは Radiata からのキー入力を受け付けません。これは Windows の制限です。",

        // ── topic:discord-setup ──
        ["Discord voice-channel setup"] =
            "Discord のボイスチャンネルの設定",
        ["discord credentials client id secret oauth voice join leave mute deafen keybind"] =
            "discord 資格情報 client id シークレット oauth ボイス 参加 退出 ミュート スピーカーミュート キー割り当て",
        ["**Launch Discord** works out of the box. **Join/Leave Voice Channel**, **Deafen** and **Mute Me** talk to Discord directly, so those three need your own free Discord application credentials. Until they exist the slice editor shows a **Configure Discord Integration** button, and the same wizard sits in **Settings ▸ Advanced**; firing one of those slices from the wheel opens it too, rather than doing nothing."] =
            "**Discord を起動**はそのまま使えます。**ボイスチャンネルに参加・退出**、**スピーカーミュート**、**自分をミュート**は Discord と直接やり取りするため、この 3 つには自分の無料の Discord アプリケーション認証情報が必要です。認証情報がない間、スライスエディターには **Discord 連携を設定**ボタンが表示され、同じウィザードが**設定 ▸ 詳細**にもあります。これらのスライスをホイールから実行した場合も、何も起こらないのではなくそのウィザードが開きます。",
        ["**Deafen** and **Mute Me** toggle Discord's own switches - the same ones the headphone and microphone buttons at the bottom-left of Discord flip - and they work while a game has focus. No keybind to set up, and the game never sees a keystroke."] =
            "**スピーカーミュート**と**自分をミュート**は Discord 自体のスイッチを切り替えます。Discord の左下にあるヘッドホンとマイクのボタンが動かすのと同じものです。ゲームにフォーカスがある状態でも動作します。キー割り当ての設定は不要で、ゲームがキー入力を見ることもありません。",
        ["To get the **channel link**: in Discord, right-click the voice channel → **Copy Link**, and paste it into the slice's **Discord URL** field (Radiata normalizes it to the `discord://` form)."] =
            "**チャンネルのリンク** を取得するには、Discord でボイスチャンネルを右クリックして **Copy Link**（リンクをコピー）を選び、項目の **Discord URL** 欄に貼り付けてください（Radiata が `discord://` 形式に整えます）。",

        // ── topic:steam-xbox-voice ──
        ["Steam voice chat"] =
            "Steam のボイスチャット",
        ["steam chat friends voice mic mute push to talk hotkey open limits deafen join leave"] =
            "steam チャット フレンズ ボイス 音声 マイク ミュート プッシュトゥトーク ホットキー 開く 制限 スピーカーミュート 参加 退出",
        ["The **Steam Chat** group does everything Steam allows a third-party app to do, which is less than Discord allows. Discord provides a local control channel that Radiata's Join/Leave slice uses; **Steam provides none**. What you can do:"] =
            "**Steam チャット**のグループは、Steam がサードパーティ製アプリに許している操作をすべて行えますが、それは Discord が許している範囲より狭いものです。Discord には Radiata の参加・退出スライスが使うローカルの制御チャンネルがありますが、**Steam にはありません**。できるのは次のとおりです。",
        ["**Open Steam Chat** - opens Steam's **Friends & Chat** window. Join a group's voice channel from there. Steam gives another app no way to join, leave or switch voice channels, and has no mute-incoming-voice control at all."] =
            "**Steam チャットを開く** — Steam の**フレンドとチャット**のウィンドウを開きます。グループのボイスチャンネルへはそこから参加できます。Steam は他のアプリにボイスチャンネルへの参加・退出・切り替えの手段を一切提供しておらず、受信音声をミュートする機能もまったくありません。",
        ["**Mic Mute** - in the group, and the same slice as System ▸ Audio. It mutes your **Windows microphone**, which is what Steam transmits from, so the others can't hear you. Every other app loses the mic as well, since Steam exposes no app-scoped mute. Live Muted/Unmuted state shows in the hub."] =
            "**マイクをミュート** — このグループにあり、システム ▸ オーディオのスライスと同じものです。Steam が送信元にしている **Windows のマイク**をミュートするので、相手には聞こえなくなります。Steam にはアプリ単位のミュートがないため、他のすべてのアプリもマイクを失います。ミュート／解除の状態は中央にリアルタイムで表示されます。",
        ["For full voice control from a slice - join, leave, mute, deafen - Discord remains the best-supported option; see [[discord-setup|Discord voice-channel setup]]."] =
            "セクターから音声を完全に制御したい場合（参加、退出、ミュート、スピーカーミュート）は、Discord が最も対応の進んだ選択肢です。[[discord-setup|Discord ボイスチャンネルの設定]] をご覧ください。",

        // ── topic:obs-studio ──
        ["OBS Studio"] =
            "OBS Studio",
        ["obs studio websocket streaming recording replay buffer scene source mute setup port password"] =
            "obs studio websocket 配信 録画 リプレイバッファー シーン ソース ミュート 設定 ポート パスワード",
        ["**OBS Studio** slices - Toggle Streaming, Toggle Recording, Save Replay Buffer, Switch Scene…, Toggle Source Mute… - drive OBS over the **obs-websocket** protocol."] =
            "**OBS Studio** のスライス（Toggle Streaming、Toggle Recording、Save Replay Buffer、Switch Scene…、Toggle Source Mute…）は、**obs-websocket** プロトコルで OBS を操作します。",
        ["**One-time setup:** in OBS, **Tools ▸ WebSocket Server Settings** ▸ enable the server, then copy its **port** (default `4455`) and **password** into **Settings ▸ Advanced ▸ Integrations ▸ Configure OBS Integration…**. It's one shared setting, and **Test** confirms the connection on the spot. Until it's set up, OBS slices arm as **\"Configure in Settings\"**, and an OBS slice's editor offers the same setup pane."] =
            "**一度だけの設定:** OBS で**ツール ▸ WebSocket サーバー設定** ▸ サーバーを有効にし、その**ポート**（既定は `4455`）と**パスワード**を**設定 ▸ 詳細 ▸ 連携 ▸ OBS 連携を設定…**にコピーします。設定は 1 つを共有し、**テスト**でその場で接続を確認できます。設定が済むまで OBS のスライスは **「設定で構成」** として選択され、OBS スライスのエディターからも同じ設定パネルを開けます。",
        ["**Scene** and **audio-source** names on the slice must match OBS exactly."] =
            "項目に入力する **シーン** 名と **音声ソース** 名は、OBS 側の名称と完全に一致していなければなりません。",
        ["**What's supported:** **OBS Studio 28 or later** (the WebSocket server is built in) and **OBS 27 or earlier with the obs-websocket 5.x plugin**. Forks that speak the same protocol (**StreamElements OBS.Live**, for one) work identically. **Streamlabs Desktop does NOT** - it's a different app without obs-websocket."] =
            "**対応状況:** **OBS Studio 28 以降**（WebSocket サーバーが内蔵されています）と、**OBS 27 以前で obs-websocket 5.x プラグインを入れたもの**です。同じプロトコルを話すフォーク（たとえば **StreamElements OBS.Live**）も同じように動きます。**Streamlabs Desktop は動きません。** obs-websocket を持たない別のアプリだからです。",

        // ── topic:text-chat ──
        ["Text Chat (send a message into a game)"] =
            "Text Chat（ゲームにメッセージを送る）",
        ["text chat message send game keybind enter t y chat button custom quick phrase gg glhf cooldown"] =
            "テキストチャット メッセージ 送信 ゲーム キー割り当て enter t y チャットボタン カスタム 定型文 gg glhf クールダウン",
        ["**Try Game Default sends nothing unless the focused game is in the index.** When the game in front isn't covered, the hub names it and says **\"No chat key default found. Configure in Settings\"** - switch that slice to **Custom…** and set the key."] =
            "**フォーカス中のゲームが索引にない場合、ゲームの既定値を試すは何も送りません。** 手前のゲームが対象外のときは、中央にそのゲーム名と **「既定のチャットキーが見つかりません。設定で指定してください」** が表示されます。そのスライスを**カスタム…**に切り替えてキーを設定してください。",
        ["**Limits:** the game must be focused and must accept its chat key at that moment."] =
            "**制限:** ゲームにフォーカスがあり、その時点でチャットキーを受け付ける状態である必要があります。",

        // ── topic:volume-mixer ──
        ["D-Pad 🡄 🡆"] =
            "十字キー 🡄 🡆",
        ["volume mixer balance dpad left right game chat discord music spotify browser desktop song track app session advanced mic microphone input alt-tab task switcher window"] =
            "音量ミキサー バランス 方向キー 左 右 ゲーム チャット discord 音楽 spotify ブラウザー デスクトップ 曲 詳細 マイク 入力 alt-tab ウィンドウ切り替え",
        ["**While a wheel is open, D-Pad 🡄 🡆** does one of four things. Pick in the **Settings ▸ Customize ▸ D-Pad 🡄 🡆** section:"] =
            "**ホイールが開いている間、十字キー 🡄 🡆** は次の 4 つのうち 1 つを行います。**設定 ▸ カスタマイズ ▸ 十字キー 🡄 🡆** のセクションで選んでください。",
        ["**Cycles Windows** (default) - steps through the Alt-Tab switcher, one window per press. The switcher stays up while the wheel is open and lands on the highlighted window when the wheel closes."] =
            "**ウィンドウを切り替え**（ウィンドウを順に切り替え、既定）: Alt-Tab の切り替え画面を 1 回押すごとに 1 ウィンドウ進めます。切り替え画面はホイールが開いている間は表示されたままで、ホイールを閉じると選択中のウィンドウに切り替わります。",
        ["**Cycles Desktops** - switches Windows virtual desktops, the same as **Win+Ctrl+🡄 🡆**."] =
            "**デスクトップを切り替える** — Windows の仮想デスクトップを切り替えます。**Win+Ctrl+🡄 🡆** と同じです。",
        ["**Skips Songs** - the same track-skip keys as the Audio slices; each skip flashes a ⏮ / ⏭ icon in the hub."] =
            "**曲をスキップ**（曲送り）: Audio セクターと同じ曲送りキーを使います。送るたびにハブに ⏮ / ⏭ のアイコンが一瞬表示されます。",
        ["**Mic Volume** - turns your default mic up and down in 5% steps, holding to repeat, and un-mutes it on the way up. The level shows in the hub with a **microphone** icon."] =
            "**マイク音量**（マイク音量）: 既定のマイクの音量を 5 % 刻みで上下させます。押し続けると連続して変化し、上げるときにはミュートも解除します。音量は**マイク**のアイコンとともにハブに表示されます。",
        ["See [[wheel-open-extras|While a wheel is open]] for everything else the D-Pad does with a wheel up."] =
            "ホイールが開いている間に D-Pad が行うその他の操作については、[[wheel-open-extras|ホイールが開いている間]] をご覧ください。",

        // ── topic:supported-controllers ──
        ["Supported controllers"] =
            "対応コントローラー",
        ["dualsense edge dualshock ds4 xbox xinput bluetooth usb bleed through shared input"] =
            "dualsense edge dualshock ds4 xbox xinput bluetooth usb 入力の漏れ 入力の共有",
        ["**Third-party Xbox-style pads over Bluetooth** - most present as a DualShock 4 over BT, so they get the full isolation path too."] =
            "**Bluetooth 接続のサードパーティ製 Xbox 系コントローラー**: 多くは BT 接続時に DualShock 4 として認識されるため、同じく完全な分離が行われます。",
        ["**If you've remapped L4 or R4 on the pad itself** (holding L4/R4 + a button + the mapping key), that paddle now sends the button you assigned and Radiata can no longer see it - so it stops opening wheels. Clear the remap on the pad to get it back, or pick a chord in [[triggers|Settings ▸ Customize ▸ Triggers]] instead."] =
            "**パッド本体で L4 や R4 を再割り当てしている場合**（L4/R4 + ボタン + 割り当てキーの長押し）、そのパドルは割り当てたボタンを送信するようになり、Radiata からは見えなくなるため、ホイールを開けなくなります。パッド側の再割り当てを解除して元に戻すか、代わりに [[triggers|設定 ▸ カスタマイズ ▸ 呼び出し操作]] でコードを選んでください。",
        ["**Xbox pads over USB or wireless dongle (XInput)** - isolated with the same cloak, presenting a virtual **Xbox 360** pad to the game. If a pad can't be cloaked for any reason, Radiata falls back automatically to **shared-input mode**: the wheel still works, but the game also sees your input while a wheel is up."] =
            "**USB またはワイヤレスドングル経由の Xbox コントローラー（XInput）** — 同じクロークで分離し、ゲームには仮想の **Xbox 360** パッドを提示します。何らかの理由でコントローラーをクロークできない場合、Radiata は自動的に**入力共有モード**に切り替わります。ホイールは引き続き使えますが、ホイールが開いている間もゲームがあなたの入力を見ることになります。",
        ["**Two or more Xbox pads plugged in at once** - isolation switches off and both pads keep working in shared-input mode, since cloaking would make a second player's controller disappear. Unplug the second pad and isolation comes back on its own."] =
            "**Xbox コントローラーを 2 台以上同時に接続している場合**: 隠蔽すると 2 人目のコントローラーが消えてしまうため、分離は無効になり、両方のコントローラーが入力共有モードで動作します。2 台目を取り外せば分離は自動的に戻ります。",

        // ── topic:input-isolation ──
        ["Input isolation (what the drivers do)"] =
            "入力の分離（ドライバーの役割）",
        ["isolation virtual pad cloak hidhide vigem drivers double input neutral joy.cpl lag latency delay ms milliseconds input lag polling rate overhead rumble"] =
            "分離 仮想コントローラー 隠蔽 hidhide vigem ドライバー 二重入力 中立 joy.cpl 遅延 レイテンシー ミリ秒 ms ポーリングレート オーバーヘッド 振動",
        ["With successful isolation, the game reads the virtual pad; in Passthru Mode or with no drivers installed, the game reads your controller directly and Radiata simply watches alongside it."] =
            "分離が成功していればゲームは仮想パッドを読み取ります。パススルーモードのとき、またはドライバーが入っていないときは、ゲームがコントローラーを直接読み取り、Radiata はその横で見ているだけになります。",
        ["Isolation forwards sticks, triggers, D-Pad and standard buttons. Sony touchpad, gyro, speaker/mic, adaptive triggers, haptics and rumble are not forwarded. Captured Xbox input supports standard two-motor rumble, but not Share or impulse-trigger motors. [[passthru-mode|Passthru Mode]] or quitting requests removal of Radiata's capture; games may need to reconnect or restart, and other remappers can still affect native features."] =
            "分離が中継するのはスティック、トリガー、十字キー、標準のボタンです。Sony のタッチパッド、ジャイロ、スピーカーとマイク、アダプティブトリガー、ハプティクス、振動は中継されません。キャプチャーした Xbox 入力は標準の 2 モーター振動には対応しますが、Share やインパルストリガーのモーターには対応しません。[[passthru-mode|パススルーモード]] にするか終了すると、Radiata のキャプチャーの解除が要求されます。ゲームによっては接続し直しや再起動が必要になり、他のリマッパーがネイティブ機能に影響し続けることもあります。",
        ["Input latency"] =
            "入力遅延",
        ["**Sony pads and Xbox pads over Bluetooth** - **imperceptible**. Reports are passed straight through as they arrive."] =
            "**Bluetooth 経由の Sony パッドと Xbox パッド** — **知覚できません**。レポートは届いた順にそのまま素通ししています。",
        ["**Xbox pads over USB or a wireless dongle (XInput)** - **a few milliseconds at most**, because these have to be polled."] =
            "**USB またはワイヤレスアダプター接続の Xbox コントローラー（XInput）**: ポーリングが必要なため、**最大でも数ミリ秒**です。",
        ["**Passthru Mode, or no drivers installed** - **none**. The game reads your physical controller directly."] =
            "**パススルーモード、またはドライバー未インストール** - **なし**。ゲームは物理コントローラーを直接読み取ります。",
        ["For scale: a 60 fps game draws a frame every **16.7 ms**. Radiata never injects into or hooks a game, so it adds nothing to rendering or frame pacing."] =
            "目安として、60 fps のゲームは **16.7 ms** ごとに 1 フレームを描画します。Radiata はゲームにコードを挿入したりフックしたりしないため、描画やフレームの間隔には一切影響しません。",
        ["**While a wheel, Game Grid or editor is on screen, Radiata holds its virtual pad neutral.** If the game still reacts, another input path may be active - follow the [[controller-conflict-checklist|Controller conflict checklist]]. Passthru Mode deliberately lets controller input reach the game."] =
            "**ホイール、ゲームグリッド、エディターが画面に出ている間、Radiata は仮想パッドをニュートラルに保ちます。** それでもゲームが反応する場合は、別の入力経路が動いている可能性があります。[[controller-conflict-checklist|コントローラー競合チェックリスト]] をたどってください。パススルーモードでは、コントローラーの入力が意図的にゲームへ届きます。",
        ["For driver repair, HP OMEN buses and version checks, see [[driver-conflicts|HP OMEN & driver version conflicts]]. For busy or hidden-device access, see [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "ドライバーの修復、HP OMEN のバス、バージョンの確認については [[driver-conflicts|HP OMEN とドライバーのバージョン競合]] をご覧ください。ビジー状態や隠しデバイスへのアクセスについては [[hidhide-troubleshooting|HidHide のトラブルシューティング]] をご覧ください。",

        // ── topic:passthru-mode ──
        ["Set Passthru Mode (Bypass Input Isolation)"] =
            "パススルーモード（入力の分離をバイパス）",
        ["passthru mode passthrough safe mode bypass input isolation controller interception anticheat valorant vanguard eac battleye exceptions per game automatic capture"] =
            "passthru モード パススルー passthrough セーフモード バイパス 入力 分離 コントローラー 傍受 アンチチート valorant vanguard eac battleye 例外 ゲームごと 自動 キャプチャ",
        ["Some competitive titles with kernel anticheat (Valorant, Call of Duty, Fortnite, Rainbow Six Siege) could theoretically react to emulated or hidden devices. **Passthru Mode** bypasses [[input-isolation|input isolation]] entirely: Radiata removes its virtual pad and requests release of its own controller blocks. Other tools may still hide or remap the controller. The wheel still works; the trade-off is that input reaches the game while a wheel is open, which is what \"bleed-through\" or \"double input\" means. Don't confuse it with disabling the **wheels** - that keeps the virtual pad in place and only stops summons. Passthru Mode removes the virtual pad altogether."] =
            "カーネルアンチチートを使う一部の競技タイトル（Valorant、Call of Duty、Fortnite、Rainbow Six Siege）は、理論上はエミュレートされたデバイスや隠されたデバイスに反応する可能性があります。**パススルーモード**は [[input-isolation|入力の分離]] を完全に迂回します。Radiata は仮想パッドを取り除き、自身が設定したコントローラーのブロックの解除を要求します。他のツールが引き続きコントローラーを隠したり再割り当てしたりすることはあります。ホイールは使えますが、引き換えにホイールが開いている間も入力がゲームへ届きます。これが「漏れ」や「二重入力」と呼ばれるものです。**ホイール**を無効にすることと混同しないでください。そちらは仮想パッドをそのままにして呼び出しだけを止めます。パススルーモードは仮想パッド自体を取り除きます。",
        ["**Automatic per-game:** add a game to the **Always use Passthru Mode for** list - from the installed-games dropdown, or **Add Application…** for a specific .exe. Radiata enters Passthru Mode while it runs and restores capture on exit; the tray shows \"(auto: <game>)\"."] =
            "**ゲームごとの自動切り替え:** **常にパススルーモードを使うゲーム**の一覧にゲームを追加します。インストール済みゲームのドロップダウンから、または特定の .exe を指定する**アプリケーションを追加…**から行えます。Radiata はそのゲームの実行中だけパススルーモードになり、終了時にキャプチャーを復帰させます。トレイには「(auto: <ゲーム名>)」と表示されます。",
        ["Each entry engages **While Running** (recommended - anticheat watches from launch) or **While Frontmost** (only while the game's window is focused)."] =
            "各項目は **実行中**（実行中ずっと。アンチチートは起動時から監視するため推奨）または **最前面のとき**（そのゲームのウィンドウが前面にある間だけ）で動作します。",
        ["Passthru Mode is **best-effort**. Per-game detection polls every ~2 seconds, so a just-launched game can briefly see normal capture. For the strictest titles, toggle Passthru Mode on manually *before* launching. The isolation drivers also stay installed system-wide either way."] =
            "パススルーモード は**ベストエフォート**です。ゲームごとの検出は約 2 秒間隔のポーリングなので、起動直後のゲームが一瞬だけ通常のキャプチャを見ることがあります。最も厳格なタイトルでは、起動する*前*に手動で パススルーモード をオンにしてください。いずれの場合も、分離ドライバーはシステム全体にインストールされたまま残ります。",

        // ── topic:tray-and-settings ──
        ["Tray & Settings (mouse/keyboard, at the desk)"] =
            "通知領域と設定（机の前でマウスとキーボードを使う操作）",
        ["tray icon left click right click menu settings tabs f1 f2 test"] =
            "通知領域 アイコン 左クリック 右クリック メニュー 設定 タブ f1 f2 テスト",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Show in Explorer, Start with Windows, Exit)."] =
            "**トレイアイコン:** 左クリックで設定が開き、右クリックでメニューが表示されます（ホイールを無効化／ホイールを有効にする、パススルーモード、**ゲームグリッド**、設定、ヘルプ、Radiata について、エクスプローラーで表示、Windows 起動時に開始、終了）。",
        ["**Tray icon:** left-click opens Settings; right-click has the menu (Disable/Enable Wheels, Passthru Mode, **Game Grid**, Settings, Help, About Radiata, Start with Windows, Exit)."] =
            "**トレイアイコン:** 左クリックで設定が開き、右クリックでメニューが表示されます（ホイールを無効化／ホイールを有効にする、パススルーモード、**ゲームグリッド**、設定、ヘルプ、Radiata について、Windows 起動時に開始、終了）。",
        ["**Test Wheel** - each Wheel tab has a **Test Left/Right Wheel** button under the slice list that opens that wheel on screen (with the wheels enabled)."] =
            "**ホイールをテスト**: 各ホイールのタブには、セクター一覧の下に **左/右ホイールをテスト** ボタンがあり、そのホイールを画面に表示します（ホイールが有効な状態で開きます）。",

        // ── topic:customize ──
        ["Customize (look, feel & sound)"] =
            "カスタマイズ（外観、操作感、効果音）",
        ["customize material light dark flat pearl obsidian mesa gloss terra theme slices thickness ring thick medium thin button icons glyphs best guess sound effects themed material digital physical silent none preview appearance look feel triggers accessibility"] =
            "カスタマイズ マテリアル ライト ダーク フラット pearl obsidian mesa グロス terra テーマ スライス 太さ リング 太い 中 細い ボタンアイコン グリフ 自動判定 効果音 テーマ連動 マテリアル デジタル フィジカル サイレント なし プレビュー 外観 見た目 操作感 トリガー アクセシビリティ",
        ["**Mesa** - rounded cream wedges on cracked terracotta; the armed slice lifts like a 3D card."] =
            "**メサ**: ひび割れたテラコッタの上に並ぶ、角の丸いクリーム色のセクターです。選択中のセクターは 3D のカードのように浮き上がります。",
        ["**Sound Effects** - **Themed** (the default) plays whatever sound set matches the material above. **Digital** and **Physical** are fixed sets if you'd rather pin one, and **Silent** turns the sounds off. Picking a material switches the choice back to Themed."] =
            "**サウンドエフェクト** — **テーマに合わせる**（既定）は、上で選んだマテリアルに合うサウンドセットを鳴らします。**デジタル**と**フィジカル**は 1 つに固定したい場合の固定セットで、**無音**は音を切ります。マテリアルを選び直すと、この設定はテーマに合わせるへ戻ります。",
        ["**Slice Thickness** - the radial thickness of the slice ring. **Thick** only reads well up to **8 slices**, so a 9th slice on either wheel switches the setting to **Medium** - and it switches back on its own once you're at 8 or fewer again."] =
            "**スライスの太さ**（セクターの太さ）: セクターのリングの半径方向の太さです。**太い** が見やすいのは **8 セクター**までのため、どちらかのホイールが 9 個目になると設定が自動的に **標準** へ切り替わり、8 個以下に戻ると自動的に元へ戻ります。",
        ["**Button Icons** - the symbols in on-screen prompts: **Best Guess** (the default - follows the connected pad), **PlayStation** (✕ ○ □ △), or **Xbox** (A B X Y)."] =
            "**ボタンアイコン**（ボタンアイコン）: 画面上の案内に使う記号です。**自動判定**（既定。接続中のコントローラーに合わせます）、**PlayStation**（✕ ○ □ △）、**Xbox**（A B X Y）から選べます。",
        ["**Triggers** - which controller gestures summon a wheel. See [[triggers|Triggers]]."] =
            "**呼び出し操作**（トリガー）: どのコントローラー操作でホイールを呼び出すかを設定します。[[triggers|呼び出し操作]] をご覧ください。",
        ["**D-Pad 🡄 🡆** - what left and right on the D-Pad do while a wheel is open. See [[volume-mixer|D-Pad controls]]."] =
            "**十字キー 🡄 🡆**: ホイールが開いている間、D-Pad の左右が何を行うかを設定します。[[volume-mixer|D-Pad の操作]] をご覧ください。",
        ["**Show labels on** - which slices draw their text label. See [[show-labels|Show labels on]]."] =
            "**ラベルを表示するスライス**（ラベルを表示するセクター）: どのセクターにテキストラベルを表示するかを設定します。[[show-labels|ラベルを表示するスライス]] をご覧ください。",
        ["**Accessibility** - activation, wheel sides, both-stick aiming, the hub, Reduce motion and narration - lives on **Settings ▸ Advanced**. See [[accessibility|Accessibility]]."] =
            "**アクセシビリティ**（アクセシビリティ）- 起動方式、ホイールの左右、両スティック照準、ハブ、Reduce motion、ナレーションは **設定 ▸ 詳細** にあります。[[accessibility|アクセシビリティ]] を参照してください。",
        ["**Make your own material** - drop a theme package into Radiata's Materials folder and it joins the list. See [[custom-materials|Custom materials]]."] =
            "**独自のマテリアルを作る** - テーマパッケージを Radiata の Materials フォルダーに置くと一覧に加わります。[[custom-materials|カスタムマテリアル]] を参照してください。",
        ["A wheel holds up to **12** slices, but **6-8** is the sweet spot."] =
            "1 つのホイールには最大 **12** 個のセクターを配置できますが、**6〜8 個**が最適です。",

        // ── topic:workshop ──
        ["Workshop: make your own"] =
            "ワークショップ: 自分で作る",
        ["workshop make build create author own custom package packages theme material game arcade javascript sample samples template download guide folder restart confirm share tutorial"] =
            "ワークショップ workshop 作成 制作 自作 作者 カスタム パッケージ テーマ マテリアル ゲーム アーケード javascript サンプル 見本 テンプレート ダウンロード ガイド フォルダー 再起動 確認 共有 チュートリアル",
        ["Radiata can load things you make yourself: **materials** that restyle the wheel, and **Arcade games** that play in the Arcade's round window. Each one is a **package** - a folder of plain files you can write in any text editor."] =
            "Radiata は自分で作ったものを読み込めます。ホイールの見た目を変える**マテリアル**と、アーケードの丸いウィンドウで遊べる**アーケードゲーム**です。どちらも**パッケージ**、つまりテキストエディターで書けるプレーンなファイルを入れたフォルダーです。",
        ["**Materials** are data only: colors, gradients, a font name, and optionally images and sounds. See [[custom-materials|Custom materials]]."] =
            "**マテリアル**はデータのみです。色、グラデーション、フォント名、必要に応じて画像とサウンド。[[custom-materials|カスタムマテリアル]] を参照してください。",
        ["**Arcade games** are a `game.json` and one JavaScript file, run in a sandbox. See [[custom-arcade-games|Custom Arcade games]]."] =
            "**アーケードゲーム**は `game.json` と 1 つの JavaScript ファイルで、サンドボックスの中で動きます。[[custom-arcade-games|カスタムアーケードゲーム]] を参照してください。",
        ["The **Workshop** on the Radiata website is the full guide: step-by-step walkthroughs, every setting with its range, design advice, and **sample packages to download**. Start there: [getradiata.app/workshop](https://getradiata.app/workshop/)."] =
            "Radiata のウェブサイトにある**ワークショップ**が完全なガイドです。手順ごとの解説、各設定とその範囲、デザインのコツ、そして**ダウンロードできるサンプルパッケージ**があります。まずはここから: [getradiata.app/workshop](https://getradiata.app/workshop/ja.html)",
        ["How making a package works"] =
            "パッケージ作りの流れ",
        ["**1.** Make a folder for your package - or unzip a sample - inside Radiata's packages folder. Paste the path into File Explorer's address bar to open it:"] =
            "**1.** Radiata のパッケージフォルダーの中に、パッケージ用のフォルダーを作ります（またはサンプルを展開します）。パスをエクスプローラーのアドレスバーに貼り付けると開けます:",
        ["a material: `%APPDATA%\\Radiata\\Packages\\Materials\\<your theme>`"] =
            "マテリアル: `%APPDATA%\\Radiata\\Packages\\Materials\\<テーマ名>`",
        ["a game: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<your game>`"] =
            "ゲーム: `%APPDATA%\\Radiata\\Packages\\Arcade Games\\<ゲーム名>`",
        ["**2.** Write the manifest (`material.json` or `game.json`) and put every file it names directly inside that folder."] =
            "**2.** マニフェスト（`material.json` または `game.json`）を書き、そこで指定したファイルをすべてそのフォルダーの直下に置きます。",
        ["**3.** **Restart Radiata** - right-click the tray icon, choose **Exit**, then start it again. Packages are read once, at startup."] =
            "**3.** **Radiata を再起動します**。トレイアイコンを右クリックして**終了**を選び、もう一度起動します。パッケージは起動時に 1 回だけ読み込まれます。",
        ["**4.** Accept the confirmation Radiata shows for a new or changed package."] =
            "**4.** 新しいパッケージや変更されたパッケージについて Radiata が表示する確認を承諾します。",
        ["**5.** Try it out: pick the material in **Settings ▸ Customize**, or open the game from the Arcade. Change something, then go back to step 3."] =
            "**5.** 試します。**設定 ▸ カスタマイズ** でマテリアルを選ぶか、アーケードからゲームを開きます。何かを変えたら手順 3 に戻ります。",
        ["Making a package is a loop: every change goes back through a restart and the confirmation before you can try it."] =
            "パッケージ作りはループです。変更するたびに、試す前に再起動と確認をもう一度通ります。",
        ["Keep a copy of your work somewhere else too. Radiata reads a package where it sits, but nothing backs it up for you."] =
            "作ったものは別の場所にもコピーを残しておきましょう。Radiata はパッケージをその場所から読むだけで、バックアップはしてくれません。",
        ["**Only install packages from people you trust.** Radiata asks before it loads a package and asks again whenever one changes, but it can't tell you whether a package is any good."] =
            "**信頼できる人のパッケージだけをインストールしてください。** Radiata はパッケージを読み込む前と、変更されるたびに確認しますが、そのパッケージが良いものかどうかまでは判断できません。",
        ["When a package doesn't show up, or you want to give one to a friend, see [[workshop-sharing|Testing and sharing packages]]."] =
            "パッケージが表示されないときや、誰かに渡したいときは [[workshop-sharing|パッケージのテストと共有]] を参照してください。",

        // ── topic:custom-materials ──
        ["Custom materials (build your own theme)"] =
            "カスタムマテリアル（独自のテーマを作る）",
        ["Beyond the eight built-in materials you can drop in **your own theme**. A theme is one folder holding a text file called `material.json` - colors, gradients, a system font name, and optionally images and sounds sitting beside it. Themes are **data only**: the format cannot express code, a network address, or a file outside the theme's own folder, so a theme can restyle the wheel and do nothing else."] =
            "内蔵の 8 種類のマテリアルに加えて、**独自のテーマ**を追加できます。テーマは `material.json` というテキストファイルを含む 1 つのフォルダーで、色、グラデーション、システムフォント名、必要に応じてその隣に画像やサウンドを置きます。テーマは**データのみ**です。この形式ではコード、ネットワークアドレス、テーマ自身のフォルダー外のファイルを表現できないため、テーマができるのはホイールの見た目を変えることだけです。",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has two to download: **Starter**, the smallest complete theme with every line explained, and **Ember**, which uses every block below. The Workshop also covers color, contrast and texture advice this topic leaves out."] =
            "**サンプルから始めましょう。** [ワークショップ](https://getradiata.app/workshop/ja.html#samples) では 2 つをダウンロードできます。すべての行に説明が付いた最小の完全なテーマ **Starter** と、以下のすべてのブロックを使った **Ember** です。このトピックでは省いている色、コントラスト、テクスチャのコツもワークショップにあります。",
        ["Where it goes"] =
            "配置場所",
        ["One folder per theme under `%APPDATA%\\Radiata\\Packages\\Materials` - for example `…\\Materials\\Lava\\material.json`. Paste that path into Explorer's address bar; Radiata creates the folder on first run."] =
            "`%APPDATA%\\Radiata\\Packages\\Materials` の下にテーマごとに 1 つのフォルダーを置きます。例えば `…\\Materials\\Lava\\material.json` です。このパスをエクスプローラーのアドレスバーに貼り付けてください。フォルダーは初回起動時に Radiata が作成します。",
        ["That folder also holds a **README.txt** written by Radiata, carrying a copy-paste example of every field. It's the reference; this topic is the tour."] =
            "そのフォルダーには Radiata が書き出した **README.txt** もあり、すべてのフィールドのコピーして使える例が載っています。それがリファレンスで、このトピックは概要です。",
        ["The smallest theme that works"] =
            "動作する最小のテーマ",
        ["A **format 1** manifest is a JSON object with the keys below. `format`, `id`, `name`, `dark`, and a `colors` object holding at least `resting` and `armed` are required; everything else is optional."] =
            "**format 1** のマニフェストは、以下のキーを持つ JSON オブジェクトです。`format`、`id`、`name`、`dark`、および少なくとも `resting` と `armed` を含む `colors` オブジェクトが必須で、それ以外はすべて任意です。",
        ["`\"format\": 1` - which manifest version you wrote. Radiata reads **1 to 3**; the richer blocks further down need the higher number."] =
            "`\"format\": 1` - 記述したマニフェストのバージョン。Radiata は **1〜3** を読み取り、後述のより高機能なブロックには大きい番号が必要です。",
        ["`\"id\": \"lava\"` - 2-31 characters of lowercase a-z, 0-9 and `-`, starting with a letter or digit. This is the theme's identity: it becomes the token `custom-lava` in your config, so changing it later makes a **different** theme."] =
            "`\"id\": \"lava\"` - 小文字の a-z、0-9、`-` からなる 2〜31 文字で、先頭は英字か数字です。これがテーマの識別子で、設定内では `custom-lava` というトークンになります。後で変更すると**別の**テーマになります。",
        ["`\"name\": \"Lava\"` - up to 24 characters; the label on the Customize tile. `\"author\"` (up to 64) is optional."] =
            "`\"name\": \"Lava\"` - 24 文字以内。カスタマイズのタイルに表示されるラベルです。`\"author\"`（64 文字以内）は任意です。",
        ["`\"dark\": true` - whether the slices are dark. It flips labels and the hub to light ink and picks the dark fallbacks, so getting it wrong shows up as unreadable text rather than a wrong color."] =
            "`\"dark\": true` - スライスが暗い色かどうか。ラベルとハブを明るいインクに切り替え、暗い色向けのフォールバックを選びます。間違えると、色の違いではなく読めないテキストとして現れます。",
        ["`\"colors\"` - `\"resting\"` and `\"armed\"` are required; `\"confirm\"` (defaults to the armed color), `\"label\"` and `\"outline\"` are optional. Each is `#RRGGBB` or `#AARRGGBB`, where the leading pair is alpha - `\"#40FFFFFF\"` is a 25%-opaque white outline."] =
            "`\"colors\"` - `\"resting\"` と `\"armed\"` は必須、`\"confirm\"`（既定は armed の色）、`\"label\"`、`\"outline\"` は任意です。それぞれ `#RRGGBB` または `#AARRGGBB` で、先頭の 2 桁はアルファです。`\"#40FFFFFF\"` は不透明度 25% の白いアウトラインになります。",
        ["`\"labelFont\": \"Cascadia Code\"` - optional, and it must be a font **already installed on the PC**. An unknown name is ignored rather than treated as an error. Font *files* inside a package are never supported, deliberately."] =
            "`\"labelFont\": \"Cascadia Code\"` — 任意です。**PC にすでにインストールされている**フォントである必要があります。未知の名前はエラーとして扱われるのではなく無視されます。パッケージ内のフォント*ファイル*は、意図的に一切サポートしていません。",
        ["`\"soundTheme\": \"physical\"` - which sounds the wheel makes on your theme. Name a sound set directly - `physical` (the default), `digital`, `kawaii`, `mesa`, `salvage`, `reactor` or `obsidian` - **or name a built-in material** and borrow whatever that one uses, so `\"pearl\"` gives you the digital set and `\"flat-dark\"` the physical set."] =
            "`\"soundTheme\": \"physical\"` - テーマでホイールが鳴らすサウンド。サウンドセットを直接指定する（`physical`（既定）、`digital`、`kawaii`、`mesa`、`salvage`、`reactor`、`obsidian`）か、**内蔵マテリアルの名前を指定**してそのマテリアルが使うものを借用します。`\"pearl\"` ならデジタルセット、`\"flat-dark\"` ならフィジカルセットになります。",
        ["Naming the **material** is usually the better choice: your theme keeps sounding like the look you styled it after, even if that look's sounds are retuned in a later release. Naming a set pins it exactly."] =
            "通常は**マテリアル**を指定する方が良い選択です。後のリリースでそのマテリアルのサウンドが調整されても、テーマは参考にした見た目と同じ音のままになります。セットを指定すると、その音に固定されます。",
        ["`material.json` may contain `//` comments and trailing commas, so you can leave yourself notes. A color can also be written short as `#RGB`."] =
            "`material.json` には `//` コメントと末尾のカンマを書けるので、メモを残しておけます。色は `#RGB` と短く書くこともできます。",
        ["Richer looks - format 2"] =
            "より豊かな表現 - format 2",
        ["`\"fills\"` - a gradient per state (`resting` / `armed` / `confirm`) instead of a flat color. `\"type\"` is `solid`, `bowed` (the glassy Pearl/Obsidian ramp), or `linear` with an `\"angle\"`, plus a list of `\"stops\"` (each an `\"at\"` from 0 to 1 and a `\"color\"`)."] =
            "`\"fills\"` - フラットな色の代わりに、状態ごと（`resting` / `armed` / `confirm`）のグラデーション。`\"type\"` は `solid`、`bowed`（Pearl/Obsidian のガラス質のランプ）、または `\"angle\"` 付きの `linear` で、`\"stops\"` の一覧（それぞれ 0〜1 の `\"at\"` と `\"color\"`）を持ちます。",
        ["`\"hueWalk\"` - gives every slice its own hue around the ring, Kawaii-style, from `sat` / `light` / `armedSat` / `armedLight` (0-1) and `hueOffset`. It **overrides** the resting and armed fills."] =
            "`\"hueWalk\"` - `sat` / `light` / `armedSat` / `armedLight`（0〜1）と `hueOffset` から、Kawaii 風にリングの各スライスに固有の色相を与えます。resting と armed の塗りを**上書き**します。",
        ["`\"outline\"` and `\"armedOutline\"` - `color`, `width`, and an optional `dash` pattern for the slice edge."] =
            "`\"outline\"` と `\"armedOutline\"` - スライスの縁の `color`、`width`、任意の `dash` パターン。",
        ["`\"armed\"` - how an armed slice moves: `liftPx` (up to 24), `northPx` (±12), `scale` (1.0-1.15). It's a state treatment rather than continuous motion, so it survives [[accessibility|Reduce motion]]."] =
            "`\"armed\"` - 選択されたスライスの動き: `liftPx`（最大 24）、`northPx`（±12）、`scale`（1.0〜1.15）。継続的な動きではなく状態の表現なので、[[accessibility|動きを減らす]] でも維持されます。",
        ["`\"glyph\"` - how slice icons are treated: `edge` (`inner`, `outer` or `none`) with `edgeColor` / `edgeWidth` / `edgeShadow`; a `glow` whose `color` can be the literal `\"slice\"` to take each slice's own accent, with `strength` 0-1; plus `castShadow` and `armedWash`."] =
            "`\"glyph\"` - スライスアイコンの扱い: `edgeColor` / `edgeWidth` / `edgeShadow` を持つ `edge`（`inner`、`outer`、`none`）、`color` にリテラル `\"slice\"` を指定すると各スライスのアクセント色を使う `glow`（`strength` は 0〜1）、さらに `castShadow` と `armedWash`。",
        ["`\"label\"` - `case` (`upper` for stamped all-caps labels) and `sizeMul` (0.8-1.3). `\"gapPx\"` (0-14) sets the gap between slices."] =
            "`\"label\"` - `case`（型押し風の大文字ラベルには `upper`）と `sizeMul`（0.8〜1.3）。`\"gapPx\"`（0〜14）はスライス間の隙間を設定します。",
        ["`\"tile\"` - how the theme's swatch looks on the Customize tab: `edgeColor`, `sheen`, `lifted`, and an optional `texture`."] =
            "`\"tile\"` - カスタマイズタブでのテーマのスウォッチの見え方: `edgeColor`、`sheen`、`lifted`、任意の `texture`。",
        ["Images and sounds - format 3"] =
            "画像とサウンド - format 3",
        ["Set `\"format\": 3` to reference files that live **in the theme's own folder**, by bare file name - a path isn't expressible in the format."] =
            "`\"format\": 3` を設定すると、**テーマ自身のフォルダー内**にあるファイルをパスなしのファイル名で参照できます。パスはこの形式では表現できません。",
        ["`\"textures\"` - `slice` and `hub` paint over the fill; `backdrop` draws behind the whole wheel. Each takes a `\"file\"` and an `\"opacity\"`, and slice/hub also take `\"tile\": true` to repeat the image at its natural size instead of stretching it. **PNG or JPG, up to 4 MB**; anything wider than 2048px is scaled down as it's decoded."] =
            "`\"textures\"` - `slice` と `hub` は塗りの上に描かれ、`backdrop` はホイール全体の背後に描かれます。それぞれ `\"file\"` と `\"opacity\"` を取り、slice/hub は `\"tile\": true` を指定すると画像を引き伸ばさずに元のサイズで繰り返します。**PNG または JPG、4 MB 以内**。幅 2048px を超えるものはデコード時に縮小されます。",
        ["`\"sounds\"` - one file per event: `armed`, `fired`, `enableWheels`, `disableWheels`. **WAV only, up to 1 MB and 3 seconds each**; events you leave out keep the paired sound theme's own sound."] =
            "`\"sounds\"` - イベントごとに 1 ファイル: `armed`、`fired`、`enableWheels`、`disableWheels`。**WAV のみ、各 1 MB ・ 3 秒以内**。省略したイベントは、対応するサウンドテーマの音のままです。",
        ["A theme that ships sounds is **badged** on its Customize tile and takes over the **Sound Effects** picker - the Digital and Physical overrides go inactive, while Themed and Silent stay live."] =
            "サウンドを同梱するテーマは カスタマイズのタイルに**バッジ**が付き、**効果音** ピッカーを引き継ぎます。Digital と Physical の上書きは無効になり、Themed と Silent は有効のままです。",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**. Changing *any* file - not just the manifest - re-asks for your confirmation on the next start."] =
            "フォルダーの制限: 最大 **32 ファイル**、**1 ファイル 4 MB**、**合計 16 MB**。マニフェストだけでなく*どの*ファイルを変更しても、次の起動時に再度確認を求められます。",
        ["If your theme doesn't appear"] =
            "テーマが表示されない場合",
        ["**A package loads whole or not at all.** One bad value rejects the theme rather than half-applying it, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped material`."] =
            "**パッケージは全体が読み込まれるか、まったく読み込まれないかのどちらかです。** 不正な値が 1 つでもあれば、半端に適用されるのではなくテーマ全体が拒否され、理由は `%APPDATA%\\Radiata\\radiata-trace.log` に書き込まれます。そのファイル内で `[Packages] skipped material` を検索してください。",
        ["The usual causes: a missing `dark` or `colors`, a color that isn't `#RRGGBB` / `#AARRGGBB`, an `id` with capitals or spaces, or a `format` number lower than the blocks you used."] =
            "よくある原因: `dark` や `colors` の欠落、`#RRGGBB` / `#AARRGGBB` 形式でない色、大文字や空白を含む `id`、使用したブロックより小さい `format` 番号。",
        ["**A theme you had selected that stops loading** - package removed, or a change you declined - falls back to **Pearl**, quietly. Nothing else in your wheels changes."] =
            "**選択していたテーマが読み込めなくなった場合**（パッケージの削除、または拒否した変更）は、静かに **パール** に戻ります。ホイールの他の部分は何も変わりません。",
        ["Custom themes are never offered during first-run setup, and the theme folder isn't part of a settings backup - copy the folder itself to move a theme to another PC."] =
            "カスタムテーマは初回セットアップでは提示されず、テーマフォルダーは設定のバックアップにも含まれません。別の PC にテーマを移すには、フォルダーそのものをコピーしてください。",

        // ── topic:custom-arcade-games ──
        ["The Arcade also plays **games you write yourself**. One is a folder holding a `game.json` and a single **JavaScript** file. Drop it into Radiata's Arcade Games folder and it plays in the same round window as the built-in games, with the same buttons and the same best-score tracking."] =
            "アーケードでは**自分で書いたゲーム**も遊べます。1 つのゲームは `game.json` と 1 つの **JavaScript** ファイルを収めたフォルダーです。Radiata の Arcade Games フォルダーに入れると、組み込みのゲームと同じ丸いウィンドウで、同じボタン、同じ最高得点の記録で動きます。",
        ["**Start from a sample.** The [Workshop](https://getradiata.app/workshop/#samples) has **Firefly** to download, a short but complete game with every line explained. The Workshop also walks through writing a game step by step."] =
            "**サンプルから始めましょう。** [ワークショップ](https://getradiata.app/workshop/ja.html#samples) では、すべての行に説明が付いた短くても完成したゲーム **Firefly** をダウンロードできます。ワークショップではゲームの書き方も順を追って説明しています。",
        ["**A game package contains code.** Radiata asks you to confirm a package before it ever runs, and again whenever any file in it changes - but the sandbox below is a limit on what a game *can* do, not a judgement about whether it's worth running. **Only install games from a source you trust.**"] =
            "**ゲームパッケージにはコードが含まれます。** Radiata は、パッケージを初めて実行する前と、その中のファイルが変更されるたびに確認を求めます。ただし以下で説明するサンドボックスはゲームが*できること*の上限であり、実行する価値があるかどうかの判断ではありません。**信頼できる提供元のゲームだけをインストールしてください。**",
        ["One folder per game under `%APPDATA%\\Radiata\\Packages\\Arcade Games` - for example `…\\Arcade Games\\Firefly\\game.json` beside `firefly.js`. Radiata creates the folder on first run."] =
            "`%APPDATA%\\Radiata\\Packages\\Arcade Games` の下にゲームごとに 1 つのフォルダーを置きます。例えば `…\\Arcade Games\\Firefly\\game.json` と、その隣の `firefly.js` です。このフォルダーは初回起動時に Radiata が作成します。",
        ["That folder's **README.txt** is the full API reference, kept current by Radiata itself."] =
            "そのフォルダーの **README.txt** が完全な API リファレンスで、Radiata 自身が最新の状態に保ちます。",
        ["The manifest"] =
            "マニフェスト",
        ["`game.json` is a small JSON object. `format`, `id`, `title` and `entry` are required:"] =
            "`game.json` は小さな JSON オブジェクトです。`format`、`id`、`title`、`entry` が必須です。",
        ["`\"format\": 1` - the manifest version."] =
            "`\"format\": 1` - マニフェストのバージョン。",
        ["`\"id\": \"firefly\"` - 1-32 characters of lowercase a-z, 0-9 and `-`. The game's identity, and the `pkg-<id>` token."] =
            "`\"id\": \"firefly\"` - 小文字の a-z、0-9、`-` からなる 1〜32 文字。ゲームの識別子で、`pkg-<id>` トークンになります。",
        ["`\"title\": \"Firefly\"` - up to 24 characters, shown in the picker."] =
            "`\"title\": \"Firefly\"` - 24 文字以内。ピッカーに表示されます。",
        ["`\"entry\": \"firefly.js\"` - the script, as a **bare file name** in the same folder (no paths), up to **256 KB**."] =
            "`\"entry\": \"firefly.js\"` - スクリプト。同じフォルダー内の**パスなしのファイル名**で指定し、**256 KB** 以内です。",
        ["`\"tint\": \"#5B8DEF\"` - optional: your cabinet's colour in the **Arcade Launcher**, as `#RGB` or `#RRGGBB`. Leave it out for the plain grey cabinet. Either way, your `title` is printed on the cabinet's nameplate."] =
            "`\"tint\": \"#5B8DEF\"`: 任意。**アーケードランチャー**での筐体の色を `#RGB` または `#RRGGBB` で指定します。省略すると灰色の筐体になります。どちらの場合も、`title` は筐体の名前プレートに表示されます。",
        ["`\"preview\": \"preview.png\"` - optional: the picture on your cabinet's screen until the game has been played, as a **bare PNG or JPG file name** in the same folder. Make it square, with the round playfield filling it."] =
            "`\"preview\": \"preview.png\"`: 任意。ゲームが遊ばれるまで筐体の画面に表示される画像で、同じフォルダーにある **PNG または JPG のファイル名だけ**を指定します。丸いプレイフィールドがいっぱいに収まる正方形にしてください。",
        ["`\"badge\": \"badge.png\"` - optional: an illustration for your cabinet's nameplate, drawn to the left of your title the way the built-in cabinets carry theirs. A **PNG with a transparent background**, in its own colours; it overflows the nameplate above and below."] =
            "`\"badge\": \"badge.png\"`: 任意。筐体の名前プレートに載せるイラストで、内蔵の筐体と同じようにタイトルの左側に描かれます。**背景が透明な PNG** で、色はそのまま使われます。名前プレートの上下にはみ出して表示されます。",
        ["`\"glyph\": \"glyph.png\"` - optional: your game's icon on its wheel slices. A **PNG** whose transparency is the shape - the wheel colours it like every other slice icon, so draw it in one colour on a transparent background. Without one, drop-in games share a script icon."] =
            "`\"glyph\": \"glyph.png\"`: 任意。ホイールのスライスに表示されるゲームのアイコンです。透明部分が形になる **PNG** で、ホイールはほかのスライスアイコンと同じように色を付けます。透明な背景に 1 色で描いてください。指定しない場合、追加したゲームは共通のスクリプトアイコンを使います。",
        ["Once your game has been played, its cabinet shows the player's own last board instead - Radiata saves it as `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png`. That file is also the easiest way to make a preview: play your game, close it, and copy the file into your package as `preview.png`."] =
            "ゲームが一度遊ばれると、筐体にはそのプレイヤーの最後の画面が表示されます。Radiata はそれを `%APPDATA%\\Radiata\\arcade-shots\\pkg-<id>.png` として保存します。このファイルはプレビューを作るいちばん簡単な方法でもあります。ゲームを遊んで閉じ、ファイルを `preview.png` としてパッケージにコピーしてください。",
        ["Folder limits: at most **32 files**, **4 MB per file**, **16 MB total**."] =
            "フォルダーの制限: 最大 **32 ファイル**、**1 ファイル 4 MB**、**合計 16 MB**。",
        ["How a game runs"] =
            "ゲームの動作",
        ["Your script runs in a **locked-down interpreter inside a separate sandboxed process**: no files, no network, no clipboard, no other programs. Only the functions below exist at all. Memory is capped by Windows at 128 MB."] =
            "スクリプトは**独立したサンドボックスプロセス内の、厳しく制限されたインタープリター**で実行されます。ファイルもネットワークもクリップボードも他のプログラムもありません。存在するのは以下の関数だけです。メモリーは Windows によって 128 MB に制限されます。",
        ["Define **`tick(dt)`**, called for every fixed **1/120 second** step, and **`draw()`**, called once per screen frame. Issue drawing commands only from inside `draw()`."] =
            "**1/120 秒**の固定ステップごとに呼ばれる **`tick(dt)`** と、画面フレームごとに 1 回呼ばれる **`draw()`** を定義します。描画コマンドは `draw()` の中からのみ発行してください。",
        ["There's a hard time and instruction budget **per drawn frame**. A script that overruns is stopped and restarted (your saved data survives); one that keeps overrunning ends in a plain card you can back out of. It can slow itself down - it can't slow the PC down."] =
            "**描画フレームごと**に厳格な時間と命令数の上限があります。超過したスクリプトは停止して再起動され（保存データは残ります）、超過を繰り返すものは戻ることのできるシンプルなカードで終了します。自分自身を遅くすることはできても、PC を遅くすることはできません。",
        ["**Radiata owns {circle} and {triangle}** - closing the game and the help card - so your script never sees those two buttons."] =
            "**{circle} と {triangle} は Radiata が管理します**（ゲームを閉じる、ヘルプカードを閉じる）。そのため、スクリプトがこの 2 つのボタンを見ることはありません。",
        ["Drawing: the playfield is a disc"] =
            "描画: プレイフィールドは円盤",
        ["Everything is drawn in **polar coordinates**: `r` runs 0 at the center to 1 at the rim, angles are **degrees** with 0 at 12 o'clock, increasing clockwise. Radiata does the trigonometry and clips to the circle, so a game can't draw outside its window."] =
            "すべては**極座標**で描画されます。`r` は中心の 0 から縁の 1 まで、角度は 12 時方向を 0 とし時計回りに増加する**度**です。三角関数の計算と円への切り抜きは Radiata が行うため、ゲームがウィンドウの外に描くことはできません。",
        ["A game places everything by radius and angle: r runs from 0 at the center to 1 at the rim, and angles are degrees clockwise from 12 o'clock."] =
            "ゲームはすべてを半径と角度で配置します。r は中心の 0 から縁の 1 まで、角度は 12 時方向から時計回りの度数です。",
        ["Colors are numbers in **`0xAARRGGBB`** form - alpha first, so `0xFFFF0000` is opaque red."] =
            "色は **`0xAARRGGBB`** 形式の数値で、アルファが先頭です。`0xFFFF0000` は不透明な赤になります。",
        ["The commands, up to **1024 per frame**: `arc(r0, r1, a0, a1, color)` for a ring segment, `ring(r, width, color, edge)`, `dot(r, a, size, color)`, `line(r0, a0, r1, a1, w, color)`, `poly([r,a, r,a, …], color)` for 3-16 points, and `text(r, a, size, \"str\", color)` for up to 64 characters."] =
            "コマンドは**フレームごとに最大 1024 個**: リングの一部を描く `arc(r0, r1, a0, a1, color)`、`ring(r, width, color, edge)`、`dot(r, a, size, color)`、`line(r0, a0, r1, a1, w, color)`、3〜16 点の `poly([r,a, r,a, …], color)`、64 文字以内の `text(r, a, size, \"str\", color)` です。",
        ["Input"] =
            "入力",
        ["Read-only globals, refreshed every frame: **`stickX`** / **`stickY`** (-1 to 1, with y positive **downward**, matching the screen), **`crossDown`** / **`squareDown`** while held, **`crossPressed`** / **`squarePressed`** true for one frame per press, and **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**, also one frame per press."] =
            "毎フレーム更新される読み取り専用のグローバル変数: **`stickX`** / **`stickY`**（-1〜1。画面に合わせて y は**下向き**が正）、押している間の **`crossDown`** / **`squareDown`**、押すごとに 1 フレームだけ真になる **`crossPressed`** / **`squarePressed`**、同じく押すごとに 1 フレームの **`dpadUp`** / **`dpadRight`** / **`dpadDown`** / **`dpadLeft`**。",
        ["Saving, randomness, and sound"] =
            "保存、乱数、サウンド",
        ["Write the reserved key **`hiscore`** (a whole number as text) to publish a best score to the Arcade picker."] =
            "予約キー **`hiscore`**（整数をテキストで）に書き込むと、ベストスコアがアーケードピッカーに公開されます。",
        ["**`rand()`** returns 0-1 and is seeded per session, so a replay of the same inputs behaves the same way."] =
            "**`rand()`** は 0〜1 を返し、セッションごとにシードされるため、同じ入力を再現すれば同じ挙動になります。",
        ["**`cue(\"name\")`** plays one of nine built-in sounds: `fire`, `tick`, `good`, `denied`, `kill`, `zap`, `hurt`, `clear`, `gameover`. Any other name is silent, and there's no way to ship your own audio."] =
            "**`cue(\"name\")`** は内蔵の 9 種類のサウンドのいずれかを再生します: `fire`、`tick`、`good`、`denied`、`kill`、`zap`、`hurt`、`clear`、`gameover`。それ以外の名前は無音で、独自の音声を同梱する方法はありません。",
        ["Things that trip people up"] =
            "つまずきやすい点",
        ["Scripts run in **strict mode**, so a variable you forget to declare is an error. There's no `console`, `eval`, timer or `import` - to see a value while you work, draw it with `text()`."] =
            "スクリプトは **strict モード**で動くため、宣言し忘れた変数はエラーになります。`console`、`eval`、タイマー、`import` はありません。作業中に値を確かめたいときは `text()` で描画してください。",
        ["**Keep every drawing value in range**: radii 0-1, `dot` size up to 0.5, `line` width up to 0.1, `text` size up to 0.3, angles within ±3600. Radiata treats an out-of-range call as a broken game and restarts the script; after three restarts it shows a problem card. Clamp your numbers."] =
            "**描画の値はすべて範囲内に収めてください**: 半径は 0〜1、`dot` のサイズは 0.5 まで、`line` の幅は 0.1 まで、`text` のサイズは 0.3 まで、角度は ±3600 以内。範囲外の呼び出しは壊れたゲームとして扱われ、スクリプトが再起動されます。3 回再起動すると問題カードが表示されます。数値は制限してから使いましょう。",
        ["JavaScript's bit operators (`|`, `&`, `<<`) produce **signed** numbers, and a negative color draws as nothing. Build colors with arithmetic, or finish the expression with `>>> 0`."] =
            "JavaScript のビット演算子（`|`、`&`、`<<`）は**符号付き**の数値を返し、負の色は何も描画されません。色は算術で組み立てるか、式の最後に `>>> 0` を付けてください。",
        ["Input is read **once per drawn frame**, but `tick` can run several times in that frame, and each run sees the same `crossPressed`. Make a press count once - the Firefly sample shows a way."] =
            "入力は**描画フレームごとに 1 回**読み込まれますが、そのフレームの中で `tick` は複数回動くことがあり、どの回も同じ `crossPressed` を見ます。1 回の押下が 1 回だけ数えられるようにしてください。サンプルの Firefly に方法があります。",
        ["`text()` centers the string on its point. The last argument of `ring()` is a thin edge color, or `0` for none."] =
            "`text()` は文字列をその点の中央に置きます。`ring()` の最後の引数は細い縁の色で、縁なしなら `0` です。",
        ["Each drawn frame gets **2,000,000 statements and 8 ms** for all of its `tick` runs plus `draw`, at most **8** `cue` calls and **16** `kvSet` writes. `kvSet` throws an error when a key or value is too long or the 4 KB store is full."] =
            "描画フレームごとに、そのフレームのすべての `tick` と `draw` を合わせて **2,000,000 ステートメントと 8 ms**、`cue` の呼び出しは最大 **8** 回、`kvSet` の書き込みは最大 **16** 回です。キーや値が長すぎるときや 4 KB の保存領域がいっぱいのとき、`kvSet` はエラーを投げます。",
        ["If your game doesn't appear"] =
            "ゲームが表示されない場合",
        ["**A package loads whole or not at all**, and the reason is written to `%APPDATA%\\Radiata\\radiata-trace.log` - search that file for `[Packages] skipped arcade game`."] =
            "**パッケージは全体が読み込まれるか、まったく読み込まれないかのどちらか**で、理由は `%APPDATA%\\Radiata\\radiata-trace.log` に書き込まれます。そのファイル内で `[Packages] skipped arcade game` を検索してください。",
        ["The usual causes: an `entry` that isn't a plain `.js` file name sitting in the same folder, a script over 256 KB, an `id` with capitals or spaces, or a confirmation that was declined (it only re-asks once the package changes)."] =
            "よくある原因: `entry` が同じフォルダー内の単純な `.js` ファイル名になっていない、スクリプトが 256 KB を超えている、`id` に大文字や空白が含まれている、確認を拒否した（パッケージが変更されるまで再確認されません）。",
        ["A game that loaded but misbehaves shows its card in the window rather than an error - and a package you delete simply stops being offered."] =
            "読み込まれたものの動作がおかしいゲームは、エラーではなくウィンドウ内にカードを表示します。削除したパッケージは単に候補から消えるだけです。",
        ["**Script errors** go to the same log: search it for `[Arcade] script` to see the error message."] =
            "**スクリプトのエラー**も同じログに記録されます。`[Arcade] script` を検索するとエラーメッセージを確認できます。",

        // ── topic:workshop-sharing ──
        ["Testing and sharing packages"] =
            "パッケージのテストと共有",
        ["Testing"] =
            "テスト",
        ["Radiata reads packages **only at startup**: exit from the tray and start it again after every change."] =
            "Radiata がパッケージを読むのは**起動時だけ**です。変更するたびに、トレイから終了して起動し直してください。",
        ["A change to **any** file in a package brings the confirmation back on the next start. If you decline it, the package stays off until its files change again."] =
            "パッケージ内の**どの**ファイルを変更しても、次の起動時に確認が再び表示されます。拒否すると、ファイルが再び変更されるまでそのパッケージは読み込まれません。",
        ["**Nothing happening?** Open `%APPDATA%\\Radiata\\radiata-trace.log` in a text editor and search for `[Packages] skipped`. Each line names the package folder and the exact problem - a missing field, a value out of range, a file that isn't there."] =
            "**何も起きない？** `%APPDATA%\\Radiata\\radiata-trace.log` をテキストエディターで開き、`[Packages] skipped` を検索してください。各行にパッケージのフォルダーと、足りないフィールド、範囲外の値、見つからないファイルといった具体的な問題が書かれています。",
        ["**The most common mistake is one folder too many.** Unzipping often makes `Materials\\Lava\\Lava\\material.json`; Radiata looks for the manifest directly inside `Materials\\Lava`. Move the files up a level."] =
            "**いちばん多い間違いは、フォルダーが 1 段多いことです。** 展開すると `Materials\\Lava\\Lava\\material.json` のようになりがちですが、Radiata はマニフェストを `Materials\\Lava` の直下で探します。ファイルを 1 段上に移してください。",
        ["Radiata looks for the manifest directly inside the package's own folder; the extra folder level an unzip often adds hides it."] =
            "Radiata はマニフェストをパッケージ自身のフォルダーの直下で探します。展開でよく増える余分なフォルダー階層があると見つかりません。",
        ["A game that loads but misbehaves writes its script errors to the same log - search for `[Arcade] script`."] =
            "読み込まれても正しく動かないゲームは、スクリプトのエラーを同じログに書きます。`[Arcade] script` を検索してください。",
        ["Sharing"] =
            "共有",
        ["Say what the package is and what it does, and only include images, sounds and code you have the right to share."] =
            "パッケージが何で、何をするのかを説明し、共有する権利のある画像、サウンド、コードだけを含めてください。",
        ["Radiata's license doesn't extend to your package: what you make in these formats is yours to license however you like."] =
            "Radiata のライセンスはあなたのパッケージには及びません。これらの形式で作ったものは、好きなライセンスで公開できます。",
        ["Moving to another PC? Settings backups don't include packages - copy the `Packages` folder across yourself."] =
            "別の PC に移るときは？設定のバックアップにはパッケージが含まれません。`Packages` フォルダーを自分でコピーしてください。",

        // ── topic:triggers ──
        ["Triggers (summon chords)"] =
            "呼び出し操作（呼び出しの組み合わせ）",
        ["triggers chord builder hold tap add another trigger remove row fn bumper trigger touchpad swipe select start l3 r3 dpad combined summon invoke gesture customize"] =
            "triggers トリガー 組み合わせ 作成 長押し 短押し 追加 行の削除 fn バンパー トリガー タッチパッド スワイプ select start l3 r3 dpad 組み合わせ 呼び出し ジェスチャー カスタマイズ",
        ["**Settings ▸ Customize ▸ Triggers** is the chord builder: which controller gestures summon a wheel. Each row is one live chord - a **button** (Fn or L4/R4 / Bumper / Trigger / Touchpad) paired with how it's **combined** (Trigger, Home, L3/R3, Select/Start, D-Pad L/R, or a touchpad edge-swipe). The options adapt to the detected pad: **Fn** appears for a DualSense Edge, **L4/R4** for a pad with extra buttons on Bluetooth, and **Touchpad** only for pads that have one. Those dedicated buttons need no second button - they open a wheel on their own."] =
            "**設定 ▸ カスタマイズ ▸ トリガー**はコードの組み立て画面で、どのコントローラー操作でホイールを呼び出すかを決めます。各行が 1 つの有効なコードで、**ボタン**（Fn または L4/R4 ／ バンパー ／ トリガー ／ タッチパッド）と、その**組み合わせ方**（トリガー、Home、L3/R3、Select/Start、十字キーの左右、タッチパッドの端からのスワイプ）の組で表されます。選択肢は検出されたコントローラーに合わせて変わります。**Fn** は DualSense Edge で、**L4/R4** は Bluetooth 接続で追加ボタンを持つコントローラーで、**タッチパッド**はタッチパッドのあるコントローラーでのみ現れます。これらの専用ボタンに 2 つ目のボタンは不要で、単独でホイールを開きます。",
        ["**+ Add Another Trigger** appends a row; a row's **✕** removes it. Every row stays live at once - up to **three** - and the set is remembered **per controller type**, so an Edge and an Xbox pad each keep their own chords."] =
            "**+ 呼び出し操作を追加** で行を追加し、行の **✕** で削除します。すべての行が同時に有効で、最大 **3 行**まで設定できます。設定は**コントローラーの種類ごと**に記憶されるため、Edge と Xbox コントローラーはそれぞれ独自の組み合わせを保持します。",
        ["How each chord behaves (hold + tap, which wheel it opens, Hold vs Toggle) is in [[opening-a-wheel|Opening a wheel]]; the both-sides version of a chord toggles the wheels on and off, see [[wheel-open-extras|While a wheel is open]]."] =
            "各組み合わせの挙動（押し続けとタップ、どちらのホイールが開くか、Hold と Toggle の違い）は [[opening-a-wheel|ホイールを開く]] にあります。組み合わせの両側同時版はホイールの有効／無効を切り替えます。[[wheel-open-extras|ホイールが開いている間]] をご覧ください。",

        // ── topic:accessibility ──
        ["Accessibility (Settings ▸ Advanced)"] =
            "アクセシビリティ（設定 ▸ 詳細）",
        ["accessibility wheels toggle on off swap left right both sticks either stick one stick ignores opposite stick sidedness aim drift always show hub battery reduce motion confetti fade parallax animation effects still narration speak speech spoken screen reader narrator voice volume system-wide windows narrator settings onboarding blind low vision"] =
            "アクセシビリティ ホイール トグル オン オフ 入れ替え 左 右 両スティック どちらのスティック 片方のスティック 反対側のスティックを無視 左右 照準 ドリフト ハブを常に表示 バッテリー 視覚効果を減らす 紙吹雪 フェード パララックス アニメーション 効果 静止 ナレーション 読み上げ 音声 スクリーンリーダー ナレーター ボイス 音量 システム全体 windows ナレーター 設定 初回セットアップ 全盲 弱視",
        ["The **Accessibility** section gathers six checkboxes, in **Settings ▸ Advanced**. The same set is offered during first-run setup from the **Accessibility…** button on the Look step:"] =
            "**アクセシビリティ** セクションには 6 つのチェックボックスがまとまっており、**設定 ▸ 詳細** にあります。同じ設定は、初回セットアップの Look ステップにある **アクセシビリティ** ボタンからも設定できます。",
        ["Two of them are **indented under the box that ticks them**: turning on **Wheels toggle on/off** also ticks **Swap left/right**, and turning on **Reduce motion** also ticks **Always show hub**, because each pair works best together. Both children stay yours to tick or clear on their own, and once you set one by hand it stops following its parent."] =
            "そのうち 2 つは**それをチェックするボックスの下にインデント**されています。**ホイールをトグルで開閉** をオンにすると **左右を入れ替え** もチェックされ、**動きを減らす** をオンにすると **常にハブを表示** もチェックされます。それぞれの組は一緒に使うと最も効果的だからです。子の項目はどちらも自由にチェック・解除でき、一度手動で設定すると親に追従しなくなります。",
        ["**Wheels toggle on/off** and **Swap left/right** - whether an invoked wheel stays up until you dismiss it, and which wheel each hand opens (see [[opening-a-wheel|Opening a wheel]])."] =
            "**ホイールをトグルで開閉** と **左右を入れ替え** - 呼び出したホイールを閉じるまで表示し続けるかどうかと、どちらの手がどちらのホイールを開くか（[[opening-a-wheel|ホイールを開く]] を参照）。",
        ["**Narration** - speaks what you're doing aloud: which wheel opened, the slice you arm and its current state, hold-to-confirm progress, what a fire actually did, volume levels as you scrub, edit-mode moves, and Game Grid browsing. It works alongside a screen reader."] =
            "**音声読み上げ**（読み上げ）: 操作内容を音声で読み上げます。どのホイールが開いたか、選択中のセクターとその現在の状態、押し続けて確定する際の進捗、実行された内容、音量調整中の音量、編集モードでの移動、ゲームグリッドの閲覧が対象です。スクリーンリーダーと併用できます。",
        ["**Narration covers the wheel and the Game Grid only** - the overlay surfaces a screen reader can't see. Settings, first-run setup and every other ordinary window are **Windows Narrator's** job, so run Narrator alongside Radiata if you want those read too. Ticking **Narration** offers a button to turn Narrator on; and if Narrator is running when you first set Radiata up, Narration starts on by itself."] =
            "**読み上げが対象とするのはホイールとゲームグリッドだけです。** スクリーンリーダーからは見えないオーバーレイの部分だからです。設定、初回セットアップ、その他の通常のウィンドウは **Windows ナレーター**の担当なので、それらも読ませたい場合はナレーターを Radiata と併用してください。**読み上げ**にチェックを入れるとナレーターを有効にするボタンが提示されます。また、Radiata を初めてセットアップするときにナレーターが動いていれば、読み上げは自動的にオンで始まります。",

        // ── topic:show-labels ──
        ["Show labels on (slice text)"] =
            "ラベルを表示するスライス（項目の文字ラベル）",
        ["show labels on slice labels label text names icons not logos standard icons all slices no slices slices i choose show label checkbox unlabelled artwork logo cover png"] =
            "ラベル表示 スライスのラベル 文字 名前 アイコン ロゴ以外 標準アイコン 全スライス なし 選んだスライス show label チェックボックス ラベルなし 画像 ロゴ カバー png",
        ["**Show labels on** - which slices draw their text label on the wheel."] =
            "**ラベルを表示するスライス**（ラベルを表示するセクター）: ホイール上でどのセクターにテキストラベルを表示するかを設定します。",
        ["**All Slices** - every slice is labelled, artwork ones included."] =
            "**すべてのスライス**（すべてのセクター）: アートを持つものも含め、すべてのセクターにラベルが付きます。",
        ["**No Slices** - no slice is labelled."] =
            "**なし**（ラベルなし）: どのセクターにもラベルが付きません。",
        ["**Editing a wheel is exempt:** in edit mode and its Add picker, non-logo slices always show their labels whatever this is set to."] =
            "**ホイールの編集中は例外です。** 編集モードとその追加ピッカー では、この設定にかかわらず、ロゴのないスライスに常にラベルが表示されます。",
        ["The setting lives in **Settings ▸ Customize**, directly under the **D-Pad 🡄 🡆** selector, and applies **live** - bring up a wheel to see it."] =
            "この設定は **設定 ▸ カスタマイズ** の **十字キー 🡄 🡆** セレクターのすぐ下にあり、**その場で反映**されます。ホイールを呼び出して確認してください。",

        // ── topic:integrations ──
        ["Integrations (SteamGridDB, Discord & OBS)"] =
            "連携（SteamGridDB、Discord、OBS との連携）",
        ["integrations steamgriddb sgdb api key discord obs websocket port password configure cover art logos"] =
            "連携 steamgriddb sgdb api キー discord obs websocket ポート パスワード 設定 カバー画像 ロゴ",
        ["**Settings ▸ Advanced ▸ Integrations** connects optional external services:"] =
            "**設定 ▸ 詳細 ▸ 連携** では、任意で利用できる外部サービスとの連携を設定します。",
        ["**Configure Discord Integration…** - sets the Discord credentials (Client ID/Secret) that Join/Leave Voice Channel slices use, the same wizard the slice editor offers. Stored encrypted (Windows DPAPI) and sent only to Discord."] =
            "**Discord 連携を設定…** — ボイスチャンネルの参加・退出スライスが使う Discord の認証情報（クライアント ID とシークレット）を設定します。スライスエディターが提示するのと同じウィザードです。暗号化して保存され（Windows DPAPI）、送信先は Discord だけです。",
        ["**SteamGridDB API key** - unlocks portrait cover art and logos for every storefront's games (the Game Grid's **Select**/**Start** cycling). Get a free key at [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api). It's checked when you finish entering it, and entering your first key automatically fills in covers skipped while you had none."] =
            "**SteamGridDB API キー**: すべてのストアのゲームで縦長のカバーアートとロゴを利用できるようにします（ゲームグリッドの **選択** / **Start** による切り替え）。無料のキーは [steamgriddb.com ▸ Preferences ▸ API](https://www.steamgriddb.com/profile/preferences/api) で取得できます。入力を終えると自動的に検証され、最初のキーを登録すると、キーがなかった間に取得を見送ったカバーが自動的に補完されます。",
        ["**OBS Studio** - **Configure OBS Integration…** sets the WebSocket port and password, one connection shared by every OBS slice; see [[obs-studio|OBS slices]]. **Test** checks it against a running OBS. The password is stored encrypted (DPAPI)."] =
            "**OBS Studio** — **OBS 連携を設定…**で WebSocket のポートとパスワードを設定します。すべての OBS スライスが 1 つの接続を共有します。[[obs-studio|OBS のスライス]] をご覧ください。**テスト**は動作中の OBS に対して確認します。パスワードは暗号化して保存されます（DPAPI）。",
        ["With [[playnite|Playnite]] and a SteamGridDB key both set up, a **Prefer Playnite covers** toggle appears in the **Game Grid** section. Without Playnite, an **Install Playnite…** button appears here instead."] =
            "[[playnite|Playnite]] と SteamGridDB のキーが両方そろっていると、**ゲームグリッド**のセクションに **Playnite のカバーを優先**の切り替えが現れます。Playnite がない場合は、代わりに **Playnite をインストール…**のボタンがここに現れます。",

        // ── topic:playnite ──
        ["Playnite (optional library manager)"] =
            "Playnite（任意で利用できるライブラリー管理ソフト）",
        ["playnite library manager games covers metadata optional install third party emulators"] =
            "playnite ライブラリー管理 ゲーム カバー画像 メタデータ 任意 インストール 他社製 エミュレーター",
        ["**Playnite** is a free, open-source game-library manager for Windows ([playnite.link](https://playnite.link)) that gathers all your games - Steam, Epic, GOG, Xbox, emulators, standalone - with metadata and cover art."] =
            "**Playnite** は Windows 向けの無料・オープンソースのゲームライブラリ管理ソフトです（[playnite.link](https://playnite.link)）。Steam、Epic、GOG、Xbox、エミュレーター、単体のゲームまで、すべてをメタデータとカバーアート付きでまとめます。",
        ["Radiata works fully **without** it, scanning your storefronts directly. Playnite is an optional enhancement:"] =
            "Radiata は Playnite が **なくても** 完全に動作し、各ストアを直接調べます。Playnite は任意の補助的な機能です。",
        ["**More games found** - Radiata reads Playnite's library, so emulated and manually-added games a raw storefront scan misses can appear in the Game Grid."] =
            "**見つかるゲームが増えます**: Radiata は Playnite のライブラリを読み取るため、ストアの検出だけでは見落とされるエミュレーターのゲームや手動で追加したゲームもゲームグリッドに表示できます。",
        ["**Curated cover art** - Playnite's own covers become an art source, with a **Prefer Playnite covers** toggle (Advanced ▸ Game Grid) to favor them over SteamGridDB."] =
            "**厳選されたカバーアート**: Playnite 自身のカバーがアートの取得元になります。**Playnite のカバーを優先**（詳細 ▸ ゲームグリッド）で SteamGridDB より優先させることもできます。",
        ["Not installed? **Install Playnite…** in Advanced ▸ Integrations opens its download page. Set up your libraries in Playnite and Radiata picks them up automatically."] =
            "インストールしていませんか。詳細 ▸ 連携の **Playnite をインストール…**からダウンロードページを開けます。Playnite でライブラリーを設定すれば、Radiata が自動的に取り込みます。",

        // ── topic:system-actions ──
        ["System tools & Backup (Settings ▸ Advanced)"] =
            "システム関連の機能とバックアップ（設定 ▸ 詳細）",
        ["run first run setup onboarding wizard reset starter slices customize recommended install repair drivers recover controller setup email log hid diagnostics quit exit backup restore reset wipe zip factory defaults undo clean install always show hub practice reduce motion check for updates automatic update skip version"] =
            "初回設定 実行 ウィザード リセット 開始時のセクター カスタマイズ 推奨 ドライバー インストール 修復 コントローラー 復旧 セットアップ メール ログ hid 診断 終了 バックアップ 復元 リセット 消去 zip 工場出荷時 元に戻す クリーンインストール ハブを常に表示 練習 動きを減らす 更新の確認 自動更新 バージョンをスキップ",
        ["**Settings ▸ Advanced ▸ System** holds the update controls, the **Start with Windows** toggle, the **Troubleshooting** dropdown, and **Quit Radiata**:"] =
            "**設定 ▸ 詳細 ▸ システム** には、更新の操作、**Windows 起動時に開始** の切り替え、**トラブルシューティング** のドロップダウン、**Radiata を終了** があります:",
        ["**Quit Radiata** - releases its virtual controller and requests removal of the HidHide blocks Radiata owns. Another tool's blocks remain; see [[hidhide-troubleshooting|HidHide troubleshooting]] if the pad stays hidden."] =
            "**Radiata を終了** — 仮想コントローラーを解放し、Radiata が所有する HidHide のブロックの解除を要求します。他のツールのブロックは残ります。コントローラーが隠れたままの場合は [[hidhide-troubleshooting|HidHide のトラブルシューティング]] をご覧ください。",
        ["The Troubleshooting dropdown"] =
            "「トラブルシューティング…」ドロップダウン",
        ["**Run First-Run Setup…** - re-runs the setup wizard (controller check, drivers, look, cover art, starter wheels). Your customized wheels are never overwritten without asking."] =
            "**初回セットアップを実行…**: セットアップウィザード（コントローラーの確認、ドライバー、外観、カバーアート、開始時のホイール）をもう一度実行します。カスタマイズ済みのホイールが確認なしに上書きされることはありません。",
        ["**Install/Repair Drivers…** - installs or repairs the isolation drivers (ViGEmBus + HidHide). Fixes most isolation problems, and shows a result log."] =
            "**ドライバーをインストール／修復…** — 分離ドライバー（ViGEmBus と HidHide）をインストールまたは修復します。分離まわりの問題の多くはこれで直り、結果のログが表示されます。",
        ["**HID Diagnostics…** - a live view of the raw controller reports Radiata reads. Useful when support asks what your pad is actually sending."] =
            "**HID 診断…** — Radiata が読み取っている生のコントローラーレポートをリアルタイムで表示します。コントローラーが実際に何を送っているのかをサポートに尋ねられたときに役立ちます。",
        ["**Controller Setup…** - re-runs the controller detection and mapping wizard on its own, without the rest of first-run setup."] =
            "**コントローラーのセットアップ…**: 初回設定の他の項目を行わずに、コントローラーの検出と割り当てのウィザードだけをもう一度実行します。",
        ["**Email Log to Developer…** - saves a diagnostic ZIP to your Desktop and opens an addressed email. Review the ZIP before attaching and sending it: logs can include device identifiers, account names in file paths, and application or game names. Radiata does not automatically send the attachment."] =
            "**開発者にログを送信…** — 診断用の ZIP をデスクトップに保存し、宛先を入れたメールを開きます。添付して送る前に ZIP を確認してください。ログにはデバイス識別子、ファイルパス内のアカウント名、アプリやゲームの名前が含まれることがあります。Radiata が添付ファイルを自動送信することはありません。",
        ["**Back Up Settings…** and **Restore Settings…** sit lower in the same dropdown, and the **resets** below them - all described under Backup & reset."] =
            "**設定をバックアップ…** と **設定を復元…** は同じドロップダウンの下の方にあり、その下に**リセット**の各項目が並びます。いずれもバックアップとリセットの項で説明しています。",
        ["Backup & reset"] =
            "バックアップとリセット",
        ["**Back Up Settings…** - saves everything that makes Radiata yours (wheels, colors, settings, and your Game Grid cover/logo picks) to a .zip in `Documents\\Radiata Backups`."] =
            "**設定をバックアップ…**: Radiata をご自身のものにしている設定一式（ホイール、色、各種設定、ゲームグリッドで選んだカバーとロゴ）を `Documents\\Radiata Backups` 内の .zip に保存します。",
        ["**Reset All Settings…** - factory defaults for wheels, colors and settings; first-run setup runs again on the next launch. Cached art survives."] =
            "**すべての設定をリセット…** — ホイール、色、設定を工場出荷時に戻します。次回起動時に初回セットアップがもう一度実行されます。キャッシュ済みのアートは残ります。",
        ["**Uninstall Radiata…** - removes the startup entry, the HidHide registration, and Radiata's own files, with OFF-by-default opt-ins for the shared drivers and your settings. Anything else in Radiata's folder is left alone."] =
            "**Radiata をアンインストール…**: スタートアップ登録、HidHide への登録、Radiata 自身のファイルを削除します。共有ドライバーと設定については、既定でオフのチェックボックスが用意されています。Radiata のフォルダー内のそれ以外のものはそのまま残ります。",
        ["Updates"] =
            "更新",
        ["**Check for Updates** - asks `getradiata.app/update` for a newer version right now. When one is found the button becomes **Install Update** and opens the update prompt."] =
            "**更新を確認**: `getradiata.app/update` に、より新しいバージョンがあるかを今すぐ問い合わせます。 見つかると、ボタンが**更新をインストール**に変わり、更新の確認画面が開きます。",
        ["**Automatic** - the checkbox beside the button: when on, the same check runs at startup and once a day. Found updates announce themselves with an on-screen notice (click it to open the update) and a line in this tab; a version you choose to **skip** stops announcing itself, though the line here still shows it. **Skip This Version** is offered only after you have pressed **Later** on that version once."] =
            "**自動** — ボタンの横のチェックボックスです。オンにすると、同じ確認が起動時と 1 日 1 回実行されます。見つかった更新は画面上の通知（クリックすると更新が開きます）とこのタブの 1 行で知らせます。**スキップ**を選んだバージョンは以後知らせなくなりますが、ここの行には引き続き表示されます。 **このバージョンをスキップ**は、そのバージョンで一度**後で**を押したあとにだけ表示されます。",
        ["Resets can't be undone - **back up first**."] =
            "リセットは元に戻せません。**先にバックアップしてください。**",
        ["The game-art buttons live in their own **Game Grid** section - see [[game-grid-options|Game Grid options]]."] =
            "ゲームアートのボタンは専用の **ゲームグリッド** セクションにあります。[[game-grid-options|ゲームグリッドのオプション]] をご覧ください。",

        // ── topic:game-grid-options ──
        ["Game Grid options (Settings ▸ Advanced)"] =
            "ゲームグリッドのオプション（設定 ▸ 詳細）",
        ["game grid options clear game art cache retry missing game art reset hidden games unhide covers redownload"] =
            "game grid オプション 画像キャッシュの削除 画像の再取得 非表示のゲームを戻す 再表示 再ダウンロード",
        ["**Settings ▸ Advanced ▸ Game Grid** collects the grid's housekeeping buttons:"] =
            "**設定 ▸ 詳細 ▸ ゲームグリッド** には、一覧の管理用ボタンがまとめられています。",
        ["**Retry Missing Game Art** - re-attempts only the covers and logos that came up empty, keeping everything already downloaded and every cover you picked by hand (see [[cover-art|Cover art & logos]])."] =
            "**不足しているゲームアートを再取得**: 取得できなかったカバーとロゴだけを再取得します。すでにダウンロード済みのものと、手動で選んだカバーはそのまま保持されます（[[cover-art|カバーアートとロゴ]] をご覧ください）。",
        ["**Clear Game Art Cache** - deletes ALL cached covers, so everything re-downloads. **Images you dropped onto a slice are kept.** Try **Retry Missing Game Art** first if you only want to fill blanks."] =
            "**ゲームアートのキャッシュを消去** — キャッシュ済みのカバーを**すべて**削除するので、すべて再ダウンロードされます。**スライスにドロップした画像は残ります。** 空欄だけを埋めたい場合は、先に**不足しているゲームアートを再取得**を試してください。",
        ["**Reset Hidden Games** - brings back every game and storefront you hid with **hold {square}** (see [[storefronts|Hiding a storefront]])."] =
            "**非表示のゲームをリセット**: **{square} を押し続ける**操作で隠したゲームとストアをすべて元に戻します（[[storefronts|ストアを隠す]] をご覧ください）。",
        ["**Prefer Playnite covers** - shown when [[playnite|Playnite]] and a SteamGridDB key are both set up: favors Playnite's own cover art."] =
            "**Playnite のカバーを優先**: [[playnite|Playnite]] と SteamGridDB のキーが両方そろっている場合に表示され、Playnite 自身のカバーアートを優先します。",

        // ── topic:storefronts ──
        ["Hiding a storefront"] =
            "ストアを非表示にする",
        ["storefront steam epic gog xbox battle.net amazon itch ubisoft ea hide exclude opt out include reset hidden games launcher card"] =
            "ストア storefront steam epic gog xbox battle.net amazon itch ubisoft ea 非表示 除外 対象外 含める 非表示ゲームのリセット ランチャー カード",
        ["A whole storefront can be hidden from the [[game-grid|Game Grid]], the same way a single game can."] =
            "[[game-grid|ゲームグリッド]] からは、1 本のゲームと同じ要領で、ストア全体を非表示にすることもできます。",
        ["**Filter to the store with L1 / R1**, then **hold {square}** on its **Open <store>** card. A notice asks **\"Hide <store> and all its games in Radiata?\"** - **{cross}** hides it, **{circle}** cancels."] =
            "**L1 / R1 でそのストアに絞り込み**、その **<ストア> を開く**カードの上で **{square} を長押し**します。**「<ストア> とそのすべてのゲームを Radiata で非表示にしますか？」**という通知が出るので、**{cross}** で非表示、**{circle}** で取り消しです。",
        ["Hiding a store **hides its games** in the Game Grid, the [[edit-mode|Add picker]], and the starter wheels the first-run wizard suggests."] =
            "ストアを隠すと、ゲームグリッド、[[edit-mode|追加メニュー]]、初回設定ウィザードが提案する開始時のホイールから**そのストアのゲームが除かれます**。",
        ["Bring it back with **Settings ▸ Advanced ▸ Game Grid ▸** [[game-grid-options|Reset Hidden Games]], which un-hides storefronts as well as games."] =
            "元に戻すには **設定 ▸ 詳細 ▸ ゲームグリッド ▸** [[game-grid-options|非表示のゲームをリセット]] を使います。ゲームだけでなくストアの表示も戻ります。",
        ["Newly installed storefronts appear **automatically** the next time Radiata scans."] =
            "新しくインストールされたストアは、Radiata が次に検出を行ったときに**自動的に**表示されます。",
        ["Only a store with its own **Open <store>** card can be hidden this way. A [[playnite|Playnite]] game you added by hand, or one from a third-party Playnite plugin, carries no storefront card. Hide those games individually."] =
            "この方法で非表示にできるのは、専用の **<ストア> を開く**カードを持つストアだけです。手作業で追加した [[playnite|Playnite]] のゲームや、サードパーティ製 Playnite プラグイン由来のゲームにはストアのカードがありません。そうしたゲームは個別に非表示にしてください。",

        // ── topic:controller-not-detected ──
        ["Controller not detected"] =
            "コントローラーが検出されない",
        ["controller dead not detected blind hidhide lockout whitelist recover reset bluetooth radio frozen stuck wedge"] =
            "コントローラー 反応しない 検出されない 見えない hidhide ロックアウト 許可リスト 復旧 リセット bluetooth 無線 フリーズ 固まる",
        ["**Replug / re-pair first.** Bluetooth stacks occasionally wedge, and power-cycling the pad fixes most one-offs."] =
            "**まずは挿し直すかペアリングし直してください。** Bluetooth スタックはときどき固まりますが、コントローラーの電源を入れ直せば一度きりの不具合はたいてい直ります。",
        ["**Bluetooth pad connected but frozen** (Windows still lists it, input never moves)? That's a Windows Bluetooth wedge that power-cycling the pad **won't** fix - toggle the PC's **Bluetooth off and on** instead. Radiata shows a \"toggle Bluetooth\" notification when it spots this."] =
            "**Bluetooth のコントローラーが接続されているのに固まっていますか**（Windows には一覧表示されるのに入力が一切動かない）。それは Windows の Bluetooth の固着で、コントローラーの電源を入れ直しても**直りません**。代わりに PC の **Bluetooth をオフにしてからオン**にしてください。Radiata はこれを検出すると「Bluetooth を切り替えてください」という通知を表示します。",
        ["**HidHide lockout:** if the cloak hides the pad while Radiata isn't on its allow-list, Radiata goes blind. That means no input, and the Current Controller readout shows nothing even though Windows sees the pad. Radiata catches this at startup and offers a one-click fix via a clickable on-screen notice; repair can help with registration, but it does not cure every access problem. See [[hidhide-troubleshooting|HidHide troubleshooting]]."] =
            "**HidHide によるロックアウト:** Radiata が許可リストに入っていない状態でクロークがコントローラーを隠すと、Radiata は何も見えなくなります。つまり入力がなくなり、Windows にはコントローラーが見えているのに「現在のコントローラー」の表示は空になります。Radiata は起動時にこれを検知し、クリックできる画面上の通知からワンクリックでの修正を提示します。修復は登録の問題には有効ですが、あらゆるアクセスの問題が直るわけではありません。[[hidhide-troubleshooting|HidHide のトラブルシューティング]] をご覧ください。",
        ["**Peer controller tools** can remap or hide the controller and create additional outputs. Installed software alone does not establish a conflict; see [[controller-tool-conflicts|reWASD, DS4Windows & other tools]]."] =
            "**他のコントローラーツール**がコントローラーを再割り当てしたり隠したり、追加の出力を作ったりすることがあります。ソフトウェアが入っているというだけでは競合の証拠になりません。[[controller-tool-conflicts|reWASD、DS4Windows などのツール]] をご覧ください。",

        // ── topic:controller-conflict-checklist ──
        ["Controller conflict checklist"] =
            "コントローラー競合チェックリスト",
        ["double input duplicate bleed through wrong pad player slot remote play diagnostic log"] =
            "二重入力 重複 漏れ 誤ったパッド プレイヤースロット リモートプレイ 診断ログ",
        ["Use this sequence when one press moves a menu twice, the game reacts beneath a wheel, the wrong controller responds, or input disappears. Change one thing at a time and test between changes."] =
            "1 回の押下でメニューが 2 つ進む、ホイールの下でゲームが反応する、別のコントローラーが応答する、入力が消える。こうしたときはこの手順を使ってください。一度に 1 つだけ変え、変えるたびに試します。",
        ["**1. Save and exit the game.** Controller mode, Passthru Mode, remapper output changes and reconnects can all replace the device a game is using. Relaunch after the test setup is stable."] =
            "**1. 保存してゲームを終了します。** コントローラーモード、パススルーモード、リマッパーの出力変更、再接続はいずれも、ゲームが使っているデバイスを差し替えることがあります。テスト環境が安定してから起動し直してください。",
        ["**2. Establish a simple baseline.** Temporarily use one controller and one connection (USB, Bluetooth or receiver). Disable other tools' remapping and automatic profile switching; close their tray agents and HidHide configuration windows. For a local test, end unused streaming sessions that create virtual pads."] =
            "**2. 単純な基準状態を作ります。** 一時的にコントローラー 1 台、接続 1 つ（USB、Bluetooth、レシーバーのいずれか）にします。他のツールの再割り当てとプロファイルの自動切り替えを無効にし、それらのトレイ常駐と HidHide の設定ウィンドウを閉じます。ローカルでの確認では、仮想パッドを作る未使用のストリーミングセッションを終了してください。",
        ["**3. Check Radiata first.** Read **Settings ▸ Advanced ▸ Current Controller** and hover its tray icon for isolation status. No controller points to detection or hiding; a working wheel with game input underneath points to isolation or another input path."] =
            "**3. 先に Radiata を確認します。** **設定 ▸ 詳細 ▸ 現在のコントローラー**を読み、トレイアイコンにカーソルを合わせて分離の状態を確認します。コントローラーが出てこないなら検出か隠蔽の問題、ホイールは動くのにその下でゲームが反応するなら分離か別の入力経路の問題です。",
        ["**4. Reconnect in a controlled order.** With the game and competing readers closed, start Radiata, connect the controller, and wait for its status to settle. Start Steam or the launcher afterwards, then the game. A reader that opened the device before cloaking may retain access until it closes or the device reconnects."] =
            "**4. 決めた順序で接続し直します。** ゲームと競合する読み取り側を閉じた状態で Radiata を起動し、コントローラーを接続して、状態が落ち着くまで待ちます。そのあとに Steam やランチャー、続いてゲームを起動します。クロークの前にデバイスを開いた読み取り側は、それが閉じるかデバイスが再接続されるまでアクセスを保持することがあります。",
        ["Start Radiata before anything else that reads the controller, connect the pad and let its status settle, then open Steam or your launcher, and the game last."] =
            "コントローラーを読み取るほかのものより先に Radiata を起動し、コントローラーを接続して状態が落ち着くのを待ってから、Steam やランチャーを開き、最後にゲームを起動します。",
        ["**5. Test in a safe game menu.** Opening a wheel should stop Radiata's virtual pad from driving the game until the wheel closes. Windows' `joy.cpl` can help identify extra controllers, but one entry there does not prove isolation in every game or input API."] =
            "**5. 安全なゲーム内メニューで試します。** ホイールを開いている間は、Radiata の仮想パッドがゲームを操作しない状態になるはずです。Windows の `joy.cpl` は余分なコントローラーの特定に役立ちますが、そこに 1 つ出ているからといって、すべてのゲームや入力 API で分離できている証拠にはなりません。",
        ["**6. Restore other tools one at a time.** The combination that brings back the symptom is useful evidence. Reading a controller and successfully isolating it are separate things: Radiata does not provide a general physical-device-to-XInput-slot picker."] =
            "**6. 他のツールを 1 つずつ戻します。** 症状が再発する組み合わせは有力な手掛かりです。コントローラーを読み取れることと、正しく分離できることは別の話です。Radiata には、物理デバイスと XInput のスロットを対応付ける汎用の機能はありません。",
        ["For comparison, enable [[passthru-mode|Passthru Mode]] before launching the game. Other tools can still hide or remap the device, and wheel input reaching the game is expected in this mode. Disabling the wheels alone does not release capture."] =
            "比較のために、ゲームを起動する前に [[passthru-mode|パススルーモード]] を有効にしてみてください。他のツールが引き続きデバイスを隠したり再割り当てしたりすることはありますし、このモードではホイールの入力がゲームへ届くのが正常です。ホイールを無効にするだけではキャプチャーは解放されません。",
        ["What to include in a support report"] =
            "サポートへの報告に含めるもの",
        ["Record Windows and Radiata versions, controller model and transport, controller mode, tray status, driver versions, other tools and active profiles, startup order, and the first step that changes the result. Include whether input works with Radiata closed and in Passthru Mode."] =
            "Windows と Radiata のバージョン、コントローラーの機種と接続方法、コントローラーモード、トレイの状態、ドライバーのバージョン、他のツールと有効なプロファイル、起動順、そして結果が変わる最初の手順を記録してください。Radiata を閉じた状態とパススルーモードで入力が動くかどうかも添えてください。",
        ["Use **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to Developer…** to prepare a diagnostic ZIP. Review it before attaching it; it may contain device identifiers and personal paths. Include the actual error text from driver setup or HID Diagnostics."] =
            "**設定 ▸ 詳細 ▸ トラブルシューティング ▸ 開発者にログを送信…**で診断用の ZIP を用意できます。添付する前に中身を確認してください。デバイス識別子や個人のパスが含まれることがあります。ドライバーのセットアップや HID 診断で出た実際のエラー文も添えてください。",

        // ── topic:controller-tool-conflicts ──
        ["reWASD, DS4Windows & other controller tools"] =
            "reWASD、DS4Windows などのコントローラーツール",
        ["rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit remapper conflict virtual controller duplicate xbox slot autodetect"] =
            "rewasd ds4windows dsx inputmapper steam input joytokey antimicrox x360ce vjoy hidhide hidguardian scptoolkit リマッパー 競合 仮想コントローラー 重複 xbox スロット 自動検出",
        ["Two tools can read one pad and produce two outputs, or one can hide the pad from the other. Start with one active remapper for that controller. Coexistence depends on versions, hiding rules, transport and game."] =
            "2 つのツールが 1 つのパッドを読んで 2 系統の出力を作ることもあれば、一方がもう一方からパッドを隠すこともあります。そのコントローラーに対して有効なリマッパーは 1 つから始めてください。共存できるかどうかは、バージョン、隠蔽のルール、接続方法、ゲームによって変わります。",
        ["reWASD"] =
            "reWASD",
        ["Turn **Remap OFF** for the affected device or group and pause **Autodetect** for the test. Closing the main window does not necessarily stop mappings. Check the tray agent and confirm its virtual output is gone before retesting. See [reWASD Tray Agent](https://help.rewasd.com/interface/tray-agent.html)."] =
            "対象のデバイスまたはグループで **Remap** をオフにし、テストの間は **Autodetect** を一時停止します。メインウィンドウを閉じても割り当てが止まるとはかぎりません。トレイ常駐を確認し、その仮想出力が消えたことを確かめてから試し直してください。[reWASD のトレイ常駐](https://help.rewasd.com/interface/tray-agent.html) をご覧ください。",
        ["reWASD has its own virtual-device and hiding settings. Repairing ViGEmBus or adding Radiata to HidHide cannot fix every reWASD visibility rule. Record which devices remain visible; consult [reWASD troubleshooting](https://help.rewasd.com/faq/troubleshooting.html) for its own errors."] =
            "reWASD には独自の仮想デバイス設定と隠蔽設定があります。ViGEmBus を修復したり Radiata を HidHide に追加したりしても、reWASD の可視性ルールすべてを直せるわけではありません。どのデバイスが見えたままかを記録し、reWASD 側のエラーについては [reWASD のトラブルシューティング](https://help.rewasd.com/faq/troubleshooting.html) を参照してください。",
        ["DS4Windows, DSX and InputMapper"] =
            "DS4Windows、DSX、InputMapper",
        ["Stop controller output and fully exit the tool, including its tray process, for the baseline. Check automatic startup and profiles if it returns. Another virtual Xbox or DualShock controller can cause duplicate actions or change which device the game selects."] =
            "基準状態を作るため、コントローラー出力を止め、トレイのプロセスも含めてツールを完全に終了します。戻ってくる場合は自動起動とプロファイルを確認してください。別の仮想 Xbox コントローラーや DualShock コントローラーがあると、動作が重複したり、ゲームが選ぶデバイスが変わったりすることがあります。",
        ["If the setup uses HidHide, physical-device blocks can remain after the remapper exits. Check Radiata's access using [[hidhide-troubleshooting|HidHide troubleshooting]]. Radiata does not take ownership of another tool's existing blocks, so quitting Radiata does not clear them."] =
            "構成が HidHide を使っている場合、リマッパーを終了したあとも物理デバイスのブロックが残ることがあります。[[hidhide-troubleshooting|HidHide のトラブルシューティング]] を使って Radiata のアクセスを確認してください。Radiata は他のツールが作った既存のブロックを引き継がないため、Radiata を終了してもそれらは消えません。",
        ["Other sources of input"] =
            "その他の入力源",
        ["**JoyToKey, AntiMicroX, macros and hardware profiles** can emit keyboard or mouse events alongside gamepad input. Neutralizing Radiata's virtual pad does not neutralize those events. Disable the mapping or controller [[turbo-mode|Turbo mode]] for the test."] =
            "**JoyToKey、AntiMicroX、マクロ、ハードウェアのプロファイル**は、ゲームパッドの入力と並んでキーボードやマウスのイベントを送ることがあります。Radiata の仮想パッドを無効化しても、それらのイベントは無効化されません。テストの間は、その割り当てかコントローラーの [[turbo-mode|連射モード]] を無効にしてください。",
        ["**x360ce, vJoy-based tools, streaming clients and vendor utilities** can add controllers or translation layers. Check Steam Remote Play, Sunshine/Moonlight, Parsec and controller software when relevant. End only unused sessions; a remote player's virtual controller may be their only input."] =
            "**x360ce、vJoy 系のツール、ストリーミングのクライアント、メーカー製ユーティリティ**は、コントローラーや変換層を追加することがあります。必要に応じて Steam Remote Play、Sunshine/Moonlight、Parsec、コントローラー用ソフトウェアを確認してください。終了するのは未使用のセッションだけにしてください。離れた場所にいるプレイヤーの仮想コントローラーが、その人の唯一の入力かもしれません。",
        ["**Old HidGuardian or ScpToolkit installations** can leave filtering or replacement drivers behind. Use the original project's removal guidance or support; do not delete arbitrary HID devices, Bluetooth drivers or registry filters. HidHide and HidGuardian are different components."] =
            "**古い HidGuardian や ScpToolkit のインストール**は、フィルタードライバーや置き換えドライバーを残していることがあります。元のプロジェクトの削除手順やサポートを利用してください。HID デバイス、Bluetooth ドライバー、レジストリのフィルターを手当たり次第に削除してはいけません。HidHide と HidGuardian は別のコンポーネントです。",
        ["**Wrong player or no spare Xbox slot?** XInput exposes four slots, which may include virtual pads. Temporarily stop unused virtual outputs and reconnect in the intended order. If Radiata reports uncertainty about its own output, wait for reconnection or quit and reopen Radiata; reinstalling drivers is not the first fix."] =
            "**プレイヤー番号が違う、または Xbox のスロットに空きがありませんか。** XInput は 4 つのスロットを公開し、そこには仮想パッドも含まれることがあります。使っていない仮想出力を一時的に止め、意図した順序で接続し直してください。Radiata が自身の出力について不確実だと報告する場合は、再接続を待つか、Radiata を終了して開き直してください。ドライバーの再インストールは最初の手段ではありません。",
        ["If another remapper is essential, test it with Radiata in [[passthru-mode|Passthru Mode]] first. That avoids a second Radiata stand-in, but it does not promise isolation or preservation of native controller features through the other tool."] =
            "他のリマッパーがどうしても必要なら、まず Radiata を [[passthru-mode|パススルーモード]] にして試してください。Radiata 側の代役パッドが二重になるのは避けられますが、もう一方のツールを通して分離できることや、コントローラーのネイティブ機能が保たれることを約束するものではありません。",

        // ── topic:hidhide-troubleshooting ──
        ["HidHide: contention, lockouts & shared settings"] =
            "HidHide: 競合、ロックアウト、共有設定",
        ["hidhide contention busy access denied configuration client cli lockout whitelist allow list inverse cloak path moved renamed usb bluetooth shared hidden controller recovery"] =
            "hidhide 競合 ビジー アクセス拒否 設定クライアント cli ロックアウト ホワイトリスト 許可リスト 反転 クローク パス 移動 名前変更 usb bluetooth 共有 隠れたコントローラー 復旧",
        ["Busy or access-failed status"] =
            "ビジー、またはアクセス失敗の状態",
        ["The **HidHide Configuration Client** holds the driver's exclusive configuration connection while it's open. A running or stuck **HidHideCLI** can also contend with Radiata. Close those tools completely, then allow about **15 seconds** for Radiata's retry before trying **Recover Controller**. Contention does not mean the driver needs reinstalling."] =
            "**HidHide 設定クライアント**は、開いている間ドライバーの排他的な設定接続を占有します。動作中または固まった **HidHideCLI** も Radiata と競合することがあります。それらのツールを完全に閉じ、**コントローラーを復旧**を試す前に Radiata の再試行のために **15 秒**ほど待ってください。競合していることは、ドライバーの再インストールが必要という意味ではありません。",
        ["Windows sees the controller, but Radiata does not"] =
            "Windows にはコントローラーが見えるのに Radiata には見えない",
        ["Close the game and quit Radiata before inspecting HidHide. In normal mode, the **Applications** list grants access to hidden controllers. Verify the exact `Radiata.exe` you launch is listed - installed, portable, renamed and moved copies all have different paths. Radiata normally registers itself; **Install/Repair Drivers…** can repair registration, subject to its reported result."] =
            "HidHide を調べる前に、ゲームを閉じて Radiata を終了してください。通常モードでは、**Applications** の一覧が隠されたコントローラーへのアクセスを許可します。実際に起動している `Radiata.exe` がそのまま一覧にあるか確認してください。インストール版、ポータブル版、名前を変えたもの、移動したものは、いずれもパスが異なります。Radiata は通常みずから登録しますが、**ドライバーをインストール／修復…**でも登録を修復できます。結果の表示に従ってください。",
        ["Check the selected physical device on **Devices**. USB and Bluetooth can have separate entries. Do not hide the virtual stand-in the game needs. Use [Nefarius's setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) to identify the device, and close the client before restarting Radiata."] =
            "**Devices** で選択されている物理デバイスを確認してください。USB と Bluetooth で別々の項目になることがあります。ゲームが必要としている仮想の代役を隠してはいけません。デバイスの特定には [Nefarius のセットアップガイド](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/) を使い、Radiata を再起動する前にクライアントを閉じてください。",
        ["**Inverse application cloak reverses the list's meaning.** Radiata preserves this shared setting and declines capture when it is enabled. Record the configuration and coordinate with the tool that needs it before choosing normal mode for Radiata; changing it affects other applications too."] =
            "**アプリケーションの反転クロークは一覧の意味を逆転させます。** Radiata はこの共有設定をそのまま維持し、有効な間はキャプチャーを行いません。Radiata を通常モードにする前に、設定を記録し、それを必要としているツールと調整してください。変更は他のアプリケーションにも影響します。",
        ["The game still receives input"] =
            "それでもゲームに入力が届く",
        ["An application allowed through HidHide can still read the physical device. Review entries deliberately; do not add the game, Steam, or every executable as a general fix for double input."] =
            "HidHide で許可されたアプリケーションは、物理デバイスを読み取れたままです。項目は慎重に見直してください。二重入力の一般的な対処としてゲームや Steam、あらゆる実行ファイルを追加してはいけません。",
        ["After a fresh install, reconnect the controller or restart Windows if requested, so the filter can attach. Restart readers that opened the device before cloaking. Configuration readback alone does not verify what a running game receives."] =
            "新規インストール後は、求められた場合はコントローラーを接続し直すか Windows を再起動して、フィルターが取り付けられるようにしてください。クロークの前にデバイスを開いていた読み取り側は再起動してください。設定を読み返しただけでは、動作中のゲームが何を受け取っているかの確認にはなりません。",
        ["HidHide has limitations, including some Raw Input readers and Xbox/XInput configurations. If leakage survives a clean startup, record the game and transport rather than assuming a successful hide operation guarantees exclusive input. See the [HidHide FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/)."] =
            "HidHide には、一部の Raw Input の読み取り側や Xbox/XInput の構成を含め、限界があります。クリーンな起動をしても漏れが残る場合は、隠蔽の操作が成功したから入力が占有できているはずだと考えるのではなく、ゲームと接続方法を記録してください。[HidHide の FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/) をご覧ください。",
        ["The controller stays hidden after exit"] =
            "終了後もコントローラーが隠れたままになる",
        ["Radiata adopts existing hidden-device entries matching the controller model it manages, including entries from another connection method, so they can be released on exit. This can also release another tool's matching entries; you should never have two tools manage hiding for the same controller. Uninstall clears all hidden-device entries unless another Radiata copy is running. A failed release must be recovered before removal can finish."] =
            "Radiata は、自身が管理しているコントローラーの機種に一致する既存の隠しデバイス項目を、別の接続方法によるものも含めて引き受けます。終了時に解除できるようにするためです。これは他のツールの一致する項目まで解除してしまうことがあります。同じコントローラーの隠蔽を 2 つのツールに管理させてはいけません。アンインストールでは、別の Radiata のコピーが動作していないかぎり、すべての隠しデバイス項目が消去されます。解除に失敗した場合は、それを復旧しないと削除を完了できません。",
        ["Keep keyboard and mouse access available while changing controller visibility. Record existing settings first, change only the identified controller or application entry, and close the configuration client before retesting."] =
            "コントローラーの可視性を変更している間は、キーボードとマウスを使える状態にしておいてください。まず既存の設定を記録し、特定したコントローラーまたはアプリケーションの項目だけを変更し、試し直す前に設定クライアントを閉じてください。",

        // ── topic:driver-conflicts ──
        ["HP OMEN & driver version conflicts"] =
            "HP OMEN とドライバーのバージョン競合",
        ["hp omen gaming hub fusion vigem vigembus foreign fork driver version mismatch 10.x 1.22.0 1.5.230 oculus virtual desktop repair install bus device manager restart"] =
            "hp omen gaming hub fusion vigem vigembus 他社 フォーク ドライバー バージョン 不一致 10.x 1.22.0 1.5.230 oculus virtual desktop 修復 インストール バス デバイスマネージャー 再起動",
        ["**ViGEmBus creates the virtual controller; HidHide controls access to the physical one.** A working driver of one kind does not establish that the other works. Check the driver result log and Radiata's isolation status before repeating an installer."] =
            "**ViGEmBus が仮想コントローラーを作り、HidHide が物理コントローラーへのアクセスを制御します。** 片方のドライバーが動いているからといって、もう片方が動いている証拠にはなりません。インストーラーを繰り返す前に、ドライバーの結果ログと Radiata の分離状態を確認してください。",
        ["HP OMEN Gaming Hub / OMEN Fusion"] =
            "HP OMEN Gaming Hub / OMEN Fusion",
        ["Some HP OMEN systems have a vendor-modified ViGEmBus, and Radiata can connect to that bus instead of the correct one. A reported **10.x** version can be HP's old fork, not a newer compatible Nefarius driver."] =
            "一部の HP OMEN 搭載機にはメーカーが改変した ViGEmBus が入っており、Radiata が正しいほうではなくそちらのバスに接続してしまうことがあります。報告される **10.x** というバージョンは、新しい互換性のある Nefarius のドライバーではなく、HP の古いフォークである可能性があります。",
        ["Radiata names detected foreign buses and skips installing over them. Its driver removal also leaves another program's bus in place. Repeated **Install/Repair Drivers…** attempts will not switch an HP-owned bus to Nefarius's."] =
            "Radiata は検出した他社製のバスの名前を挙げ、その上から上書きインストールすることはしません。ドライバーの削除でも、他のプログラムのバスはそのまま残します。**ドライバーをインストール／修復…**を何度試しても、HP が所有するバスが Nefarius のものに入れ替わることはありません。",
        ["Follow [Nefarius's HP OMEN guidance](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) and its linked [HP issue and switching instructions](https://github.com/nefarius/ViGEmBus/issues/99), or contact HP. Disabling the vendor bus can break dependent OMEN features; Radiata does not perform the device or registry changes for you."] =
            "[Nefarius による HP OMEN の案内](https://docs.nefarius.at/projects/ViGEm/How-to-Install/#vigembus-issues-in-hp-omen-laptops) と、そこからリンクされた [HP の問題と切り替え手順](https://github.com/nefarius/ViGEmBus/issues/99) に従うか、HP に問い合わせてください。メーカー製のバスを無効にすると、それに依存する OMEN の機能が壊れることがあります。Radiata が代わりにデバイスやレジストリの変更を行うことはありません。",
        ["A foreign bus that refuses a virtual DualShock 4 may make Radiata fall back to an **Xbox 360 stand-in for that session**. Unexpected Xbox prompts can therefore be a driver clue rather than a changed glyph setting. This fallback is not a compatibility guarantee."] =
            "仮想の DualShock 4 を受け付けない他社製バスがあると、Radiata が**そのセッションの間だけ Xbox 360 の代役**に切り替わることがあります。そのため、思いがけず Xbox のボタン表示になったときは、グリフ設定が変わったのではなくドライバーの手掛かりである場合があります。この切り替えは互換性を保証するものではありません。",
        ["Versions and duplicate buses"] =
            "バージョンとバスの重複",
        ["This Radiata build bundles **ViGEmBus 1.22.0** and **HidHide 1.5.230**. ViGEmBus is retired; 1.22.0 is its final official release. Installer, application, client-library and driver versions are different numbers - they are not supposed to match each other."] =
            "この Radiata のビルドには **ViGEmBus 1.22.0** と **HidHide 1.5.230** が同梱されています。ViGEmBus は開発終了で、1.22.0 が最後の公式リリースです。インストーラー、アプリケーション、クライアントライブラリ、ドライバーのバージョンはそれぞれ別の番号であり、一致するようにはなっていません。",
        ["Open **Device Manager ▸ View ▸ Devices by connection** and inspect virtual gamepad bus entries. Record each bus's name, provider, driver version and device status. Multiple buses, unexpected providers, or a version different from the one Radiata bundles all warrant investigation; a higher number alone does not prove compatibility."] =
            "**デバイスマネージャー ▸ 表示 ▸ 接続別デバイス**を開き、仮想ゲームパッドのバスの項目を確認してください。各バスの名前、提供元、ドライバーのバージョン、デバイスの状態を記録します。バスが複数ある、提供元が想定外である、Radiata に同梱されているものと違うバージョンである。いずれも調べる価値があります。番号が大きいというだけでは互換性の証拠になりません。",
        ["**Oculus and Virtual Desktop** setups can also supply their own buses. Several virtual gamepads beneath one bus are different from several competing bus drivers. Identify the owning program before changing either."] =
            "**Oculus や Virtual Desktop** の構成も独自のバスを持ち込むことがあります。1 つのバスの下に仮想ゲームパッドが複数ある状態と、競合するバスドライバーが複数ある状態は別物です。どちらかを変更する前に、所有しているプログラムを特定してください。",
        ["For an ordinary missing or older bundled driver, use **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers…**, review its result, and complete any requested restart. Close HidHide tools first. If repair fails, keep the error text and driver versions for support."] =
            "同梱ドライバーが単に入っていない、または古いだけの場合は、**設定 ▸ 詳細 ▸ トラブルシューティング ▸ ドライバーをインストール／修復…**を使い、その結果を確認して、求められた再起動を行ってください。先に HidHide 関連のツールを閉じてください。修復に失敗した場合は、エラー文とドライバーのバージョンをサポート用に控えておいてください。",
        ["Driver removal affects every application using that shared component. Use the [official ViGEmBus install/remove guide](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) for a confirmed conflict. Its full purge is advanced recovery, not a first step for double input. Do not force-delete unrelated drivers or use unofficial download sites."] =
            "ドライバーの削除は、その共有コンポーネントを使うすべてのアプリケーションに影響します。競合が確認できた場合は [ViGEmBus の公式インストール・削除ガイド](https://docs.nefarius.at/projects/ViGEm/How-to-Install/) を使ってください。そこにある完全な消去は高度な復旧手段であり、二重入力に対する最初の一手ではありません。無関係なドライバーを強制削除したり、非公式のダウンロードサイトを使ったりしないでください。",

        // ── topic:overlay-not-visible ──
        ["Wheel not visible over a game"] =
            "ゲームの上にホイールが表示されない",
        ["overlay fullscreen borderless windowed exclusive primary display monitor uac"] =
            "オーバーレイ 全画面 ボーダーレス ウィンドウ 排他 メインディスプレイ モニター uac",
        ["**Run games Borderless Windowed**, not exclusive fullscreen. Exclusive fullscreen bypasses the compositor Radiata draws through. The setting is in most games' display options, and the performance difference on Windows 10/11 is negligible."] =
            "**ゲームはボーダーレスウィンドウで実行してください。** 排他的フルスクリーンは避けます。排他的フルスクリーンは Radiata が描画に使っているコンポジターを迂回してしまいます。設定はたいていのゲームの表示オプションにあり、Windows 10 と 11 での性能差はごくわずかです。",
        ["Radiata draws on the **primary display only** - on a multi-monitor rig, make your gaming display the Windows primary (Settings ▸ System ▸ Display)."] =
            "Radiata が描画するのは**メインディスプレイのみ**です。マルチモニター環境では、ゲーム用のディスプレイを Windows のメインディスプレイに設定してください（設定 ▸ システム ▸ ディスプレイ）。",
        ["Windows-secured screens (UAC prompts, the lock screen) can never be drawn over. That's a Windows limitation, and nothing can work around it."] =
            "Windows が保護している画面（UAC のダイアログ、ロック画面）の上には描画できません。これは Windows の制限で、回避する方法はありません。",

        // ── topic:steam-conflicts ──
        ["Steam Input & Steam quirks"] =
            "Steam Input と Steam の癖",
        ["steam playstation controller support big picture guide magnifier chord double input unlock controller"] =
            "steam playstation コントローラーサポート big picture guide 拡大鏡 組み合わせ 二重入力 コントローラーのロック解除",
        ["Steam Input can translate a controller into gamepad, keyboard or mouse input. Another mapping layer can change prompts and bindings or create duplicate actions. Test per game before changing global settings."] =
            "Steam Input はコントローラーをゲームパッド、キーボード、マウスの入力に変換できます。別の割り当て層があると、ボタン表示や割り当てが変わったり、動作が重複したりすることがあります。全体設定を変える前に、ゲームごとに試してください。",
        ["**Wrong buttons or duplicate actions?** With the game closed, open its **Steam Library ▸ Properties ▸ Controller** and try **Disable Steam Input** in the per-game override. Relaunch and compare; restore the previous setting if the game or remote setup needs Steam Input. Global options are under **Steam ▸ Settings ▸ Controller**, with names that vary by Steam version."] =
            "**ボタンが違う、動作が重複しますか。** ゲームを閉じた状態で **Steam ライブラリ ▸ プロパティ ▸ コントローラ**を開き、ゲームごとの上書きで **Steam Input を無効にする**を試してください。起動し直して比べ、ゲームやリモート構成が Steam Input を必要とする場合は元の設定に戻します。全体のオプションは **Steam ▸ 設定 ▸ コントローラ**にあり、名称は Steam のバージョンによって異なります。",
        ["**No input with Steam Input disabled?** The game may not support Radiata's virtual DualShock controller. For a Sony pad, try [[controller-mode|Xbox Mode]] before launching, or restore Steam Input. That's a game compatibility choice, not necessarily a driver failure."] =
            "**Steam Input を無効にすると入力がなくなりますか。** そのゲームが Radiata の仮想 DualShock コントローラーに対応していない可能性があります。Sony のパッドなら、起動前に [[controller-mode|Xbox モード]] を試すか、Steam Input を戻してください。これはゲームの互換性の問題であって、必ずしもドライバーの不具合ではありません。",
        ["**Double input despite a successful cloak?** Steam may have opened the physical controller before Radiata hid it. Save and close Steam games before fully exiting Steam, then start Radiata and let capture settle before reopening Steam. Reconnecting the pad can also release stale handles. Follow any controller-unblock notice; closing Steam's window alone may leave it running."] =
            "**クロークに成功しているのに二重入力になりますか。** Radiata が隠す前に Steam が物理コントローラーを開いていた可能性があります。Steam を完全に終了する前に Steam のゲームを保存して閉じ、そのあと Radiata を起動してキャプチャーが落ち着くのを待ってから Steam を開き直してください。パッドを接続し直すことでも古いハンドルが解放されることがあります。コントローラーのブロック解除の通知が出たら従ってください。Steam はウィンドウを閉じただけでは動作し続けることがあります。",
        ["**Desktop keys or mouse movement?** Check Steam's **Desktop Layout** and **Guide Button Chord** layout as well as the game's layout. These can emit input outside the game. See [[controller-conflict-checklist|Controller conflict checklist]] for a controlled comparison."] =
            "**デスクトップのキー操作やマウスの動きが出ますか。** ゲームのレイアウトに加えて、Steam の**デスクトップレイアウト**と **Guide ボタンのコード**のレイアウトも確認してください。これらはゲームの外へ入力を送ることがあります。条件をそろえて比べるには [[controller-conflict-checklist|コントローラー競合チェックリスト]] をご覧ください。",
        ["**Steam Remote Play may rely on Steam Input.** Keep a local fallback before changing its input path. Valve explains the translation layer in [Steam Input gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)."] =
            "**Steam Remote Play は Steam Input に依存している場合があります。** その入力経路を変える前に、ローカルで使える手段を残しておいてください。Valve による変換層の説明は [Steam Input のゲームパッドエミュレーション](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices) にあります。",
        ["**Windows Magnifier opens by itself?** That's Steam's *Guide Button Chord* layout (Guide + face button), not Radiata - and powering a pad off by holding the PS button can leave that layout latched. One clean Guide press-and-release clears it; disable it under Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout."] =
            "**Windows の拡大鏡が勝手に開く場合は？** それは Radiata ではなく、Steam の *Guide Button Chord* レイアウト（Guide + フェイスボタン）です。PS ボタンを長押ししてコントローラーの電源を切ると、このレイアウトが掛かったままになることがあります。Guide ボタンを一度きちんと押して離せば解除されます。無効にするには Steam ▸ Settings ▸ Controller ▸ Non-Game Controller Layouts ▸ Guide Button Chord layout を開いてください。",

        // ── topic:turbo-mode ──
        ["Controller Turbo / rapid-fire mode"] =
            "コントローラーの連射（ターボ）機能",
        ["turbo rapid fire auto repeat macro cycling flicker wheel closes dismiss premature bounce double input jitter"] =
            "ターボ 連射 自動繰り返し マクロ ちらつき ホイールが閉じる 早すぎる 二重入力",
        ["Many third-party pads have a hardware **Turbo** / rapid-fire mode that auto-repeats a held button. An accidental button combo can toggle it on in the controller firmware, and that can look exactly like a bug:"] =
            "サードパーティ製のパッドの多くには、押しっぱなしのボタンを自動で連打するハードウェアの **Turbo**（連射）モードがあります。ボタンの組み合わせを誤って押すとコントローラーのファームウェア側でこれが入ってしまい、まるで不具合のように見えることがあります。",
        ["The wheel **flickers open and shut**, or **dismisses on its own** right after opening."] =
            "ホイールが**開いたり閉じたりを繰り返す**、あるいは開いた直後に**勝手に閉じる**。",
        ["The Game Grid's **filters cycle rapidly**, or the selection jumps on its own."] =
            "ゲームグリッドの **絞り込みが高速で切り替わる**、または選択が勝手に移動する。",
        ["Slices **fire the instant a wheel opens**, or a hold-to-confirm slice never settles."] =
            "**ホイールが開いた瞬間に項目が実行される**、または長押しで確認する項目が完了しない。",
        ["Turn Turbo off on the controller itself - usually a button combo (often Home/Guide + a face or shoulder button, or a dedicated Turbo button), frequently with its own LED. Check your pad's manual for the exact combo."] =
            "Turbo はコントローラー本体でオフにしてください。多くはボタンの組み合わせ（Home/Guide とフェイスボタンまたはショルダーボタン、あるいは専用の Turbo ボタン）で切り替え、専用の LED を備えていることもよくあります。正確な組み合わせはコントローラーの説明書をご確認ください。",
        ["If it persists with Turbo confirmed off, it's something else - see [[opening-a-wheel|Opening a wheel]] and [[picking-an-action|Aiming & firing]]."] =
            "Turbo がオフであることを確認しても現象が続く場合は、別の原因です。[[opening-a-wheel|ホイールを開く]] と [[picking-an-action|照準と実行]] をご覧ください。",

        // ── topic:common-issues ──
        ["Other common issues"] =
            "その他のよくある問題",
        ["hdr unavailable rdp remote play streaming config json double input"] =
            "hdr 利用不可 rdp remote play 配信 config json 二重入力",
        ["**HDR shows `Unavailable`** - the display state can't be read in that context, such as in Remote Play or streaming."] =
            "**HDR が `Unavailable`（利用不可）と表示される**: Remote Play や配信中など、その状況ではディスプレイの状態を読み取れません。",
        ["**Editing `config.json` by hand** (`%APPDATA%\\Radiata`) - supported. The app hot-reloads its own writes reliably, but outside edits are occasionally missed, so restart Radiata after manual edits."] =
            "**`config.json` を手で編集すること**（`%APPDATA%\\Radiata`）はサポートしています。アプリは自身が書き込んだ内容は確実にホットリロードしますが、外部からの編集は見落とすことがあるので、手で編集したあとは Radiata を再起動してください。",

        // ── figure ──
        ["Isolated"] =
            "隔離あり",
        ["Your controller"] =
            "コントローラー",
        ["Virtual pad"] =
            "仮想コントローラー",
        ["The game"] =
            "ゲーム",
        ["cloaked"] =
            "クローク中",
        ["Passthru Mode, or no drivers"] =
            "パススルーモード、またはドライバーなし",
        ["no virtual pad"] =
            "仮想コントローラーなし",
        ["{cross} picks the slice up"] =
            "{cross} でスライスを持ち上げる",
        ["Aim to the target slot"] =
            "置きたい位置に狙いを定める",
        ["{cross} drops it — the ring reflows"] =
            "{cross} で置くとリングが並び直る",
        ["Its chord"] =
            "組み合わせ",
        ["Slices on it — this wheel opens."] =
            "スライスがある側。このホイールは開きます。",
        ["No slices — this wheel draws nothing, so its chord reaches the game."] =
            "スライスがない側。何も描画されないため、その組み合わせはゲームに届きます。",
        ["Modifiers — held around it"] =
            "修飾キー。その間押し続けられます",
        ["The key that's pressed"] =
            "実際に押されるキー",
        ["Steam or your launcher"] =
            "Steam またはランチャー",
        ["Game and other tools closed"] =
            "ゲームとほかのツールは終了済み",
        ["Wait for its status to settle"] =
            "状態が落ち着くまで待つ",
        ["Make the folder"] =
            "フォルダーを作成",
        ["Write the manifest"] =
            "マニフェストを書く",
        ["Restart Radiata"] =
            "Radiata を再起動",
        ["Accept the confirmation"] =
            "確認を承諾",
        ["Try it out"] =
            "試す",
        ["Change something"] =
            "何かを変更",
        ["clockwise"] =
            "時計回り",
        ["Radiata finds it"] =
            "Radiata が見つけられる",
        ["One folder too many"] =
            "フォルダーが 1 段多い",
        ["Move the files up a level"] =
            "ファイルを 1 段上へ移動",
        ["custom material theme package material.json drop in author make build own skin palette colors colours gradient fill hue walk outline glyph glow texture png jpg sound wav appdata packages folder soundtheme sound set kawaii mesa salvage reactor obsidian digital physical format token consent confirm restart trace log rejected not showing workshop sample starter ember comments drag drop zip install uninstall remove delete recycle bin right-click"] =
            "カスタム マテリアル テーマ パッケージ material.json ドロップ 追加 作者 作成 自作 スキン パレット 色 グラデーション 塗り 色相 輪郭 グリフ 発光 テクスチャ png jpg サウンド wav appdata packages フォルダー サウンドテーマ サウンドセット kawaii mesa salvage reactor obsidian デジタル 物理 フォーマット トークン 同意 確認 再起動 トレース ログ 拒否 表示されない ワークショップ サンプル starter ember コメント ドラッグ ドロップ zip インストール アンインストール 削除 ごみ箱 右クリック",
        ["**Restart Radiata after adding a theme to that folder by hand, or changing one.** Packages are scanned once, at startup, on purpose."] =
            "**フォルダーに手作業でテーマを追加したり、テーマを変更したりした場合は、Radiata を再起動してください。** パッケージは意図的に、起動時に一度だけ読み込まれます。",
        ["**Or drag and drop it.** Drop the theme's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. What you dropped stays where it was."] =
            "**ドラッグ＆ドロップでも追加できます。** テーマのフォルダー（またはその ZIP）を **設定** ウィンドウのどこかや、`Radiata.exe` またはそのショートカットにドロップします。Radiata が所定の場所にコピーし、確認を求めます。再起動は不要です。ドロップした元のファイルはそのまま残ります。",
        ["Dropping a theme you already have asks before replacing it; the old copy goes to the **Recycle Bin**. A changed version takes effect after a restart."] =
            "すでにあるテーマをドロップすると、置き換える前に確認します。古いコピーは **ごみ箱** に移動します。変更したバージョンは再起動後に反映されます。",
        ["**To remove a theme,** right-click its tile in **Settings ▸ Customize** and choose **Uninstall theme**. Its folder goes to the Recycle Bin; if it was your material, the wheel switches to **Pearl**. Restore the folder from the Recycle Bin and restart Radiata to get it back."] =
            "**テーマを削除するには**、**設定 ▸ カスタマイズ** でそのタイルを右クリックし、**テーマをアンインストール** を選びます。フォルダーはごみ箱に移動し、そのテーマを使っていた場合はホイールが **パール** に切り替わります。元に戻すには、ごみ箱からフォルダーを復元して Radiata を再起動します。",
        ["custom arcade game package game.json javascript js script write author make build own sandbox jint helper polar disc draw tick input kv hiscore cue sound appdata packages folder restart consent confirm trace log rejected not showing workshop sample firefly strict console error budget tint colour color cabinet preview screenshot nameplate launcher drag drop zip install"] =
            "カスタム アーケード ゲーム パッケージ game.json javascript js スクリプト 作成 作者 自作 サンドボックス jint ヘルパー 極座標 ディスク 描画 tick 入力 kv hiscore キュー サウンド appdata packages フォルダー 再起動 同意 確認 トレース ログ 拒否 表示されない ワークショップ サンプル firefly 厳格 コンソール エラー 予算 色合い 色 筐体 プレビュー スクリーンショット ネームプレート ランチャー ドラッグ ドロップ zip インストール",
        ["**Or drag and drop it.** Drop the game's folder, or a ZIP of it, anywhere on the **Settings** window, or onto `Radiata.exe` or a shortcut to it. Radiata copies it into place and asks you to confirm - no restart. A game you already have asks before replacing it, and a changed version takes effect after a restart."] =
            "**ドラッグ＆ドロップでも追加できます。** ゲームのフォルダー（またはその ZIP）を **設定** ウィンドウのどこかや、`Radiata.exe` またはそのショートカットにドロップします。Radiata が所定の場所にコピーし、確認を求めます。再起動は不要です。すでにあるゲームは置き換える前に確認し、変更したバージョンは再起動後に反映されます。",
        ["**Restart Radiata** after adding a game to that folder by hand, or changing one, then accept the confirmation. It shows up in the Arcade picker beside the built-in games, and as a choice when you add an **Arcade** slice. In your config it's the token `pkg-<id>`."] =
            "フォルダーに手作業でゲームを追加したり、ゲームを変更したりした場合は、**Radiata を再起動**して確認に同意してください。組み込みゲームと並んでアーケードの選択画面に表示され、**アーケード** スライスを追加するときの選択肢にもなります。設定内ではトークン `pkg-<id>` になります。",
        ["workshop test testing debug debugging share sharing zip unzip send friend install license trace log skipped error not showing missing folder nested backup move another pc drag drop"] =
            "ワークショップ テスト 試験 デバッグ 共有 zip 展開 送る 友達 インストール ライセンス トレース ログ スキップ エラー 表示されない 見つからない フォルダー 入れ子 バックアップ 別の PC 移行 ドラッグ ドロップ",
        ["To share a package, zip the files **inside** its folder (not the folder itself) and name the zip after the package. The person installing it drops the zip onto Radiata's **Settings** window and accepts the confirmation - or uses **Extract All** into their own Materials or Arcade Games folder and restarts Radiata."] =
            "パッケージを共有するには、フォルダー自体ではなく、フォルダー**の中**のファイルを ZIP に圧縮し、ZIP にパッケージ名を付けます。インストールする側は、その ZIP を Radiata の **設定** ウィンドウにドロップして確認に同意するか、自分の Materials または Arcade Games フォルダーに **すべて展開** して Radiata を再起動します。",
        ["Radiata is a feature-rich, controller-based radial menu utility for a Windows gaming PC. Launch a game, join the Discord call, swap to headphones, start your stream, all from a controller."] =
            "Radiata は Windows ゲーミング PC 向けの、多機能なコントローラーベースのラジアルメニューユーティリティです。ゲームの起動、Discord の通話への参加、ヘッドホンへの切り替え、配信の開始まで、すべてコントローラーから行えます。",
        ["Try it now: open a wheel with **{invoke}**."] =
            "さっそく試してみましょう。次の操作でホイールを開きます: **{invoke}**。",
        ["**Game Grid** - a universal launcher for every installed game across Steam, Epic, Playnite, GOG, Xbox, Battle.net, Amazon, itch, Ubisoft and EA."] =
            "**ゲームグリッド** — Steam、Epic、Playnite、GOG、Xbox、Battle.net、Amazon、itch、Ubisoft、EA にインストール済みのすべてのゲームに使える、汎用のランチャーです。",
        ["**Nothing hooked, nothing injected** - Radiata reads your controller directly and allows your input through only when a wheel isn't up, so the game underneath doesn't pick up duplicate input. For strict-anticheat titles, see [[passthru-mode|Passthru Mode]]."] =
            "**フックもインジェクションもなし** — Radiata はコントローラーを直接読み取り、ホイールが開いていないときだけ入力をそのまま通すので、下のゲームが入力を二重に拾うことはありません。アンチチートの厳しいタイトルについては [[passthru-mode|パススルーモード]] をご覧ください。",
        ["Start with [[opening-a-wheel|Opening a wheel]]. Then open one and click the aiming stick (L3/R3) when you're ready to start editing."] =
            "まずは [[opening-a-wheel|ホイールを開く]] から始めてください。次にホイールを開き、編集を始める準備ができたら照準スティック（L3/R3）をクリックします。",
        ["Radiata ships as a single installer, **Radiata-<version>-setup.exe**. Download it from [getradiata.app](https://getradiata.app)."] =
            "Radiata は単一のインストーラー **Radiata-<version>-setup.exe** として配布されています。[getradiata.app](https://getradiata.app) からダウンロードしてください。",
        ["**Only download Radiata from known sources.** Anything else claiming to be Radiata isn't from the developer."] =
            "**Radiata は信頼できる提供元からのみダウンロードしてください。** それ以外で Radiata を名乗るものは開発者によるものではありません。",
        ["**Administrator account needed** for driver installation (optional but strongly recommended)"] =
            "ドライバーのインストールには**管理者アカウントが必要です**（任意ですが、強くお勧めします）。",
        ["Windows SmartScreen may show a blue **\"Windows protected your PC\"** box the first time you run the installer, and your browser may warn that the file **\"isn't commonly downloaded\"**."] =
            "インストーラーを初めて実行すると、Windows SmartScreen が青い**「Windows によって PC が保護されました」**のボックスを表示することがあり、ブラウザーもそのファイルが**「一般的にダウンロードされていません」**と警告することがあります。",
        ["**No Run anyway button at all?** A managed or locked-down PC can have SmartScreen set to block outright. Radiata can't work around it."] =
            "**実行ボタンがまったく表示されない場合**、管理された PC やロックダウンされた PC では SmartScreen が完全ブロックに設定されていることがあります。Radiata が回避することはできません。",
        ["You can confirm you have the genuine file before running it: every GitHub release lists the installer's **SHA-256**, and `Get-FileHash .\\Radiata-<version>-setup.exe` in PowerShell should print the same value. Radiata's updater automatically runs the same verification check on every update."] =
            "実行前に本物のファイルかどうかを確認できます。GitHub の各リリースにはインストーラーの **SHA-256** が記載されており、PowerShell で `Get-FileHash .\\Radiata-<version>-setup.exe` を実行すると同じ値が表示されるはずです。Radiata のアップデーターは、更新のたびに同じ検証を自動で行います。",
        ["**Antivirus false positives** happen for the same reason. If yours quarantines the installer, restore it and run it again, or download it fresh from getradiata.app."] =
            "**ウイルス対策ソフトの誤検知**も同じ理由で起こります。インストーラーが隔離された場合は、復元してもう一度実行するか、getradiata.app から新たにダウンロードしてください。",
        ["Installs **for your user account only**, into `%LOCALAPPDATA%\\Programs\\Radiata`. It never touches other accounts on the PC."] =
            "`%LOCALAPPDATA%\\Programs\\Radiata` に**現在のユーザーアカウント専用**としてインストールします。PC 上の他のアカウントには一切触れません。",
        ["Adds a **Start menu** shortcut, and on a **first** install sets Radiata to **start with Windows**. You can turn that off in the tray menu or **Settings ▸ Advanced**."] =
            "**スタートメニュー**のショートカットを追加し、**初回**インストール時には Radiata を **Windows と同時に起動**するよう設定します。トレイメニューまたは **設定 ▸ 詳細** で無効にできます。",
        ["Nothing sneaky or malicious comes with Radiata. Radiata is GPLv3 free software."] =
            "Radiata には、怪しいものや悪意のあるものは一切付属しません。Radiata は GPLv3 のフリーソフトウェアです。",
        ["Installing over an existing copy is an **upgrade in place**. Your wheels, settings and game art are left alone."] =
            "既存のコピーに上書きインストールすると**その場でのアップグレード**になります。ホイール、設定、ゲームアートはそのまま残ります。",
        ["Driver prompts (UAC prompts)"] =
            "ドライバーの確認（UAC ダイアログ）",
        ["Radiata installs without admin rights; Windows asks for permission when you install the controller drivers. Leave **Install drivers (recommended)** selected and approve the Windows prompts that follow. **ViGEmBus** and **HidHide** are the open-source drivers that keep duplicate controller input out of the game. See [[input-isolation|Input isolation]]."] =
            "Radiata は管理者権限なしでインストールされ、コントローラー用ドライバーをインストールするときに Windows が許可を求めます。**ドライバーをインストール（推奨）** を選択したままにして、続いて表示される Windows の確認を承認してください。**ViGEmBus** と **HidHide** は、コントローラー入力の二重化をゲームに届かないようにするオープンソースのドライバーです。[[input-isolation|入力の分離]] をご覧ください。",
        ["**Declining is safe.** Radiata still works; games just also see your controller while a wheel is open, which is annoying. Install them later any time from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**."] =
            "**拒否しても安全です。** Radiata は引き続き動作しますが、ホイールが開いている間はゲームにもコントローラー入力が届き、邪魔になります。後からいつでも **設定 ▸ 詳細 ▸ トラブルシューティング ▸ ドライバーをインストール/修復** からインストールできます。",
        ["The drivers are shared system components other tools may also use, so if you uninstall Radiata, removing the drivers as well is optional."] =
            "このドライバーは他のツールも使う共有システムコンポーネントなので、Radiata をアンインストールするときにドライバーも削除するかどうかは任意です。",
        ["Radiata then lives in the **system tray**. Click the icon for Settings or right-click for the tray menu. Then start at [[opening-a-wheel|Opening a wheel]]."] =
            "その後、Radiata は**システムトレイ**に常駐します。アイコンをクリックすると設定が開き、右クリックするとトレイメニューが開きます。次は [[opening-a-wheel|ホイールを開く]] から始めてください。",
        ["**Updates are discovered automatically** by default. You can manually check for updates at **Settings ▸ Advanced ▸ Check for Updates**. Radiata always verifies the download before running it."] =
            "**更新は既定で自動的に検出されます。** 手動で確認するには **設定 ▸ 詳細 ▸ 更新を確認** を使います。Radiata はダウンロードを実行前に必ず検証します。",
        ["**Uninstall** from **Settings ▸ Advanced ▸ Troubleshooting ▸ Uninstall Radiata…**, or from Windows' **Installed apps** list. Your settings and the shared drivers can be removed in the same step."] =
            "**アンインストール**は **設定 ▸ 詳細 ▸ トラブルシューティング ▸ Radiata をアンインストール…** から、または Windows の**インストールされているアプリ**の一覧から行えます。設定と共有ドライバーも同じ手順で削除できます。",
        ["Your current setting: open a wheel with **{invoke}**."] =
            "現在の設定では、次の操作でホイールを開きます: **{invoke}**。",
        ["Set your own chords (button combos that open a wheel) in [[triggers|Settings ▸ Customize ▸ Triggers]]."] =
            "独自の組み合わせ（ホイールを開くボタンの組み合わせ）は [[triggers|設定 ▸ カスタマイズ ▸ 呼び出し操作]] で設定できます。",
        ["**Flip mid-gesture:** while holding a chord, tap the opposite bumper or trigger to switch to the other wheel without having to re-input the entire chord."] =
            "**操作の途中で切り替える:** 組み合わせを押し続けたまま、反対側のバンパーまたはトリガーをタップすると、組み合わせ全体を入力し直さなくても、もう一方のホイールに切り替わります。",
        ["**Either** analog stick aims, but it's easiest if you use the hand that's not holding the shoulder button. **Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) narrows it to one stick per wheel."] =
            "**どちらの**アナログスティックでも照準できますが、ショルダーボタンを押していない手のスティックを使うのがいちばん楽です。**ホイールは反対側のスティックを無視**（[[accessibility|アクセシビリティの設定]]）を有効にすると、ホイールごとに 1 本のスティックに絞られます。",
        ["**Tilt the stick** toward a slice and it lights up. **Release the trigger** to fire it."] =
            "**スティックをスライスの方向へ傾ける**と、そのスライスが光ります。**トリガーを離す**と実行されます。",
        ["**Release while centered** (stick in the deadzone) **cancels**."] =
            "**中央のまま離す**（スティックがデッドゾーン内）と**取り消し**になります。",
        ["A slice that still **needs configuring** (such as a voice-join with no URL) arms as **\"Configure in Settings\"**. Choosing it takes you to the configuration screen or the setup wizard it needs."] =
            "まだ**設定が必要な**スライス（URL のないボイス参加など）は **「設定で構成」** として選択されます。選択すると、必要な設定画面またはセットアップウィザードに移動します。",
        ["Slices with **Hold to confirm** are protected from accidental triggering, which is useful for Sleep, Power Down, etc. Hold the stick on them (0.8 s) and they'll activate. You can set this on any slice in the Settings wheel editors."] =
            "**長押しで確認**のスライスは、誤って実行されないよう保護されます。スリープや電源オフなどに便利です。スティックをそのスライスに合わせたまま 0.8 秒保持すると実行されます。設定のホイールエディターでどのスライスにも指定できます。",
        ["**D-Pad 🡅 🡇** adjusts system volume."] =
            "**十字キー 🡅 🡇** はシステム音量を調整します。",
        ["**D-Pad 🡄 🡆** steps the Alt-Tab window switcher by default - or [[volume-mixer|virtual desktops, track skip, or mic volume]]. Choose in **Settings ▸ Customize ▸ D-Pad 🡄 🡆**. "] =
            "**十字キー 🡄 🡆** は既定で Alt-Tab のウィンドウ切り替えを進めます。または [[volume-mixer|仮想デスクトップ、曲送り、マイク音量]] に変更できます。**設定 ▸ カスタマイズ ▸ 十字キー 🡄 🡆** で選択してください。",
        ["You can also toggle wheels on/off from tray menu's **Disable/Enable Wheels** or add a **Disable Wheels** slice action."] =
            "トレイメニューの **ホイールを無効化／ホイールを有効にする** から、または **ホイールを無効化** スライスを追加して、ホイールの有効・無効を切り替えることもできます。",
        ["Disabling the wheels changes wheel routing only. The virtual controller and cloak stay exactly where they are, basic controls keep running through that controller, and native features do **not** come back. To release capture and get your real controller, use [[passthru-mode|Passthru Mode]]. This will re-enable vendor features like special haptics and touchpads."] =
            "ホイールを無効にしても変わるのはホイールのルーティングだけです。仮想コントローラーとクロークはそのままの位置に残り、基本的な操作はそのコントローラーを通り続け、ネイティブ機能は**戻りません**。キャプチャーを解放して実機のコントローラーを使うには [[passthru-mode|パススルーモード]] を使ってください。これにより、特殊な触覚フィードバックやタッチパッドなど、メーカー固有の機能が再び使えるようになります。",
        ["Your current setting: toggle the wheels with **{disable}**."] =
            "現在の設定では、次の操作でホイールの有効・無効を切り替えます: **{disable}**。",
        ["**Start editing a wheel:** with a wheel open, **click either stick** (L3/R3). The wheel centers and stays up after you release the trigger."] =
            "**ホイールの編集を始める:** ホイールを開いた状態で**どちらかのスティックをクリック**（L3/R3）します。ホイールが中央に寄り、トリガーを離しても表示されたままになります。",
        ["**Use either stick**. While you're editing, non-logo slices always show their labels regardless of the [[show-labels|Label setting]]."] =
            "**どちらのスティックでも操作できます。** 編集中は、ロゴのないスライスは [[show-labels|ラベル設定]] にかかわらず常にラベルを表示します。",
        ["**Wheel ignores opposite stick** ([[accessibility|Accessibility setting]]) can override this."] =
            "**ホイールは反対側のスティックを無視**（[[accessibility|アクセシビリティの設定]]）で、この動作を変更できます。",
        ["**Move:** **{cross}** picks up the selected slice; aim at a target slot and **{cross}** drops it."] =
            "**移動:** **{cross}** で選択中のスライスを持ち上げ、移動先に照準して **{cross}** で置きます。",
        [" **D-Pad 🡄 🡆** will nudge a slice one spot left or right."] =
            " **十字キー 🡄 🡆** でスライスを左右に 1 つずつずらせます。",
        ["**Remove:** **hold {square}** on a slice until it disappears. Removing the **last** slice disables that wheel; see [[empty-wheel|Single-wheel mode]]."] =
            "**削除:** スライス上で **{square} を長押し**し、消えるまで押し続けます。**最後の**スライスを削除するとそのホイールは無効になります。[[empty-wheel|シングルホイールモード]] をご覧ください。",
        ["A wheel holds up to **12** slices."] =
            "1 つのホイールには最大 **12** 個のスライスを配置できます。",
        ["**Undo / Redo:** **L1 / R1**. You can undo/redo multiple steps while you remain in Edit mode."] =
            "**元に戻す／やり直す:** **L1 / R1**。編集モードにとどまっている間は、複数のステップを元に戻したりやり直したりできます。",
        ["**Exit + save:** **{circle}**, click the stick again, or press an **Fn** / **L4/R4** button."] =
            "**終了して保存:** **{circle}**、スティックの再クリック、または **Fn** / **L4/R4** ボタンを押します。",
        ["**Want only one wheel?** Delete every slice off the other one. A wheel with **no slices is disabled**. That side's chord stays fully usable in the game. Remove slices from Settings, or in [[edit-mode|edit mode]] with **hold {square}** until the last one is gone."] =
            "**ホイールを 1 つだけにしたいですか。** もう一方のスライスをすべて削除してください。**スライスのないホイールは無効**になり、その側のコードはゲーム内でそのまま使えます。設定から、または [[edit-mode|編集モード]] で **{square} を長押し**して最後の 1 つがなくなるまで削除します。",
        ["If you accidentally empty **both** wheels, Settings opens so you can rebuild one or both of them."] =
            "誤って**両方**のホイールを空にした場合は、一方または両方を作り直せるように設定が開きます。",
        ["If a wheel has only one slice and it's **the Arcade Launcher**, then that wheel is replaced by the Arcade Launcher directly."] =
            "ホイールのスライスが **アーケードランチャー** 1 つだけの場合、そのホイールは開かずに、アーケードランチャーが直接開きます。",
        ["**To set it up:** just delete all slices on a wheel except a single **Arcade ▸ Arcade Launcher** slice. You can do that in Settings, or in [[edit-mode|edit mode]] with **hold {square}**."] =
            "**設定方法:** ホイールの **アーケード ▸ アーケードランチャー** のスライスを 1 つだけ残して、ほかのスライスをすべて削除します。設定から、または [[edit-mode|編集モード]] で **{square} を長押し**して行えます。",
        ["**This applies to Arcade Launcher only.** A single slice with just an individual Arcade game on it still draws as a one-slice wheel."] =
            "**これはアーケードランチャーだけに当てはまります。** 個別のアーケードゲーム 1 つだけのスライスは、これまでどおり 1 スライスのホイールとして描画されます。",
        ["**An Arcade Launcher wheel** can't be edited with R3/L3; edit it in **Settings ▸ Left/Right Wheel**, or add a second slice to get the wheel back."] =
            "**アーケードランチャーのホイール**は R3/L3 では編集できません。**設定 ▸ 左/右ホイール**で編集するか、スライスをもう 1 つ追加してホイールを取り戻してください。",
        ["If you want, you can pair this with [[empty-wheel|Single-wheel mode]]: empty the OTHER wheel and you have one gesture that opens the arcade and one that passes straight through to the game."] =
            "必要に応じて [[empty-wheel|シングルホイールモード]] と組み合わせることもできます。もう一方のホイールを空にすれば、アーケードを開くジェスチャーと、ゲームへそのまま通すジェスチャーが 1 つずつになります。",
        ["**Drag an app (.exe or .lnk) from Explorer into the slice list**. A **Launch** slice lands at the drop position with its icon already extracted. Drop several at once for several slices."] =
            "**エクスプローラーからアプリ（.exe または .lnk）をスライス一覧へドラッグ**します。ドロップした位置に**起動**スライスが作られ、アイコンも抽出済みになります。複数まとめてドロップすれば、その数だけスライスが作られます。",
        ["**Color swatches are paired** - the wheel shows whichever variation suits its [[customize|material]]. If you type an exact **Hex** value instead, that color is used exactly as-is, without tinting lighter or darker based on wheel Material. **Reset to Default** returns to the action-type color."] =
            "**カラースウォッチは対になっています。** ホイールはその [[customize|マテリアル]] に合うほうの変化形を表示します。代わりに正確な **Hex** 値を入力すると、その色はホイールのマテリアルに応じて明るくも暗くも調整されず、そのまま使われます。**既定に戻す**とアクションタイプの色に戻ります。",
        ["**Drop your own image onto the preview well** to give any slice custom artwork. It needs a **transparent background**, so a logo-style PNG is best. Something like a screenshot or a photo with no transparency would be a solid block so it's rejected. The file is **copied** into Radiata's art cache, so moving the original later won't blank the slice."] =
            "**プレビュー枠に自分の画像をドロップ**すると、どのスライスにも独自のアートを設定できます。**背景が透明**である必要があるので、ロゴのような PNG が最適です。スクリーンショットや透明部分のない写真のようなものはべた塗りのブロックになるため、受け付けられません。ファイルは Radiata のアートキャッシュに**コピー**されるので、あとで元のファイルを移動してもスライスが空になることはありません。",
        ["When you choose a game, Radiata fetches its transparent **logo** automatically. The **↻ button** restores that logo (downloading it if needed), and the **🡄 🡆** buttons cycle every logo already downloaded for that game. **Requires [[integrations|SteamGridDB]] to be configured.**"] =
            "ゲームを選ぶと、Radiata が透過**ロゴ**を自動的に取得します。**↻ ボタン**でそのロゴに戻せます（必要ならダウンロードします）。**🡄 🡆** ボタンでそのゲーム用にダウンロード済みのロゴを順に切り替えられます。**[[integrations|SteamGridDB]] の設定が必要です。**",
        ["**Choosing a different icon drops the original game logo**. A slice's logo is independent of the [[cover-art|Game Grid's]]."] =
            "**別のアイコンを選ぶと、元のゲームロゴは破棄されます。** スライスのロゴは [[cover-art|ゲームグリッドのもの]] とは独立しています。",
        ["**Everything auto-saves** - adds, removals, reorders, and edits to an existing slice (**Revert** undoes an in-progress edit). **A new slice created in Settings is not saved until you click Save Slice**. Entering **Ctrl+S** forces a save at any point."] =
            "**すべて自動保存されます。** 追加、削除、並べ替え、既存スライスの編集が対象です（**元に戻す** は編集中の変更を取り消します）。**設定で作成した新しいスライスは、「スライスを保存」をクリックするまで保存されません。** **Ctrl+S** を押すと、いつでも強制的に保存されます。",
        ["**Select** (Create/Share) - **cycle the selected game's cover** and save it. Your choices will cycle through default, then up to 10 top-rated SteamGridDB covers, then 5 flat colors if you just want the logo on a clean background."] =
            "**選択**（Create/Share）: **選択中のゲームのカバーを順に切り替えて**保存します。既定 → SteamGridDB の高評価カバー最大 10 点 → 単色 5 種の順に切り替わります。単色は、ロゴだけをすっきりした背景に載せたい場合に便利です。",
        ["Covers come from [[integrations|SteamGridDB]] - add a free API key in **Settings ▸ Advanced ▸ Integrations** for portrait covers and logos across every storefront. This is the best option. However, without SteamGridDB, you still get Steam's own art, [[playnite|Playnite]]'s covers, and the flat colors."] =
            "カバーは [[integrations|SteamGridDB]] から取得されます。**設定 ▸ 詳細 ▸ 連携** に無料の API キーを登録すると、すべてのストアのゲームで縦長カバーとロゴが使えるようになります。これが最善の方法です。ただし SteamGridDB がなくても、Steam 自身のアート、[[playnite|Playnite]] のカバー、単色は利用できます。",
        ["An **Arcade** slice opens a little game in a **round window, right where the wheel was**. Play a game while you wait on a loading screen or a big lobby, no alt-tabbing required."] =
            "**アーケード**スライスは、**ホイールがあった場所にそのまま丸いウィンドウ**で小さなゲームを開きます。ロード画面や大きなロビーの待ち時間に、Alt-Tab せずに遊べます。",
        ["**Arcade Launcher** opens the whole arcade. Each game is a cabinet on a round carousel with a live screenshot of where it was left. **Left/right** on the stick or D-Pad swings the next cabinet to the front, **{cross}** plays it. It **picks up where you left off** - straight back into the game you were last playing, or at the cabinets if that's where you closed it. Each game can also be directly-launched by adding a slice for it."] =
            "**アーケードランチャー** はアーケード全体を開きます。各ゲームは円形のカルーセル上の筐体になっていて、最後に遊んだ画面のライブスクリーンショットが表示されます。スティックか十字キーの**左右**で次の筐体を手前に回し、**{cross}** で遊びます。**前回の続きから再開**し、最後に遊んでいたゲームにそのまま戻るか、筐体の一覧で閉じた場合はその一覧に戻ります。各ゲームは、そのスライスを追加すれば直接起動することもできます。",
        ["**A wheel with the Arcade Launcher and nothing else** skips the wheel and goes straight to the Arcade Launcher - see [[arcade-direct-launch|Arcade direct-launch]]."] =
            "**アーケードランチャーだけのホイール**はホイールを省略して、アーケードランチャーへ直接移動します。[[arcade-direct-launch|アーケードの直接起動]] をご覧ください。",
        ["**{circle} always backs you out**. One press closes a menu or help card, the next steps out of the game: back to the **Arcade Launcher** when that's how you got in, otherwise straight out. **The game freezes exactly as you left it**, so you can come back later and carry on. Each game stores its own state and scoreboard."] =
            "**{circle} を押すと常に一段階戻ります。** 1 回押すとメニューやヘルプカードが閉じ、もう一度押すとゲームから出ます。**アーケードランチャー** から入った場合はそこへ戻り、それ以外はそのまま外へ出ます。**ゲームは終了時の状態のまま凍結される**ため、後で戻って続きから遊べます。各ゲームは自分の状態とスコアボードを個別に保存します。",
        ["Over a game, the arcade only plays while your controller is **isolated** from it. Otherwise a card explains why and offers **hold {triangle} to play anyway**. Passthru Mode will mean no Arcade games can be played while you're in another game. See [[input-isolation|Input isolation]]."] =
            "ゲームの上では、コントローラーがゲームから**分離**されている間だけアーケードが動作します。そうでない場合はカードが理由を説明し、**{triangle} を長押しでそれでも遊ぶ**を提示します。パススルーモードでは、別のゲームを実行している間はアーケードのゲームを遊べません。[[input-isolation|入力の分離]] をご覧ください。",
        ["**Kabloom**: Minesweeper logic on a Floret Pentagonal Tiled field of flower petals. Move the cursor with the stick or d-pad. **{cross}** reveals a tile, **{square}** flags where you think there's a bee (or multiple bees, on later levels). Press **{square}** again to increase the flag count, or hold it for a question mark flag. Remaining bees are shown at the bottom of the screen. At the center of each floret is a nectar gem, collected when all the petals around it are cleared, which adds to your total score. Every board is solvable with no forced guesses."] =
            "**Kabloom** — 花びらのフローレット五角形タイリングの盤面で遊ぶ、マインスイーパーのロジックです。カーソルはスティックか十字キーで動かします。**{cross}** でタイルを開き、**{square}** でハチ（後半のレベルでは複数のハチ）がいると思う場所に旗を立てます。**{square}** をもう一度押すと旗の数が増え、長押しするとクエスチョンマークの旗になります。残りのハチの数は画面の下部に表示されます。各フローレットの中心にはネクターの宝石があり、周囲の花びらをすべて開くと獲得でき、合計スコアに加算されます。どの盤面も、運任せの手なしで解けます。",
        ["**More about \"no forced guesses\":** Boards 1-10 are generated on the fly and validated by the Solver before play. Every board from level 11 up is baked ahead of time and certified by a complete solver before it ships. Individual petal tiles can hold up to three bees on the later levels. The solver plays the board from every zero-clue petal you could open on. It runs the human patterns first (saturation, subset difference, overlap bounds, chained constraints) and when those run dry it groups the unknown petals that share the same set of clues into boxes, enumerates every way the remaining bees can be spread over each connected group of boxes, and folds in the total bee count so the petals no clue touches get reasoned about too. A level ships only when the certified start-points cover the whole crop, so the first petal you open is always one the proof began from, ensuring every possible start guarantees a solvable board. "] =
            "**「運任せの手がない」についてもう少し。** レベル 1 から 10 の盤面はその場で生成され、プレイ前にソルバーで検証されます。レベル 11 以降の盤面はあらかじめ焼き込まれ、出荷前に完全なソルバーによって証明されています。後半のレベルでは、個々の花びらタイルに最大 3 匹のハチが入ります。ソルバーは、あなたが最初に開ける可能性のある手掛かりゼロの花びらすべてから盤面を解き進めます。まず人間的なパターン（飽和、部分集合の差、重なりの上下限、連鎖する制約）を適用し、それが尽きると、同じ手掛かりの組を共有する未知の花びらを箱にまとめ、連結した箱のグループごとに残りのハチの配り方をすべて数え上げ、ハチの総数も突き合わせることで、どの手掛かりも触れていない花びらについても推論します。証明済みの開始点が畑全体を覆うときだけレベルが出荷されるので、最初に開ける花びらは必ず証明の出発点のいずれかです。つまり、どの開始位置でも解ける盤面が保証されます。 ",
        ["**Connate**: Your craft rides the rim around a cluster of orbs and garbage blocks. Shoot orbs to merge the numbers before the pile grows past the inner ring. **{cross}** fires your held number into the cluster. Hold to fire with more force. Star and Star-Gap pieces merge to make orbs of 3, and matching numbers from 3 up combines their values. Only **matching colors** merge, although mixed-color orbs can be created by matching stars and gaps of opposite colors; these merge with either color or with other mixed-color orbs. Combos charge up a bomb you can fire. Bomb high-value orbs to collect them to your score."] =
            "**Connate** — あなたの機体は、オーブとガベージブロックのかたまりの周囲の縁を走ります。山が内側のリングを越える前に、オーブを撃って数字をつなげましょう。**{cross}** で手持ちの数字をかたまりへ撃ち込み、長押しすると強く撃てます。スターとスターギャップのピースは合体して 3 のオーブになり、3 以上は同じ数字どうしで値が合算されます。つながるのは**同じ色**だけですが、反対色のスターとギャップを合わせると混色のオーブができます。混色のオーブはどちらの色とも、ほかの混色のオーブともつながります。コンボを重ねると、撃てるボムがたまります。高い値のオーブをボムで撃つと、スコアに加算されます。",
        ["**Stages**: the pace follows your score. Each time your collected total crosses 100, 250, 450, 700 and 1,000, the shot clock gets a little shorter, garbage arrives a little sooner, and a bomb takes one more combo charge to fill. The current stage is shown under the score. From stage 2, every 48 seconds of play ends with 8 seconds of relief: no garbage, and a longer shot clock."] =
            "**ステージ** — 進行ペースはスコアに応じて変わります。回収した合計が 100、250、450、700、1,000 を超えるたびに、撃つまでの制限時間が少し短くなり、ガベージの出現が少し早まり、ボムを満タンにするのに必要なコンボのチャージが 1 つ増えます。現在のステージはスコアの下に表示されます。ステージ 2 以降は、プレイ 48 秒ごとの終わりに 8 秒の小休止があり、その間はガベージが出ず、撃つまでの制限時間も長くなります。",
        ["**Petalpop**: a ring of paddles around a flower of petals. Your analog stick controls every paddle together, so be careful! Hold **{cross}** to draw the paddles back like a slingshot, then release to **smash** the ball. Hit the core with a smash shot to clear the level. Pop a blue petal for multi-ball. \n\nFour sides and four levels to start, then five, six, seven and eight. You get one extra life for each size increase. Switch between spring and rail control via the **Start** menu."] =
            "**Petalpop** — 花びらの花を囲むパドルのリングです。アナログスティックは**すべてのパドル**を同時に動かすので、注意してください！ **{cross}** を長押しするとパチンコのようにパドルが引かれ、離すとボールを**強打**します。コアを強打で当てるとレベルクリアです。青い花びらを割るとマルチボールになります。\n\n最初は 4 辺と 4 レベル、続いて 5、6、7、8 と増えます。大きくなるたびに残機が 1 つ増えます。バネ操作とレール操作は **Start** メニューで切り替えます。",
        ["**Internode**: shoot down a twisting half-pipe and collect tokens while avoiding mines and gaps. Hitting a mine will drop your tokens, and hitting one when carrying no tokens sets you back one stretch. Falling in a gap always sets you back one stretch. **{cross}** jumps. \n\nCatching a full token streak will upgrade the final token in the pattern to a gold 10x token. Reach each checkpoint with enough tokens to bank them in your score, with extra bonuses for passing a stretch on the first try and for collecting every token in a stretch. Insufficient tokens keeps you looping the same stretch until you have enough. The course twists and turns harder every stage. \n\nCamera roll can be enabled/disabled in the **START** menu."] =
            "**Internode** — ねじれたハーフパイプを駆け抜け、地雷と隙間を避けながらトークンを集めます。地雷に当たるとトークンを落とし、トークンを持たずに当たると 1 区間戻されます。隙間に落ちると常に 1 区間戻されます。**{cross}** でジャンプします。\n\nトークンを連続で取りきると、パターンの最後のトークンが金色の 10 倍トークンに変わります。各チェックポイントに十分なトークンを持って到達するとスコアに預けられ、区間を 1 回で突破した場合や区間内のトークンをすべて集めた場合にはボーナスが加わります。トークンが足りないと、十分になるまで同じ区間を繰り返します。コースはステージが進むほどねじれが強くなります。\n\nカメラロールは **START** メニューで有効・無効を切り替えられます。",
        ["**Choose the app** two ways: **Browse for App…** picks an `.exe` from disk, and **Installed Apps…** lists everything with a Start-menu entry (including **Microsoft Store apps**, which have no `.exe` to browse to). Either way the icon is pulled in automatically. You can also drag an `.exe`, a shortcut, or a Start-menu app straight into the slice list."] =
            "**アプリの選び方は 2 通りあります。** **アプリを参照…** はディスク上の `.exe` を選び、**インストール済みアプリ…** はスタートメニューに登録されたものをすべて一覧表示します（参照できる `.exe` を持たない **Microsoft Store アプリ**も含まれます）。どちらの場合もアイコンは自動的に取り込まれます。`.exe`、ショートカット、スタートメニューのアプリをスライス一覧へ直接ドラッグすることもできます。",
        ["A **Store app** can't be detected as already-running, so **Run** just re-opens it and **Toggle** won't reliably close it. And an app started through an updater or launcher **stub** may run under a different name than the file you picked, so picking the app's real `.exe` is the reliable choice."] =
            "**Store アプリ**は起動済みかどうかを検出できないため、**実行**は単に開き直すだけで、**切り替え**では確実に終了できません。また、アップデーターや起動用の**スタブ**を経由して起動したアプリは、選んだファイルとは別の名前で動いていることがあるため、アプリ本体の `.exe` を選ぶのが確実です。",
        ["**Installed Game** slices launch the game **directly** where possible. A GOG game runs its own exe even without GOG Galaxy installed; only stores that need their client running (like Steam) will route through the launcher."] =
            "**インストール済みゲーム** スライスは、可能な場合はゲームを**直接**起動します。GOG のゲームは GOG Galaxy が未インストールでも自身の exe を実行します。クライアントの起動が必要なストア（Steam など）だけが、ランチャーを経由します。",
        ["Radiata forwards a **virtual controller**, and **Controller mode** decides which kind the game sees: an **Xbox** pad or a **DualShock** pad. Two slices under **System ▸ Controller** flip it - **Toggle Xbox Mode** and **Toggle DualShock Mode**."] =
            "Radiata は**仮想コントローラー**を転送し、**コントローラーモード**がゲームから見える種類を決めます。**Xbox** パッドか **DualShock** パッドかです。**システム ▸ コントローラー**の 2 つのスライス、**Xbox モードを切り替え**と **DualShock モードを切り替え**で変更します。",
        ["**What it's for:** many games only accept one class of controller. A Game Pass or Xbox-app title that refuses a DualSense controller will allow it if it's in **Xbox Mode** in Radiata. Games that want PlayStation input go the other way."] =
            "**何のためか:** 多くのゲームは 1 種類のコントローラーしか受け付けません。DualSense コントローラーを受け付けない Game Pass や Xbox アプリのタイトルも、Radiata で **Xbox モード**にすれば受け付けます。PlayStation の入力を求めるゲームでは逆になります。",
        ["**Button prompts follow the mode.** The glyphs a game draws come from the pad it thinks is plugged in, so Xbox Mode gets you **A B X Y** and DualShock Mode **{cross} {circle} {square} {triangle}**."] =
            "**ボタン表示はモードに従います。** ゲームが描く記号は、接続されていると認識しているコントローラーに由来します。そのため Xbox モードでは **A B X Y**、DualShock モードでは **{cross} {circle} {square} {triangle}** になります。",
        ["The switch is instant and stays put until you flip it back, but the game sees a controller swap at that moment. **Flip it before launching the game** for best results."] =
            "切り替えは即座で、戻すまで維持されますが、その瞬間ゲームからはコントローラーが差し替わったように見えます。**最良の結果を得るには、ゲームを起動する前に切り替えて**ください。",
        ["**A wired Xbox pad stays an Xbox pad.** If your physical controller is an Xbox pad on USB, or a USB wireless dongle, the game always gets a virtual Xbox pad and DualShock Mode cannot change it."] =
            "**有線の Xbox コントローラーは Xbox コントローラーのままです。** 物理コントローラーが USB 接続の Xbox コントローラー、または USB ワイヤレスアダプターの場合、ゲームには常に仮想 Xbox コントローラーが渡され、DualShock モードでは変更できません。",
        ["This requires installing the isolation drivers. There's no virtual pad to switch without them, and firing the slice puts up an on-screen notice saying so. In [[passthru-mode|Passthru Mode]] the game reads your real controller, so the mode has nothing to change. See [[input-isolation|Input isolation]]."] =
            "これには分離ドライバーのインストールが必要です。ドライバーがなければ切り替える仮想パッドが存在せず、スライスを実行すると画面上にその旨の通知が表示されます。[[passthru-mode|パススルーモード]] ではゲームは実際のコントローラーを読み取るため、モードを変えるものがありません。[[input-isolation|入力の分離]] を参照してください。",
        ["Discord must be running. These slices talk to the Discord app on your PC, not to Discord's website. "] =
            "Discord が起動している必要があります。これらのスライスがやり取りするのは PC 上の Discord アプリであって、Discord のウェブサイトではありません。 ",
        ["**OBS Studio** ([obsproject.com](https://obsproject.com)) is the free, open-source program that dominates the Twitch and YouTube streaming space, and also allows you to record the screen. It builds a broadcast out of **scenes** - named layouts of game capture, camera, mic and overlays - that you switch between while live."] =
            "**OBS Studio**（[obsproject.com](https://obsproject.com)）は、Twitch や YouTube の配信分野で圧倒的に使われている無料のオープンソースプログラムで、画面の録画にも使えます。配信は**シーン**（ゲームキャプチャ、カメラ、マイク、オーバーレイの名前付きレイアウト）から構成され、ライブ中に切り替えて使います。",
        ["A **Text Chat** slice (Chat & Streaming) types a message into the running game's text chat: it presses the game's **chat-open key**, types your text, and presses Enter."] =
            "**テキストチャット** スライス（Chat & Streaming）は、実行中のゲームのテキストチャットにメッセージを入力します。ゲームの**チャットを開くキー**を押し、指定した文章を入力して Enter を押します。",
        ["**Chat Button** - **Try Game Default** looks up the chat key for whatever game is running **each time the slice fires**, from a built-in index of around 1,000 PC games, so the same slice works across multiple games. **Custom…** lets you enter the key yourself, so use it for games with remapped or unusual chat keys, or for a game the index doesn't cover."] =
            "**チャットボタン** — **ゲームの既定を試す**は、**スライスを実行するたびに**そのとき動いているゲームのチャットキーを、約 1,000 本の PC ゲームを収めた内蔵の索引から調べます。同じスライスが複数のゲームで使えます。**カスタム…**ではキーを自分で入力できるので、チャットキーを変更しているゲームや変わったキーのゲーム、索引に載っていないゲームに使ってください。",
        ["**Message** - the line of text to send. An empty message (or Custom with no key) arms as **\"Configure in Settings\"**, and firing opens the slice's editor."] =
            "**メッセージ** — 送信するテキストの 1 行です。メッセージが空の場合（またはカスタムでキーがない場合）は **「設定で構成」** として選択され、実行するとそのスライスのエディターが開きます。",
        ["**Sends are limited to one per 15 seconds**. Hub shows the remaining cooldown. No spamming, please!"] =
            "**送信は 15 秒に 1 回までに制限されています。** ハブに残りの待ち時間が表示されます。連投はご遠慮ください。",
        ["**Sony family** (DualSense Edge, DualSense, DualShock 4) over Bluetooth or USB: good support with clean input isolation. The **Edge's Fn buttons** are the reference trigger. Haptic triggers and touchpad will not be seen by games due to driver limitations unless you are in Passthru Mode."] =
            "**Sony 系**（DualSense Edge、DualSense、DualShock 4）は Bluetooth と USB のいずれでも、入力の分離まで含めて良好に対応しています。**Edge の Fn ボタン**が基準となるトリガーです。ドライバーの制約により、パススルーモードでない限り、ゲームには触覚トリガーとタッチパッドが認識されません。",
        ["**Pads with extra paddles or buttons** (e.g. **8BitDo Ultimate 2C**) - over **Bluetooth** the extra **L4/R4** buttons are read directly, so you can pick them in [[triggers|Settings ▸ Customize ▸ Triggers]] to summon a wheel on their own, or paired with a second button if you also use them in games. They're offered, not assumed: the default stays the standard bumper chord. Over **USB** the extra buttons are invisible to Radiata, except as mapped by the controller's own drivers. So connect over Bluetooth if you want native L4/R4 support, or map them deliberately."] =
            "**追加のパドルやボタンを持つコントローラー**（例: **8BitDo Ultimate 2C**）— **Bluetooth** 経由では追加の **L4/R4** ボタンを直接読み取れるので、[[triggers|設定 ▸ カスタマイズ ▸ 呼び出し操作]] で選んで単独でホイールを呼び出したり、ゲーム内でも使う場合は 2 つ目のボタンと組み合わせたりできます。提示されるだけで前提にはしていません。既定は標準のバンパーの組み合わせのままです。**USB** 経由では、コントローラー自身のドライバーが割り当てた形を除き、追加のボタンは Radiata から見えません。ネイティブに L4/R4 を使いたい場合は Bluetooth で接続するか、意図して割り当ててください。",
        ["**Genuine Xbox pads over Bluetooth** - isolated too, with an extra safeguard. Before presenting the stand-in pad, Radiata has a separate helper process **observe** that games really can't see the physical pad any more. If that check can't pass - or can't run - it falls back to shared-input mode instead of guessing, so a hidden pad with no stand-in won't leave you without a working controller."] =
            "**Bluetooth 経由の純正 Xbox コントローラー** — こちらも分離しますが、追加の安全策があります。代役のパッドを提示する前に、Radiata は独立したヘルパープロセスに、ゲームから物理コントローラーが本当に見えなくなったかを**観測**させます。その確認に通らない場合、あるいは実行できない場合は、推測せず入力共有モードに切り替えます。これにより、代役のないまま隠されたコントローラーで操作手段を失うことはありません。",
        ["Isolation needs the drivers (ViGEm + HidHide). Without them every pad runs in shared-input mode (e.g. \"bleed-thru\"). Install from **Settings ▸ Advanced ▸ Troubleshooting ▸ Install/Repair Drivers**. Not sure which mode you're in? Hover the tray icon - see [[input-isolation|Input isolation]]."] =
            "分離にはドライバー（ViGEm + HidHide）が必要です。ドライバーがない場合、すべてのコントローラーが入力共有モードで動作します（いわゆる入力漏れ）。**設定 ▸ 詳細 ▸ トラブルシューティング ▸ ドライバーをインストール/修復** からインストールしてください。どちらのモードか分からない場合は、トレイアイコンにマウスを重ねてください。[[input-isolation|入力の分離]] をご覧ください。",
        ["With the optional isolation drivers installed, Radiata gives games a **virtual controller** - a DualShock 4 for Sony pads, an Xbox 360 pad for Xbox pads or in Xbox Mode - and **cloaks the physical pad** (HidHide) so the game can't see it twice. That's when capture succeeds. Other remappers, existing device access and unsupported input paths can all sabotage full isolation, and without the drivers games keep seeing your controller while a wheel is up. This is how you get bleed-thru."] =
            "任意の分離ドライバーを入れておくと、Radiata はゲームに**仮想コントローラー**（Sony のパッドには DualShock 4、Xbox のパッドや Xbox モードでは Xbox 360 パッド）を渡し、ゲームから二重に見えないよう**物理パッドをクローク**します（HidHide）。これはキャプチャーが成功した場合の話です。他のリマッパー、すでに開かれているデバイスアクセス、非対応の入力経路はいずれも完全な分離を妨げることがあり、ドライバーがなければホイールが開いている間もゲームはコントローラーを見続けます。これが入力漏れの原因です。",
        ["**Which mode am I actually in? Hover the tray icon.** It reads **\"Radiata - isolated\"**, or names the reason it isn't (no drivers, Passthru Mode, a pad that can't be cloaked). When capture drops to shared-input mode you also get an on-screen notice."] =
            "**実際にどのモードなのかは、トレイアイコンにマウスを重ねて確認してください。** **\"Radiata - isolated\"** と表示されるか、分離されていない理由（ドライバーなし、パススルーモード、隠せないパッド）が示されます。キャプチャが共有入力モードに落ちたときは画面上の通知も表示されます。",
        ["**Global toggle:** the tray's checkable **Passthru Mode** item, or the checkbox on **Settings ▸ Passthru Mode**. The tab appears only when the isolation drivers are installed."] =
            "**全体の切り替え:** トレイのチェック可能な **パススルーモード** 項目、または **設定 ▸ パススルーモード** のチェックボックス。このタブは分離ドライバーがインストールされているときだけ表示されます。",
        ["**Switch Passthru Mode on or off between play sessions, not mid-game.** Either direction swaps the controller a running game is reading - the virtual pad for your real one, or back - and most games don't go looking for a new controller once they've started, so the game loses input until it's relaunched."] =
            "**パススルーモードの切り替えはプレイセッションの合間に行ってください。ゲームの最中は避けてください。** どちらの向きでも、動作中のゲームが読んでいるコントローラーが入れ替わり（仮想パッドから実機へ、またはその逆）、多くのゲームは開始後に新しいコントローラーを探さないため、起動し直すまで入力を失います。",
        ["**Settings tabs:** Left Wheel, Right Wheel, **Customize** (look, feel, sound, triggers, the [[volume-mixer|D-Pad 🡄 🡆]] picker and the [[show-labels|Show labels on]] picker), **Passthru Mode** ([[passthru-mode|Passthru Mode]] - shown when the isolation drivers are installed), **Advanced** (a **Current Controller** readout, [[integrations|Integrations]], [[game-grid-options|Game Grid]] housekeeping, [[accessibility|Accessibility]], **Start with Windows**, and [[system-actions|System tools]] incl. backup & reset and **Quit Radiata**), **Help**, and **About**. Settings auto-save; **Ctrl+S** forces a save."] =
            "**設定のタブ:** 左ホイール、右ホイール、**カスタマイズ**（外観、操作感、サウンド、呼び出し操作、[[volume-mixer|十字キー 🡄 🡆]] のピッカーと [[show-labels|ラベルを表示するスライス]] のピッカー）、**パススルーモード**（[[passthru-mode|パススルーモード]]。分離ドライバーがインストールされているときに表示）、**詳細**（**現在のコントローラー** の表示、[[integrations|連携]]、[[game-grid-options|ゲームグリッド]] のメンテナンス、[[accessibility|アクセシビリティ]]、**Windows 起動時に開始**、バックアップとリセットや **Radiata を終了** を含む [[system-actions|システムツール]]）、**ヘルプ**、**情報**。設定は自動保存され、**Ctrl+S** で強制的に保存できます。",
        ["**Language** - **Settings ▸ Advanced ▸ Language** sets the language for the whole app. Help language switches immediately. Everything else follows at the next launch."] =
            "**言語** - **設定 ▸ 詳細 ▸ 言語** でアプリ全体の言語を設定します。ヘルプの言語はすぐに切り替わります。それ以外は次回の起動時に反映されます。",
        ["**Settings ▸ Customize** sets how the wheels look, feel and sound. Every pick applies **live**."] =
            "**設定 ▸ カスタマイズ**では、ホイールの見た目、操作感、音を決めます。どの選択も**その場で**反映されます。",
        ["**Material** - the resting-slice look. Eight, in two groups. **Simple** contains solid-color **Flat Light** and **Flat Dark**; **Deluxe** contains the glassy **Pearl** and **Obsidian**, plus four styled looks:"] =
            "**マテリアル** — 待機中のスライスの見た目です。2 つのグループに全 8 種類あります。**シンプル**には単色の **フラット ライト** と **フラット ダーク**、**デラックス**にはガラスのような **パール** と **黒曜石**、さらに 4 つのスタイルが含まれます:",
        ["**Kawaii** - pastel wedges, each slice a different hue; firing bursts heart-and-star confetti."] =
            "**かわいい**: パステル調のセクターで、1 つずつ色合いが異なります。実行するとハートと星の紙吹雪がはじけます。",
        ["**Salvage** - charcoal slices with a rusted-metal texture, fluorescent-light highlighting, and a stamped plate edge."] =
            "**スクラップ**: 錆びた金属の質感、蛍光灯のようなハイライト、刻印されたプレート縁を備えた、炭色のスライスです。",
        ["**Reactor** - dark hollow wedges; the armed slice lights an animated circuit-board of traces and sparks."] =
            "**リアクター**: 暗く中が抜けたセクターです。選択中のスライスでは、回路基板の配線と火花がアニメーションで光ります。",
        ["On the Kawaii sound set, arming a slice strikes the next note of a xylophone melody, so scrubbing around the ring plays the song. There are 5 melodies... you might recognize a few of them :)"] =
            "Kawaii のサウンドセットでは、スライスを選択するたびに木琴のメロディーの次の音が鳴るので、リングをなぞると曲が流れます。メロディーは 5 種類あります。聞き覚えのあるものもあるかもしれません :)",
        ["The first time Radiata sees a new **or changed** package it asks you to confirm before loading anything. Accepting will show it in **Settings ▸ Customize** in a third group, **Custom**, below Simple and Deluxe."] =
            "Radiata が新しい**または変更された**パッケージを初めて検出すると、何かを読み込む前に確認を求めます。承諾すると、テーマは **設定 ▸ カスタマイズ** のシンプルとデラックスの下にある 3 つ目のグループ **カスタム** に表示されます。",
        ["A package is content from whoever wrote it. **Only install themes from a source you trust**. The confirmation prompt returns whenever any file in the package changes."] =
            "パッケージは、それを書いた人によるコンテンツです。**信頼できる提供元のテーマだけをインストールしてください。** パッケージ内のファイルが変更されるたびに、確認プロンプトが再表示されます。",
        ["Custom themes render through Radiata's **flat** slice paths with your colors substituted, so they can't reach the built-in styled materials' procedural effects (Kawaii's confetti, Reactor's circuit board)."] =
            "カスタムテーマは色を置き換えた Radiata の**フラット**なスライス描画パスで描かれるため、内蔵のスタイル付きマテリアルが持つプロシージャルな効果（Kawaii の紙吹雪、Reactor の回路基板）には届きません。",
        ["Set `\"format\": 2` and add any of these optional blocks. Every number is clamped to a safe range, so an extreme value is pulled back rather than rejected."] =
            "`\"format\": 2` を設定し、以下の任意のブロックを追加します。すべての数値は安全な範囲に丸められるため、極端な値は拒否されず引き戻されます。",
        ["Custom Arcade games - build your own!"] =
            "カスタムアーケードゲーム — 自分で作ろう！",
        ["`\"howTo\"` - optional, up to **5 lines of 80 characters**, which become the **{triangle}** help card. `{cross}` `{circle}` `{square}` `{triangle}` in a line are replaced with the player's own button glyphs. No lines means no help card."] =
            "`\"howTo\"` - 任意。**80 文字以内の行を最大 5 行**まで指定でき、**{triangle}** のヘルプカードになります。行内の `{cross}` `{circle}` `{square}` `{triangle}` はプレイヤー自身のボタン記号に置き換えられます。行を指定しなければ、ヘルプカードは表示されません。",
        ["**`kvSet(\"key\", \"value\")`** and **`kvGet(\"key\")`** are the **only** state that survives closing a game. Contains strings only, keys up to 64 characters, values up to 1024, 4 KB per game in total. Everything else resets on dismiss, so design for it."] =
            "**`kvSet(\"key\", \"value\")`** と **`kvGet(\"key\")`** は、ゲームを閉じても残る**唯一**の状態です。含まれるのは文字列のみで、キーは 64 文字以内、値は 1024 文字以内、ゲームごとに合計 4 KB までです。それ以外はすべて閉じるとリセットされるので、それを前提に設計してください。",
        ["**Wheel ignores opposite stick** - normally **either** thumbstick aims (whichever you tilt further), and either stick's click opens [[edit-mode|edit mode]]. This option has each wheel listen to **one** stick only: the free hand's under Hold, the wheel's own side under Toggle. "] =
            "**ホイールは反対側のスティックを無視** - 通常は、より深く傾けた方の**どちらのスティックでも**照準でき、どちらのスティックのクリックでも [[edit-mode|編集モード]] が開きます。このオプションでは、各ホイールが **1 本**のスティックだけを受け付けます。Hold では空いた手のスティック、Toggle ではホイール自身の側のスティックです。 ",
        ["**Reduce motion** - stops decorative movement everywhere. No confetti or sparks, no parallax, no zooming or drifting; wheels fade in in place, edit-mode rearranging is instant, and every hold-to-confirm effect becomes the same steady progress arc. Some Arcade features are automatically disabled. Progress meters, selection highlights and state readouts all stay. This setting respects Windows' own **Animation effects** switch as well."] =
            "**動きを減らす** — 装飾的な動きをすべて止めます。紙吹雪も火花も、パララックスも、ズームや漂う動きもありません。ホイールはその場でフェードインし、編集モードの並べ替えは瞬時になり、長押しで確定する効果はすべて同じ一定の進捗アークになります。一部のアーケード機能は自動的に無効になります。進捗メーター、選択のハイライト、状態表示はいずれも残ります。この設定は Windows 自体の**アニメーション効果**のスイッチにも従います。",
        ["**Always show hub** - off (the default), the wheel's centre hub appears only when it has something to show. On, the hub is always drawn and also shows the controller battery: a steadier centre to read. **Reduce motion** assumes you want this checked as well, but you can set them independently too."] =
            "**常にハブを表示** — オフ（既定）では、ホイール中央のハブは見せるものがあるときだけ現れます。オンにするとハブが常に描かれ、コントローラーのバッテリー残量も表示されます。読み取りやすい落ち着いた中央になります。**動きを減らす**はこれもオンにする前提ですが、両方を個別に設定することもできます。",
        ["**Icons, not Logos** (the default) - only slices showing one of Radiata's built-in icons are labelled. A slice carrying artwork (a game logo, cover art, or a PNG you added) goes unlabelled, assuming the artwork includes or replaces the name."] =
            "**アイコンのみ（ロゴは除く）**（既定）: Radiata 内蔵のアイコンを表示しているスライスだけにラベルが付きます。アート（ゲームのロゴ、カバーアート、追加した PNG）を持つスライスにはラベルが付きません。アートが名前を含んでいる、または名前の代わりになっていると見なすためです。",
        ["**Slices I Choose** - each slice's own **Show Label** checkbox decides, and it's the only mode in which that checkbox appears in the slice editor (see [[editor-desktop|Slice editor tricks]]). Every slice is created with **Show Label** unchecked, so switching to this mode starts you from an unlabelled wheel: check the few slices you want named."] =
            "**選んだスライスのみ** — 各スライスの **ラベルを表示** チェックボックスで決まります。このチェックボックスがスライスエディターに現れるのはこのモードのときだけです（[[editor-desktop|スライスエディターの小技]] をご覧ください）。すべてのスライスは **ラベルを表示** がオフの状態で作成されるため、このモードに切り替えるとラベルのないホイールから始まります。名前を表示したい数個にチェックを入れてください。",
        ["Radiata only ever **reads** Playnite's local database. Playnite does not need to be running."] =
            "Radiata は Playnite のローカルデータベースを**読み取る**だけです。Playnite が起動している必要はありません。",
        ["**Recover Controller** - the ↻ button beside the **Current Controller** name. This is a soft input reset for a wedged pad. If the pad stays silent afterwards, turn it off (hold its home button until the light goes out), turn it back on, and reconnect it — a controller whose input has frozen at the device only comes back from a power-cycle."] =
            "**コントローラーを復旧** — **現在のコントローラー** の名前の横にある ↻ ボタンです。反応しなくなったコントローラーの入力をソフトリセットします。それでも反応がない場合は、コントローラーの電源を切り（ライトが消えるまでホームボタンを長押し）、再び電源を入れてから接続し直してください。デバイス側で入力が固まったコントローラーは、電源の入れ直しでしか復帰しません。",
        ["**Battery** - beside the **Current Controller** heading, the same reading the wheel hub shows. PlayStation pads report a percentage (in 10% steps); Xbox-compatible pads only report four coarse levels. Nothing shows until the pad has reported a level."] =
            "**バッテリー**: **現在のコントローラー** の見出しの横に、ホイールのハブと同じ残量が表示されます。PlayStation のコントローラーは 10 % 刻みの割合で報告し、Xbox 互換のコントローラーは 4 段階の大まかな水準しか報告しません。コントローラーが残量を報告するまでは何も表示されません。",
        ["**Restore Settings…** - replaces settings and art picks only after validation and a successful save, then restarts Radiata. Automatic recovery backups are encrypted for your Windows account; restore them through this command. Exported ZIPs contain readable settings and artwork, but protected credentials may need re-entry on another account or PC."] =
            "**設定を復元…** — 検証と保存の成功を確認してから設定とアート選択を置き換え、その後 Radiata を再起動します。自動の復旧バックアップは Windows アカウント向けに暗号化されています。このコマンドから復元してください。書き出した ZIP には読み取り可能な設定とアートが入っていますが、保護された認証情報は別のアカウントや PC では入力し直しが必要な場合があります。",
        ["**Wipe App Data and Reset…** - deletes everything in `%APPDATA%\\Radiata`, including automatic backups. Only backups saved outside that folder survive. Export a settings ZIP first if you want. Radiata restarts after a successful reset."] =
            "**アプリデータを消去してリセット…** — 自動バックアップを含め、`%APPDATA%\\Radiata` の中身をすべて削除します。そのフォルダーの外に保存したバックアップだけが残ります。必要なら、先に設定の ZIP を書き出してください。リセットに成功すると Radiata が再起動します。",
        ["**Settings ▸ Advanced** shows a ↻ button beside the Current Controller name. This does a soft input reset - drops and reopens the HID stream - without restarting Radiata."] =
            "**設定 ▸ 詳細** の**現在のコントローラー**の名前の横に ↻ ボタンがあります。これは Radiata を再起動せずに、入力のソフトリセット（HID ストリームを一度閉じて開き直す）を行います。",
    };
}
