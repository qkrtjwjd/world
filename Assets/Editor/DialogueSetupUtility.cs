#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using Yarn.Unity;

/// <summary>
/// Dialogue.prefab에 필요한 Yarn Spinner 3.x 컴포넌트를 자동으로 추가/연결합니다.
/// 메뉴: 무채색낙원 > Dialogue 전체 설정
/// </summary>
public static class DialogueSetupUtility
{
    [MenuItem("무채색낙원/Dialogue 전체 설정")]
    static void SetupAll()
    {
        const string prefabPath      = "Assets/Prefabs/Dialogue.prefab";
        const string yarnProjectPath = "Assets/Dialogue/GameDialogue.yarnproject";

        // ── YarnProject 로드 ──────────────────────────────────────────────
        var yarnProject = AssetDatabase.LoadAssetAtPath<YarnProject>(yarnProjectPath);
        if (yarnProject == null)
        {
            Debug.LogError($"[DialogueSetup] YarnProject 없음: {yarnProjectPath}");
            return;
        }

        // ── Prefab 편집 모드 ──────────────────────────────────────────────
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[DialogueSetup] Prefab 로드 실패: {prefabPath}");
            return;
        }

        try
        {
            // 1. DialogueRunner 확인
            var runner = root.GetComponentInChildren<DialogueRunner>(true);
            if (runner == null)
            {
                Debug.LogError("[DialogueSetup] DialogueRunner를 찾지 못했습니다.");
                return;
            }

            // 2. YarnProject 연결 (SerializedObject 경유 — internal 필드)
            var runnerSO  = new SerializedObject(runner);
            var projProp  = runnerSO.FindProperty("yarnProject");
            if (projProp != null)
            {
                projProp.objectReferenceValue = yarnProject;
                runnerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3. InMemoryVariableStorage 추가 (없으면)
            var storage = root.GetComponentInChildren<InMemoryVariableStorage>(true);
            if (storage == null)
                storage = root.AddComponent<InMemoryVariableStorage>();

            // DialogueRunner.variableStorage 연결
            var storageProp = runnerSO.FindProperty("variableStorage");
            if (storageProp != null)
            {
                storageProp.objectReferenceValue = storage;
                runnerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // 4. BodyText / NameText 오브젝트 찾기
            var bodyTextGO = FindChildByName(root, "BodyText");
            var nameTextGO = FindChildByName(root, "NameText");

            // 5. TMP_Text로 교체 (기존 UnityEngine.UI.Text 제거 → TMP 추가)
            var bodyTMP = EnsureTMP(bodyTextGO, "대사");
            var nameTMP = EnsureTMP(nameTextGO, "화자");

            // 6. LinePresenter 추가 (없으면 DialoguePanel 또는 root에)
            var linePresenter = root.GetComponentInChildren<LinePresenter>(true);
            if (linePresenter == null)
            {
                var dialoguePanel = FindChildByName(root, "DialoguePanel") ?? root;
                linePresenter = dialoguePanel.AddComponent<LinePresenter>();
            }

            // LinePresenter 필드 연결
            if (bodyTMP != null)  linePresenter.lineText          = bodyTMP;
            if (nameTMP != null)  linePresenter.characterNameText = nameTMP;
            if (nameTextGO != null)
                linePresenter.characterNameContainer = nameTextGO;

            // 7. LineAdvancer 추가 (입력으로 대사 진행)
            var lineAdvancer = root.GetComponentInChildren<LineAdvancer>(true);
            if (lineAdvancer == null)
                lineAdvancer = root.AddComponent<LineAdvancer>();

            // 8. DialogueRunner.dialoguePresenters 에 추가
            var presentersProp = runnerSO.FindProperty("dialoguePresenters");
            if (presentersProp != null)
            {
                presentersProp.ClearArray();
                presentersProp.arraySize = 2;
                presentersProp.GetArrayElementAtIndex(0).objectReferenceValue = linePresenter;
                presentersProp.GetArrayElementAtIndex(1).objectReferenceValue = lineAdvancer;
                runnerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // 9. YarnCommandBridge 추가 (없으면)
            var bridge = root.GetComponentInChildren<YarnCommandBridge>(true);
            if (bridge == null)
                bridge = root.AddComponent<YarnCommandBridge>();

            // YarnCommandBridge 필드 연결
            var bridgeSO = new SerializedObject(bridge);
            SetObjRef(bridgeSO, "dialogueRunner", runner);
            SetObjRef(bridgeSO, "variableStorage", storage);

            // 포트레이트 이미지 연결
            var portraitLeft  = FindChildByName(root, "PortraitImage");
            var portraitRight = FindChildByName(root, "PortraitImage Right");
            if (portraitLeft  != null) SetObjRef(bridgeSO, "portraitImage",      portraitLeft.GetComponent<Image>());
            if (portraitRight != null) SetObjRef(bridgeSO, "portraitImageRight", portraitRight.GetComponent<Image>());
            bridgeSO.ApplyModifiedPropertiesWithoutUndo();

            // ── 저장 ─────────────────────────────────────────────────────
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[DialogueSetup] ✓ Dialogue.prefab 설정 완료. 씬을 다시 열어 확인하세요.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────

    static GameObject FindChildByName(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    /// <summary>
    /// Pretendard-Medium.otf 소스에서 Dynamic TMP Font Asset을 새로 생성합니다.
    /// Dynamic 모드: 런타임에 필요한 글자(한글 포함)를 OTF에서 자동 생성.
    /// 기존 SDF.asset을 덮어씁니다.
    /// </summary>
    [MenuItem("무채색낙원/한글 폰트 재생성 (Dynamic)")]
    static void RecreateFontDynamic()
    {
        const string otfPath  = "Assets/Font/Pretendard-Medium.otf";
        const string savePath = "Assets/Font/Pretendard-Medium SDF.asset";

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(otfPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[DialogueSetup] OTF 파일 없음: {otfPath}");
            return;
        }

        // Dynamic TMP FontAsset 생성 (runtime에 필요한 글자를 OTF에서 자동 렌더링)
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize : 90,
            atlasPadding      : 9,
            renderMode        : GlyphRenderMode.SDFAA,
            atlasWidth        : 1024,
            atlasHeight       : 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("[DialogueSetup] FontAsset 생성 실패");
            return;
        }

        // 기존 에셋 삭제 후 저장 (GUID 변경되지 않도록 덮어쓰기)
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(savePath);
        if (existing != null)
            AssetDatabase.DeleteAsset(savePath);

        AssetDatabase.CreateAsset(fontAsset, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DialogueSetup] ✓ Dynamic TMP FontAsset 생성 완료: {savePath}");
        Debug.Log("  → '무채색낙원 > Dialogue 폰트 수정 (한글)'을 실행해 BodyText에 적용하세요.");
    }

    [MenuItem("무채색낙원/Dialogue 폰트 수정 (한글)")]
    static void FixFontOnly()
    {
        const string prefabPath = "Assets/Prefabs/Dialogue.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) { Debug.LogError("[DialogueSetup] Prefab 로드 실패"); return; }
        try
        {
            bool changed = false;
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (AssignKoreanFont(tmp)) changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("[DialogueSetup] ✓ 한글 폰트 적용 완료");
            }
            else
                Debug.LogWarning("[DialogueSetup] 적용할 한글 폰트를 찾지 못했습니다. Assets/Font/ 폴더 확인.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets(); }
    }

    static bool AssignKoreanFont(TMP_Text tmp)
    {
        string[] fontPaths =
        {
            "Assets/Font/Pretendard-Medium SDF.asset",
            "Assets/Font/DungGeunMo SDF.asset",
            "Assets/Font/RIDIBatang SDF.asset",
            "Assets/Font/MapoFlowerIsland SDF.asset",
        };
        foreach (var path in fontPaths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) { tmp.font = font; return true; }
        }
        return false;
    }

    static TMP_Text EnsureTMP(GameObject go, string placeholder)
    {
        if (go == null) return null;

        // 이미 TMP가 있으면 폰트만 갱신
        var existing = go.GetComponent<TMP_Text>();
        if (existing != null)
        {
            AssignKoreanFont(existing);
            return existing;
        }

        // 구형 Text 제거
        var oldText = go.GetComponent<Text>();
        string prevText = oldText != null ? oldText.text : placeholder;
        if (oldText != null) Object.DestroyImmediate(oldText);

        // TMP_Text 추가 + 한글 폰트
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = prevText;
        tmp.fontSize = 16;
        AssignKoreanFont(tmp);
        return tmp;
    }

    static void SetObjRef(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.objectReferenceValue = value;
    }
}
#endif
