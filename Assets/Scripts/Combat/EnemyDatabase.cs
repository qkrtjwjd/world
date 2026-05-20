using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 ID와 전투 프리팹을 한 곳에서 관리하는 데이터베이스.
///
/// [사용법]
/// 1. Project 창 우클릭 → Create → Battle → Enemy Database 로 에셋 생성
/// 2. Enemies 리스트에 (Enemy ID, 프리팹) 쌍 추가
/// 3. EncounterManager의 Enemy Database 슬롯에 연결
/// 4. 씬의 EnemySymbol 오브젝트 Enemy ID를 이 리스트의 ID와 동일하게 입력
/// </summary>
[CreateAssetMenu(fileName = "EnemyDatabase", menuName = "Battle/Enemy Database")]
public class EnemyDatabase : ScriptableObject
{
    [System.Serializable]
    public class EnemyEntry
    {
        [Tooltip("EnemySymbol의 Enemy ID 와 일치해야 합니다.")]
        public string enemyID;
        [Tooltip("턴제 전투에서 소환할 적 프리팹. Unit 컴포넌트 필수.")]
        public GameObject battlePrefab;
        [Tooltip("이 적이 드랍할 수 있는 아이템 목록. 없으면 드랍 없음.")]
        public LootTable lootTable;
    }

    [Tooltip("등록된 적 목록. Enemy ID는 중복 없이 입력하세요.")]
    public List<EnemyEntry> enemies = new List<EnemyEntry>();

    /// <summary>ID로 전투 프리팹을 반환합니다. 없으면 null.</summary>
    public GameObject GetPrefab(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var entry in enemies)
            if (entry != null && entry.enemyID == id)
                return entry.battlePrefab;
        return null;
    }

    /// <summary>ID로 LootTable을 반환합니다. 없으면 null.</summary>
    public LootTable GetLootTable(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var entry in enemies)
            if (entry != null && entry.enemyID == id)
                return entry.lootTable;
        return null;
    }
}
