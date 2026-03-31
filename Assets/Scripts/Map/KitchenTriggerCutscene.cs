using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// S#5 거실 / 아침 컷씬 트리거.
/// 플레이어가 트리거존에 진입하면 아침 식사 시퀀스를 재생한다.
/// hasWatchedBreakfast 플래그로 한 번만 발동된다.
/// </summary>
public class KitchenTriggerCutscene : MonoBehaviour
{
    public static KitchenTriggerCutscene Instance { get; private set; }

    void Awake() => Instance = this;
    // ── 대사 ─────────────────────────────────────
    [Header("S#5 — 대사")]
    public DialogueData dialogue_Part1;    // 세라↔루 (미소 굳기 전): "우리 아가..." ~ "정원에 계시던데"
    public DialogueData dialogue_Part2;    // 세라↔루 (미소 풀린 후): "어머 봤니..." ~ 루 "...네."
    public DialogueData lu_InnerMonologue; // 『다락방. 엄마가 유일하게 못 들어가게 하는 곳.』

    // ── 세라 Animator ─────────────────────────────
    [Header("S#5 — 세라 Animator")]
    public Animator seraAnimator;
    public string seraSmileFreezeTrigger = "SmileFreeze"; // 미소 굳음 (0.5초 후 자동 복귀)
    public string seraKissAndExitTrigger = "KissAndExit"; // 이마 입맞춤 + 퇴장

    // ── 효과음 ───────────────────────────────────
    [Header("S#5 — 효과음")]
    public AudioSource doorCloseAudio; // 현관문 닫히는 소리

    // ── 목표 UI ──────────────────────────────────
    [Header("목표 UI")]
    public GameObject objectivePanel;
    public Text objectiveHeaderText;
    public Text objectiveBodyText;

    // ─────────────────────────────────────────────

    /// <summary>NightSequenceManager 종료 후 자동 호출.</summary>
    public void BeginCutscene()
    {
        if (GameState.hasWatchedBreakfast) return;
        GameState.hasWatchedBreakfast = true;
        StartCoroutine(PlayCutscene());
    }

    /// <summary>직접 트리거존에 진입했을 때 fallback.</summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        BeginCutscene();
    }

    IEnumerator PlayCutscene()
    {
        var ctrl = LockPlayer();

        // Part1: 세라↔루 대사 (세라 미소 굳기 전)
        yield return PlayDialogueAndWait(dialogue_Part1);

        // 세라 미소 굳는 연출 (0.5초)
        if (seraAnimator != null)
            seraAnimator.SetTrigger(seraSmileFreezeTrigger);
        yield return new WaitForSeconds(0.5f);

        // Part2: 세라↔루 대사 (해충 발언 ~ 루 "...네.")
        yield return PlayDialogueAndWait(dialogue_Part2);

        // 세라 이마 입맞춤 + 퇴장
        if (seraAnimator != null)
            seraAnimator.SetTrigger(seraKissAndExitTrigger);
        yield return new WaitForSeconds(1.5f);

        // 현관문 소리
        if (doorCloseAudio != null)
            doorCloseAudio.Play();
        yield return new WaitForSeconds(0.5f);

        // 목표 UI 표시
        ShowObjective("현재 목표", "어머니가 돌아오기 전에 단서를 찾으세요.");

        // 루 내면 독백: 『다락방. 엄마가 유일하게 못 들어가게 하는 곳.』
        yield return PlayDialogueAndWait(lu_InnerMonologue);

        yield return new WaitForSeconds(2f);
        HideObjective();

        UnlockPlayer(ctrl);
    }

    // ─── 헬퍼 ────────────────────────────────────

    IEnumerator PlayDialogueAndWait(DialogueData data)
    {
        if (data == null || DialogueManager.Instance == null) yield break;
        DialogueManager.Instance.StartDialogue(data);
        yield return null; // 1프레임 대기 — isTalking 활성화 보장
        while (DialogueManager.Instance != null && DialogueManager.Instance.isTalking)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                DialogueManager.Instance.DisplayNextSentence();
            yield return null;
        }
    }

    void ShowObjective(string header, string body)
    {
        if (objectivePanel == null) return;
        if (objectiveHeaderText != null) objectiveHeaderText.text = header;
        if (objectiveBodyText   != null) objectiveBodyText.text   = body;
        objectivePanel.SetActive(true);
    }

    void HideObjective()
    {
        if (objectivePanel != null) objectivePanel.SetActive(false);
    }

    static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return null;
        var rb = ctrl.GetComponent<Rigidbody2D>();
        ctrl.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        return ctrl;
    }

    static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.enabled = true;
    }
}
