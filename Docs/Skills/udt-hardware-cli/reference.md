# udt feature names

CLI uses kebab-case. Host JSON-RPC uses camelCase. Do not mix them.

CLI only exposes names that probe as supported on **this** machine. Always `feature --list` first.

## On CLI

| CLI (`udt feature`) | RPC `feature` key |
| --- | --- |
| `always-on-usb` | `alwaysOnUsb` |
| `battery` | `battery` |
| `battery-night-charge` | `batteryNightCharge` |
| `flip-to-start` | `flipToStart` |
| `fn-lock` | `fnLock` |
| `hdr` | `hdr` |
| `hybrid-mode` | `hybridMode` |
| `instant-boot` | `instantBoot` |
| `microphone` | `microphone` |
| `one-level-white-keyboard-backlight` | `oneLevelWhiteKeyboard` |
| `over-drive` | `overDrive` |
| `panel-logo-backlight` | `panelLogo` |
| `ports-backlight` | `portsBacklight` |
| `power-mode` | `powerMode` |
| `refresh-rate` | `refreshRate` |
| `resolution` | `resolution` |
| `speaker` | `speaker` |
| `touchpad-lock` | `touchpadLock` |
| `win-key` | `winKey` |
| `white-keyboard-backlight` | `whiteKeyboard` |

Typical `power-mode` values: `quiet`, `balance`, `performance` (and machine-specific extras). Use `feature set power-mode --list`.

`udt-cli` is a one-train alias of `udt`; `udt feature` and `udt-cli feature` are equivalent.

## RPC only (not CLI)

`gSync`, `igpuMode`, `itsMode`, `dpiScale`. Also not CLI: sensors, God Mode / fan tables, GPU overclock, boot logo, macros.

## Other CLI verbs

| Verb | RPC-ish equivalent | Agent default |
| --- | --- | --- |
| `spectrum profile` / `brightness` | `spectrum.*` | Allow |
| `rgb get` / `set` | `rgb.*` | Allow |
| `quickAction` | automation pipelines by **display name** | Allow (names are not stable IDs) |
| `status` | `app.getStatus` | Allow |
| `network` | `network.*` | Refuse unless the user explicitly asks |
| `shell` | shell-integration plugin | Refuse (`--install` is a stub) |
