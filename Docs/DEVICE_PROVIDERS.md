# Adding a hardware provider (brand EC adaptation)

UDT ships Lenovo hardware control built in. Since 5.x the architecture accepts
additional **brand providers** behind vendor seams — ASUS (ATKACPI), HP
(WMI BIOS), Razer (EC over USB HID), Alienware/Dell (AWCC WMAX), Acer
(WMID Gaming), Gigabyte (GB_WMIACPI, sensors-only), MSI, Clevo and Tongfang
(EC port I/O over PawnIO) are the reference implementations. This document describes
how to add another brand.

### EC port I/O (PawnIO)

Brands whose control lives in EC RAM (MSI, Clevo and Tongfang) go through
`IEcChannel` / `PawnIoEcChannel` (`Libraries/Device/System/EC/`): standard ACPI
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
| Device catalog | `Libraries/Device/DeviceSupport/LenovoDeviceSupportProvider.cs` | Detection (vendor aliases, model keywords, MTMs) + feature gate per pack |
| Catalog JSON | `Resources/device-packs.json` | Generated mirror of the catalog — **the single source the release pipeline and installers read**. Regenerate after any catalog edit |
| Protocol channel | e.g. `Libraries/Device/System/AsusAtkDriver.cs` | Brand-specific hardware path (WMI/ACPI, USB HID, EC) |
| Feature backend | e.g. `Libraries/Device/Features/Asus/AsusPowerModeFeature.cs` | `IFeature<T>` implementation; dashboard cards light up automatically |
| Facade | `Libraries/Device/Features/PowerModeFeature.cs` | Vendor-agnostic concrete facade, Lenovo first then other brands |
| Sensors probe | `Libraries/Device/Sensors/SensorsController.cs` | Probe chain V5→…→V1→brand→generic |
| IoC | `Libraries/Device/IoCModule.cs` | Brand feature (`selfOnly: true`), driver singleton, sensors controller |
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

- **Clevo/Tongfang coverage** — existing EC providers must retain their
  read-only protocol probes. Additional register layouts require protocol
  research and model-specific hardware validation before writes are enabled.
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

## Verified model identities

Business laptops can report only a machine type or CTO SKU in SMBIOS, without
a ThinkPad/ThinkBook marketing name. The built-in and portable catalogs now
recognize these Lenovo Support entries by machine type while retaining all
basic-mode hardware restrictions. This records identity coverage only;
physical hardware-control validation has not been performed for these models.

- ThinkPad T14 Gen 5 Intel: `21ML`, `21MM` ([Lenovo Support](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkpad-t-series-laptops/thinkpad-t14-gen-5-type-21ml-21mm)).
- ThinkPad T14 Gen 5 AMD: `21MC`, `21MD` ([Lenovo Support](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkpad-t-series-laptops/thinkpad-t14-gen-5-type-21mc-21md)).
- ThinkPad T16 Gen 3: `21MN`, `21MQ` ([Lenovo Support](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkpad-t-series-laptops/thinkpad-t16-gen-3-type-21mn-21mq)).
- ThinkPad X1 Carbon Gen 12: `21KC`, `21KD` ([Lenovo Support](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkpad-x-series-laptops/thinkpad-x1-carbon-12th-gen-type-21kc-21kd)).
- ThinkBook 16 G7 IML: `21MS` ([Lenovo Support](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkbook-series/thinkbook-16-g7-iml/21ms)).
- ThinkBook 16 G7 ARP: `21MW` ([Lenovo Support](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/thinkbook-series/thinkbook-16-g7-arp/21mw)).

Installed catalogs override definitions with the same pack ID. Automatic
selection compares the resulting catalogs together: an exact machine type
outranks a model keyword, model prefix or family match. A hardware pack must
explicitly enable `lenovo-hardware-controls` and must not hide it; omitting the
feature never implies support.

Different packs that share the highest match score fall back to the generic
basic profile until an exact identity is available or the user selects a pack.
SMBIOS placeholder manufacturer text does not override an identifying
computer-system or baseboard manufacturer. Portable clients reject catalogs
with null or empty required fields, invalid collections or duplicate pack IDs.
