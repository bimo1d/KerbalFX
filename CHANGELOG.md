## 0.6.1
- Added new module: **AeroFX**.
- AeroFX adds atmospheric ribbon condensation trails to suitable wings, control surfaces, and fins.
- AeroFX supports up to four ribbon emitters per vessel.
- Expanded **BlastFX** beyond pyro rings with a separate soft-puff path for ordinary decouplers.
- Retuned BlastFX separator visuals for denser decoupler bursts and sharper pyro fragment response.
- Continued cleanup of shared FX runtime, module docs, localization.
- Rebuilt `KerbalFX.Core.dll`.

## 0.6c
- Continued cleanup after 0.6b.
- Moved duplicated utility, surface-color, and sunlight helpers into shared core source.
- Reduced duplicated module-side helper code in Rover Dust and Impact Puffs.
- Simplified cfg for release stability.

## 0.6b
- New module: **BlastFX** (separator pyro-ring FX only for now).
- Continued technical-debt cleanup: split large runtime code into focused source files for Impact Puffs.
- Standardized module docs/config comments/localization wording to a single KerbalFX style.
- Expanded Difficulty Settings/localization coverage for module toggles and debug messages.
- Preserved lightweight design goals while improving maintainability and diagnostics.

## 0.6a
- Refactored Rover Dust source layout into smaller focused runtime files:
  - controller/bootstrap
  - wheel emitter logic
  - shared asset helpers
- Reduced code complexity and cleaned up internal structure for easier maintenance.
- Preserved existing Rover Dust behavior and Difficulty Settings compatibility.
- No gameplay-facing tuning changes in this step.

## 0.5.1
- Reworked non-simplified touchdown into ring-shock style ground burst.
- Added non-simplified terrain-aware light response (better behavior in shadowed relief areas). (WIP)
- Unified non-simplified light logic between volumetric plume and touchdown effects.
- Tuned non-simplified volumetric brightness in low-light scenes (reduced over-bright look).
- Touchdown intensity now scales with impact energy (capped at 30 m/s). (WIP)
- Rebuilt `KerbalFX.Core.dll`.

### Note
0.5.1 is the final feature-focused release before 0.6.
Version 0.6 is planned as a technical-debt release only (cleanup/refactor/stability), with no new features.

## 0.5c
- Reworked Impact Puffs touchdown burst.
- Improved Impact Puffs with light-aware behavior (effect depends on scene lighting, without unrealistic glow in full darkness).
- Added performance tuning: reduced Rover Dust scene-light refresh frequency to lower CPU overhead.
- Rebuilt `KerbalFX.Core.dll` and cleaned up non-essential code.

## 0.5b
- Impact Puffs volumetric behavior tuning.
- Reworked engine exhaust direction detection to reduce opposite-side/invalid plume triggering.
- Retuned plume vs touchdown balance.
- Updated Impact Puffs docs/localization/config references and rebuilt `KerbalFX.Core.dll`.

## 0.5a
- Added new module: **KerbalFX - Impact Puffs**.
- Added engine plume-ground puffs for launch and landing.
- Added one-shot touchdown burst and tuned splashdown-specific behavior.
- Improved landing plume placement to spread farther from vessel center and reduce hull-enveloping clouds.
- Added/updated localization and config coverage for Impact Puffs.
- Updated core module slot registry and docs for modular KerbalFX layout.

## 0.4a
- Tuned body-specific dust visibility multipliers:
  - Increased: Minmus, Pol
  - Reduced: Moho, Vall, Bop, Tylo
- Kept Duna baseline unchanged.
- Rebalanced quality slider impact (so scaling is less aggressive overall).
- Increased wheel-size influence (large wheels now scale dust effects more strongly).
- Updated and added new Rover Dust config values and rebuilt KerbalFX.Core.dll.

## 0.3
- Reworked lighting logic for Rover Dust: visible in daylight, suppressed in darkness unless lit by meaningful artificial lights. (Very alpha)
- Updated UI structure: KerbalFX section in difficulty settings.
- Added localization keys to KerbalFX_* naming.

### KerbalFX UI structure
KerbalFX is the global section in Difficulty Settings.
Rover Dust is shown as a module page inside that section.
