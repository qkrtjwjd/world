using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S#6 다락방 상자 상호작용.
/// - 일반 아이템 2개를 동시 획득 알림으로 지급한다.
/// - 이후 글리치 → 단검 획득 알림 → 대사 → 루 둘러보기 → 대사 순서로 연출한다.
/// - 한 번 사용하면 InteractionTrigger 가 비활성화되어 재사용 불가.
///
/// [설정 방법]
/// 1. 이 컴포넌트를 다락방 상자 GameObject 에 추가
/// 2. InteractionTrigger.onInteract 에 OnBoxInteract() 연결
/// 3. ItemPickup 컴포넌트는 제거
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class AtticBoxInteraction : MonoBehaviour
{
    [Header("일반 아이템 (동시 획득)")]
    public ItemData firstItem;
    public ItemData secondItem;
    public ItemData thirdItem;   // 3개 획득

    [Header("단검 아이템 (글리치 연출 후 획득)")]
    public ItemData daggerItem;

    [Header("대사")]
    public DialogueData daggerIntroDialogue;    // 글리치 전
    public DialogueData daggerPreLookDialogue;  // 둘러보기 전
    public DialogueData daggerPostLookDialogue; // 둘러보기 후

    [Header("캐릭터 전환 후")]
    [Tooltip("암전 중 플레이어 스프라이트를 이것으로 교체합니다. 비워두면 교체하지 않습니다.")]
    public Sprite newCharacterSprite;
    [Tooltip("암전 후 재생할 대사. 비워두면 건너뜁니다.")]
    public DialogueData afterChangeDialogue;

    private bool _used = false;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnBoxInteract()
    {
        if (_used) return;
        _used = true;

        GetComponent<InteractionTrigger>().enabled = false;

        StartCoroutine(BoxRoutine());
    }

    IEnumerator BoxRoutine()
    {
        var ctrl = LockPlayer();

        // ── 1. 일반 아이템 2개 동시 획득 ──────────────────────────────
        var normalItems = new List<ItemData>();
        if (firstItem  != null) normalItems.Add(firstItem);
        if (secondItem != null) normalItems.Add(secondItem);
        if (thirdItem != null) normalItems.Add(thirdItem);

        if (normalItems.Count > 0)
            InventoryManager.Instance?.AddItems(normalItems);

        float notifWait = ItemAcquisitionUI.Instance != null
            ? ItemAcquisitionUI.Instance.displayDuration : 2f;
        yield return new WaitForSeconds(notifWait);

        // ── 2. 글리치 전 대사 (Space / 마우스 클릭으로 진행) ───────────
        yield return StartCoroutine(DialogueRunner.PlayAndWait(daggerIntroDialogue));

        // ── 3. 글리치 → 단검 획득 ─────────────────────────────────────
        GlitchManager.Instance?.PlayGlitch(1.5f, GlitchManager.PresetStrong);
        yield return new WaitForSeconds(0.5f); // 글리치 도중 단검 알림 등장

        if (daggerItem != null)
        {
            InventoryManager.Instance?.AddItem(daggerItem);
            DaggerSystem.Instance?.Equip();
            Debug.Log("[AtticBoxInteraction] 단검 획득 완료");
        }

        float daggerNotifWait = ItemAcquisitionUI.Instance != null
            ? ItemAcquisitionUI.Instance.displayDuration : 2f;
        yield return new WaitForSeconds(daggerNotifWait);

        // ── 4. 단검 전 대사 (Space / 마우스 클릭으로 진행) ─────────────
        yield return StartCoroutine(DialogueRunner.PlayAndWait(daggerPreLookDialogue));

        // ── 5. 루 둘러보기 모션 ────────────────────────────────────────
        if (ctrl != null)
            yield return StartCoroutine(LookAroundRoutine(ctrl.transform));

        // ── 6. 단검 후 대사 (Space / 마우스 클릭으로 진행) ─────────────
        yield return StartCoroutine(DialogueRunner.PlayAndWait(daggerPostLookDialogue));

        // ── 7. 암전 → 캐릭터 전환 → 암전 해제 → 전환 후 대사 ───────────
        yield return StartCoroutine(TransitionManager.Instance.FadeToBlack());
        OnCharacterChange();
        yield return StartCoroutine(TransitionManager.Instance.FadeFromBlack());

        if (afterChangeDialogue != null)
            yield return StartCoroutine(DialogueRunner.PlayAndWait(afterChangeDialogue));

        // ── 8. 목표 갱신 ──────────────────────────────────────────────
        ObjectiveManager.Instance?.ShowObjective("현재 목표", "아빠를 찾으러 가세요.");

        UnlockPlayer(ctrl);
    }

    /// <summary>암전 중 플레이어 스프라이트를 newCharacterSprite 로 교체합니다.</summary>
    void OnCharacterChange()
    {
        if (newCharacterSprite == null) return;

        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return;

        var sr = ctrl.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sprite = newCharacterSprite;
    }

    /// <summary>루가 좌→우→원위치 순서로 잠깐 둘러보는 연출.</summary>
    IEnumerator LookAroundRoutine(Transform playerTransform)
    {
        float absX  = Mathf.Abs(playerTransform.localScale.x);
        float origX = playerTransform.localScale.x;
        float y     = playerTransform.localScale.y;
        float z     = playerTransform.localScale.z;

        playerTransform.localScale = new Vector3(-absX, y, z); // 왼쪽
        yield return new WaitForSeconds(0.4f);

        playerTransform.localScale = new Vector3(absX, y, z);  // 오른쪽
        yield return new WaitForSeconds(0.4f);

        playerTransform.localScale = new Vector3(origX, y, z); // 원래 방향
        yield return new WaitForSeconds(0.2f);
    }

    static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return null;
        ctrl.Lock();
        return ctrl;
    }

    static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.Unlock();
    }
}
