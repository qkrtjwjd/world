using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 빠른 저장 키(기본 F5, 설정에서 리바인딩 가능) 폴링 컨트롤러.
/// 씬 배선 없이 RuntimeInitializeOnLoadMethod 로 자동 생성됩니다 (GameOverUI 패턴).
/// 저장 가능 여부 판정(전투/대화/씬/디바운스)은 SaveManager.SaveCheckpoint 가 담당하고,
/// 여기서는 실제 저장된 경우에만 "저장 완료" 토스트를 띄웁니다.
/// </summary>
public class QuickSaveController : MonoBehaviour
{
    static QuickSaveController _instance;

    CanvasGroup     _toastGroup;
    TextMeshProUGUI _toastText;
    Coroutine       _toastRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var root = new GameObject("QuickSaveController [Auto]");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<QuickSaveController>();
    }

    void Update()
    {
        KeyCode key = SettingsManager.Instance != null
                      ? SettingsManager.Instance.keyQuickSave : KeyCode.F5;
        if (!Input.GetKeyDown(key)) return;
        if (SaveManager.Instance == null) return;

        // 명시적 사용자 요청이므로 자동 저장용 디바운스를 건너뛴다 (방금 자동 저장돼도 즉시 저장)
        switch (SaveManager.Instance.SaveCheckpoint("빠른 저장", bypassDebounce: true))
        {
            case SaveManager.CheckpointResult.Saved:
                ShowToast("저장 완료");
                break;
            case SaveManager.CheckpointResult.Busy:
                ShowToast("지금은 저장할 수 없습니다");
                break;
            // NotGameplay(타이틀 등): 조용히 무시. Debounced: bypass라 발생하지 않음.
        }
    }

    // ─────────────────────────────────────────────
    //  토스트 (코드 생성 UI — GameOverUI 패턴)
    // ─────────────────────────────────────────────
    void EnsureToast()
    {
        if (_toastGroup != null) return;

        var canvasGo = new GameObject("QuickSaveToast");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasGo.AddComponent<CanvasScaler>();

        _toastGroup = canvasGo.AddComponent<CanvasGroup>();
        _toastGroup.alpha          = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable   = false;

        var bg  = new GameObject("BG");
        bg.transform.SetParent(canvasGo.transform, false);
        var img = bg.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.7f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin        = bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0f, 90f);
        bgRect.sizeDelta        = new Vector2(220f, 44f);

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(bg.transform, false);
        _toastText = txtGo.AddComponent<TextMeshProUGUI>();
        _toastText.fontSize  = 20;
        _toastText.color     = Color.white;
        _toastText.alignment = TextAlignmentOptions.Center;
        var tr = txtGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
    }

    void ShowToast(string message)
    {
        EnsureToast();
        _toastText.text = message;
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastRoutine());
    }

    IEnumerator ToastRoutine()
    {
        _toastGroup.alpha = 1f;

        float shown = 0f;
        while (shown < 1.5f)
        {
            shown += Time.unscaledDeltaTime;
            yield return null;
        }

        const float fadeDuration = 0.3f;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _toastGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        _toastGroup.alpha = 0f;
        _toastRoutine = null;
    }
}
