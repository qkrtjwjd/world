using UnityEngine;

/// <summary>
/// GameState.battleReturn 쿨타임을 매 프레임 감소시킵니다.
/// GameManager 처럼 DontDestroyOnLoad 가 설정된 오브젝트에 붙여주세요.
/// </summary>
public class BattleCooldownUpdater : MonoBehaviour
{
    void Update()
    {
        GameState.battleReturn.Tick(Time.deltaTime);
    }
}
