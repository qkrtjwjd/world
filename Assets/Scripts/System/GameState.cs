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
    public static System.Collections.Generic.List<string> defeatedEnemyIDs = new System.Collections.Generic.List<string>(); // 처치된 적 ID 목록

    // ★ 인벤토리 상태 저장 (씬 이동 간 유지) ★
    public static System.Collections.Generic.List<ItemData> inventoryItems = null;

    // ★ 플레이어 스탯 상태 저장 (씬 이동 간 유지) ★
    // -1로 초기화해서 "아직 데이터 없음"을 표시
    public static float playerHealth = -1f;
    public static float playerMental = -1f;
    public static float playerHunger = -1f;
    // playerTrauma 제거됨 (Mental로 통합)
    public static float playerPuppetization = -1f; // 인형화 (환상 전투/음식 섭취 시 상승)

    // ★ 멘탈 붕괴 상태 타이머 (0보다 크면 강제로 환상세계 고정)
    public static float mentalBreakdownTimer = 0f;

    // --- [씬 짝꿍 시스템] ---
    // 현실(Dark) <-> 환상(Map) 매핑 헬퍼 함수

    public static string GetFantasyScene(string currentSceneName)
    {
        // 현실(DarkWorld)에 있다면 -> 환상(MapScene) 반환
        if (currentSceneName == "DarkWorld") return "MapScene";
        
        // 여기에 다른 씬들 추가 가능 (예: DarkHome -> Home)
        // if (currentSceneName == "DarkHome") return "Home";

        return "MapScene"; // 기본값
    }

    public static bool IsRealityScene(string currentSceneName)
    {
        // 이름에 "Dark"가 들어가거나 특정 이름이면 현실로 취급
        return currentSceneName == "DarkWorld" || currentSceneName.Contains("Dark");
    }
}