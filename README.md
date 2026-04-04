# KerbalFX

Lightweight modular FX framework for Kerbal Space Program.

KerbalFX is a core plugin + module structure where each module adds visuals without replacing stock physics systems.

## Current module

### KerbalFX - Rover Dust
Adds wheel dust effects while driving rovers and wheeled craft.

Current Rover Dust features:
- Dust emission based on wheel-ground contact and motion.
- Supports dust quality scaling from Difficulty Settings.
- Supports per-body tuning values through config files. (Only stock planets for now)
- Supports optional surface color adaptation.
- Wheel-size scaling (larger wheels produce stronger dust effects).
- Uses light-aware rendering so dust behaves better across day/night conditions.

## Installation

1. Download the latest release from [Releases](https://github.com/bimo1d/KerbalFX/releases).
2. Copy `KerbalFX` into your KSP `GameData` folder.
3. Remove any older KerbalFX version before installing a new one.

## Dependencies

- ModuleManager

## Configuration

Rover Dust config:
- `GameData/KerbalFX/RoverDust/KerbalFX_RoverDust.cfg`

Core config:
- `GameData/KerbalFX/Core/KerbalFX_Core.cfg`

Difficulty Settings:
- `KerbalFX` section in game settings
- `Rover Dust` module page inside that section

## Mod compatibility and support

- Built for KSP `1.12.5`.
- Designed with compatibility in mind to run alongside visual and terrain mods.

## Development status

Public beta.
A dedicated KSP Forum thread will be added later.

## Disclaimer and License

This mod is released under the Unlicense, which means it is in the public domain.
