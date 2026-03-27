using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class IntroSlide
{
    public Sprite illustration;
    [TextArea(2, 5)] public string[] captions;
}

public class IntroManager : MonoBehaviour
{
    [Header("슬라이드 목록")]
    public IntroSlide[] slides;

    [Header("UI 연결")]
    public Image illustrationImage;
    public GameObject captionPanel;
    public Text captionText;
    public CanvasGroup canvasGroup;

    [Header("설정")]
    public float fadeDuration = 0.5f;

    private int currentSlideIndex = 0;
    private int currentCaptionIndex = 0;
    private bool isFading = false;

    void Start()
    {
        if (slides == null || slides.Length == 0)
        {
            TransitionManager.Instance?.DoSceneTransition(SceneNames.Home);
            return;
        }

        canvasGroup.alpha = 0f;
        ShowContent(0, 0);
        StartCoroutine(InitialFadeIn());
    }

    void Update()
    {
        if (isFading) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            Advance();
        }
    }

    void ShowContent(int slideIndex, int captionIndex)
    {
        IntroSlide slide = slides[slideIndex];
        illustrationImage.sprite = slide.illustration;

        bool hasCaption = slide.captions != null && slide.captions.Length > 0
                          && !string.IsNullOrEmpty(slide.captions[captionIndex]);
        captionPanel.SetActive(hasCaption);
        captionText.text = hasCaption ? slide.captions[captionIndex] : string.Empty;
    }

    void Advance()
    {
        IntroSlide current = slides[currentSlideIndex];
        int captionCount = (current.captions != null) ? current.captions.Length : 0;

        // 현재 슬라이드에 대사가 더 있으면 페이드 없이 다음 대사만 표시
        if (currentCaptionIndex < captionCount - 1)
        {
            currentCaptionIndex++;
            captionText.text = current.captions[currentCaptionIndex];
            return;
        }

        // 마지막 대사였으면 다음 슬라이드로 전환
        StartCoroutine(AdvanceSlide());
    }

    IEnumerator InitialFadeIn()
    {
        isFading = true;
        yield return StartCoroutine(FadeIn());
        isFading = false;
    }

    IEnumerator AdvanceSlide()
    {
        isFading = true;

        yield return StartCoroutine(FadeOut());

        currentSlideIndex++;
        currentCaptionIndex = 0;

        if (currentSlideIndex >= slides.Length)
        {
            TransitionManager.Instance?.DoSceneTransition(SceneNames.Home);
            yield break;
        }

        ShowContent(currentSlideIndex, currentCaptionIndex);
        yield return StartCoroutine(FadeIn());

        isFading = false;
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
