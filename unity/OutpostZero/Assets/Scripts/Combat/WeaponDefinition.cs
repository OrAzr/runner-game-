using UnityEngine;

namespace OutpostZero
{
    /// <summary>Per-model data for the normalized weapon FBX assets (muzzle +Z, up +Y, origin at the
    /// right-hand grip - see tools/blender/normalize_weapon.py). lengthMeters is the asset's own length
    /// along +Z, used to scale the model rather than measuring its mesh bounds at runtime. fallbackGripL/
    /// fallbackMuzzle are only used if the FBX is missing its Grip_L/Muzzle empty; they were measured on the
    /// raw Meshy source (plan-notes/weapon-grips.json) and carried through the same rotate-and-rescale the
    /// normalizer applies, so they land in the same frame as the real empties.</summary>
    public struct WeaponDefinition
    {
        public string resourceName;
        public float lengthMeters;
        public bool twoHanded;
        public float recoilKick;
        public Vector3 fallbackGripL;
        public Vector3 fallbackMuzzle;

        public static WeaponDefinition Get(WeaponType t)
        {
            switch (t)
            {
                case WeaponType.Shotgun:
                    return new WeaponDefinition
                    {
                        resourceName = "shotgun",
                        lengthMeters = 0.65f,
                        twoHanded = true,
                        recoilKick = 1f,
                        fallbackGripL = new Vector3(0f, 0.052f, 0.174f),
                        fallbackMuzzle = new Vector3(0f, 0.113f, 0.478f)
                    };
                case WeaponType.Flamethrower:
                    return new WeaponDefinition
                    {
                        resourceName = "flamethrower",
                        lengthMeters = 0.70f,
                        twoHanded = true,
                        recoilKick = 0f,
                        fallbackGripL = new Vector3(0f, 0.042f, 0.170f),
                        fallbackMuzzle = new Vector3(0f, 0.129f, 0.557f)
                    };
                default:
                    return new WeaponDefinition
                    {
                        resourceName = "rifle",
                        lengthMeters = 0.70f,
                        twoHanded = true,
                        recoilKick = 0.35f,
                        fallbackGripL = new Vector3(0f, 0.044f, 0.168f),
                        fallbackMuzzle = new Vector3(0f, 0.117f, 0.503f)
                    };
            }
        }
    }
}
