using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// S#11 라디오 — 유의 목소리.
///
/// 2026-08-08 (D 정본 2026-08-07): 상호작용이 아니라 **AtticBoxInteraction 이 S#10 직후에
/// PlayRoutine() 을 호출**한다. 정본 S#11: "라디오 다이얼이 저 혼자 미세하게 떨리다가
/// 지지직— 하고 갈라진 소리를 뱉는다."
///
/// ⚠ D이관-3절 규약 — **라디오가 스스로 재생되는 것은 이 씬이 유일하다.**
///   이후로는 플레이어가 오브젝트에 다가갔을 때 [라디오] 선택지로만 호출된다
///   (Radio_Yu.yarn / RadioManager). 첫 재생만 예외라는 것을 잊지 말 것.
///
/// ※ 이 씬이 데모 전체의 동기다. 루는 자유를 찾아 나가는 것이 아니라 아빠를 찾아 나간다.
/// </summary>
public class AtticRadioCutscene : MonoBehaviour
{
    public static AtticRadioCutscene Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── S#11 ─────────────────────────────────────────────────────────────
    [Header("S#11 — 효과음")]
    public AudioClip sfxRadioButton;
    [Tooltip("AudioManager 에 등록된 라디오 잡음 루프 클립 이름")]
    public string sfxRadioStaticName = "radio_static";

    [Header("S#11 — Yarn 노드 이름 (아빠 녹음 대사)")]
    public string yarnNode_radio = "House_Radio_Yu_First";

    [Header("연출")]
    [Tooltip("라디오 다이얼 클로즈업 Image. 정본: 유의 목소리가 나오는 동안 컷을 바꾸지 않는다.")]
    public Image radioDialImage;

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>단독 호출용. 기다리지 않고 시작만 한다.</summary>
    public void BeginCutscene()
    {
        if (GameState.isAtticRadioPlayed) return;
        StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// 시퀀스 안에서 순서를 지켜 기다려야 할 때 쓴다 (AtticBoxInteraction 의 S#11).
    /// 이미 재생됐으면 즉시 끝난다.
    /// </summary>
    public IEnumerator PlayRoutine()
    {
        if (GameState.isAtticRadioPlayed) yield break;
        GameState.isAtticRadioPlayed = true;
        yield return StartCoroutine(PlayCutscene());
    }

    IEnumerator PlayCutscene()
    {
        var ctrl = YarnDialogue.LockPlayer();

        // 다이얼이 저 혼자 떨린다 — 루는 버튼을 누르지 않았다.
        AudioManager.Instance?.Play(sfxRadioButton);

        if (radioDialImage != null)
            yield return StartCoroutine(FadeInImage(radioDialImage, 1f, 0.3f));

        yield return new WaitForSeconds(0.5f);

        // 지직거리는 잡음 루프
        if (!string.IsNullOrEmpty(sfxRadioStaticName))
            AudioManager.Instance?.PlayLoop(sfxRadioStaticName);
        yield return new WaitForSeconds(1f);

        // 아빠 녹음 Yarn 대사
        // ⚠ 정본: 유의 목소리가 나오는 동안 컷을 바꾸지 않는다. 루의 얼굴을 보여주지 않는다.
        if (!string.IsNullOrEmpty(yarnNode_radio))
            yield return YarnDialogue.PlayAndWait(yarnNode_radio, false);

        // 잡음 정지
        if (!string.IsNullOrEmpty(sfxRadioStaticName))
            AudioManager.Instance?.StopLoop(sfxRadioStaticName);

        if (radioDialImage != null)
            yield return StartCoroutine(FadeOutImage(radioDialImage, 0.3f));

        yield return new WaitForSeconds(0.3f);

        // 루가 스스로 결론을 낸다 — "제가 아빠 데리러 갈게요."
        // 현관문은 이제 이 플래그가 아니라 현관문 열쇠(S#10)로 열린다.
        // 플래그는 저널·엔딩 판정 등이 참조하므로 계속 세운다.
        GameState.isResolved = true;

        // 목표는 시퀀스 전체가 끝난 뒤 AtticBoxInteraction 이 한 번만 띄운다.

        YarnDialogue.UnlockPlayer(ctrl);
    }

    IEnumerator FadeInImage(Image image, float targetAlpha, float duration)
    {
        Color c = image.color;
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, targetAlpha, elapsed / duration);
            image.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        image.color = c;
    }

    IEnumerator FadeOutImage(Image image, float duration)
    {
        Color c = image.color;
        float start = c.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(start, 0f, elapsed / duration);
            image.color = c;
            yield return null;
        }
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(false);
    }
}
