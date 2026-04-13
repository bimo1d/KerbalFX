# KerbalFX Core

Shared runtime for KerbalFX modules.

Bundled modules:
- `KerbalFX - Rover Dust`
- `KerbalFX - Impact Puffs`
- `KerbalFX - BlastFX`
- `KerbalFX - AeroFX`

`KerbalFX_Core.cfg`:
- Shared core note file only.
- Module-specific configuration is kept in each module's own cfg file.

Build entry point:
- `build.ps1` compiles shared core and module plugin sources into `Core/Plugins/KerbalFX.Core.dll`
