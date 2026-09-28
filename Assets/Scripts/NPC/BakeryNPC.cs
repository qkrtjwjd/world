using System.Collections;
using UnityEngine;

/// <summary>
/// 빵집 NPC · 미루 (개정 D S#15 15-B · 문단 588~602).
///
///   · 평소 — 같은 말을 반복하고 같은 행동을 반복한다(루프 노드).
///   · 단검을 <b>쥐면</b> 행동을 멈추고 눈만 루를 본다. 처음 한 번만 「…그거 치워요」 → 「죄송해요」 →
///     미루가 몸을 돌려 진열대 위에 반죽을 올려둔다(루에게 건네지 않는다) → [아이템 획득: 빵 반죽] (591~598).
///   · 그 뒤로 단검을 쥐면 멈추고 바라보기만 한다 — 「한번만 말한다」(595).
///   · 단검을 치우면 다시 같은 말을 반복한다(599).
///
/// 2026-09-27 정본 대조로 고쳤다(배치 실측 · 코드 대조):
///   · 단검 판정이 DaggerSystem.IsEquipped(= 단검을 가졌는가)라 S#12 이후 마을 내내 「단검 상태」였다 → 쥐고 있는가(IsRealityView).
///   · 스토리 노드가 단검과 상관없이 첫 방문에 통째로 재생돼 단검 없이도 「그거 치워요」가 나왔다.
///   · 「그거 치워요」 루프가 4초마다 반복됐다.
///   · 빵 반죽 아이템이 지급되지 않았다(플래그만 섰다 — BreadDoughInteractable 은 씬에 없다).
/// </summary>
public class BakeryNPC : MonoBehaviour
{
    [Header("스프라이트")]
    [SerializeField] private SpriteRenderer npcRenderer;
    [SerializeField] private Sprite         normalSprite;
    [SerializeField] private Sprite         daggerSprite;

    [Header("Yarn 노드")]
    [Tooltip("단검을 처음 쥐었을 때 한 번 — 인사 · 그거 치워요 · 죄송해요.")]
    [SerializeField] private string storyNode      = "Village_Bakery";
    [SerializeField] private string loopNodeNormal = "BakeryNPC_Loop_Normal";
    [Tooltip("⚠ 쓰지 않는다 — 「…그거 치워요」는 한 번만 말한다(D 595). 이름 호환을 위해 필드만 남긴다.")]
    [SerializeField] private string loopNodeDagger = "BakeryNPC_Loop_Dagger";

    [Header("획득")]
    [Tooltip("빵 반죽. 비우면 Resources/Items/BreadDough 를 쓴다.")]
    [SerializeField] private ItemData breadDoughItem;

    [Header("설정")]
    [SerializeField] private float loopInterval = 4f;

    private bool      _playerNear;
    private Coroutine _loopRoutine;
    private bool      _storyRunning;

    static bool DaggerHeld => DaggerFilterController.IsRealityView;
    static bool StoryDone  => GameState.isBreadDoughAcquired;

    void Update()
    {
        if (!_playerNear) return;

        if (npcRenderer != null)
            npcRenderer.sprite = DaggerHeld && daggerSprite != null ? daggerSprite : normalSprite;

        // 단검을 처음 쥔 순간 — 반복을 끊고 한 번만 반응한다.
        if (DaggerHeld && !StoryDone && !_storyRunning)
        {
            if (_loopRoutine != null) { StopCoroutine(_loopRoutine); _loopRoutine = null; }
            if (YarnDialogue.IsRunning) YarnDialogue.Runner.Stop();   // 반복 중이던 인사를 끊는다 — 행동을 멈춘다(593)
            StartCoroutine(StoryRoutine());
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        _playerNear = true;
        if (_loopRoutine == null && !_storyRunning) _loopRoutine = StartCoroutine(DialogueLoop());
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        _playerNear = false;
        if (_loopRoutine != null) { StopCoroutine(_loopRoutine); _loopRoutine = null; }
        if (npcRenderer != null && normalSprite != null)
            npcRenderer.sprite = normalSprite;
    }

    IEnumerator StoryRoutine()
    {
        _storyRunning = true;
        yield return null;   // Runner.Stop 이 정리될 한 프레임

        yield return YarnDialogue.PlayAndWait(storyNode, true);

        // 미루가 몸을 돌려 진열대 위에 반죽을 올려둔다. 루에게 건네지 않는다(597) → [아이템 획득: 빵 반죽](598).
        // 진열대 위 반죽 그림은 아트 대기 — 지금은 획득 알림만 뜬다.
        var item = breadDoughItem != null ? breadDoughItem : Resources.Load<ItemData>("Items/BreadDough");
        if (item != null && InventoryManager.Instance != null && !StoryDone)
            InventoryManager.Instance.AddItem(item);
        GameState.isBreadDoughAcquired = true;
        FlagManager.Instance?.SetFlag("빵반죽_획득", true);

        _storyRunning = false;
        if (_playerNear && _loopRoutine == null) _loopRoutine = StartCoroutine(DialogueLoop());
    }

    /// <summary>평소의 반복. 단검을 쥐고 있는 동안에는 멈추고 바라보기만 한다 — 말하지 않는다.</summary>
    IEnumerator DialogueLoop()
    {
        while (_playerNear)
        {
            if (!YarnDialogue.IsRunning && !DaggerHeld && !_storyRunning)
                yield return YarnDialogue.PlayAndWait(loopNodeNormal, false);
            yield return new WaitForSeconds(loopInterval);
        }
        _loopRoutine = null;
    }
}
