# AGENTS.md

Guidelines for how Claude Code and the manager/tester work together on Outpost Zero.

### Roles
- Claude Code only writes code; it never builds, runs QA, takes screenshots, or judges results visually.
- The manager/tester (Grok bot) does all builds, QA, screenshots, and visual approval.
- No Cursor.
- Screenshots go to Or before any PR is opened.

### Lean QA
- Scope QA to the current phase: 1170x2532 only, and only the frames relevant to the change being tested.
- Full two-size QA (1170x2532 and 1080x1920) only before a PR.

### Keep Claude usage low
- Sonnet by default.
- Prompts should name exact files to edit.
- Batch fixes into one prompt per round instead of many small prompts.

### Weapon pipeline
- Weapon models come from Meshy, then are normalized with tools/blender/normalize_weapon.py: grip pivot at the origin, muzzle +Z / up +Y, with Grip_R / Grip_L / Muzzle empties, in real metres.
- Keep Grip_L at most about 0.18 m from Grip_R (Rook's arm reach is about 0.45 m).
- After normalizing, add a WeaponDefinition table entry.
- The gun is mounted by WeaponMount at the chest using an explicit aim basis; it is never parented or rotated via a hand bone. Hands follow the weapon via ArmIk.
- Never guess weapon angles.

### Existing shipping rules
- Never commit Unity noise: Packages/manifest.json, ProjectSettings, packages-lock.json, *.meta.
- One PR per iOS build.
- QA runs locally and never on iOS.
