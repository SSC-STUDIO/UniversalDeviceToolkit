# UI Performance

How Universal Device Toolkit keeps the Electron shell fast, and which tools to use when a page or the Host regresses. The renderer hot-path rules that every change must follow are summarized here; the process model is in [ARCHITECTURE.md](./ARCHITECTURE.md).

## Ready-latency target

| Rating | Median ready time | UDT status |
|--------|-------------------|------------|
| excellent | <= 400 ms | Baseline for every page |
| good | <= 900 ms | - |
| fair | <= 1800 ms | - |
| needs work | > 1800 ms | - |

"Ready" means the route has rendered its real content (not a skeleton) and the first sensor or feature payload from the Host has been applied. Measure in **Release** builds on the target hardware; Debug and dev-server numbers are not comparable.

## Renderer hot-path rules

1. **Lazy routes.** Every page component is loaded with `React.lazy()` + `Suspense`; ECharts and modal trees are imported on first use, never at startup.
2. **No static object churn in render loops.** Sensor and chart components refresh at 1 Hz. Static ECharts options, theme tokens, and metric mapping tables live in module constants or `useMemo`; data updates go through the incremental path (`setOption` with the changed series only).
3. **Isolate high-frequency subscriptions.** Cards and list rows that subscribe to sensor stores are wrapped in `React.memo`; parent layouts must not re-render on every tick.
4. **Everything cancellable.** Polling intervals, Host event listeners, and store subscriptions are cleaned up on unmount; hidden windows suspend polling entirely.
5. **Tray sleep destroys the DOM.** Minimizing or closing to the tray destroys the main window and renderer; the tray popup unloads on idle. Nothing may keep a hidden renderer alive.
6. **Bundle discipline.** Fluent UI icons are imported per glyph; new dependencies need a stated reason and must tree-shake.
7. **Motion.** Animations respect `prefers-reduced-motion` and animate `transform` / `opacity` only.

## Analysis tools (use together)

### 1. Chromium DevTools (renderer)

Run `npm run dev` in `UniversalDeviceToolkit.Electron/` and open DevTools from the window (or `npm run dev:web` and use a browser against the real Host). Use:

- **Performance** panel: record a route change and read the time to the first meaningful paint of the page content.
- **Memory** panel: heap snapshot before and after visiting every page; the delta after returning to the dashboard should be near zero.
- **React Profiler** (React DevTools): find components that re-render on every sensor tick.

### 2. Electron main-process memory report

`src/main/memory-report.ts` logs process memory for the main, renderer, and Host processes. Compare the tray-idle figure (windows destroyed) against the active figure to confirm the tray-sleep path still releases the renderer.

### 3. `dotnet-counters` (Host runtime)

```powershell
dotnet tool install -g dotnet-counters   # once

# While UDT Host is running:
dotnet-counters monitor --process-id <pid> System.Runtime
```

Watch `Time in GC`, `Allocation Rate`, and `Working Set` while navigating every page.

### 4. `dotnet-trace` / PerfView (Host deep stacks)

```powershell
dotnet tool install -g dotnet-trace
dotnet-trace collect -p <pid> --duration 00:01:00 --providers Microsoft-Windows-DotNETRuntime
```

Or PerfView:

```text
PerfView /nogui collect /MaxCollectSec:60
# flip every page, then open the .etl.zip in PerfView -> CPU Stacks / GC Stats
```

### 5. Windows-hosted Linux verification through WSL

When a Windows developer needs to validate the portable Host and Linux TFM, run this from the repository root after installing a WSL distribution with the .NET 10 SDK:

```powershell
.\Scripts\Test-CrossPlatformInWsl.ps1 -Distro Ubuntu -Configuration Release
```

The script runs a locked restore, builds the Linux platform assembly and the portable test project, runs the cross-platform tests, and smoke-tests the diagnostics CLI inside WSL. It fails when WSL or the requested distribution is unavailable.

## Interpreting results

| Symptom | Likely cause | Next tool |
|---------|--------------|-----------|
| High ready ms on first visit only | Lazy chunk too large, or a store hydrating synchronously | DevTools Performance + bundle analyzer |
| High ready ms every visit | Page awaits a slow Host RPC before rendering | Host-side timing (`dotnet-trace`), render a skeleton first |
| Renderer memory grows per page visit | Listener or interval not cleaned up on unmount | DevTools Memory snapshots |
| Whole dashboard re-renders every second | Sensor subscription placed on a parent instead of the card | React Profiler, `React.memo` |
| Host CPU high while idle | WMI query on a timer without change detection | `dotnet-counters`, cache and diff |

## Notes

- Close other UDT instances before a timed run so two Hosts do not compete for WMI.
- Prefer **Release | x64** for numbers you care about.
- Use a sandbox app-data folder (`UDT_APPDATA_OVERRIDE`) so production settings are not touched.
- The Keyboard page may be absent on non-Legion hardware; the empty state counts as ready when it appears promptly.
