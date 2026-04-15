using UnityEngine;

/// <summary>
/// 도메인 리로드 후 SimplePlayerController가 비활성화된 채 남아있는 것을 방지합니다.
/// Player 오브젝트에 이 컴포넌트를 추가하세요.
/// </summary>
public class PlayerBootstrap : MonoBehaviour
{
    void Awake()
    {
        // 이 컴포넌트 자체는 항상 enabled → 도메인 리로드 후에도 Awake가 실행됨
        // 대화 중이 아니라면 SimplePlayerController를 강제 활성화
        var ctrl = GetComponent<ClearSky.SimplePlayerController>();
        if (ctrl == null) return;

        bool talking = DialogueManager.Instance != null && DialogueManager.Instance.isTalking;
        if (!talking) ctrl.Unlock();
    }
}
