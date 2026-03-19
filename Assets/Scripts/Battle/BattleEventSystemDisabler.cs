using UnityEngine;

/// <summary>
/// BattleScene의 EventSystem에 붙여두는 컴포넌트.
/// Additive 로드 시 MapScene의 EventSystem과 충돌하지 않도록
/// Awake에서 즉시 자신의 GameObject를 비활성화합니다.
///
/// ★ 사용법: BattleScene의 EventSystem GameObject에 이 스크립트를 추가하세요.
/// </summary>
[DefaultExecutionOrder(-32001)]   // BattleSystem(-32000)보다도 먼저 실행
public class BattleEventSystemDisabler : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(false);
    }
}
