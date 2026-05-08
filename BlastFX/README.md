# KerbalFX - BlastFX

FX module for separators, decouplers, docking ports, and vacuum part explosions.

## What it does

- Triggers blast effects on supported separator parts.
- Spawns sparks, smoke, and chunk fragments for separators.
- Softer smoke-puff burst for ordinary decouplers.
- Gas venting effect when docking ports undock. WIP.
- Lingering debris field for separations in space.
- Vacuum explosion replacement FX for eligible command, engine, and fuel-carrying parts destroyed in space.
- Size-based effects scaling.
- Distance LOD and pooled particle systems.
- Supports optional detached-ring despawn flow and hidden-ring cleanup logic.

## Credits

[Halbann](https://github.com/Halbann)

## Depends on

- `KerbalFX Core`
- `Harmony2`

## Config

- `GameData/KerbalFX/BlastFX/KerbalFX_BlastFX.cfg`
- Toggles in Difficulty Settings.

## Notes

- Designed as an FX-only module.
- Does not change vessel physics, separator impulse, or part force behavior.
- The vacuum explosion prefab and debris sprite material in `AssetBundles/kcseffects` are adapted from Halban's StockCombatAI explosions project, used with the author's permission. Treated as Unlicense.
