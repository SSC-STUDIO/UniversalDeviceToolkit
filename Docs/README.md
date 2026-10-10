# Documentation index

The default Windows UI is the native **WebView2** shell (`Apps/Windows`). The separate **Electron compatibility** shell and shared React renderer live in `Apps/Electron`. Both talk to the same headless **.NET Host** (`Apps/Host`) over JSON-RPC; business logic stays in .NET. The plugin system was retired in 6.1. Stable release is 6.1.4, with a default WebView2 installer and a separate Electron compatibility installer. The Windows packages are unsigned; verify downloads against the release SHA256 manifest.

The current shell keeps the primary navigation compact: **Dashboard**, **Actions** (automation and macros), **Keyboard**, **Tools** (cleanup, network, drivers, system adjustments, and pointer controls), **Settings**, and **About**. Older `/automation`, `/macro`, and `/optimization` URLs remain compatibility redirects.

Start here, then follow the topic docs.

## Start here

| Doc | Notes |
| --- | --- |
| [../README.md](../README.md) / [../README_zh-hans.md](../README_zh-hans.md) | Product overview |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | How to build and contribute |
| [../CHANGELOG.md](../CHANGELOG.md) | Project changelog |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Process model: WebView2 primary / Electron compatibility + shared renderer + Host |
| [DEPLOYMENT.md](./DEPLOYMENT.md) | Build, package, release |

## Product and runtime

| Doc | Notes |
| --- | --- |
| [LanguagePacks.md](./LanguagePacks.md) | Shared renderer i18n + Host `.resx` + resource catalog |
| [NamespaceMigration.md](./NamespaceMigration.md) | Completed LLT → UDT ABI cutover and remaining compat surfaces |
| [NetworkAcceleration.md](./NetworkAcceleration.md) | Built-in network acceleration |
| [DEVICE_PROVIDERS.md](./DEVICE_PROVIDERS.md) | Brand EC / hardware providers |
| [UI_PERFORMANCE.md](./UI_PERFORMANCE.md) | Renderer performance principles and profiling tools |
| [../Apps/Electron/resources/README.md](../Apps/Electron/resources/README.md) | Runtime extras vs `Assets/` vs `buildResources/` |
| [SECURITY.md](./SECURITY.md) | Vulnerability reporting |
| [TEST_DIAGNOSTICS.md](./TEST_DIAGNOSTICS.md) | Test map: Host / Windows shells, CI ladder, testhost locks |
| [CLI.md](./CLI.md) | `udt` contract: `--json`, `doctor`, exit codes (`udt-cli` alias) |
| [Skills/udt-hardware-cli/SKILL.md](./Skills/udt-hardware-cli/SKILL.md) | Copyable Agent skill for local `udt` |
| [SCRIPTS.md](./SCRIPTS.md) | Scripts & Tools index: `Scripts/*.ps1` and `Tools/` usage |
| [CODE_OF_CONDUCT.md](./CODE_OF_CONDUCT.md) | Community guidelines |

## Promotion and community

| Doc | Notes |
| --- | --- |
| [Promotion/PROMOTION_EN.md](./Promotion/PROMOTION_EN.md) / [Promotion/PROMOTION_CN.md](./Promotion/PROMOTION_CN.md) | Ready-to-post social copy |
| [Promotion/COMMUNITY_OUTREACH.md](./Promotion/COMMUNITY_OUTREACH.md) | Where to post, and the weekly star digest workflow |
| [Promotion/SUBMISSIONS.md](./Promotion/SUBMISSIONS.md) | Directory and awesome-list tracker |

## Retired surfaces

The plugin system was retired in 6.1; plugin loading, the Plugin Extensions page, and catalog tooling are gone from this repository. The WPF and Avalonia clients were retired in 6.0. Their authoring docs, audits, and migration matrices live only in git history and are not the shipping UI contract.

Backend entry point for agents: [UniversalDeviceToolkit.Host](../Apps/Host) domain handlers (`Device`, `Telemetry`, `Actions`, `Keyboard`, `Tools`, `Settings`, `Application`).
