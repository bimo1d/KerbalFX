# KerbalFX - AeroFX

Atmospheric ribbon condensation module for Kerbal Space Program.

## What it does

- Adds ribbon-style atmospheric trails to suitable wings, control surfaces, and fins.
- Uses up to four emitters per vessel based on detected wing-like anchor parts.
- Activation based on speed, atmospheric density, dynamic pressure, and maneuver load.
- Optional minimum-Mach activation mode.
- Optional fast anchor-scan mode for lower CPU on large or heavily modded vessels.
- Optional light-aware dimming toggle in Difficulty Settings.

## Depends on

- `KerbalFX Core`

## Config

- `GameData/KerbalFX/AeroFX/KerbalFX_AeroFX.cfg`
- Config is limited for now for release stability.
- Toggles in Difficulty Settings.

## Notes

- Designed as an FX-only module.
- Does not modify lift, drag, control-surface behavior, or vessel physics.
