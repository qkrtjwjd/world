#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// 전투 모드 전환 테스트용 디버그 도구.
/// 게임 오브젝트에 붙이거나 씬에 배치하면 됩니다.
///
/// 키 바인딩 (기본값):
///   F8  — 턴제 → 핵앤슬래시 강제 전환
///   F9  — 핵앤슬래시 → 턴제 강제 전환
///   F10 — 모드 전환 잠금 해제 (다시 전환 가능하게)
/// (F5는 빠른 저장 기본 키라 디버그 키에서 제외)
///
/// 에디터 또는 Development Build에서만 화면 표시됩니다.
/// </summary>
public class BattleModeSwitchDebug : MonoBehaviour
{
    [Header("키 바인딩")]
    public KeyCode switchToHackSlashKey = KeyCode.F8;
    public KeyCode switchToTurnBasedKey = KeyCode.F9;
    public KeyCode unlockSwitchKey      = KeyCode.F10;

    [Header("화면 표시")]
    [Tooltip("좌측 상단 디버그 오버레이를 그릴지 여부. 동료 패널 초상화 자리를 덮기 때문에 기본은 꺼 둡니다. 키(F8·F9·F10)는 꺼져 있어도 동작합니다.")]
    public bool showOverlay = false;

    [Header("디버그 테스트 적")]
    [Tooltip("EncounterManager에 적 정보가 없을 때 사용할 폴백 프리팹.\n" +
             "할당하면 실제 인카운터 없이도 F8로 핵앤슬래시를 바로 시작할 수 있습니다.")]
    public GameObject debugEnemyPrefab;

    void Update()
    {
        if (Input.GetKeyDown(switchToHackSlashKey))
            DebugSwitchToHackSlash();

        if (Input.GetKeyDown(switchToTurnBasedKey))
            DebugSwitchToTurnBased();

        if (Input.GetKeyDown(unlockSwitchKey))
            DebugUnlockSwitch();
    }

    void DebugSwitchToHackSlash()
    {
        // EncounterManager에 적 정보가 없으면 디버그 프리팹으로 보완
        if (debugEnemyPrefab != null && EncounterManager.Instance != null)
        {
            if (EncounterManager.Instance.CurrentEnemyObject == null &&
                EncounterManager.Instance.enemyPrefabToSpawn == null)
            {
                EncounterManager.Instance.enemyPrefabToSpawn = debugEnemyPrefab;
                Dbg.Log("[DEBUG] EncounterManager에 디버그 적 프리팹을 설정했습니다.");
            }
        }

        if (BattleSystem.IsActive && BattleSystem.Instance != null)
        {
            // 턴제 → 핵앤슬래시 전환 (글리치 연출 포함)
            BattleModeController.GetOrCreate().ResetBattleSession();
            Dbg.Log("[DEBUG] 턴제 → 핵앤슬래시 강제 전환 실행");
            BattleSystem.Instance.ForceSwitchToHackSlash();
        }
        else if (debugEnemyPrefab != null && HackSlashCombatManager.Instance != null)
        {
            // 턴제 없이 직접 핵앤슬래시 시작 (글리치 연출 생략)
            if (HackSlashCombatManager.IsActive)
            {
                Debug.LogWarning("[DEBUG] 이미 핵앤슬래시 전투 중입니다.");
                return;
            }
            BattleModeController.GetOrCreate().ResetBattleSession();
            Dbg.Log("[DEBUG] 핵앤슬래시 직접 시작 (디버그 프리팹 사용)");
            HackSlashCombatManager.Instance.BeginCombat(null, debugEnemyPrefab);
        }
        else
        {
            Debug.LogWarning("[DEBUG] 턴제 전투(BattleSystem)가 활성화되지 않았습니다.\n" +
                             "Inspector에 debugEnemyPrefab을 연결하면 직접 핵앤슬래시를 시작할 수 있습니다.");
        }
    }

    void DebugSwitchToTurnBased()
    {
        if (!HackSlashCombatManager.IsActive || HackSlashCombatManager.Instance == null)
        {
            Debug.LogWarning("[DEBUG] 핵앤슬래시 전투(HackSlashCombatManager)가 활성화되지 않았습니다.");
            return;
        }
        // 잠금 해제 후 강제 전환
        BattleModeController.GetOrCreate().ResetBattleSession();
        Dbg.Log("[DEBUG] 핵앤슬래시 → 턴제 강제 전환 실행");
        HackSlashCombatManager.Instance.ForceSwitchToTurnBased();
    }

    void DebugUnlockSwitch()
    {
        BattleModeController.GetOrCreate().ResetBattleSession();
        Dbg.Log("[DEBUG] 모드 전환 잠금 해제 완료");
    }

    void OnGUI()
    {
        if (!showOverlay) return;
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        var style    = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        var boxStyle = new GUIStyle(GUI.skin.box)   { fontSize = 13 };

        GUILayout.BeginArea(new Rect(10, 10, 280, 175), boxStyle);
        try
        {
            GUILayout.Label("=== 전투 모드 디버그 ===", style);

            var ctrl = BattleModeController.Instance;
            GUILayout.Label($"턴제(BattleSystem): {BattleSystem.IsActive}", style);
            GUILayout.Label($"핵앤슬래시: {HackSlashCombatManager.IsActive}", style);
            GUILayout.Label($"전환 잠금: {(ctrl != null ? ctrl.HasSwitchedMode.ToString() : "N/A")}", style);
            GUILayout.Space(4);
            string prefabLabel = debugEnemyPrefab != null ? debugEnemyPrefab.name : "없음 (Inspector 연결 필요)";
            GUILayout.Label($"디버그 적: {prefabLabel}", style);
            GUILayout.Space(4);
            GUILayout.Label($"[{switchToHackSlashKey}] 턴제 → 핵앤슬래시", style);
            GUILayout.Label($"[{switchToTurnBasedKey}] 핵앤슬래시 → 턴제", style);
            GUILayout.Label($"[{unlockSwitchKey}] 잠금 해제", style);
        }
        finally
        {
            GUILayout.EndArea();
        }
    }
}
#endif
