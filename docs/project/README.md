# Project: TireDamage

Project layer for `WWTHP/oth-tire-damage`. Game layer: [Over the Hill](../games/over-the-hill/README.md).

Tires take damage from hard hits, pop, and rupture. Field repairs with mounted cosmetics; free
replacement at houses, the mechanic and camp. Local car only, no networking.

Current version: `Mod.Version` in `TireDamage/Mod.cs`. Settings: `MelonPreferences.cfg [TireDamage]`,
edited in the settings hub tab **Tires** (`HubTab.cs`), or with **F1** (`MenuKey`) when the hub isn't
installed. **F4** (`RepairKey`, hold) repairs.

## Rules

| What | Rule (defaults) |
| --- | --- |
| Hit | per wheel: suspension `contactSpeed`; body: `VehicleBase.isHardImpact` within 0.8 m of a wheel (×0.5). Damage = 0.04 × (speed − 4 m/s)², one hit per wheel per 0.3 s |
| Pop | health (1.0) reaches 0 → **popped**; if that hit was ≥ 0.9 → **ruptured** |
| Flat | popped: carcass 1.0 − 0.5/km driven − 2× hit damage → ruptured at 0 |
| Effect | popped: radius ×0.85, grip ×0.6; ruptured: radius ×0.75, grip ×0.35 (`VehicleBase.SetWheelRadius`, `SetWheelTireFrictionMultiplier`) |
| Tool kit | part name contains `pipewrench`/`hiliftjack`: patches a **popped** tire to 50 %, reusable, 6 s |
| Spare | each part containing `sparewheel` (not `wreath`, max 4): replaces a popped or ruptured tire once, 10 s; the cosmetic is hidden when used |
| Order | ruptured first (spare), then popped (tool kit if mounted, else spare); car stopped (< 1 m/s) |
| Service | all tires new, spares back: the game's own repairs (hooks below), or stopping within 25 m of a house (Cabin) or the mechanic (Merchant POI) |

## Layout

| File | What |
| --- | --- |
| `Mod.cs` | lifecycle; registers the hub tab |
| `Settings.cs` | settings (ranges via `ValueRange`, so the hub's plain editor works too) |
| `Car.cs` | local car (input authority), `VPVehicleController`, mounted spare/tool parts |
| `Tires.cs` | hit detection (FixedUpdate), wear, effects, hold-to-repair, service |
| `Service.cs` | house/mechanic proximity; Harmony postfixes on `CarDamageSystem.HealOnCabinArrival`, `HealOnPointOfInterestArrival`, `PerformLocalPlayerHealingOperations(reason)` (HealedByPOI/HealedByCamping), `CampManager.RepairPartyCars`, `RPC_EndCamping` (installed late, CrashGuard stage `hooks`) |
| `Menu.cs`, `Toast.cs` | tab/window, on-screen messages and repair progress |
| `HubTab.cs` | settings hub snippet (mod-template `snippets/generic/HubTab.cs.txt`) |
| `Core/TireModel.cs`, `Core/FieldRepair.cs` | damage model, repair planning, part counting, wheel labels |

## Design notes

- The wheel radius the game sets (e.g. a wheel upgrade) is detected and becomes the new normal;
  the mod only ever scales it. Disabling the mod restores radius and grip.
- The grip multiplier is assumed to be 1.0 normally (the game's tire tiers use the friction object,
  `SetupTireTier`). Unverified: check grip after a repair in game.
- Tire state is not saved, by design: new games start at a camp or the mechanic anyway.
- Multiplayer: only the local car's wheels change; nothing is sent. Whether VPP applies the radius
  for a client's car is untested.

## Test plan (in game)

1. Log: `Local car found … (4 wheels…)`, `Tires: FL r=… …`, `Mounted parts … spare wheel(s), tool kit: …`, `Hooked …` ×5.
2. Drive hard over rocks/jumps: `Hit FL (suspension) 7.1 m/s → -0.39 = 61%` lines. Tune `HitThreshold`/`DamageScale` from them.
3. F5 opens on "Tires" → Pop a tire: car leans, pulls, less grip. Stopped, click and hold its button (or F4): patch (tool kit) or spare (cosmetic disappears).
4. Rupture a tire with no spare left: F4 says it needs a spare.
5. Drive to a house / camp: "All tires replaced", spares visible again.

## Changelog

| Version | Date | Change |
| --- | --- | --- |
| 0.2.0 | 2026-11-17 | Tires tab is the hub's first tab (F5): each tire's health in %, click and hold a tire's button to patch it (popped, tool kit) or fit a spare (ruptured, or popped without a tool kit). F4 still repairs the worst tire. Tire state is deliberately not saved (new games start at a camp or the mechanic). |
| 0.1.0 | 2026-11-17 | First version: hits, pop/rupture, flat effect, tool kit + spare repairs, service at house/mechanic/camp, settings hub tab. |
