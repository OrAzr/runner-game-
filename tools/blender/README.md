# Weapon normalizer

`normalize_weapon.py` converts a raw Meshy weapon export into this
project's weapon convention before it ever reaches Unity or Mixamo rigging.

## Convention (Unity frame)

- Muzzle points down **+Z**.
- Up is **+Y**.
- Origin is the right-hand grip.
- Three empties are baked into the exported FBX, parented to the mesh:
  `Grip_R` (at the origin), `Grip_L`, and `Muzzle`.

Gameplay code must read weapon orientation only from this normalized asset
and an explicit aim basis - never from a bone's local axes - so getting
this conversion right here is what keeps every downstream gun-holding
calculation honest.

## Input

Raw Meshy FBX files live in `art-source/meshy/weapons/`. Grip and muzzle
measurements for each weapon (taken on the raw, un-normalized mesh) live in
`weapons.json`, alongside a `_frame` note describing that raw coordinate
system: muzzle on -X, up +Z, thin axis Y.

## Running it

```
tools/blender/install_blender.sh   # once, downloads Blender 4.2.3 if missing
blender -b --python tools/blender/normalize_weapon.py -- \
    art-source/meshy/weapons/rifle_raw.fbx rifle \
    unity/OutpostZero/Assets/Resources/Models/Weapons/rifle.fbx
```

The script factory-resets the scene, imports the raw FBX, rotates/scales/
translates it (and the grip/muzzle points) into the Unity frame above,
exports the result, then re-imports the export and prints PASS/FAIL checks
on bbox length and empty placement. Exit code is 0 only if every check
passes.
