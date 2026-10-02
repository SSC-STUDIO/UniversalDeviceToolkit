# Test map

Host tests are split by project so Test Explorer, the solution, and CI use the same layers. Electron UI contracts run with `npm test`; the plugin system and its test projects were retired in 6.1.

## Projects

| Project | What it contains | How CI runs it |
| --- | --- | --- |
| `UniversalDeviceToolkit.Tests.Contracts` | Guard and Security contracts (layout, CI YAML, signatures, path safety) | Fail-fast, no category filter |
| `UniversalDeviceToolkit.Fast.Tests` | Isolation-free unit tests (network proxy IPC) | After Contracts |
| `UniversalDeviceToolkit.Tests` | Parallel unit tests (no process-wide shared state) | Main parallel layer |
| `UniversalDeviceToolkit.Tests.Stateful` | `[Collection(Localization/Settings/ProcessState)]` and PowerMode cache tests | Last; collection parallelism off |
| `UniversalDeviceToolkit.CrossPlatform.Tests` | Portable diagnostics CLI | `cross-platform-cli` job (Ubuntu / macOS / Windows matrix) |
| `UniversalDeviceToolkit.Electron/tests/*.mjs` | Electron/Host RPC, renderer, installer, and security contracts | `npm test` (with lint and typecheck) |

`TestCategories` (`Security`, `Guard`, `Unit`) is at most one trait per class. After the project split, CI selects by project; Category is optional documentation. Do not add `Coverage`, `Plugin`, `Utils`, `Controller`, or `Smoke`.

Host tests keep namespaces under `UniversalDeviceToolkit.Tests.*` (folder = namespace). Explorer grouping is by project.

## How to run

```bash
# Fail-fast contracts + Fast.Tests (same order as Scripts/Run-TestFailFast.ps1)
dotnet test UniversalDeviceToolkit.Tests.Contracts/UniversalDeviceToolkit.Tests.Contracts.csproj -c Release
dotnet test UniversalDeviceToolkit.Fast.Tests/UniversalDeviceToolkit.Fast.Tests.csproj -c Release

# Parallel unit, then stateful
dotnet test UniversalDeviceToolkit.Tests/UniversalDeviceToolkit.Tests.csproj -c Release
dotnet test UniversalDeviceToolkit.Tests.Stateful/UniversalDeviceToolkit.Tests.Stateful.csproj -c Release

# Cross-platform diagnostics
dotnet test UniversalDeviceToolkit.CrossPlatform.Tests/UniversalDeviceToolkit.CrossPlatform.Tests.csproj -c Release

# Electron UI contracts
cd UniversalDeviceToolkit.Electron
npm run lint
npm run typecheck
npm test
```

xUnit: Contracts and Unit set `parallelizeTestCollections: true`. Stateful sets `false` and uses `[CollectionDefinition(..., DisableParallelization = true)]` for Localization / Settings / ProcessState.

## CI ladder

Windows job `build-test-and-smoke` in `.github/workflows/Ci-tests.yml`:

1. Contracts
2. Fast.Tests
3. Unit (coverage)
4. Stateful (coverage)

The same workflow also runs `electron-ui-tests` (`npm run lint`, `npm run typecheck`, then `npm test`).

Release.yml runs the same four Host projects after the solution build.

## testhost file locks

`dotnet test` can leave `testhost.exe` holding output DLLs on Windows, so a later build fails with MSB3021 (file in use). The assembly name `UniversalDeviceToolkit.Lib.dll` is unrelated to the test project folder name.

Workarounds:

```bash
dotnet build UniversalDeviceToolkit.Tests/UniversalDeviceToolkit.Tests.csproj -c Release
dotnet test UniversalDeviceToolkit.Tests/UniversalDeviceToolkit.Tests.csproj -c Release --no-build
```

```powershell
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force
```

```bash
dotnet build UniversalDeviceToolkit.Tests/UniversalDeviceToolkit.Tests.csproj -c Release -o _test_out
dotnet test _test_out/UniversalDeviceToolkit.Tests.dll -c Release --no-build
```

Close Visual Studio Test Explorer / Live Unit Testing if the default `bin` path stays locked. CI always builds once, then tests with `--no-build`.

## Related

- Unicode: `node Tools/CheckSourceUnicode/check-unicode.mjs`
- Logs: `%LOCALAPPDATA%\UniversalDeviceToolkit\logs` (`main.log`, `renderer.log`, `host.log`)
- Host Debug build if `Host.exe` is locked: `-o %TEMP%\udt-host-build`
