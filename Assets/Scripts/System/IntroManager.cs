using System.Collections;
using UnityEngine;
using TMPro;

public class IntroManager : MonoBehaviour
{
    [Header("나레이션 텍스트")]
    [TextArea] public string[] lines =
    {
        "시간이 흐르면 자연적으로 해결되는 일들이 있다고 한다",
        "짧은 머리가 길어지는 것, 이별의 통증, 새 신이 발에 알맞게 맞춰지는 것",
        "그렇다면 어른 또한 시간이 흐르면 자연적으로 되는 걸까",
        "모르겠다"
    };

    [Header("연출 설정")]
    public float typeInterval = 0.05f;  // 글자 간격(초)
    public float holdTime     = 2.0f;   // 한 줄 표시 후 대기(초)
    public float fadeDuration = 0.6f;   // 페이드 인/아웃 시간(초)

    private CanvasGroup _group;
    private TMP_Text    _text;

    void Start() => StartCoroutine(PlayIntro());

    IEnumerator PlayIntro()
    {
        BuildOverlay();

        yield return FadeGroup(0f, 1f);   // 검은 배경 등장

        foreach (string line in lines)
        {
            _text.color = Color.white;
            _text.text  = "";
            bool skipped = false;

            foreach (char c in line)
            {
                _text.text += c;
                float elapsed = 0f;
                while (elapsed < typeInterval)
                {
                    elapsed += Time.deltaTime;
                    if (Input.anyKeyDown) { skipped = true; break; }
                    yield return null;
                }
                if (skipped) { _text.text = line; break; }
            }

            // 스킵에 사용된 키 입력이 같은 프레임의 대기 루프까지 건너뛰지 않도록 1프레임 소비
            if (skipped) yield return null;

            float hold = 0f;
            while (hold < holdTime)
            {
                hold += Time.deltaTime;
                if (Input.anyKeyDown) break;
                yield return null;
            }

            yield return FadeText(1f, 0f);  // 텍스트 페이드 아웃
        }

        yield return FadeGroup(1f, 0f);   // 전체 페이드 아웃

        TransitionManager.Instance?.DoSceneTransition(SceneNames.Home);
    }

    void BuildOverlay()
    {
        var root = new GameObject("IntroOverlay");

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        root.AddComponent<UnityEngine.UI.CanvasScaler>();
        _group = root.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.blocksRaycasts = false;

        // 검은 배경
        var bg   = new GameObject("Background");
        bg.transform.SetParent(root.transform, false);
        var img  = bg.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        var bgRt  = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 나레이션 텍스트
        var textGo = new GameObject("NarrationText");
        textGo.transform.SetParent(root.transform, false);
        _text           = textGo.AddComponent<TextMeshProUGUI>();
        _text.fontSize  = 20;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color     = Color.white;
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = new Vector2(0.1f, 0.3f);
        trt.anchorMax = new Vector2(0.9f, 0.7f);
        trt.sizeDelta = Vector2.zero;
    }

    IEnumerator FadeGroup(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t           += Time.deltaTime;
            _group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        _group.alpha = to;
    }

    IEnumerator FadeText(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t        += Time.deltaTime;
            _text.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        _text.color = new Color(1f, 1f, 1f, to);
    }
}
