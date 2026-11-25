#if UNITY_EDITOR
using UnityEditor;

namespace OutpostZero.EditorTools
{
    /// <summary>The Meshy weapon FBX models carry their own embedded material and texture; WeaponVisual always
    /// replaces it with a runtime Standard material built from the sibling PNG (see WeaponModels.cs), so
    /// importing that embedded copy would only bloat the build for an asset nothing ever uses.</summary>
    public class WeaponModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/Models/Weapons/")) return;
            if (!(assetImporter is ModelImporter importer)) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            // WeaponModels.TryBuild measures each model's real Unity-space geometry (which way the barrel
            // and magazine actually point) from its own vertices at runtime rather than assuming a fixed
            // axis convention - these are tiny props, so keeping them CPU-readable is a trivial memory cost.
            importer.isReadable = true;
        }
    }
}
#endif
