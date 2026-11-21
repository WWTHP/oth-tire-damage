# Project: TireDamage

Project layer for `WWTHP/oth-tire-damage`. Game layer: [Over the Hill](../games/over-the-hill/README.md).

Tires take damage from hard hits, pop, and rupture. Field repairs with mounted cosmetics; free
replacement at houses, the mechanic and camp. In multiplayer the host applies every player's
pressure and flats to their car (see Multiplayer).

Current version: `Mod.Version` in `TireDamage/Mod.cs`. Settings: `MelonPreferences.cfg [TireDamage]`,
edited in the settings hub tab **Tires** (`HubTab.cs`: diagnostics + settings), or with **F1**
(`MenuKey`) when the hub isn't installed. **F4** (`TireKey`, was `RepairKey`) opens the tire repair
screen, the place where tires are fixed. Both keys can be set with "press a key" in the tab.
Controller: `TirePadButtons` (default `L3+R3`, pressed together) opens/closes it; D-pad or left
stick picks a tire, hold A repairs, B closes. The game binds every pad button, hence a chord.

## Rules

| What | Rule (defaults) |
| --- | --- |
| Hit | per wheel: suspension `contactSpeed`; body: `VehicleBase.isHardImpact` within 0.8 m of a wheel (×0.5). Damage = 0.04 × (speed − 4 m/s)², one hit per wheel per 0.3 s |
| Pop | health (1.0) reaches 0 → **popped**; if that hit was ≥ 0.9 → **ruptured** |
| Flat | popped: carcass 1.0 − 0.5/km driven − 2× hit damage → ruptured at 0 |
| Effect | popped: radius ×0.85, grip ×0.6; ruptured: radius ×0.75, grip ×0.35 (`VehicleBase.SetWheelRadius`, `SetWheelTireFrictionMultiplier`) |
| Tool kit | part name contains `pipewrench`/`hiliftjack`: patches a **popped** tire to 50 %, reusable, 6 s |
| Spare | each part containing `sparewheel` (not `wreath`, max 4): replaces a popped or ruptured tire once, 10 s; the cosmetic is hidden when used |
| Repair | on the F4 screen, click and hold a tire (car stopped, < 1 m/s): popped → tool kit patch, else a spare; ruptured → a spare |
| Pressure | one PSI for all tires (`TirePsi`, 8–32): full (32) = the game's tire, untouched. Lower, linear to the minimum: traction on loose ground (`MaterialLabel` Mud, DeepMud, Sand, DeepSand, QuickSand, Snow, DeepSnow, ShallowSnow, Ice) up to ×1.3, cornering grip on any ground down to ×0.85, loose-ground drag down to ×0.5 (see Ground drag), wheel radius down to ×0.92 (lower top speed). Per wheel and fixed step the friction multiplier blends the two by the sideways share of the tire's slip (`slipAngle`/`peakSlipAngle` vs `slipRatio`/`peakSlipRatio`, smoothed). Below `SafePsi` (16), on hard ground only (not loose ground, where low pressure helps): wear up to 0.5 health/km and hit damage up to ×1.5 at the minimum (pinch flats); wear can pop a tire. Set on the F4 screen (−/+ or LB/RB, stopped) or the hub tab; kept across sessions |
| Ground drag | drag is a field of the game's shared `GroundMaterialConfig` assets (one per ground type, used by every car this machine simulates). `GroundDrag` scales the loose types' `Drag` only while this machine runs the local car's physics (non-kinematic body) and no other player's car; otherwise, at full pressure or with the mod off, the originals are restored. `Car physics: …` logs which machine runs each car whenever it changes |
| Per-wheel drag | when the ground types can't be changed (multiplayer, or `WheelDragAlways`), `CarFx` scales each wheel's `wheelState.groundResistance` instead, from VPP's `onPreDynamicsStep` and `onBeforeIntegrationStep` callbacks of that car (so it is per car). Whether VPP keeps the value is checked by the grip log (`res base→read`, "rewritten by the game" counts) |
| Multiplayer | the host runs every car's physics with authority (0.6.1 logs: on the host, other players' cars `physics here, state authority here`; on a client, every car `physics here, state authority remote`: the client only predicts). A client's own grip writes do read back locally (×1.80 set, 1.78 read) but the host's result wins, so alone they don't drive its car. The client keeps applying them so its prediction matches the host. Each client sends its `CarTargets` (pressure → traction, cornering, drag; per wheel flat grip and radius) to the host every change (heartbeat 2 s); the host applies them to that player's car (`InputAuthority.Raw` = sender, non-kinematic body) and answers `Applied`. 8 s without updates → that car back to normal. Damage, wear and repairs stay on the driver's machine. Needs the mod on the host; `Multiplayer` setting |
| Service | all tires new, spares back: the game's own repairs (hooks below), or stopping within 25 m of a house (Cabin) or the mechanic (Merchant POI) |

## Layout

| File | What |
| --- | --- |
| `Mod.cs` | lifecycle; registers the hub tab |
| `Settings.cs` | settings (ranges via `ValueRange`, so the hub's plain editor works too) |
| `Car.cs` | local car (input authority), `VPVehicleController`, mounted spare/tool parts |
| `Tires.cs` | hit detection (FixedUpdate), wear, effects, hold-to-repair, service |
| `Service.cs` | house/mechanic proximity; Harmony postfixes on `CarDamageSystem.HealOnCabinArrival`, `HealOnPointOfInterestArrival`, `PerformLocalPlayerHealingOperations(reason)` (HealedByPOI/HealedByCamping), `CampManager.RepairPartyCars`, `RPC_EndCamping` (installed late, CrashGuard stage `hooks`) |
| `CarFx.cs` | applies `CarTargets` to one car's VPP physics: radius, friction multiplier per fixed step, per-wheel ground drag; restore |
| `Net.cs`, `Remote.cs` | Fusion reliable-data link (copied from the fuel mod, key `TIRE`, no relay); client sends its targets, host applies them to other players' cars |
| `Core/CarTargets.cs`, `Core/TireWire.cs` | what a car's tires do (built from settings), wire format and validation; Hello/Applied messages |
| `GroundDrag.cs` | loose-ground drag at low pressure; per-car physics location check and log (CrashGuard stage `drag`) |
| `TirePanel.cs` | F4 repair screen in the HUD canvas: cloned tilt-dial blurred glass, HUD font, tire tiles, mouse hold (CrashGuard stage `panel`) |
| `Menu.cs`, `Toast.cs` | hub tab / F1 window (diagnostics, settings, key capture), IMGUI fallback of the repair screen, on-screen messages |
| `HubTab.cs` | settings hub snippet (mod-template `snippets/generic/HubTab.cs.txt`) |
| `Core/Pressure.cs` | pressure → grip, radius, low-pressure wear and hit factor; range sanitising; UI summary |
| `Core/PadInput.cs` | controller chord parsing (`L3+R3`, aliases A/B/X/Y, LB/RB, LT/RT, View/Start…) and tile navigation by wheel position |
| `Core/TireModel.cs`, `Core/FieldRepair.cs` | damage model, repair planning, part counting, wheel labels |

## Design notes

- The wheel radius the game sets (e.g. a wheel upgrade) is detected and becomes the new normal;
  the mod only ever scales it. Disabling the mod restores radius and grip.
- The game's friction multiplier (`wheelState.tireForceMult`) reads 1.00 normally and the game
  never rewrote it in testing; the mod still re-reads it each step and scales it. VPP keeps values
  above 1 (confirmed in game, 0.5.3 log).
- Tire state is not saved, by design: new games start at a camp or the mechanic anyway.
- Multiplayer: the driver works out the targets from their own settings, so each player's sliders
  rule their own car. The client also applies them locally (harmless if its car is a network copy).

## Test plan (in game)

1. Log: `Local car found … (4 wheels…)`, `Tires: FL r=… …`, `Mounted parts … spare wheel(s), tool kit: …`, `Hooked …` ×5.
2. Drive hard over rocks/jumps: `Hit FL (suspension) 7.1 m/s → -0.39 = 61%` lines. Tune `HitThreshold`/`DamageScale` from them.
3. F5 "Tires" tab → Pop a tire: car leans, pulls, less grip. F4: the repair screen shows it red; stopped, click and hold it: patch (tool kit) or spare (cosmetic disappears).
4. Rupture a tire with no spare left: its tile says it needs a spare. Change the F4 key in the tab.
5. Drive to a house / camp: "All tires replaced", spares visible again.
6. Pressure: F4, −/+ (or LB/RB) to 20 PSI: "Loose ground +15% · Corners -7% · Speed -4%"; corners
   slide earlier, top speed a bit lower; climbs in mud/sand/deep snow/on ice get better. The hub tab's
   "Grip now" line shows each wheel's multiplier and "(loose)" on mud/sand/snow/ice. Down to 10 PSI: red "TOO LOW: wears on hard ground"; on rock/gravel/dirt health drops while driving (~0.4/km) and eventually pops; on mud/sand/snow it does not.
   Back to 32: car exactly as before. Log: `Tire pressure set to N PSI.` Grip logging for tuning:
   `LogGripSamples` in the hub tab.
7. Ground drag: at 8 PSI the log says `Loose-ground drag x0.50 on N ground type(s).` Deep snow and
   sand climbs get easier. Back at 32: `Loose-ground drag restored`. Multiplayer: the
   `Car physics: …` line should show every other player's car with `physics remote`; if one says
   `physics here`, the drag change switches off ("Ground drag change off").

8. Multiplayer (host and a client with the mod): host log `Player N has the tire mod.`,
   `Player N's tires: 8 PSI, …`, `Player N's car Car_… (4 wheels) runs here: applying their tires`;
   client log `The host applies your tire pressure and flats to your car`. The client's car climbs
   better at low pressure and drags on a flat. With `LogGripSamples` on, the host logs
   `Player N grip @8 PSI: …` lines: `res a/b→c/d` should show c/d = a/b × drag on loose ground.

## Changelog

| Version | Date | Change |
| --- | --- | --- |
| 0.7.0 | 2026-11-20 | Multiplayer: the host runs every car's physics, so each player's pressure and flats now reach their car through the host (needs the mod on the host; `Multiplayer` setting). Loose-ground drag also works per wheel (`groundResistance`), used whenever the shared ground types can't be changed; `WheelDragAlways` tests it in single player. Grip log adds the ground resistance per wheel. |
| 0.6.1 | 2026-11-19 | Grip logging (every 3 s below full pressure) is off by default (`LogGripSamples`; switch on in the hub tab for tuning). Confirmed in game: ground detection, grip above ×1, and loose-ground drag all work (single player). |
| 0.6.0 | 2026-11-19 | Low pressure floats on loose ground: drag of mud, sand, snow and ice down to ×0.5 at the minimum (`LowPsiDrag`), only while this machine runs your car's physics and no other player's car (logged as `Car physics: …`). Release of 0.4.0–0.5.3. |
| 0.5.3 | 2026-11-18 | Grip diagnostics: `LogGrip` logs every 3 s, while driving below full pressure, the ground under each tire (label, its grip and drag), the cornering share and the multiplier set vs read back. The pressure grip now multiplies the game's own friction multiplier (re-read each step) instead of replacing it. |
| 0.5.2 | 2026-11-18 | Mud and deep mud count as loose ground. Low-pressure wear and the extra hit damage only apply on hard ground: on mud, sand, snow and ice, where low pressure helps, it does no harm. |
| 0.5.1 | 2026-11-18 | Low pressure no longer helps cornering: it adds traction only on sand, snow and ice, and cornering grip drops on any ground (`LowPsiCornerGrip`, ×0.85 at the minimum). Grip is set per wheel every fixed step from the ground under it and how much it is cornering. |
| 0.5.0 | 2026-11-18 | Tire pressure: one PSI for all tires on the F4 screen (−/+, controller LB/RB; car stopped) and the hub tab. Full = normal game tires; lower = more grip, slower (smaller rolling radius); below the safe pressure the tires wear while driving and hits hurt more. Grip may now go above ×1. |
| 0.4.0 | 2026-11-17 | Controller support on the tire screen: L3+R3 (`TirePadButtons`, configurable) opens/closes it, D-pad / left stick selects a tire (highlighted), hold A repairs, B closes. |
| 0.3.1 | 2026-11-17 | Repair screen sleeker: smaller panel and tiles, thin tire bars, frames only on hover, spaced lighter text; tire state text auto-sizes (RUPTURED no longer spills out of its tile). |
| 0.3.0 | 2026-11-17 | F4 (`TireKey`, configurable, press-a-key in the tab) opens a dedicated tire repair screen in the HUD style: car from above, a tile per tire with health %, click and hold to patch or fit a spare. The hub tab is diagnostics and settings only. Holding F4 no longer repairs. |
| 0.2.0 | 2026-11-17 | Tires tab is the hub's first tab (F5): each tire's health in %, click and hold a tire's button to patch it (popped, tool kit) or fit a spare (ruptured, or popped without a tool kit). F4 still repairs the worst tire. Tire state is deliberately not saved (new games start at a camp or the mechanic). |
| 0.1.0 | 2026-11-17 | First version: hits, pop/rupture, flat effect, tool kit + spare repairs, service at house/mechanic/camp, settings hub tab. |
