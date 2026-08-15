using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 크레딧 씬 관리자.
/// 크레딧 텍스트를 위로 스크롤하고, 최종 인형화 수치를 표시한 뒤 타이틀 화면으로 전환합니다.
///
/// 씬 구성 (CreditsScene):
///   1. Canvas 하나 생성
///   2. Canvas 루트에 CanvasGroup 컴포넌트 추가
///   3. 빈 GameObject에 이 컴포넌트를 추가하고 아래 항목을 연결:
///
///   [ScrollViewport]
///     - Canvas 자식 RectTransform, 화면 전체 크기로 설정
///     - Mask 컴포넌트 추가 (Show Mask Graphic 체크 해제)
///     - → viewport 필드에 연결
///
///   [ScrollContent]
///     - ScrollViewport 자식 RectTransform
///     - Anchor: 상단 중앙, Pivot: (0.5, 1)
///     - TMP_Text 또는 VerticalLayoutGroup + 여러 TMP_Text로 크레딧 내용 구성
///     - 시작 위치: anchoredPosition Y = 0 (또는 화면 높이의 절반 아래)
///     - → scrollContent 필드에 연결
///
///   [ResultPanel]
///     - Canvas 직접 자식, 초기 비활성화
///     - 인형화 수치 Text (puppetizationText) + 메시지 Text (messageText) 포함
///     - → resultPanel, puppetizationText(TMP_Text), messageText(TMP_Text) 필드에 연결
/// </summary>
public class CreditsManager : MonoBehaviour
{
    [Header("UI 연결")]
    public CanvasGroup   canvasGroup;
    public RectTransform scrollContent;
    public RectTransform viewport;
    public GameObject    resultPanel;
    public TMP_Text      puppetizationText;
    public TMP_Text      messageText;

    [Header("설정")]
    public float scrollSpeed       = 60f;
    public float fadeDuration      = 1f;
    public float resultDisplayTime = 5f;

    void Start()
    {
        resultPanel.SetActive(false);
        canvasGroup.alpha = 0f;
        StartCoroutine(CreditsFlow());
    }

    IEnumerator CreditsFlow()
    {
        yield return StartCoroutine(Fade(0f, 1f));
        yield return StartCoroutine(ScrollCredits());
        yield return StartCoroutine(Fade(1f, 0f));

        ShowResult();

        yield return StartCoroutine(Fade(0f, 1f));
        yield return new WaitForSecondsRealtime(resultDisplayTime);
        yield return StartCoroutine(Fade(1f, 0f));

        SceneManager.LoadScene(SceneNames.Title);
    }

    IEnumerator ScrollCredits()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);

        var contentCorners  = new Vector3[4];
        var viewportCorners = new Vector3[4];

        while (true)
        {
            scrollContent.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);

            scrollContent.GetWorldCorners(contentCorners);
            viewport.GetWorldCorners(viewportCorners);

            // contentCorners[0] = 콘텐츠 하단 모서리, viewportCorners[1] = 뷰포트 상단 모서리
            // 콘텐츠 하단이 뷰포트 상단보다 올라갔으면 스크롤 완료
            if (contentCorners[0].y > viewportCorners[1].y)
                break;

            yield return null;
        }
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);

        float pct = CorruptionManager.Instance != null
            ? CorruptionManager.Instance.currentCorruption
            : (GameState.player.IsInitialized ? GameState.player.puppetization : 0f);

        puppetizationText.text = $"최종 인형화 수치: {pct:F0}%";

        if (pct >= 80f)
            messageText.text = "당신은 거의 인형이 되었습니다.\n스스로를 잃어버렸군요.";
        else if (pct >= 50f)
            messageText.text = "당신은 절반쯤 인형이 되었습니다.\n경계에서 버티고 있군요.";
        else if (pct >= 20f)
            messageText.text = "당신은 아직 자신을 유지하고 있습니다.\n하지만 언제까지일지는 모릅니다.";
        else
            messageText.text = "당신은 끝까지 자신을 지켰습니다.\n훌륭합니다.";
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
