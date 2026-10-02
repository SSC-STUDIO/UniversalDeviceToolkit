# Language Packs

UDT Online ships an English-only host. Non-English UI is delivered as culture satellite packs from the online resource catalog. The Full portable package and Full installer retain all supported satellites for offline use.

## Catalog protocol

Catalog URL defaults to `AppIdentity.StableResourceCatalogUrl` (override with `UDT_RESOURCE_CATALOG_URL`).

Each language entry:

| Field | Purpose |
|---|---|
| `culture` | Culture name in BCP 47 canonical form (e.g. `zh-Hans`) |
| `parent` | Optional parent culture for fallback lookup |
| `size` | Package size in bytes |
| `sha256` | Content hash |
| `resourceVersion` | Pack revision independent of app version |
| `minAppVersion` | Minimum host app version that may install the pack |
| `url` | Download URL |
| `displayName` | Human-readable label |

## Service API (`LanguagePackManager`)

- `QueryCatalogAsync` — list available packs
- `InstallAsync` — download → verify SHA256 → stage → validate satellite assemblies → atomic replace culture directories; reinstalling through this method is the repair/update path
- `Uninstall` / `QueueUninstall` / `ProcessPendingUninstall` — remove culture dirs (pending uninstall survives restart when the culture is active)
- Progress via `IProgress<float>`; cancellation via `CancellationToken`
- Structured failures: `LanguagePackException` + `LanguagePackFailureKind`

Fallback: if the language zip fails, extract matching culture directories from the full portable release zip.

## Culture resolution (non-Chinese UI strings)

Resource lookup uses exact culture → parent chain → English. Never falls back to Chinese for non-Chinese UI cultures.

## Startup language gate

Electron shows the in-app language picker (settings / first-run) **before** the dashboard is usable when no language is saved. Host `.resx` satellites and the Electron `i18n/locales` trees are separate: UI chrome is Electron i18n; Host strings stay in `.resx`. Online packs still install from the catalog.

1. Prompt when no saved language or device-setup is incomplete
2. Download failure presents **Retry / Continue in English / Exit** (never silent auto-English close)
3. Cancel/close cancels in-flight download and cleans temp dirs
4. Safe-start / offline mode may continue in English without requiring a pack download

Settings-page installs use in-page progress (no orphan top-level install window).

## Shipping footprint

Online packages must not ship non-English satellite assemblies. `Scripts/Build-LanguageAssets.ps1` first creates one archive per supported culture (Host `.resx` satellites plus Electron locale extras where packaged), then the Electron Online nsis-web payload is packed after `Scripts/Prune-ShippingFootprint.ps1` keeps only English Host satellites. Language packs still install from the in-app catalog. The release workflow does not prune cultures from the Full payload before language archives are created. `Scripts/Build-LanguageAssets.ps1` fails when a supported culture is missing the main Host satellite instead of publishing an incomplete pack.

Logs for Host and Electron live in one folder: `%LOCALAPPDATA%\UniversalDeviceToolkit\logs` (`main.log`, `renderer.log`, `host.log`). Override with `UDT_LOG_PATH` (preferred) or the compatibility alias `LLT_LOG_PATH`.
