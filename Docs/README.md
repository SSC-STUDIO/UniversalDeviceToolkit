# Documentation index

Shipping UI is the **Electron** shell (`UniversalDeviceToolkit.Electron`) talking to a headless **.NET Host** (`UniversalDeviceToolkit.Host`) over JSON-RPC. Business logic stays in .NET; Electron renders Host data only (the plugin system was retired in 6.1).

The current shell keeps the primary navigation compact: **Dashboard**, **Actions** (automation and macros), **Keyboard**, **Tools** (cleanup, network, drivers, system adjustments, and pointer controls), **Settings**, and **About**. Older `/automation`, `/macro`, and `/optimization` URLs remain compatibility redirects.

Start here, then follow the topic docs.

## Start here

| Doc | Notes |
| --- | --- |
| [../README.md](../README.md) / [../README_zh-hans.md](../README_zh-hans.md) | Product overview |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | How to build and contribute |
| [../CHANGELOG.md](../CHANGELOG.md) | Project changelog |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Process model: Electron UI + Host |
| [DEPLOYMENT.md](./DEPLOYMENT.md) | Build, package, release |

## Product and runtime

| Doc | Notes |
| --- | --- |
| [LanguagePacks.md](./LanguagePacks.md) | Electron i18n + Host `.resx` + online catalog |
| [NamespaceMigration.md](./NamespaceMigration.md) | Completed LLT → UDT ABI cutover and remaining compat surfaces |
| [NetworkAcceleration.md](./NetworkAcceleration.md) | Built-in network acceleration |
| [DEVICE_PROVIDERS.md](./DEVICE_PROVIDERS.md) | Brand EC / hardware providers |
| [UI_PERFORMANCE.md](./UI_PERFORMANCE.md) | Renderer performance principles and profiling tools |
| [../UniversalDeviceToolkit.Electron/resources/README.md](../UniversalDeviceToolkit.Electron/resources/README.md) | Runtime extras vs `Assets/` vs `buildResources/` |
| [SECURITY.md](./SECURITY.md) | Vulnerability reporting |
| [TEST_DIAGNOSTICS.md](./TEST_DIAGNOSTICS.md) | Test map: Host / Electron, CI ladder, testhost locks |
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

Backend entry point for agents: [UniversalDeviceToolkit.Host](../UniversalDeviceToolkit.Host) RPC handlers (`Rpc/Handlers/*`).
