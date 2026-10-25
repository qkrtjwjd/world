using System.Collections;
using UnityEngine;

/// <summary>
/// 집_마당_울타리문 (house_yard_fence_gate) — S#13 진행 오브젝트(D-S#13 · E-73-2).
/// 잠겨 있지 않다. 조사하면 열리는 소리와 함께 울타리문 스프라이트가 사라져 열림을 표시하고,
/// 이어서 루가 「안 잠겨있네. 다행이다.」라고 말한다. 1회만 반응한다. 지나가면 마을로 넘어간다(FrontGateTrigger).
/// 접촉 반응을 걸지 않는다(C-14-2-3). 제한 시간도 이미 끝나 있다.
///
/// [설정 방법]
/// 1. 이 컴포넌트를 울타리 문 GameObject 에 추가
/// 2. InteractionTrigger.onInteract 에 OnGateInteract() 를 연결
/// 3. objectsToDisable 에 사라질 오브젝트들 (문 스프라이트, 콜라이더 등) 연결
/// 4. (선택) objectsToEnable 에 문이 열린 후 활성화할 오브젝트 연결
/// </summary>
public class FenceGateInteraction : MonoBehaviour
{
    [Header("열릴 때 비활성화할 오브젝트 (문 스프라이트, 콜라이더 등)")]
    public GameObject[] objectsToDisable;

    [Header("열릴 때 활성화할 오브젝트 (선택)")]
    public GameObject[] objectsToEnable;

    [Header("열리는 소리 · 대사 (D-S#13)")]
    [Tooltip("울타리문이 열리는 소리 — 현관문 경첩 소리와 다른 소리. AudioManager 등록 이름. 비우면 무음.")]
    public string sfxOpenName = "fence_gate_open";
    [Tooltip("열린 뒤 루의 대사 노드(House_Attic.yarn).")]
    public string yarnNode = "House_FenceGate";

    private bool _opened = false;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnGateInteract()
    {
        if (_opened) return;
        _opened = true;

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;

        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        // 조사 → 열리는 소리 → 스프라이트가 사라져 열림 → 대사 → 통과 가능(마당 상호작용 정리 3-4).
        if (!string.IsNullOrEmpty(sfxOpenName))
            AudioManager.Instance?.Play(sfxOpenName);

        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);

        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);

        yield return YarnDialogue.PlayIfExists(yarnNode);
    }
}
