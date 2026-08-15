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
        if (ctrl != null && !YarnDialogue.IsRunning) ctrl.Unlock();

        // 원본 캐릭터 에셋의 모든 신체 부위 SpriteRenderer를 최상단 레이어로 설정
        // (RoomMask, roomCover 등 모든 월드 오버레이보다 항상 위에 렌더링)
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.sortingLayerName = "Player";
    }
}
