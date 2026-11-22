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
            if (assetImporter is ModelImporter importer)
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
#endif
