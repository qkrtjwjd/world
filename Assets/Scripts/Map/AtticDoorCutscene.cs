using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다락방 문 열기 + 입장 (정본 S#08 끝 ~ S#09 진입).
/// LockedDoorInteraction.UnlockAttic() 에서 PlayCutscene() 을 호출합니다.
///
/// ⚠ 2026-08-08 (D 정본): yarnNode_atticEnter 는 비워 두는 것이 맞습니다.
///   구 House_attic_in("엄마가 여기 오지 말라고 했는데...")은 폐기됐습니다 —
///   정본에는 세라의 금지 대사가 없어 성립하지 않습니다.
///   Home 씬 인스펙터에 남아 있으면 지워야 합니다 (Assets/Docs/유니티_수동작업.md).
///
/// ※ 정본 S#09: 다락방은 이 집에서 유일하게 세라의 손이 닿지 않은 공간이다.
///   아래층의 따뜻한 색조가 여기까지 올라오지 않는다. 색조로만 처리하고 대사로 말하지 않는다.
/// </summary>
public class AtticDoorCutscene : MonoBehaviour
{
    public static AtticDoorCutscene Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── S#08 ─────────────────────────────────────────────────────────────
    [Header("S#08 — 손+자물쇠 클로즈업 Image (Canvas)")]
    public Image handLockCloseupImage;

    [Header("S#08 — 문 열리는 배경 Image (Canvas)")]
    public Image doorOpenBgImage;

    [Header("S#08 — 효과음")]
    public AudioClip sfxLockClick;
    public AudioClip sfxDoorCreak;

    [Header("S#08 — Yarn 노드 이름")]
    public string yarnNode;

    // ── 입장 ─────────────────────────────────────────────────────────────
    [Header("입장 직후 Yarn 노드 이름 — 정본상 비워 두는 것이 맞습니다")]
    [Tooltip("구 House_attic_in 은 폐기됐습니다. 비워 두면 대사 없이 진행합니다.")]
    public string yarnNode_atticEnter = "";

    [Header("목표 표시")]
    public string objectiveHeader = "목표";
    public string objectiveBody   = "상자를 살펴보세요.";

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// LockedDoorInteraction 에서 호출. unlockAction 은 플레이어 위치이동·룸전환 로직.
    /// </summary>
    public IEnumerator PlayCutscene(Action unlockAction)
    {
        var ctrl = YarnDialogue.LockPlayer();

        // 손+자물쇠 클로즈업
        if (handLockCloseupImage != null)
            yield return StartCoroutine(FadeInImage(handLockCloseupImage, 1f, 0.3f));

        AudioManager.Instance?.Play(sfxLockClick);
        yield return new WaitForSeconds(0.5f);

        // 문 열리는 배경으로 전환
        if (handLockCloseupImage != null) handLockCloseupImage.gameObject.SetActive(false);
        if (doorOpenBgImage != null)      doorOpenBgImage.gameObject.SetActive(true);

        AudioManager.Instance?.Play(sfxDoorCreak);
        yield return new WaitForSeconds(0.8f);

        // S#08 대사
        if (!string.IsNullOrEmpty(yarnNode))
            yield return YarnDialogue.PlayAndWait(yarnNode);

        if (doorOpenBgImage != null)
            yield return StartCoroutine(FadeOutImage(doorOpenBgImage, 0.4f));

        // 플레이어 위치이동·룸전환
        unlockAction?.Invoke();
        yield return new WaitForSeconds(0.5f);

        // S#09 입장
        ObjectiveManager.Instance?.ShowObjective(objectiveHeader, objectiveBody);

        if (!string.IsNullOrEmpty(yarnNode_atticEnter))
            yield return YarnDialogue.PlayAndWait(yarnNode_atticEnter);

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
