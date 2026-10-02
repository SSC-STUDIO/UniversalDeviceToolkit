# Adding a hardware provider (brand EC adaptation)

UDT ships Lenovo hardware control built in. Since 5.x the architecture accepts
additional **brand providers** behind vendor seams — ASUS (ATKACPI), HP
(WMI BIOS), Razer (EC over USB HID), Alienware/Dell (AWCC WMAX), Acer
(WMID Gaming), Gigabyte (GB_WMIACPI, sensors-only) and MSI (EC port I/O
over PawnIO) are the reference implementations. This document describes
how to add another brand.

### EC port I/O (PawnIO)

Brands whose control lives in EC RAM (MSI today, Clevo next) go through
`IEcChannel` / `PawnIoEcChannel` (`Lib/System/EC/`): standard ACPI
transactions on ports 0x66/0x62 backed by the PawnIO driver via
RAMSPDToolkit-NDD's `DriverManager` (already in the dependency closure via
LibreHardwareMonitorLib — no bundled kernel driver, no custom signed
module). Discipline: every transaction serialized through the named
`Global\Access_EC` mutex, status-bit polling with timeouts, all failures
degrade to `IsAvailable = false`. EC writes happen only on explicit user
mode switches, and each brand probes its register layout read-only first
(see `MsiPowerModeFeature`'s Gen1/Gen2 detection).

## The moving parts

| Layer | Where | Role |
|---|---|---|
| Device catalog | `UniversalDeviceToolkit.Lib/DeviceSupport/LenovoDeviceSupportProvider.cs` | Detection (vendor aliases, model keywords, MTMs) + feature gate per pack |
| Catalog JSON | `Resources/device-packs.json` | Generated mirror of the catalog — **the single source the release pipeline and installers read**. Regenerate after any catalog edit |
| Protocol channel | e.g. `UniversalDeviceToolkit.Lib/System/AsusAtkDriver.cs` | Brand-specific hardware path (WMI/ACPI, USB HID, EC) |
| Feature backend | e.g. `UniversalDeviceToolkit.Lib/Features/Asus/AsusPowerModeFeature.cs` | `IFeature<T>` implementation; dashboard cards light up automatically |
| Facade | `UniversalDeviceToolkit.Lib/Features/PowerModeFeature.cs` | Vendor-agnostic concrete facade, Lenovo first then other brands |
| Sensors probe | `UniversalDeviceToolkit.Lib/Controllers/Sensors/SensorsController.cs` | Probe chain V5→…→V1→brand→generic |
| IoC | `UniversalDeviceToolkit.Lib/IoCModule.cs` | Brand feature (`selfOnly: true`), driver singleton, sensors controller |
| On-demand packs | `DevicePackManager` + `StartupDeviceSetupCoordinator` | device-pack.json download/install like language packs |

## Rules of the road

1. **Self-disable, always.** Every probe (`IsSupportedAsync`) must check vendor
   match AND protocol presence, and return `false` on any error. A provider must
   never poke hardware that is not there. The ATK implementation only writes on
   explicit user action and verifies by read-back.
2. **No hardware, no claims.** Do not ship a provider for hardware nobody can
   test. Reference implementations (G-Helper, OmenMon, Linux drivers) are the
   protocol source of truth; verify constants against them, never from memory.
3. **One catalog.** Packs live in `LenovoDeviceSupportProvider.BuiltInCatalog`.
   Hardware packs list `"lenovo-hardware-controls"` (the generic hardware gate
   id — the name predates multi-vendor support) in `EnabledFeatures`; basic
   packs keep it in `HiddenFeatures`.
4. **Regenerate the mirror** after catalog edits: `Resources/device-packs.json`
   (packdump / catalog generator). The historical `Tools/Installer/DevicePackSnapshot.cs`
   was retired with the WPF installer.

## Step-by-step (new brand "ACME")

1. Add/upgrade the pack in `LenovoDeviceSupportProvider.cs` with the brand's
   vendor aliases and model keywords. Start as a basic pack; flip
   `EnabledFeatures` to include `"lenovo-hardware-controls", "sensors",
   "power-modes"` only when a protocol provider actually ships.
2. Write the protocol channel (`IAsusAtkDriver`/`AsusAtkDriver` is the
   template): open the device lazily, expose `IsAvailable`, wrap reads/writes
   with never-throw guards.
3. Implement the feature backend (`AsusPowerModeFeature` is the template):
   map brand states onto `PowerModeState` (Quiet/Balance/Performance), reject
   vendor-specific states the UI cannot represent, verify writes by read-back.
4. Register the backend in the facade (`PowerModeFeature`) after Lenovo, and in
   `IoCModule` with `selfOnly: true`.
5. For sensors, subclass `GenericSensorsController` (like
   `AsusSensorsController`) and insert it into the probe chain before
   `GenericSensorsController`.
6. Tests with fakes only (see `AsusPowerModeFeatureTests`): state mapping,
   endpoint probing order, self-disable on wrong vendor / missing device,
   write-verification failure paths, facade preference order.

## Roadmap candidates (not scheduled)

- **Clevo/Tongfang** — same EC channel as MSI (Uniwill EC map per
  clevo-xsm-wmi / NBFC); next candidate, protocol research first.
- **Gigabyte phase 2** — fan modes (Silent/Gaming/Custom) and GPU QBoost via
  raw WMBD writes; needs the semantics proven on real AORUS/AERO hardware
  (no friendly WMI class exists and the vendor docs warn of machine damage).
- **HP phase 2** — true fan RPM + performance-mode read-back via EC registers
  (OmenMon's EC map), which needs a signed PawnIO module or a WinRing0-style
  driver; the current WMI-only implementation tracks session state instead.
- **Razer phase 2** — manual fan control (class 0x0D cmd 0x01) and Boost levels
  (cmd 0x07), gated per model year (Silent/Custom only on 2023 Blades).
- **Fan curves / per-brand tuning** — phase 2, requires community testers per
  brand.
