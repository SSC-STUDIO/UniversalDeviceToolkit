# UniversalDeviceToolkit Bridge Protocol (P1)

NDJSON JSON-RPC over Host stdio. Line = one JSON object.

```
Request:  {"id":1,"method":"...","params":{...}}
Response: {"id":1,"result":{...}} | {"id":1,"error":{"code":-32601,"message":"..."}}
Event:    {"event":"...","data":{...}}
```

## Domains

### system
| method | params | result |
|---|---|---|
| `system.info` | {} | `{ vendor, model, machineType, biosVersion, isCompatible }` |

### settings (whole-scope get/set + optional dotted path get)
Scopes (1:1 with JSON files): `application, osd, hardwareSensors, balanceMode, godMode, gpuOverclock, integrations, lampArray, fanCurves, packageDownloader, rgbKeyboard, spectrumKeyboard, sunriseSunset, updateCheck, batteryHealthAlerts, networkAcceleration, dashboard`

| method | params | result |
|---|---|---|
| `settings.getAll` | `{ scopes?: string[] }` | `{ scopes: { "<scope>": {...store} } }` |
| `settings.get` | `{ scope, path? }` | `{ scope, value }` (path = dotted, e.g. `Notifications.TypePolicies`) |
| `settings.set` | `{ scope, value }` | `{ scope, applied: true }` (whole-store replace in memory; enum names as strings) |
| `settings.save` | `{ scopes?: string[] }` | `{ saved: ["scope",...] }` (SynchronizeStore) |
| `settings.reload` | `{ scope }` | `{ reloaded: true }` |

Event `settings.changed` → `{ scope, reason: "set"|"save" }`.

Serialization: LltJson.CreateCompactOptions() (enum↔string). `set` uses JsonSerializer.Populate semantics (deserialize onto existing store instance) where possible; otherwise property-copy.

### sensors
| method | params | result |
|---|---|---|
| `sensors.getStatus` | {} | `{ initialized, isHybrid, cpuName, gpuName, gpuIsIntegrated, initialState }` |
| `sensors.getSnapshot` | {} | full snapshot (see below); `{ initialized: false }` when unavailable |
| `sensors.getDetailed` | {} | vendor path: `{ source:"vendor", cpu:{...}, gpu:{...} }` (int values) |
| `sensors.subscribe` | `{ intervalSec }` | `{ subscribed, effectiveIntervalSec }` (clamp 0.5..30) |
| `sensors.unsubscribe` | {} | `{ unsubscribed }` |
| `sensors.getSettings` | {} | `{ enableHardwareSensors, osdRefreshIntervalSec, selectedGpuIsIgpu, showCpuAverageFrequency, displayMemoryInGigabytes, visibleSections, sectionOrder }` |
| `sensors.setSettings` | partial | `{ saved }` |
| `sensors.getFps` | {} | `{ process?, fps?, lowFps?, frameTimeMs? }` (null when -1) |
| `sensors.subscribeFps` | `{ blacklist?: string[] }` | `{ monitoring }` |
| `sensors.unsubscribeFps` | {} | `{ monitoring: false }` |

Event `sensors.updated` data (snapshot, -1/0.0 sentinel → null):
```
{ ts, source: "LibreHardwareMonitor"|"vendor"|"mixed", initialized, isHybrid,
  info: { cpuName, gpuName, gpuIsIntegrated },
  cpu: { temperature, usage, fanSpeed, power, powerCores, powerMemory, powerPlatform,
         voltage, coreClockMax, coreClockAvg, pCoreClock, eCoreClock },
  gpu: { usage, temperature, coreClock, memoryClock, power, voltage, vramTemperature,
         hotSpotTemperature, vramUtilization, vramUsedMb, vramTotalMb,
         pcieRxThroughput, pcieTxThroughput, fanSpeed },
  memory: { usage, usedMb, totalMb, highestTemperature },
  motherboard: { highestTemperature },
  storage: { temperatures: [t1, t2] } }
```
Event `sensors.fpsUpdated` → `{ process?, fps?, lowFps?, frameTimeMs? }`.

Implementation notes:
- Snapshot via `SensorsGroupController` 30+ getters in `Task.WhenAll`; fall back to `SensorsController.GetDataAsync(detailed:true)` when LHM not initialized (source:"vendor").
- Subscribe maps to `SensorsGroupController.Start(subscriber, interval)`; single host-side subscriber marker; publish assembled snapshot from `SensorsUpdated` handler.
- FPS: `FpsSensorController`; parse string fields ("-1" → null).

### feature (generic IFeature<T> bridge)
Keys: `alwaysOnUsb, battery, batteryNightCharge, flipToStart, fnLock, gSync, hdr, hybridMode, igpuMode, itsMode, instantBoot, microphone, overDrive, panelLogo, portsBacklight, powerMode, refreshRate, resolution, dpiScale, speaker, touchpadLock, whiteKeyboard, winKey, oneLevelWhiteKeyboard`

| method | params | result |
|---|---|---|
| `feature.list` | {} | `{ features: [{ key, supported, stateType }] }` |
| `feature.getSupported` | `{ feature }` | `{ supported }` |
| `feature.getStates` | `{ feature }` | `{ states: [enumName|object] }` |
| `feature.getState` | `{ feature }` | `{ state }` |
| `feature.setState` | `{ feature, state }` | `{ ok, partial? }` |

State encoding: enums → string names (PowerModeState: Quiet/Balance/Performance/Extreme/GodMode); structs → `{ frequency }`/`{ width, height }`/`{ scale }`.
Errors: `NOT_SUPPORTED`, `AC_REQUIRED` (PowerMode no-AC), `UNDEFINED_STATE`.

### dashboard (P1 read-only config)
| method | params | result |
|---|---|---|
| `dashboard.getConfig` | {} | `{ showSensors, sensorsRefreshIntervalSeconds, groups: [...] }` |
| `dashboard.saveConfig` | `{ config }` | `{ saved }` |

Host has its own DashboardSettings copy (dashboard.json, same schema as Electron).

### automation (agent explores Lib.Automation `AutomationProcessor`)
| method | params | result |
|---|---|---|
| `automation.getState` | {} | `{ isEnabled, pipelines: [...] }` (camelCase serialization of AutomationSettings store) |
| `automation.setEnabled` | `{ enabled }` | `{ ok }` |
| `automation.savePipelines` | `{ pipelines }` | `{ saved }` |
| `automation.runNow` | `{ pipelineId }` | `{ ok }` |
| `automation.getSupportedSteps` | {} | `{ steps: [...] }` |

### macro (agent explores Lib.Macro `MacroController` + `MacroViewModel` IMacroWorkspace)
| method | params | result |
|---|---|---|
| `macro.getState` | {} | `{ isEnabled, slots }` |
| `macro.setEnabled` | `{ enabled }` | `{ ok }` |
| `macro.play` | `{ key }` | `{ ok }` |
| `macro.startRecording` | `{ mode, key }` | `{ ok }` |
| `macro.stopRecording` | {} | `{ events }` |
| `macro.saveSequence` | `{ key, repeatCount, ignoreDelays, interruptOnOtherKey, events }` | `{ ok }` |
| `macro.clearSequence` | `{ key }` | `{ ok }` |

## Errors
- -32601 unknown method, -32603 internal, -32800 cancelled
- Domain errors returned as `BridgeResult.Error(code, message)` with codes: NOT_SUPPORTED, AC_REQUIRED, IGPU_CHANGE_FAILED, VANTAGE_RUNNING, UNDEFINED_STATE, RANGE, WMI_UNAVAILABLE

## File layout
Host handlers: `Apps/Host/Rpc/Handlers/*.cs` each exposing `public static void Register(BridgeRpcServer rpc)`.
Program.cs calls all Register methods (already wired).
Electron renderer: `src/renderer/src/api/<domain>.ts` (typed invoke), `src/renderer/src/stores/<domain>Store.ts` (zustand).
