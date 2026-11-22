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
            }
            importer.clipAnimations = clips;
        }
    }
}
#endif
