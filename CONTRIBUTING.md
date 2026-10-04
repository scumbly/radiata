# Contributing to Radiata

Thanks for wanting to help. Radiata is a solo-maintained project with strong opinions about scope and
polish, so please read this page before opening a pull request.

## Before you start

- **Open an issue first** for anything beyond a small fix. Radiata's feature set is deliberately curated;
  an issue conversation before you write code protects your time. Small, self-evident fixes (a typo, an
  obvious crash) can go straight to a pull request.
- **Bug reports are always welcome.** The bug report form asks for what is needed to reproduce a
  controller problem:
  - the Radiata version (**Settings ▸ About**) and your Windows version;
  - the controller model and how it connects (USB, Bluetooth or wireless dongle);
  - the tray tooltip's isolation mode (hover the tray icon: it reads "Radiata - isolated" or names the
    reason it isn't);
  - the steps, what you expected, and what happened;
  - optionally, the diagnostic ZIP from **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to
    Developer…**. Review it before attaching it to a public issue: it can include device identifiers,
    account names in file paths, and application or game names.
- **Security problems are not issues.** Report them privately; see [SECURITY.md](SECURITY.md).

## Contributor License Agreement (required)

All contributions require agreement to the [Radiata CLA](CLA.md). It grants the maintainer the right to
relicense contributions (this keeps future options like commercial licensing open while the project
remains open source) and includes a standard patent grant. You keep every right to use your own work
elsewhere.

**How to sign:** in the description of your **first** pull request, include this exact sentence:

> I have read the Radiata CLA (CLA.md) and I agree to it for this and all my future contributions to
> Radiata.

Pull requests from contributors who haven't agreed to the CLA can't be accepted, no matter how good the
code is. Please don't skip this.

## Building and testing

Requires Windows and the .NET 8 SDK. Close any running `Radiata.exe` before building; a running instance
locks the output DLLs.

```
dotnet build ControllerWheel.csproj -c Debug
dotnet run --project tools/TestHarness -c Debug -- all
dotnet run --project tools/SettingsSmokeProbe -c Debug
bin\Debug\net8.0-windows\Radiata.exe --check-locales locales.txt
```

These are the commands `.github/workflows/tests.yml` runs on every push and pull request (on
`windows-latest`), and a pull request should pass all of them.

- **`TestHarness`** is the test suite. `all` runs the default suite: pure Core logic and read-only source
  scans. Run it for every change; changes to `Core` logic should come with a harness check where
  practical.
- **`SettingsSmokeProbe`** opens every Settings editor for real. A green build does not prove the
  Settings window opens, because a WPF resource lookup can compile and still throw at runtime. Run it
  after touching any Settings XAML, shared styles or `SettingsTheme.xaml` (exit code 0 means pass).
  Window-level styles must be `{DynamicResource}`, not `{StaticResource}`.
- **`--check-locales`** writes a localization parity report to the file you name. Read it: it must list
  no `MARKUP`, `PLURAL` or `STALE` entries. Delete `locales.txt` afterwards rather than committing it.

The working tree uses CRLF line endings on Windows while the repository stores LF (`.gitattributes`
handles the conversion). Don't convert or renormalize files, and use git rather than a byte-level diff
tool to see what you changed.

## Strings and Help text

- **UI strings.** User-visible text in XAML is written `{loc:T Some label}` (or `loc:LocRich.Source` for
  prose with inline markup). Strings shown from C# are constants in `Core/UiText.cs` and displayed
  through `Loc.T`. After adding or changing any XAML string, regenerate the extracted catalog from the
  repository root and commit the result:

  ```
  dotnet run --project tools/ExtractStrings
  ```

  This rewrites `Core/UiStrings.g.cs` (never edit that file by hand). It exits non-zero on a string the
  runtime would silently mangle, such as an unquoted `{loc:T}` value containing `,` `=` `{` or `}`. The
  header of `tools/ExtractStrings/Program.cs` describes its other mode, which converts a XAML file's
  plain literals.
- **Help text.** Edit only the English source, `Core/HelpContent.cs`. Don't machine-translate the
  Spanish, German, Japanese and Arabic text: those translations are refreshed by the maintainer in
  batches, and any string without a translation falls back to English automatically. The one exception
  is cleanup: when you reword or remove a string that already has translations, the old text becomes an
  orphaned key, which `--check-locales` reports as `STALE`. Delete those orphaned entries from the
  `Core/HelpText*.cs` and `Core/UiText*.cs` maps (and change nothing else in them) so the check passes.
- If you add or change a config key or an action type, update [CONFIG.md](CONFIG.md) in the same pull
  request.

## Code style

- Match the surrounding code's style.
- **Comments state the current constraint**.
- Keep `Core` OS-agnostic and WPF-free; it holds the platform-agnostic logic, and the WPF project
  is the shell around it.
- Keep a pull request to one change. Unrelated cleanups make review slower and are likely to be sent
  back.

## Pull requests

Use the pull request template: describe what changed and why, link the issue, and list the checks you
ran and anything you tested by hand (controller model, connection type, Windows version).

This GitHub repository is a published snapshot of the project's development tree, so pull requests are
**reviewed here on GitHub but not merged with the Merge button**. When a pull request is accepted, the
maintainer ports the change into the development tree, credits you as the commit author, and it reaches
this repository with the next published snapshot. The pull request is then closed with a note pointing to
the commit. A closed pull request with that note is an accepted one.

## Trademarks

"Radiata" and the Radiata flower mark are trademarks of Jesse Tarter-Holden (né Jesse Holden) and are **not** licensed by the
GPL; see [TRADEMARK.md](TRADEMARK.md). Forks must use a different name and mark.
