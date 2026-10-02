## Welcome to Universal Device Toolkit contributing guide!

### Other language versions of this contributing guide:
* [简体中文版](CONTRIBUTING_zh-hans.md)

Thanks for investing your time in contributing to this project! Given the growing popularity of UDT, here are a few rules to follow to ensure that your contribution goes smoothly.

<br/>

_Due to large number of issues created, those that do not meet the criteria will be deleted without warning. Repeating offenders will be banned._

<br/>


**Development setup** — scripts & tools index: [`Docs/SCRIPTS.md`](Docs/SCRIPTS.md)

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The supported product build is Windows; macOS/Linux can build portable libraries and the CrossPlatform CLI.
2. Install [Node.js 20+](https://nodejs.org/) (Electron client; official packaging is Windows-only)
3. Clone the repo: git clone https://github.com/SSC-STUDIO/UniversalDeviceToolkit.git
4. Restore (CI-aligned, Windows product graph): `dotnet restore UniversalDeviceToolkit.sln --locked-mode`
5. Build: `dotnet build -c Release -m:1 --no-restore`
6. Run tests: `dotnet test -c Release`  
   Or CI fail-fast layers only: `pwsh ./Scripts/Run-TestFailFast.ps1`

> [!NOTE]
> The full solution build is **Windows-only** (the Host and Lib target
> `net10.0-windows10.0.26100.0` with forced win-x64). On macOS/Linux use the
> portable path instead: `./build.sh Release` builds the cross-platform
> libraries, the `UniversalDeviceToolkit.CrossPlatform` CLI, and
> `UniversalDeviceToolkit.CrossPlatform.Tests` run there (see
> `Docs/DEPLOYMENT.md` → "Cross-platform builds").

**Electron client (UI)**

The UI is an Electron app in `UniversalDeviceToolkit.Electron/` (Node.js +
electron-vite + React; not part of the .NET solution). Install its
dependencies once, then start it:

```bash
cd UniversalDeviceToolkit.Electron
npm ci            # first time only (uses package-lock.json)
npm run dev       # dev server + Electron window (hot reload)
npm start         # run the built output (after `npm run build`)
npm run lint      # ESLint gate (errors fail CI; compiler-migration rules stay warnings)
npm run typecheck # TS type check (web + main/preload)
npm test          # renderer/main/installer contract tests
```

The repository-root `package.json` only forwards `npm run dev|build|lint|typecheck|start|dist*` to `UniversalDeviceToolkit.Electron/` so those commands also work from the repo root; it has no dependencies of its own. Its `version` is part of the release version train and must match `Directory.Build.props` (enforced by `PackagingGuardTests`).

In Visual Studio the solution contains a thin `UniversalDeviceToolkit.Electron`
launcher project (no-op stub exe). Set it as the **startup project** and press
**F5** — its "Electron (npm run dev)" launch profile runs `npm run dev` for you.

> **Do not set `UniversalDeviceToolkit.Host` as the startup project.** The Host
> is a headless JSON-RPC backend (stdio-based) that Electron spawns
> automatically when the app starts; it never shows a window. See
> `Docs/ARCHITECTURE.md` for the process model.

**Cross-platform development (macOS / Linux, experimental)**

The supported product is Windows. Official releases (`Release.yml`) publish
Windows NSIS installers with a win-x64 Host. macOS/Linux work is experimental:
there is no official Electron release, and local `npm run dist:mac` /
`npm run dist:linux` output is not a release artifact.

The Electron shell can be started for UI work on macOS/Linux:

```bash
cd UniversalDeviceToolkit.Electron
npm ci            # first time only
npm run dev       # dev server + Electron window (hot reload)
npm run lint      # ESLint gate
npm run typecheck # TS type check
```

A portable Host (`net10.0`, `UDTWindows=false`) stubs most Windows-only RPC
as `-32099`. Do not publish the default
Windows TFM for `osx-*` / `linux-x64`.

```bash
# Experimental portable Host (not a release artifact)
UDT_PLATFORM=linux ./build.sh host
UDT_PLATFORM=macos ./build.sh host

# Equivalent:
dotnet publish UniversalDeviceToolkit.Host/UniversalDeviceToolkit.Host.csproj \
    -c Release -r linux-x64 -p:UDTWindows=false --self-contained \
    -o UniversalDeviceToolkit.Host/publish/linux-x64

# Windows (x64) — shipping path embedded into the NSIS installer
dotnet publish UniversalDeviceToolkit.Host/UniversalDeviceToolkit.Host.csproj \
    -c Release -r win-x64 --self-contained \
    -o UniversalDeviceToolkit.Host/publish/win-x64
```

> [!NOTE]
> Hardware control remains Windows-only. See `Docs/DEPLOYMENT.md` for the
> Windows release path versus the experimental portable Host, and
> `Docs/ARCHITECTURE.md` → "Platform Notes" for Electron shell chrome.

NuGet restores are reproducible via committed per-project `packages.lock.json` files (`RestorePackagesWithLockFile` in `Directory.Build.props`). CI always uses `dotnet restore … --locked-mode`. Use that flag locally when validating against CI; omit it only when you intentionally refresh lock files after package version changes, then commit the updated `packages.lock.json` files. `Make.bat` and most local scripts rely on implicit restore during build/publish and do not force `--locked-mode`, so casual offline builds are not blocked by a strict lock mismatch.

The solution has 23 projects (22 .NET + the Electron launcher). Build sequentially (`-m:1`) to avoid VBCSCompiler lock conflicts. See the "Solution Structure" tree in Docs/DEPLOYMENT.md for the full project map.

**Folder naming.** Repository folders are PascalCase (`Assets/`, `Docs/`, `Packaging/`, `Resources/`, `Scripts/`, `Site/`, `Tools/`, `UniversalDeviceToolkit.*/`). A sub-folder keeps an external spelling only when one exists: `Packaging/winget` and `Packaging/scoop` are tool names, `Docs/Skills/udt-hardware-cli` is the skill id, and everything inside `UniversalDeviceToolkit.Electron/` follows the Node layout (`src/`, `tests/`, `resources/`). `Resources/` is published to GitHub Pages as lowercase `/resources/` because installed clients fetch that URL (`AppIdentity.ResourcesBaseUrl`); do not rename the published path.

**Host tests** are split by project (see `Docs/TEST_DIAGNOSTICS.md`):

| Project | Role | CI |
|---|---|---|
| `UniversalDeviceToolkit.Tests.Contracts` | Guard + Security | fail-fast, no category filter |
| `UniversalDeviceToolkit.Fast.Tests` | Isolation-free unit | after Contracts |
| `UniversalDeviceToolkit.Tests` | Parallel unit | main parallel layer |
| `UniversalDeviceToolkit.Tests.Stateful` | Localization / Settings / ProcessState / PowerMode collections | last; collection parallelism off |

`TestCategories` (`Security`, `Guard`, `Unit`): at most one Category trait per class. CI selects by project; Category is optional documentation. Do not add `Coverage`, `Plugin`, `Utils`, `Controller`, or `Smoke`.

Process-wide mutable tests use `[Collection(TestCollections.…)]` and live in `Tests.Stateful` (`parallelizeTestCollections: false`). Contracts and Unit keep collection parallelism on.

Electron UI contracts in `UniversalDeviceToolkit.Electron`: `npm run lint`, `npm run typecheck`, then `npm test`.

<br/>
**1. Before reporting an issue make yourself familiar with the README**

[README](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/blob/master/README.md) is regularly updated to include answers to frequently asked questions as well as information about most common issues. Take your time to go through what is there before creating an issue or starting a discussion.

**2. Check already reported issues**

Go through [issues](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues?q=is%3Aissue) that were already reported, as well as [discussions](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/discussions?discussions_q=). Do not create duplicate issues or discussions. Even if the issue is marked as closed, you can still leave a comment there.

**3. Use English**

This makes it easier for everyone to follow the conversation.

**4. Respect scope of the project**

This is not meant to be a do-it-all type of application. The vision is trusted local hardware control for machines with a testable provider (Lenovo Legion first; other brands only with a real provider), plus CLI for people and agents. Do not request generic Windows-utility features. New brands need a testable provider, not a feature-request issue.

**5. Verify your problem before creating an issue**

Make sure that a bug is really a bug in UDT - this isn't a free system troubleshooting forum. If you use modified version of Windows or your Windows is acting funny, that's on you.

**6. Describe your problem as best as you can**

Providing good description is key. Fill out all the fields of the form when creating an issue, including logs. The better the description, the higher the chance that we will understand each other correctly.

**7. Give a good title to issues and discussions**

It is a lot easier to browse the list and follow issues and discussions. "Error when using UDT" is not a good title.

**8. Stay on topic**

Do not leave unnecessary comments or spam.

**9. One problem per issue**

Do not report many problems or request many features withing one issue. Make one issue or discussion per problem/topic/idea. This makes it easier to follow.

**10. Translations**

Translation contributions are done using [Crowdin](https://crowdin.com/project/llt), so please request access to the project there if you want to contribute.

**10.1 Culture naming convention (cross-platform rule)**

Culture names in UDT must be written in the **BCP 47 / RFC 5646 canonical form** — the exact form produced by `CultureInfo.Name` under .NET's ICU globalization on all platforms:

| Subtag | Rule | Example |
|---|---|---|
| Language | lowercase | `en` `pt` `zh` `uz` |
| Script | TitleCase | `Hans` `Hant` `Latn` |
| Region | UPPERCASE | `BR` `NL` `PT` `UZ` |

The canonical culture set is the single source of truth in `LocalizationCatalog.SupportedCultures` (`UniversalDeviceToolkit.Lib.Abstractions/Localization/LocalizationCatalog.cs`):

`ar bg cs de el en es fr hu it ja lv nl-NL pl pt pt-BR ro ru sk tr uk uz-Latn-UZ vi zh-Hans zh-Hant`

A culture name must be spelled **byte-for-byte identically** in every one of these places:

1. Resource file names — `Resource.zh-Hans.resx` (never `zh-hans`)
2. Generated satellite directories — `zh-Hans/*.resources.dll`
3. `LocalizationCatalog.SupportedCultures` and the installer's `AppLanguages`
4. The persisted `lang` file
5. `catalog.json` `culture` fields and pack URLs
6. `crowdin.yml` locale mappings
7. Packaging / CI / smoke scripts, and culture lists in tests

Inputs stay lenient: `ResolveSupportedLanguage` and `LanguagePackManager` accept legacy or external variants case-insensitively (e.g. an old `lang` file containing `zh-hans`), but everything UDT *writes* — the `lang` file, catalogs, pack assets, new resource files — must use the canonical form. Forbidden spellings: all-lowercase (`zh-hans`), mixed casing (`zh-Hans` vs `zh-hans`), underscores (`zh_hans`), and non-canonical regions (`zh-cn`).

Why: satellite probing uses `culture.Name` with a case-sensitive directory lookup on Linux/macOS. Canonical file names are the only spellings that resolve there; on Windows they work too (case-insensitive file system).

Enforced by `Scripts/Assert-CultureNaming.ps1` in CI.

**11. Pull requests**

Pull requests are welcome (of course). Unless you create a very simple and understandable PR, make an issue first and describe the problem you are solving. It doesn't make sense to spend time working on an idea that will be rejected, because it doesn't fit the project vision. Follow the code style and architecture of the project.

<br/>

Once again, thanks for investing your time in helping UDT get better!

