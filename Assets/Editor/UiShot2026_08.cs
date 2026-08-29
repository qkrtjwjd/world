// 일회용. UI 프리팹을 정수배 창 크기로 렌더해 PNG 로 떨군다.
//
// 배치모드는 아무것도 그리지 않는다 — 단 그것은 -nographics 일 때다.
// 그래픽 디바이스를 켜고 RenderTexture 로 직접 그리면 편집 모드에서도 캡처된다.
// Overlay 캔버스는 카메라로 못 잡으므로 캡처 동안만 ScreenSpaceCamera 로 바꾼다
// (임시 씬이라 저장하지 않는다).
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UiShot2026_08
{
    private const int W = 1280;     // 640 x 360 의 정수배(2배)
    private const int H = 720;

    public static void Capture()
    {
        string outDir = System.Environment.GetEnvironmentVariable("UISHOT_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "uishot");
        Directory.CreateDirectory(outDir);

        Shoot(outDir, "dialogue.png", "Assets/Prefabs/Dialogue.prefab", SetUpDialogue);
        Shoot(outDir, "soltrade.png", "Assets/Prefabs/SolTradeCanvas.prefab", SetUpTrade);

        Debug.Log("[UiShot] done -> " + outDir);
    }

    private static void Shoot(string outDir, string file, string prefabPath,
                              System.Action<GameObject> setUp)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("ShotCam");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // 배경을 중간 회색으로 둔다. 검은 UI 도 흰 UI 도 경계가 보이게.
        cam.backgroundColor = new Color(0.25f, 0.25f, 0.28f);
        camGo.transform.position = new Vector3(0, 0, -10);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError("[UiShot] 프리팹 없음: " + prefabPath); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;

        setUp?.Invoke(go);

        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(go.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();

        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG());
        Debug.Log("[UiShot] wrote " + file);

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    // ── 대사창 — 알파를 올리고 표본 텍스트·선택지를 채운다 ──────────────────
    private static void SetUpDialogue(GameObject go)
    {
        foreach (var cg in go.GetComponentsInChildren<CanvasGroup>(true))
            cg.alpha = 1f;

        // 프리팹에서 꺼져 있는 것들 — 런타임에 대사 시스템이 켠다. 캡처에서는 손으로 켠다.
        Show(go, "DialoguePanel", true);
        Show(go, "PortraitImage", true);
        Show(go, "PortraitImage Right", true);

        SetText(go, "NameText", "쿠루");
        SetText(go, "BodyText",
                "여기까지 와서 뭘 망설이는 거야. 문은 이미 열렸고 돌아갈 길은 없어.");

        var panel = Find(go, "OptionsPanel");
        if (panel == null) return;
        panel.gameObject.SetActive(true);

        var item = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/DialogueOptionItem.prefab");
        if (item == null) return;

        string[] samples = { "열어본다", "두드려본다", "그냥 본다" };
        foreach (var s in samples)
        {
            var opt = (GameObject)PrefabUtility.InstantiatePrefab(item, panel);
            opt.SetActive(true);
            var t = opt.GetComponentInChildren<TMP_Text>(true);
            if (t != null) { t.gameObject.SetActive(true); t.text = s; }
        }
    }

    // ── 거래창 — 거래 모드 화면을 세운다 ──────────────────────────────────
    private static void SetUpTrade(GameObject go)
    {
        foreach (var cg in go.GetComponentsInChildren<CanvasGroup>(true))
            cg.alpha = 1f;

        Show(go, "TradeGroup", true);
        Show(go, "ItemFocusGroup", true);
        Show(go, "ChoiceGroup", false);

        SetText(go, "FocusName", "붉은 결정");
        SetText(go, "FocusGrade", "하");
        SetText(go, "FocusDescription",
                "숲에서 주운 것. 손에 쥐면 미지근하다. 솔이 받아 줄지는 모르겠다.");
        SetText(go, "OfferLabel", "솔이 내놓은 것");
    }

    private static RectTransform Find(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void Show(GameObject root, string name, bool on)
    {
        var t = Find(root, name);
        if (t != null) t.gameObject.SetActive(on);
    }

    private static void SetText(GameObject root, string name, string value)
    {
        var t = Find(root, name);
        if (t == null) return;
        t.gameObject.SetActive(true);
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = value;
    }
}
