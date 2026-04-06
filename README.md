# KerbalFX

Lightweight modular FX framework for Kerbal Space Program.

KerbalFX is a core plugin + module structure where each module adds visuals without replacing stock physics systems.

## Current modules

### KerbalFX - Rover Dust
Adds wheel dust effects while driving rovers and wheeled craft.

Current Rover Dust features:
- Dust emission based on wheel-ground contact and motion.
- Supports dust quality scaling from Difficulty Settings.
- Supports per-body tuning values through config files. (Only stock planets for now)
- Supports optional surface color adaptation.
- Wheel-size scaling (larger wheels produce stronger dust effects).
- Uses light-aware rendering so dust behaves better across day/night conditions.

### KerbalFX - Impact Puffs
Adds launch/landing dust puffs for engines near terrain and one-shot touchdown bursts.

Current Impact Puffs features:
- Engine plume-ground puffs for launch and landing.
- One-shot touchdown burst for harder landings. (Very alpha)
- Compatible approach with Waterfall (reads real engine output, does not depend on stock plume particles).
- Uses volumetric mode by default, with an optional simplified mode toggle.
- Supports per-body tuning values through config files.
- Ongoing WIP tuning for volumetric plume behavior and landing dynamics.

### KerbalFX - BlastFX (Placeholder)
Reserved module scaffold for future blast/contact FX work.

## Installation

1. Download the latest release from [Releases](https://github.com/bimo1d/KerbalFX/releases).
2. Copy `KerbalFX` into your KSP `GameData` folder.
3. Remove any older KerbalFX version before installing a new one.

## Dependencies

- ModuleManager

## Configuration

Rover Dust config:
- `GameData/KerbalFX/RoverDust/KerbalFX_RoverDust.cfg`

Impact Puffs config:
- `GameData/KerbalFX/ImpactPuffs/KerbalFX_ImpactPuffs.cfg`

Core config:
- `GameData/KerbalFX/Core/KerbalFX_Core.cfg`

Difficulty Settings:
- `KerbalFX` section in game settings
- `Rover Dust` module page inside that section
- `Impact Puffs` module page inside that section

## Mod compatibility and support

- Built for KSP `1.12.5`.
- Designed with compatibility in mind to run alongside visual and terrain mods.

## Development status

Public beta.
[KSP Forum thread](https://forum.kerbalspaceprogram.com/topic/230118-wip1125-kerbalfx-lightweight-modular-fx-framework-v05b-rover-dust-impact-puffs/)

## Disclaimer and License

This mod is released under the Unlicense, which means it is in the public domain.
