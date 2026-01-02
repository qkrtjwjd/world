using UnityEngine; // Vector3를 쓰려면 이거 필요!

public static class GameState
{
    public static bool isZombieDefeated = false; 

    // ★ 추가된 부분: 주인공 위치 저장용 변수
    public static Vector3 lastPosition = Vector3.zero; 
    public static bool hasPositionSaved = false; // 위치 저장된 적 있는지 확인
}