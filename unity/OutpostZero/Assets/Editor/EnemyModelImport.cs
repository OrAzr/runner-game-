#if UNITY_EDITOR
using System;
using UnityEditor;

namespace OutpostZero.EditorTools
{
    /// <summary>Import settings for the rigged Meshy zombie/boss (same Mixamo-style skeleton/take convention
    /// as SoldierModelImport/HeroModelImport). A run can have 16-20 zombies plus the boss on screen at once,
    /// so this tunes mesh compression and the sibling texture's max size the same way the squad's own import
    /// does rather than at single-hero quality.</summary>
    public class EnemyModelImport : AssetPostprocessor
    {
        static readonly string[] LoopingClips = { "Walking", "Running" };

        static bool IsEnemyAsset(string path)
        {
            var p = path.Replace('\\', '/');
            return p.Contains("Resources/Models/Zombie/") || p.Contains("Resources/Models/Boss/");
        }

        void OnPreprocessModel()
        {
            if (!IsEnemyAsset(assetPath)) return;
            if (!(assetImporter is ModelImporter importer)) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            // EnemyAvatar always replaces this with a runtime Standard material shared across every
            // instance of that model (see EnemyAvatar's zombieMaterial/bossMaterial), so the embedded one
            // would only bloat the build.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;

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
                // Punch_Combo, Charged_Ground_Slam, Zombie_Scream and Dead all stay one-shot (loop = false).
                clips[i].loopTime = loop;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!IsEnemyAsset(assetPath)) return;
            if (!(assetImporter is TextureImporter importer)) return;

            // One shared material/texture per model regardless of how many zombies are on screen, so the
            // cost is paid once - same 1024 ceiling SoldierModelImport uses for its own crowd.
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
        }
    }
}
#endif
