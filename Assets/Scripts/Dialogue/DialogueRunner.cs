using System.Collections;
using UnityEngine;

/// <summary>
/// 대사 재생 + 플레이어 이동 제어를 위한 공통 유틸리티.
/// 여러 스크립트에서 반복되던 PlayDialogue 코루틴과 LockPlayer 패턴을 하나로 통합합니다.
/// </summary>
public static class DialogueRunner
{
    /// <summary>
    /// 대사를 재생하고 Space/마우스 클릭으로 진행을 기다리는 코루틴.
    /// lockPlayer=true 이면 대사 시작 전 이동을 잠그고 종료 후 해제합니다.
    /// </summary>
    public static IEnumerator PlayAndWait(DialogueData data, bool lockPlayer = false)
    {
        if (data == null) yield break;

        var dm = DialogueManager.Instance;
        if (dm == null)
        {
            Debug.LogWarning("[DialogueRunner] DialogueManager 인스턴스를 찾을 수 없습니다.");
            yield break;
        }
        if (dm.isTalking)
        {
            Debug.LogWarning("[DialogueRunner] 이미 대사가 진행 중입니다 — 새 대사를 무시합니다.");
            yield break;
        }

        ClearSky.SimplePlayerController ctrl = lockPlayer ? LockPlayer() : null;

        dm.StartDialogue(data);
        yield return null; // isTalking 활성화 보장 (1프레임 대기)

        while (dm != null && dm.isTalking)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                dm.DisplayNextSentence();
            yield return null;
        }

        if (lockPlayer && ctrl != null)
            UnlockPlayer(ctrl);
    }

    /// <summary>플레이어 이동을 잠그고 컨트롤러를 반환합니다. UnlockPlayer() 에 전달하세요.</summary>
    public static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = PlayerStats.Instance != null
            ? PlayerStats.Instance.GetComponent<ClearSky.SimplePlayerController>()
            : Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();

        if (ctrl == null)
        {
            Debug.LogWarning("[DialogueRunner] SimplePlayerController를 찾을 수 없습니다.");
            return null;
        }

        ctrl.Lock();
        return ctrl;
    }

    /// <summary>LockPlayer() 가 반환한 컨트롤러의 이동을 다시 활성화합니다.</summary>
    public static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.Unlock();
    }
}
