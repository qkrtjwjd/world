using UnityEngine;

/// <summary>
/// 마당의 각설탕 배치물 — 거실 창 너머와 마당 맵(S#13)이 같은 녹음 플래그로 바꿔 낀다(F-6 · D-4-1).
/// 멀쩡한 상태 · 녹은 상태 두 오브젝트를 넣는다. S#04F 이전에는 둘 다 끈다(각설탕이 아직 없다).
/// 그림은 아직 없다(🔴) — 자리 표시 스프라이트를 쓴다.
/// </summary>
public class YardSugarState : MonoBehaviour
{
    public GameObject intact;
    public GameObject melted;

    void OnEnable()
    {
        HouseClock.OnSugarMelted += Apply;
        Apply();
    }

    void OnDisable() => HouseClock.OnSugarMelted -= Apply;

    void Update() => Apply();   // 되감기 · 불러오기로 플래그가 바뀌는 경우까지 — 값만 비교하므로 가볍다

    void Apply()
    {
        bool seen = GameState.isYardSugarSeen;
        bool m = HouseClock.SugarMelted;
        if (intact != null && intact.activeSelf != (seen && !m)) intact.SetActive(seen && !m);
        if (melted != null && melted.activeSelf != (seen && m))  melted.SetActive(seen && m);
    }
}
