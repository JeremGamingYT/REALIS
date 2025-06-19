# REALIS Modular GTA V Mod

This repository implements a modular architecture for the REALIS GTA V mod.

## Projects

- **REALIS.Common**: Core utilities and interfaces
  - `IModule`, `ModuleManager`, `CrashHandler`, logging, version check

- **REALIS.Loader**: Single ScriptHookVDotNet `Script` entry point
  - `RealisLoader` subscribes to SHVDN events and dispatches to modules

- **REALIS.Police**: Police-related systems
  - `PoliceModule` implements police logic (pursuits, barriers, jobs)

- **REALIS.Traffic**: Traffic AI enhancements
  - `TrafficModule` implements advanced driving AI, traffic realism

- **REALIS.Services**: Emergency & transport services
  - `ServicesModule` for ambulance, taxi, bus, firefighter

- **REALIS.Dealership**: Vehicle dealership
  - `DealershipModule` wraps vehicle purchase/test-drive logic

- **REALIS.Events**: In-game events
  - `EventsModule` for races, UFO invasion, etc.

## Build & Deploy

1. **Build**: Open solution in Visual Studio (or use `msbuild`).
   Each project targets **.NET Framework 4.8**.

2. **Output**: All project DLLs are emitted to `bin\` at root:
   - `REALIS.Common.dll`
   - `REALIS.Loader.dll` (only ScriptHookVDotNet script)
   - `REALIS.Police.dll`
   - `REALIS.Traffic.dll`
   - `REALIS.Services.dll`
   - `REALIS.Dealership.dll`
   - `REALIS.Events.dll`

3. **Install**: Copy **all** DLLs from `bin\` into your GTA V `scripts\` folder.
   - Ensure **only one** `Script` class is present: `RealisLoader` in `REALIS.Loader.dll`.

4. Launch GTA V. The loader initializes modules on tick and unload.

## Next Steps

- Migrate existing systems from `Core/` folder into module projects.
- Remove any remaining `Script` inheritance outside of `RealisLoader`.
- Implement `Initialize()`, `Update()`, `Dispose()` in each module.

---
Believe in modularity! 🚀 