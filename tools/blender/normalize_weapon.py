"""Blender 4.2 headless normalizer for raw Meshy weapon exports.

Usage:
    blender -b --python tools/blender/normalize_weapon.py -- <raw_in.fbx> <weapon_name> <out.fbx>

Converts a raw Meshy FBX (muzzle on -X, up +Z, as measured and recorded in
weapons.json) into this project's weapon convention: +Z muzzle, +Y up,
origin at the right-hand grip, with Grip_R/Grip_L/Muzzle empties baked in.
Gameplay code reads weapon orientation only from that normalized asset -
never from bone local axes - so this script is the one place the raw ->
engine frame conversion has to be right.
"""

import json
import math
import sys
import traceback
from pathlib import Path

import bpy
import mathutils

Vector = mathutils.Vector


def fail(message, code=1):
    sys.stderr.write(f"ERROR: {message}\n")
    sys.exit(code)


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    if len(argv) != 3:
        fail(
            "usage: blender -b --python normalize_weapon.py -- <raw_in.fbx> <weapon_name> <out.fbx>"
        )
    return argv[0], argv[1], argv[2]


def load_weapon_entry(weapon_name):
    weapons_path = Path(__file__).resolve().parent / "weapons.json"
    with open(weapons_path, "r") as f:
        weapons = json.load(f)
    entry = weapons.get(weapon_name)
    if entry is None:
        fail(f"'{weapon_name}' not found in {weapons_path}")
    return entry


def world_bbox(obj):
    """Axis-aligned bbox in world space; bound_box is local, so it must go through matrix_world."""
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs, ys, zs = [c.x for c in corners], [c.y for c in corners], [c.z for c in corners]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def rotate_90_about_z(point):
    """The same +90 deg Z rotation applied to the mesh: raw muzzle -X lands on -Y."""
    x, y, z = point
    return Vector((-y, x, z))


def normalize(raw_in, weapon_name, out_path):
    entry = load_weapon_entry(weapon_name)
    length_m = entry["length_m"]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=raw_in)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(meshes) != 1:
        fail(f"expected exactly one mesh object in {raw_in}, found {len(meshes)}")
    mesh_obj = meshes[0]

    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()

    # Everything downstream (the JSON grip/muzzle points, the Unity-side gun
    # code) assumes the raw Meshy export is long along X. Fail loudly here
    # instead of silently baking a sideways weapon.
    bmin, bmax = world_bbox(mesh_obj)
    dims = bmax - bmin
    if dims.x < dims.y or dims.x < dims.z:
        fail(
            f"longest bbox axis of {raw_in} is not X (dims={tuple(dims)}); "
            "raw Meshy export is not in the expected frame",
            code=2,
        )
    bbox_length_raw = dims.x
    scale = length_m / bbox_length_raw

    # Raw Meshy meshes are 1-2 mm asymmetric across their thin (Y) axis, which
    # would otherwise bake that asymmetry into the normalized centre-line.
    # Snap the recorded points onto the raw bbox centre-line first so they -
    # and the mesh's own transformed bbox centre - agree exactly after the
    # same rotate/scale pipeline runs on both.
    raw_centre_y = (bmin.y + bmax.y) / 2.0

    def on_centreline(point):
        x, _y, z = point
        return (x, raw_centre_y, z)

    mesh_obj.rotation_euler = (0.0, 0.0, math.radians(90))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    mesh_obj.scale = (scale, scale, scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    grip_r = rotate_90_about_z(on_centreline(entry["Grip_R"])) * scale
    grip_l = rotate_90_about_z(on_centreline(entry["Grip_L"])) * scale
    muzzle = rotate_90_about_z(on_centreline(entry["Muzzle"])) * scale

    mesh_obj.location = -grip_r
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    bpy.context.view_layer.update()

    grip_l_final = grip_l - grip_r
    muzzle_final = muzzle - grip_r
    grip_r_final = Vector((0.0, 0.0, 0.0))

    for name, loc in (
        ("Grip_R", grip_r_final),
        ("Grip_L", grip_l_final),
        ("Muzzle", muzzle_final),
    ):
        empty = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(empty)
        empty.location = loc
        empty.parent = mesh_obj

    mesh_obj.name = weapon_name

    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=False,
        object_types={"MESH", "EMPTY"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        add_leaf_bones=False,
        path_mode="STRIP",
        embed_textures=False,
        use_mesh_modifiers=True,
    )

    return length_m


def self_check(out_path, weapon_name, length_m):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=out_path)
    bpy.context.view_layer.update()

    objs = {o.name: o for o in bpy.context.scene.objects}
    mesh_obj, grip_r, grip_l, muzzle = (
        objs.get(weapon_name),
        objs.get("Grip_R"),
        objs.get("Grip_L"),
        objs.get("Muzzle"),
    )
    if None in (mesh_obj, grip_r, grip_l, muzzle):
        missing = [
            n
            for n, o in (("mesh", mesh_obj), ("Grip_R", grip_r), ("Grip_L", grip_l), ("Muzzle", muzzle))
            if o is None
        ]
        fail(f"self-check: missing object(s) in exported file: {missing}")

    bmin, bmax = world_bbox(mesh_obj)
    bbox_length = (bmax - bmin).y
    centre_x = (bmin.x + bmax.x) / 2.0

    grip_r_pos = grip_r.matrix_world.translation
    grip_l_pos = grip_l.matrix_world.translation
    muzzle_pos = muzzle.matrix_world.translation

    checks = [
        (f"bbox length {bbox_length:.4f} m vs expected {length_m:.4f} m", abs(bbox_length - length_m) <= 0.001),
        (f"Grip_R at origin (pos={tuple(round(v, 4) for v in grip_r_pos)})", grip_r_pos.length <= 0.001),
        (
            f"Muzzle in front of Grip_R along -Y (muzzle.y={muzzle_pos.y:.4f}, grip_r.y={grip_r_pos.y:.4f})",
            muzzle_pos.y < grip_r_pos.y,
        ),
        (
            f"Muzzle on mesh centre-line (muzzle.x={muzzle_pos.x:.4f}, centre_x={centre_x:.4f})",
            abs(muzzle_pos.x - centre_x) <= 0.001,
        ),
    ]

    all_pass = True
    for message, ok in checks:
        print(f"{'PASS' if ok else 'FAIL'}: {message}")
        all_pass = all_pass and ok

    print(f"Grip_R  = {tuple(round(v, 4) for v in grip_r_pos)}")
    print(f"Grip_L  = {tuple(round(v, 4) for v in grip_l_pos)}")
    print(f"Muzzle  = {tuple(round(v, 4) for v in muzzle_pos)}")

    return all_pass


def main():
    raw_in, weapon_name, out_path = parse_args()
    length_m = normalize(raw_in, weapon_name, out_path)
    ok = self_check(out_path, weapon_name, length_m)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    # Blender's embedded Python exits 0 on an uncaught exception by default,
    # which would let a crashed normalize pass silently in CI/manager runs.
    try:
        main()
    except SystemExit:
        raise
    except Exception:
        traceback.print_exc()
        sys.exit(1)
