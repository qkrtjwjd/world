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
///   · 환상 필터에서 반복이 길어지면 루가 목 옆을 한 번 긁는다 — 대사 없음, 2초 뒤 대화가 이어진다(592 · 602 · B-루-10).
///     데모에서 여기 한 번만 쓴다. 동작 그림은 아트 대기 — 루 Animator 에 「NeckScratch」 트리거가 생기면 그대로 재생된다.
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

    [Header("목 긁기 (D 592 · 602 — 데모에서 한 번)")]
    [Tooltip("같은 인사를 이 횟수만큼 들은 뒤에 루가 목을 긁는다. 「대화가 일정 길이를 넘으면」.")]
    [SerializeField] private int   neckScratchAfterLines = 2;
    [Tooltip("긁는 동작 동안 대화를 멈추는 시간(초). 정본 「2초 뒤 모션이 끝나면 대화가 이어진다」.")]
    [SerializeField] private float neckScratchSeconds    = 2f;
    const string NeckScratchTrigger = "NeckScratch";

    // 데모 통틀어 한 번(602). 세이브에는 남기지 않는다 — 불러온 뒤 한 번 더 나와도 진행에 영향이 없다.
    static bool _neckScratchDone;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _neckScratchDone = false;

    private bool      _playerNear;
    private Coroutine _loopRoutine;
    private bool      _storyRunning;
    private bool      _scratching;
    private ClearSky.SimplePlayerController _scratchLock;   // 목 긁기 중에는 반복을 끊지 않는다 — 끊으면 조작 잠금이 풀리지 않는다

    static bool DaggerHeld => DaggerFilterController.IsRealityView;
    static bool StoryDone  => GameState.isBreadDoughAcquired;

    void Update()
    {
        if (!_playerNear) return;

        if (npcRenderer != null)
            npcRenderer.sprite = DaggerHeld && daggerSprite != null ? daggerSprite : normalSprite;

        // 단검을 처음 쥔 순간 — 반복을 끊고 한 번만 반응한다.
        if (DaggerHeld && !StoryDone && !_storyRunning && !_scratching)
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
        EndScratchIfCut();
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
        int heard = 0;
        while (_playerNear)
        {
            if (!YarnDialogue.IsRunning && !DaggerHeld && !_storyRunning)
            {
                yield return YarnDialogue.PlayAndWait(loopNodeNormal, false);
                heard++;

                // 환상 필터 상태의 NPC 대화가 길어지면 자동 1회(602). 단검을 쥐었으면 환상이 아니므로 건너뛴다.
                if (!_neckScratchDone && heard >= neckScratchAfterLines && _playerNear && !DaggerHeld)
                {
                    _neckScratchDone = true;
                    yield return NeckScratch();
                    continue;   // 동작이 끝나면 대화가 이어진다 — 반복 간격을 기다리지 않는다
                }
            }
            yield return new WaitForSeconds(loopInterval);
        }
        _loopRoutine = null;
    }

    /// <summary>루가 슬쩍 손을 들어 목 옆을 긁는다. 대사 없이, 그동안 조작을 잠근다.</summary>
    IEnumerator NeckScratch()
    {
        _scratching = true;
        var lu = _scratchLock = YarnDialogue.LockPlayer();
        var anim = lu != null ? lu.GetComponent<Animator>() : null;
        // 파라미터가 없는 컨트롤러에 트리거를 걸면 경고가 난다 — 그림이 오기 전에는 멈춤만 남는다.
        if (anim != null && HasTrigger(anim, NeckScratchTrigger)) anim.SetTrigger(NeckScratchTrigger);

        yield return new WaitForSeconds(neckScratchSeconds);

        if (anim != null && HasTrigger(anim, NeckScratchTrigger)) anim.ResetTrigger(NeckScratchTrigger);
        YarnDialogue.UnlockPlayer(lu);
        _scratchLock = null;
        _scratching = false;
    }

    /// <summary>목 긁기 도중 반복이 끊기면(순찰 발각 연출로 루가 옮겨지는 등) 걸어 둔 조작 잠금을 돌려준다.</summary>
    void EndScratchIfCut()
    {
        if (!_scratching) return;
        YarnDialogue.UnlockPlayer(_scratchLock);
        _scratchLock = null;
        _scratching = false;
    }

    void OnDisable()
    {
        if (_loopRoutine != null) { StopCoroutine(_loopRoutine); _loopRoutine = null; }
        EndScratchIfCut();
    }

    static bool HasTrigger(Animator anim, string name)
    {
        if (anim.runtimeAnimatorController == null) return false;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }
}
