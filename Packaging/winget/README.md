# Winget Submission Guide

This folder keeps the maintainer-side winget manifest draft for `SSC-STUDIO.UniversalDeviceToolkit`. The canonical submission target remains the upstream [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) repository.

Universal Device Toolkit 6.x uses a new winget identity. Pre-6.x `SSC-STUDIO.LenovoLegionToolkit` manifests were removed from the tree and live only in git history; they are not active release templates.

The repository keeps generated 6.x manifests only after real release assets exist. Do not create placeholder version folders under the active manifest tree.

## Package Identity

- PackageIdentifier: `SSC-STUDIO.UniversalDeviceToolkit`
- PackageName: `Universal Device Toolkit`
- Previous public name: `Lenovo Legion Toolkit`
- Publisher: `SSC-STUDIO`
- License: `GPL-3.0`
- Installer type: `exe`
- Installer scope: `machine`
- Architecture: `x64`

## Release Checklist

1. Create a stable GitHub release with the Full and Online Universal Device Toolkit installers and Electron portable ZIPs:
   `UniversalDeviceToolkit_vX.Y.Z_Full_Setup.exe`
   `UniversalDeviceToolkit_vX.Y.Z_Online_Setup.exe`
   `UniversalDeviceToolkit_vX.Y.Z_Full_win-x64.zip`
   `UniversalDeviceToolkit_vX.Y.Z_Online_win-x64.zip`
2. Confirm the release also includes the checksum manifest:
   `UniversalDeviceToolkit_vX.Y.Z_SHA256.txt`
3. Do not create or submit a new version manifest until the final release asset URL and SHA256 are available.
4. Generate the versioned winget folder and Scoop draft manifest from the final release metadata:
   ```powershell
   .\Packaging\Prepare-PackageManifests.ps1 -Version X.Y.Z -ReleaseDate YYYY-MM-DD -HashManifestPath path\to\UniversalDeviceToolkit_vX.Y.Z_SHA256.txt
   ```
5. Validate the package metadata against the release checksum manifest:
   ```powershell
   .\Packaging\Test-PackageManifests.ps1 -Version X.Y.Z -HashManifestPath path\to\UniversalDeviceToolkit_vX.Y.Z_SHA256.txt
   ```
6. Validate the winget schema locally:
   ```powershell
   winget validate manifests\s\SSC-STUDIO\UniversalDeviceToolkit\X.Y.Z
   ```
7. Test install and uninstall from the manifest on a clean Windows machine:
   ```powershell
   winget install --manifest manifests\s\SSC-STUDIO\UniversalDeviceToolkit\X.Y.Z
   winget uninstall SSC-STUDIO.UniversalDeviceToolkit
   ```
8. Submit the manifest folder to `microsoft/winget-pkgs`.

## Notes

- Use the GitHub Release installer, not local build output.
- Do not point winget at mirror URLs. Mirrors can be listed in community posts, but winget should use the authoritative GitHub Release URL.
- 6.x is a package-manager breaking change. Do not expect an in-place upgrade from the legacy package ID.
- The generated `exe` manifest uses the electron-builder NSIS `/S` switch. Do not reuse `--silent`, `/SILENT`, or Inno Setup `/VERYSILENT`.
