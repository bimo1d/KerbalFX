# KerbalFX Core

Shared runtime for KerbalFX modules.

Compatible modules:
- `KerbalFX - Rover Dust`
- `KerbalFX - Impact Puffs`
- `KerbalFX - BlastFX`
- `KerbalFX - AeroFX`

`KerbalFX Core` is required by all KerbalFX effect modules.

`KerbalFX_Core.cfg`:
- Shared core note file only.
- Module-specific configuration is kept in each module's own cfg file.

Build entry point:
- `build.ps1` compiles `KerbalFX.Core.dll` plus separate module DLLs for each KerbalFX effect module.
