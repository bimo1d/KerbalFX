# KerbalFX

Lightweight modular FX framework for Kerbal Space Program.

KerbalFX is built as one core plugin with independent FX modules.
Each module can be enabled or disabled separately.

## Current modules

### KerbalFX - Rover Dust
Wheel-ground dust for rovers and wheeled craft.

- Dust emission based on wheel contact, speed, and slip.
- Quality scaling from Difficulty Settings.
- Optional surface color adaptation.
- Optional light-aware behavior (sun + local lights).
- Per-body tuning through config.

### KerbalFX - Impact Puffs
Engine-ground interaction and touchdown effects.

- Engine plume-ground puffs near terrain.
- Touchdown burst on harder landings.
- Volumetric plume-ground puffs for launch and landing. Works best with single-engine vessels.
- Light-aware behavior and per-body tuning through config.
- Compatible approach with Waterfall.

### KerbalFX - AeroFX
Atmospheric ribbon condensation for wings, control surfaces, and fins.

- Ribbon emitters attached to suitable wing-like parts.
- Up to four emitters per vessel.
- Activation based on speed, density, dynamic pressure, and maneuver load.

### KerbalFX - BlastFX
FX module for separators and decouplers.

- Triggers blast effects on supported separator parts.
- Spawns sparks, smoke, and chunk fragments on separator activation.
- Softer smoke-puff burst for ordinary decouplers.
- Size-based effects scaling.

## Installation

1. Download the latest release from [Releases](https://github.com/bimo1d/KerbalFX/releases).
2. Copy the `KerbalFX` folder into your KSP `GameData`.
3. Remove older KerbalFX files before updating.

## Dependencies

- ModuleManager

## Configuration

Core:
- `GameData/KerbalFX/Core/KerbalFX_Core.cfg`

Rover Dust:
- `GameData/KerbalFX/RoverDust/KerbalFX_RoverDust.cfg`

Impact Puffs:
- `GameData/KerbalFX/ImpactPuffs/KerbalFX_ImpactPuffs.cfg`

AeroFX:
- `GameData/KerbalFX/AeroFX/KerbalFX_AeroFX.cfg`

BlastFX:
- `GameData/KerbalFX/BlastFX/KerbalFX_BlastFX.cfg`

Difficulty Settings:
- `KerbalFX: Main`
- `Rover Dust`, `Impact Puffs`
- `KerbalFX: Extras`
- `BlastFX`, `AeroFX`

## Compatibility

- Built for KSP `1.12.5`.
- Designed with compatibility in mind to run alongside visual and terrain mods (Parallax, EVE, Waterfall, etc.)

## Development status

Public beta.

[KSP Forum thread](https://forum.kerbalspaceprogram.com/topic/230118-wip1125-kerbalfx-lightweight-modular-fx-framework-v05b-rover-dust-impact-puffs/)

## Disclaimer and License

This mod is released under the Unlicense, which means it is in the public domain.
