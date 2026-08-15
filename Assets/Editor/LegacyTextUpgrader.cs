#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 레거시 UnityEngine.UI.Text 컴포넌트를 TextMeshProUGUI로 일괄 변환합니다.
/// 스크립트 컴파일 후 자동 1회 실행 + 메뉴: Tools > Upgrade Legacy Text
/// </summary>
[InitializeOnLoad]
public static class LegacyTextUpgrader
{
    // 스크립트 로드 시 자동 실행 (컴파일 완료 직후 1회)
    static LegacyTextUpgrader()
    {
        if (EditorPrefs.GetBool("LegacyTextUpgrader.Done", false)) return;
        EditorApplication.delayCall += RunOnce;
    }

    static void RunOnce()
    {
        if (EditorPrefs.GetBool("LegacyTextUpgrader.Done", false)) return;
        EditorPrefs.SetBool("LegacyTextUpgrader.Done", true);
        UpgradeAll();
        Debug.Log("[LegacyTextUpgrader] 자동 실행 완료.");
    }

    static readonly string[] s_PrefabPaths =
    {
        "Assets/Prefabs/Canvas.prefab",
        "Assets/Prefabs/Objective Canvas.prefab",
        "Assets/Prefabs/FloatingCanvas.prefab",
        "Assets/Resources/Items/Slot.prefab",
        "Assets/Resources/Items/apple.prefab",
    };

    static readonly string[] s_FontPaths =
    {
        "Assets/Font/Pretendard-Medium SDF.asset",
        "Assets/Font/DungGeunMo SDF.asset",
        "Assets/Font/RIDIBatang SDF.asset",
        "Assets/Font/MapoFlowerIsland SDF.asset",
    };

    // ── 메뉴 ──────────────────────────────────────────────────────────

    [MenuItem("Tools/Upgrade Legacy Text/Upgrade Prefabs")]
    static void UpgradePrefabs()
    {
        int total = 0;
        foreach (string path in s_PrefabPaths)
        {
            int count = UpgradePrefab(path);
            if (count >= 0)
            {
                Debug.Log($"[LegacyTextUpgrader] {path}: {count}개 변환 완료");
                total += count;
            }
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("업그레이드 완료",
            $"프리팹 {s_PrefabPaths.Length}개에서 레거시 Text {total}개를 TMP로 변환했습니다.", "확인");
    }

    /// <summary>배치 모드 진입점: -executeMethod LegacyTextUpgrader.UpgradeAll</summary>
    public static void UpgradeAll()
    {
        // 1) 프리팹
        int prefabTotal = 0;
        foreach (string path in s_PrefabPaths)
        {
            int count = UpgradePrefab(path);
            if (count >= 0)
            {
                Debug.Log($"[LegacyTextUpgrader] {path}: {count}개 변환");
                prefabTotal += count;
            }
        }
        AssetDatabase.SaveAssets();

        // 2) 씬 — 배치 모드에서 각 씬을 직접 열고 저장
        string[] scenePaths =
        {
            "Assets/Scenes/TitleScene.unity",
            "Assets/Scenes/Home.unity",
            "Assets/Scenes/CreditsScene.unity",
            "Assets/Scenes/MapScene.unity",
        };

        int sceneTotal = 0;
        foreach (string scenePath in scenePaths)
        {
            if (!System.IO.File.Exists(scenePath)) continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int count = UpgradeScene(scene);
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"[LegacyTextUpgrader] {scenePath}: {count}개 변환");
            }
            sceneTotal += count;
        }

        Debug.Log($"[LegacyTextUpgrader] 완료 — 프리팹: {prefabTotal}개, 씬: {sceneTotal}개");
    }

    [MenuItem("Tools/Upgrade Legacy Text/Upgrade Open Scenes")]
    static void UpgradeOpenScenes()
    {
        int total    = 0;
        int modified = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            int count = UpgradeScene(scene);
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                modified++;
            }
            total += count;
            Debug.Log($"[LegacyTextUpgrader] {scene.name}: {count}개 변환 완료");
        }

        if (modified > 0)
            EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog("업그레이드 완료",
            $"열린 씬에서 레거시 Text {total}개를 TMP로 변환했습니다.", "확인");
    }

    // ── 프리팹 처리 ────────────────────────────────────────────────────

    static int UpgradePrefab(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[LegacyTextUpgrader] 프리팹을 찾을 수 없습니다: {path}");
            return -1;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            int count = texts.Length;
            foreach (Text t in texts)
                ConvertText(t);

            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(root, path);

            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 씬 처리 ────────────────────────────────────────────────────────

    static int UpgradeScene(Scene scene)
    {
        var texts = new List<Text>();
        foreach (GameObject go in scene.GetRootGameObjects())
            texts.AddRange(go.GetComponentsInChildren<Text>(true));

        foreach (Text t in texts)
            ConvertText(t);

        return texts.Count;
    }

    // ── 변환 핵심 로직 ─────────────────────────────────────────────────

    static void ConvertText(Text legacy)
    {
        if (legacy == null) return;
        GameObject go = legacy.gameObject;

        string  savedText        = legacy.text;
        Color   savedColor       = legacy.color;
        int     savedFontSize    = legacy.fontSize;
        bool    savedRaycast     = legacy.raycastTarget;
        bool    savedMaskable    = legacy.maskable;
        var     savedAlignment   = legacy.alignment;
        var     savedFontStyle   = legacy.fontStyle;

        Object.DestroyImmediate(legacy, true);

        var tmp               = go.AddComponent<TextMeshProUGUI>();
        tmp.text              = savedText;
        tmp.color             = savedColor;
        tmp.fontSize          = savedFontSize;
        tmp.raycastTarget     = savedRaycast;
        tmp.maskable          = savedMaskable;
        tmp.alignment         = MapAlignment(savedAlignment);
        tmp.fontStyle         = MapFontStyle(savedFontStyle);

        AssignKoreanFont(tmp);
    }

    // ── 매핑 ──────────────────────────────────────────────────────────

    static TextAlignmentOptions MapAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:    return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:  return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:   return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:   return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:  return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:    return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:  return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:   return TextAlignmentOptions.BottomRight;
            default:                      return TextAlignmentOptions.TopLeft;
        }
    }

    static FontStyles MapFontStyle(FontStyle style)
    {
        FontStyles result = FontStyles.Normal;
        if ((style & FontStyle.Bold)   != 0) result |= FontStyles.Bold;
        if ((style & FontStyle.Italic) != 0) result |= FontStyles.Italic;
        return result;
    }

    // ── 한글 폰트 적용 ─────────────────────────────────────────────────

    static bool AssignKoreanFont(TMP_Text tmp)
    {
        foreach (string path in s_FontPaths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) { tmp.font = font; return true; }
        }
        Debug.LogWarning("[LegacyTextUpgrader] 한글 TMP 폰트를 찾을 수 없습니다. Assets/Font/ 경로를 확인하세요.");
        return false;
    }
}
#endif
