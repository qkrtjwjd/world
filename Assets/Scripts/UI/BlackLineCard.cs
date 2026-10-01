using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 검은 전면에 가운데 한 줄 — 데모 종료 화면(F-6-2)의 틀. 숲 전투 사망 화면(F-9-4)이 같은 틀을 쓰고 문자열만 바꾼다(F-9-5).
/// 글꼴 · 크기를 두 화면이 같이 쓰도록 여기 한 곳에 둔다.
///
/// <para>Screen Space - Overlay(C-11). 암전(TransitionManager 999) 위에 얹는다. 씬을 넘어 살아남으므로 부른 쪽이 지운다.</para>
/// </summary>
public static class BlackLineCard
{
    public const float FontSize     = 22f;
    public const int   SortingOrder = 1000;   // TransitionManager 의 암전(999) 위

    /// <param name="alpha">문자열의 처음 알파. 떠오르게 할 쪽은 0 으로 만들고 직접 올린다.</param>
    public static GameObject Build(string name, string line, float alpha = 1f)
    {
        var root = new GameObject(name);
        Object.DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        UiCanvasScale.Add(root);
        root.AddComponent<GraphicRaycaster>();

        var bgGo = new GameObject("Black");
        bgGo.transform.SetParent(root.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;             // 뒤쪽 UI 클릭을 막는다
        Stretch(bgGo.GetComponent<RectTransform>());

        var textGo = new GameObject("Line");
        textGo.transform.SetParent(root.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text          = line;
        text.fontSize      = FontSize;
        text.color         = new Color(1f, 1f, 1f, alpha);
        text.alignment     = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Stretch(textGo.GetComponent<RectTransform>());

        return root;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
