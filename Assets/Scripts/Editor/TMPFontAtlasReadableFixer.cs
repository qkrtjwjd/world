using UnityEditor;
using TMPro;

class TMPFontAtlasReadableFixer : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets,
        string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (!path.EndsWith(".asset")) continue;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasPopulationMode == AtlasPopulationMode.Static) continue;

            bool dirty = false;
            foreach (var tex in font.atlasTextures)
            {
                if (tex == null || tex.isReadable) continue;
                var so = new SerializedObject(tex);
                var prop = so.FindProperty("m_IsReadable");
                if (prop != null)
                {
                    prop.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                }
            }
            if (dirty) EditorUtility.SetDirty(font);
        }
    }
}
