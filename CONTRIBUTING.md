# Contributing to Radiata

Radiata is a solo-maintained project and **isn't accepting pull requests yet**. Bug reports are very
welcome, and the source is here to read, build and fork under the GPL.

## Reporting a bug

Open an issue with the bug report form. It asks for what is needed to reproduce a controller problem:

- the Radiata version (**Settings ▸ About**) and your Windows version;
- the controller model and how it connects (USB, Bluetooth or wireless dongle);
- the tray tooltip's isolation mode (hover the tray icon: it reads "Radiata - isolated" or names the
  reason it isn't);
- the steps, what you expected, and what happened;
- optionally, the diagnostic ZIP from **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to
  Developer…**. Review it before attaching it to a public issue: it can include device identifiers,
  account names in file paths, and application or game names.

**Security problems are not issues.** Report them privately; see [SECURITY.md](SECURITY.md).

## Building and testing

Requires Windows and the .NET 8 SDK. Close any running `Radiata.exe` before building; a running instance
locks the output DLLs.

```
dotnet build ControllerWheel.csproj -c Debug
dotnet run --project tools/TestHarness -c Debug -- all
dotnet run --project tools/SettingsSmokeProbe -c Debug
bin\Debug\net8.0-windows\Radiata.exe --check-locales locales.txt
```

These are the commands `.github/workflows/tests.yml` runs on every push (on `windows-latest`).

- **`TestHarness`** is the test suite. `all` runs the default suite: pure Core logic and read-only source
  scans.
- **`SettingsSmokeProbe`** opens every Settings editor for real. A green build does not prove the
  Settings window opens, because a WPF resource lookup can compile and still throw at runtime (exit code
  0 means pass).
- **`--check-locales`** writes a localization parity report to the file you name. A clean report lists
  no `MARKUP`, `PLURAL` or `STALE` entries.

The working tree uses CRLF line endings on Windows while the repository stores LF (`.gitattributes`
handles the conversion). Don't convert or renormalize files, and use git rather than a byte-level diff
tool to see what changed.

Engineering notes for each subsystem start at [docs/README.md](docs/README.md); the `config.json` schema
is [CONFIG.md](CONFIG.md).

## Trademarks

"Radiata" and the Radiata flower mark are trademarks of Jesse Tarter-Holden and are **not** licensed by the
GPL; see [TRADEMARK.md](TRADEMARK.md). Forks must use a different name and mark.
