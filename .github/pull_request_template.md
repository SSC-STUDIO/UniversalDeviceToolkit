## Summary
- 

## Verification
- [ ] `dotnet build UniversalDeviceToolkit.sln --configuration Release -m:1`
- [ ] `pwsh ./Scripts/Run-TestFailFast.ps1` (Contracts + Fast), then `dotnet test -c Release` for the full ladder
- [ ] `cd UniversalDeviceToolkit.Electron && npm run lint && npm run typecheck && npm test`
- [ ] CHANGELOG.md updated for user-visible changes

## Notes
- 
