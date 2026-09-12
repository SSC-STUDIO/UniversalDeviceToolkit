# Electron runtime resources

This folder is **runtime extras** copied next to the packaged app (tray images, Host publish output, install-channel markers). It is not the brand source of truth.

| Location | Role |
| --- | --- |
| [`Assets/`](../../../Assets/README.md) | Brand and marketing media (icons, screenshots, logos) |
| [`buildResources/`](../buildResources/README.md) | electron-builder packaging icons (`icon.ico` / `icon.icns` / Linux PNGs) |
| `resources/` (this folder) | Files the running app loads: tray templates, published Host, optional channel files |

Do not add a second copy of `Assets/Icon.ico` here. Tray PNGs are generated from `Assets/Brand/` as described in `buildResources/README.md`.
