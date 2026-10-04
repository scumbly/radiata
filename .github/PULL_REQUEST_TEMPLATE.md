## Summary

<!-- What changed and why. Link the issue it addresses (for example "Fixes #123"). Anything beyond a small fix should have an issue first; see CONTRIBUTING.md. -->

## Testing

Checks run (see CONTRIBUTING.md):

- [ ] `dotnet build ControllerWheel.csproj -c Debug`
- [ ] `dotnet run --project tools/TestHarness -c Debug -- all`
- [ ] `dotnet run --project tools/SettingsSmokeProbe -c Debug` (required if Settings XAML or shared styles changed)
- [ ] `bin\Debug\net8.0-windows\Radiata.exe --check-locales locales.txt` (no `MARKUP`, `PLURAL` or `STALE` entries)

Manual testing, if any (controller model, connection type, Windows version):

<!-- Delete this line and describe what you tried. -->

## Contributor License Agreement

<!-- First pull request only. Tick the box to agree to CLA.md for this and all your future contributions. -->

- [ ] I have read the Radiata CLA (CLA.md) and I agree to it for this and all my future contributions to Radiata.
