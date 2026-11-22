#if UNITY_EDITOR
using System;
using UnityEditor;

namespace OutpostZero.EditorTools
{
    /// <summary>The rigged Rook FBX is a Mixamo-style skeleton with six embedded animation takes. Humanoid
    /// auto-mapping can silently misbehave without a hand-verified avatar, so this imports it as Generic
    /// (safe without manual bone setup) and tags the locomotion takes as looping; Dead/Gunshot_Reaction stay
    /// one-shot. This project hand-writes no .meta files, so import settings only ever come from here.</summary>
    public class HeroModelImport : AssetPostprocessor
    {
        static readonly string[] LoopingClips = { "Idle", "Walking", "Running", "Run_and_Shoot" };
        const string HipsBoneName = "mixamorig:Hips";

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/Models/Hero/")) return;
            if (!(assetImporter is ModelImporter importer)) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            // Like the weapons, RookAvatar always replaces this with a runtime Standard material built
            // from the sibling PNG, so the embedded one would only bloat the build for nothing.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                bool loop = false;
                for (int n = 0; n < LoopingClips.Length; n++)
                {
                    if (clips[i].name.IndexOf(LoopingClips[n], StringComparison.OrdinalIgnoreCase) < 0) continue;
                    loop = true;
                    break;
                }
                clips[i].loopTime = loop;

                // The running takes bake forward travel straight into mixamorig:Hips' local X/Z (Mixamo has no
                // separate root bone to strip), so without this the rig visibly slides forward over every loop
                // and snaps back the instant it wraps - on top of whatever SquadController's own code-driven
                // position already does. Extracting X/Z as root motion (instead of baking it into the pose) and
                // then never applying it (RookAvatar sets animator.applyRootMotion = false) discards that travel
                // entirely; HeroAnimator.LockHipsHeight is a runtime-only backstop for the same thing so this
                // still holds even before a reimport. Y is left baked into the pose (untouched) - locking it to
                // a single frame here would fight each clip's own hip height the same way the runtime hard-lock
                // used to.
                clips[i].motionNodeName = HipsBoneName;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalPositionXZ = false;
            }
            importer.clipAnimations = clips;
        }
    }
}
#endif
