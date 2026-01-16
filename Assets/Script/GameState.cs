using UnityEngine;

public static class GameState
{
    public static bool isZombieDefeated = false;
    public static Vector3 lastPosition = Vector3.zero;
    public static bool hasPositionSaved = false;

    // ★ 추가된 부분: 전투 후 돌아올 정보들 ★
    public static string returnSceneName = "MapScene"; // 돌아갈 씬 이름
    public static float savedGaugeValue = 0f;          // 저장된 게이지 값
    public static bool isComingFromBattle = false;     // 전투하고 돌아오는 길인가?
}