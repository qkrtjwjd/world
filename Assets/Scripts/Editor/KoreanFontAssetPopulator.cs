using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;

public class KoreanFontAssetPopulator
{
    [MenuItem("Tools/세계/Populate Korean Font Atlas")]
    static void PopulateKoreanFontAtlas()
    {
        const string assetPath = "Assets/Font/Pretendard-Medium SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null) { Debug.LogError("Font asset not found: " + assetPath); return; }

        var chars = new HashSet<char>();

        // ASCII printable (0x20 - 0x7E)
        for (int i = 0x20; i <= 0x7E; i++) chars.Add((char)i);

        // Common Korean punctuation by unicode value
        int[] extra = { 0x2026, 0x2018, 0x2019, 0x201C, 0x201D, 0x00B7,
                        0x2014, 0x2013, 0x300C, 0x300D, 0x300E, 0x300F,
                        0x3010, 0x3011, 0x3014, 0x3015, 0x300A, 0x300B,
                        0x3008, 0x3009, 0xFF01, 0xFF1F, 0xFF0C, 0xFF0E };
        foreach (int cp in extra) chars.Add((char)cp);

        // Scan all .yarn dialogue files for used characters
        string projectRoot = Path.Combine(Application.dataPath, "..");
        foreach (var file in Directory.GetFiles(projectRoot, "*.yarn", SearchOption.AllDirectories))
            foreach (char c in File.ReadAllText(file, Encoding.UTF8))
                chars.Add(c);

        // Scan .cs files for Korean characters used in string literals
        foreach (var file in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            foreach (char c in File.ReadAllText(file, Encoding.UTF8))
                if (c >= 0xAC00 && c <= 0xD7A3)
                    chars.Add(c);

        var sb = new StringBuilder();
        foreach (char c in chars) sb.Append(c);

        bool success = font.TryAddCharacters(sb.ToString());
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();

        string[] yarnFiles = Directory.GetFiles(projectRoot, "*.yarn", SearchOption.AllDirectories);
        Debug.Log(string.Format("[KoreanFontAssetPopulator] {0} - {1} chars, {2} yarn files scanned.",
            success ? "OK" : "PARTIAL", chars.Count, yarnFiles.Length));
    }
}
