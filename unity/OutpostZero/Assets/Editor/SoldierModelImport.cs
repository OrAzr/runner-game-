#if UNITY_EDITOR
using System;
using UnityEditor;

namespace OutpostZero.EditorTools
{
    /// <summary>Import settings for the rigged Meshy soldier (same Mixamo-style skeleton/convention as
    /// HeroModelImport, but the squad can have 48-70 of these on screen at once, so this also tunes mesh
    /// compression and the sibling texture's max size for a mobile crowd rather than a single hero.</summary>
    public class SoldierModelImport : AssetPostprocessor
    {
        static readonly string[] LoopingClips = { "Idle", "Walking", "Running" };

        static bool IsSoldierAsset(string path) => path.Replace('\\', '/').Contains("Resources/Models/Soldier/");

        void OnPreprocessModel()
        {
            if (!IsSoldierAsset(assetPath)) return;
            if (!(assetImporter is ModelImporter importer)) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            // SoldierAvatar always replaces this with a runtime Standard material shared across every
            // soldier (see SoldierAvatar.RiggedMaterial), so the embedded one would only bloat the build.
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
                clips[i].loopTime = loop;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!IsSoldierAsset(assetPath)) return;
            if (!(assetImporter is TextureImporter importer)) return;

            // One shared material/texture across the whole squad (SoldierAvatar.RiggedMaterial), so the cost
            // is paid once no matter the squad size - 512 was blurring the goggles/vest detail away at
            // normal view distance; 1024 keeps that readable without approaching Rook's own 2048.
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
        }
    }
}
#endif
