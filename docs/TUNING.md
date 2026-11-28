# Tuning Screen

A "TUNE" button on the main menu opens an in-game screen so the owner can dial in difficulty on-device
(iPhone) without a new build, then hand the result to us as JSON to bake into the defaults in
`Assets/Scripts/Core/LevelConfig.cs` (see [LEVEL_RULES.md](LEVEL_RULES.md) for how that table is organised).

There is no pause screen in this build, so the tuning screen is only reachable from the main menu (before
a run starts), not mid-run.

## Using it

- **TUNE** (main menu) opens the screen.
- Each row is `label · current value · [-] [+]`. Step size is tuned per field (see table below).
- **COPY SETTINGS** copies the current settings as JSON to the clipboard ("Copied!" toast).
- **PASTE SETTINGS** reads JSON from the clipboard and applies it, ignoring any fields it doesn't
  recognise ("Pasted!" toast, or an error toast if the clipboard isn't valid JSON).
- **RESET** restores every value to today's default behaviour.
- **PLAY** jumps straight into a run using the current settings (and the "Starting level" field).
- **BACK** returns to the main menu without starting a run.
- Settings persist in `PlayerPrefs` (`outpostzero.tuning`, one JSON string) and survive app restarts; they're
  written on every +/- tap plus RESET/PASTE, so nothing is lost if the app is killed.

All defaults equal today's shipped behaviour (every multiplier `1`, every extra/flag `0`), so an install
that never opens the tuning screen plays exactly as before.

## Fields

| Field (JSON key) | Row label | Default | Step | Range |
|---|---|---|---|---|
| `rookFireRateMult` | Rook fire rate | 1.00x | 0.1 | 0–5 |
| `soldierFireRateMult` | Soldier fire rate | 1.00x | 0.1 | 0–5 |
| `damageMult` | Damage per hit | 1.00x | 0.1 | 0–5 |
| `maxShotsPerSecond` | Max shots/sec | OFF (0) | 5 | 0–500 |
| `soldierShootFraction` | Soldiers that shoot | 100% | 5% | 0–100% |
| `zombieCountMult` | Zombie count | 1.00x | 0.1 | 0–5 |
| `extraZombieWaves` | Extra zombie waves | 0 | 1 | 0–20 |
| `zombieHpMult` | Zombie HP | 1.00x | 0.1 | 0–10 |
| `zombieSpeedMult` | Zombie speed | 1.00x | 0.05 | 0.2–5 |
| `zombieContactDamageMult` | Zombie touch damage | 1.00x | 0.25 | 0–10 |
| `waveSquadScale` | Waves scale w/ squad | 0% | 5% | 0–100% |
| `positiveGateMult` | Positive gate value | 1.00x | 0.1 | 0–5 |
| `multiplierGateFactorMult` | Multiplier gate factor | 1.00x | 0.1 | 0–5 |
| `negativeGateChance` | Bonus to negative chance | 0% | 5% | 0–100% |
| `negativeGateMult` | Negative gate value | 1.00x | 0.1 | 0–5 |
| `counterGateDepthMult` | Counter gate depth | 1.00x | 0.1 | 0–5 |
| `squadCapMult` | Squad cap | 1.00x | 0.1 | 0.2–5 |
| `startingSquadSize` | Starting squad size | 1 | 1 | 1–80 |
| `bossHpMult` | Boss HP | 1.00x | 0.1 | 0.1–10 |
| `bossSpeedMult` | Boss speed | 1.00x | 0.1 | 0.1–5 |
| `startingLevel` | Starting level | 1 | 1 | 1–200 |

Plus a `version` field (currently `1`), written for forward-compatibility if the JSON shape changes later.

Shipped level-1 `bossHp` (`LevelConfig.cs`, before `bossHpMult`) is 480, tuned for a 15-25s fight against a
typical level-1 squad now that the squad's shots actually converge on the boss instead of mostly missing.

## JSON format

`COPY SETTINGS` produces something like:

```json
{"version":1,"rookFireRateMult":1.5,"soldierFireRateMult":1,"damageMult":1,"maxShotsPerSecond":0,
"soldierShootFraction":1,"zombieCountMult":1.3,"extraZombieWaves":2,"zombieHpMult":1.2,
"zombieSpeedMult":1,"zombieContactDamageMult":1,"waveSquadScale":0,"positiveGateMult":1,
"multiplierGateFactorMult":1,"negativeGateChance":0.1,"negativeGateMult":1.2,
"counterGateDepthMult":1.3,"squadCapMult":1,"startingSquadSize":1,"bossHpMult":1.4,
"bossSpeedMult":1,"startingLevel":1}
```

This is produced by `JsonUtility.ToJson` on `TuningSettings` (`Assets/Scripts/Core/Tuning.cs`), so it's
plain, flat JSON - no nesting. `PASTE SETTINGS` runs it back through `JsonUtility.FromJson<TuningSettings>`,
which fills in whatever fields match and silently drops anything it doesn't recognise (e.g. a field from a
newer build fed into an older one, or vice versa).

## How a pasted value reaches gameplay

Tuning never edits `Levels.Build`'s per-level table directly - it's applied as a second pass on top of
whatever `LevelConfig` that table produced, so the hand-tuned level shape (`docs/LEVEL_RULES.md`) stays the
source of truth and tuning only scales the numbers.

- **`TuningSettings.Apply(LevelConfig)`** (`Tuning.cs`) runs once per run, from `GameManager.BuildRun()`,
  right after `Levels.Build(LevelNumber)`. It mutates the level's `squadCap`, `startCount`, `bossHp`,
  `bossSpeed`, every `ZombieGroupDef` (`count`/`hp`/`speed`), appends `extraZombieWaves` extra waves cloned
  from the existing ones, and rewrites every gate's `left`/`right` operator string
  (`positiveGateMult`/`negativeGateMult`/`multiplierGateFactorMult`/`counterGateDepthMult`/
  `negativeGateChance`).
- **`rookFireRateMult` / `soldierFireRateMult` / `damageMult` / `maxShotsPerSecond` /
  `soldierShootFraction`** are read every frame in `AutoShooter.Update()` (`Assets/Scripts/Combat/`),
  replacing the old flat "shooters = squad count" fire-rate math with a weighted Rook + soldier count,
  an optional shots/sec ceiling, and a damage multiplier applied to each fired projectile (and to the
  flamethrower's DPS formula).
- **`zombieContactDamageMult`** is read in `Zombie.Update()` (`Assets/Scripts/Enemies/Zombie.cs`) where a
  zombie currently costs the squad exactly 1 member on contact.
- **`waveSquadScale`** is read once per zombie, the first time it aggros, in
  `Zombie.ScaleForSquad()` - it scales that zombie's HP by how much the squad has grown (via gates passed
  earlier in the run) relative to the level's starting squad size, so a bigger squad from tuning doesn't
  make every wave trivial.
- **`startingLevel`** sets `GameManager.LevelNumber` on app start (`GameManager.Start()`) and whenever
  `PLAY` is pressed from the tuning screen (`GameManager.SetLevelNumber`).

## Baking a pasted JSON into defaults later

To promote a value the owner has tuned on-device into the shipped defaults: take the corresponding field
from the pasted JSON and apply its *effect* to `LevelConfig`/`Levels.Build` directly (e.g. a `bossHpMult`
of `1.4` means multiply every level's `bossHp` in the table by 1.4, or fold it into `ScaleBeyond`'s curve),
then reset that tuning field back to its default (`1`/`0`) so the new baseline becomes "no tuning applied."
Gate-value fields (`positiveGateMult`, `negativeGateMult`, `counterGateDepthMult`,
`multiplierGateFactorMult`) map onto the `GateDef` strings the same way `TuningSettings.Apply` rewrites
them (see `ApplyGateSide` in `Tuning.cs`) - re-run that same math by hand (or script it) over the table in
`Levels.cs` rather than hand-editing every gate string.

## Unity APIs used

Everything here is `PlayerPrefs` (`GetString`/`SetString`/`Save`), `JsonUtility`
(`ToJson`/`FromJson<T>`), and `GUIUtility.systemCopyBuffer` - all already approved, plus the same
code-built uGUI pattern (`Image`/`Button`/`Text`/`RectTransform`) `GameUI.cs` already uses everywhere else.
The scrollable row list adds `ScrollRect` and `RectMask2D` (both in `UnityEngine.UI`, standard uGUI
components), which weren't previously used elsewhere in this project but required no new assembly
references - the typecheck project resolves them from the existing `Unity3D.UnityEngine.UI` package
reference.
