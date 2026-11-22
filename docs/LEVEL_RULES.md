# Level Rules

Level data lives in `Assets/Scripts/Core/LevelConfig.cs` (the `Levels` static class). Each level is a
hand-tuned `LevelConfig`: gates, zombie waves, crates, boss stats, squad cap and run length. `GameManager`
picks the config for the current `LevelNumber` via `Levels.Build(n)`, advances `LevelNumber` on victory, and
keeps the same level on defeat (retry). The HUD shows the current level ("LEVEL n").

## Curve

- **Levels 1-2** are onboarding: gates are mostly positive, counter gates are shallow (small negative
  starting values), no divide gates, and the squad cap is low enough that a big squad never happens by
  accident.
- **Levels 3-5** introduce divide gates (`/2`) and noticeably deeper counter gates, so a careless player
  (wrong side, slow to shoot a counter positive) can lose the run.
- **Levels 6-10** lean hard negative: counters start deep negative, divide gates appear more than once per
  level (even `/3`), and multiplier gates (`x1.2`-`x1.6`) get rarer and smaller instead of the flat `x2` used
  early on.
- **Beyond level 10** the run cycles level 10's shape (same gate/wave/crate layout) but keeps scaling:
  boss HP, counter/additive gate magnitudes, zombie HP/speed and the squad cap all grow ~12% (gate/zombie
  values) or a fixed step per extra level, uncapped. Multiplier/divide gate *factors* don't keep scaling
  (only how often/how large they are was tuned by hand up to level 10) - only their *size* (HP, counter
  depth, etc.) keeps climbing.

## Squad cap

Every level sets `squadCap`, a hard ceiling on squad `Count` (enforced in `SquadController.ApplyGate` /
`ApplyCounter`). Previously gates had no upper bound, so a few lucky `+`/`x2` gates could snowball the squad
indefinitely and trivialize everything after. The cap scales with level (12 at level 1 up to 48 at level 10,
+3 per level after, capped at 70 - `SquadController.MaxVisible` is 80, the hard render limit).

## Boss

Boss HP and approach speed scale per level (`bossHp`, `bossSpeed`), and the starting gap (`bossStartGap`)
shrinks slightly at higher levels for a punchier fight. `Boss.Init` floors the speed at 2.2 so a bad config
value can never read as "stuck in place."

## Table

| Level | Boss distance | Squad cap | Zombie waves (count/HP/speed range) | Boss HP | Boss speed | Gate mix |
|------:|---------------:|----------:|--------------------------------------|--------:|-----------:|----------|
| 1  | 220 | 12 | 4 waves, 3-4 count, HP 3-5, speed 1.6-1.7   | 900  | 2.4 | mostly `+`, one shallow `#` counter, no `/` |
| 2  | 260 | 16 | 5 waves, 4-5 count, HP 4-6, speed 1.7-1.8   | 1100 | 2.6 | `+` and `#` counters, one `x2` |
| 3  | 300 | 20 | 5 waves, 4-6 count, HP 5-8, speed 1.8-2.0   | 1350 | 2.9 | first `/2`, deeper `#` counters |
| 4  | 340 | 24 | 5 waves, 5-6 count, HP 6-9, speed 1.9-2.1   | 1600 | 3.1 | `/2`, `#` counters to -26, smaller `x1.6` |
| 5  | 380 | 28 | 6 waves, 5-7 count, HP 7-10, speed 2.0-2.2  | 1850 | 3.3 | two `/2`, `#` counters to -28, `x1.5` |
| 6  | 420 | 32 | 6 waves, 6-8 count, HP 8-11, speed 2.1-2.3  | 2100 | 3.5 | `/2` and `/3`, `#` counters to -30, `x1.4` |
| 7  | 460 | 36 | 6 waves, 6-8 count, HP 9-12, speed 2.2-2.4  | 2350 | 3.7 | two `/2`, `#` counters to -34, `x1.3` |
| 8  | 500 | 40 | 6 waves, 7-9 count, HP 10-13, speed 2.3-2.5 | 2600 | 3.9 | three `/2`, `#` counters to -36, `x1.3` |
| 9  | 540 | 44 | 6 waves, 7-9 count, HP 11-14, speed 2.4-2.6 | 2850 | 4.1 | `/3`, two `/2`, `#` counters to -40, `x1.2` |
| 10 | 580 | 48 | 7 waves, 8-10 count, HP 12-16, speed 2.5-2.7| 3100 | 4.3 | `/3`, three `/2`, `#` counters to -44, `x1.2` |

Levels beyond 10 repeat level 10's shape with the scaling described above.

Exact numeric balance (DPS vs. zombie HP, gate timing) was tuned by hand against the existing combat math
(`AutoShooter`, `Weapons`) but hasn't been played on-device yet - the usual iOS build/playtest loop should
confirm levels 3-5 actually punish a careless player as intended and adjust magnitudes if not.
