#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 전투 콘텐츠 데이터 에셋 일괄 생성기.
/// 스킬 4종, AI 액션/프로파일, 루트테이블 3종, 적 프리팹 3종(+미니보스),
/// EnemyDatabase 엔트리, BattleUI 기본 스킬 장착, MapScene EnemySymbol ID까지 한 번에 처리합니다.
/// 이미 존재하는 에셋은 건너뛰므로 재실행해도 안전합니다.
/// </summary>
public static class CombatContentGenerator
{
    const string SkillFolder  = "Assets/Date/Battle/Skills";
    const string AIFolder     = "Assets/Date/Battle/AI";
    const string BattleFolder = "Assets/Date/Battle";
    const string EnemyFolder  = "Assets/Prefabs/Enemies";

    [MenuItem("무채색낙원/전투 콘텐츠 에셋 생성")]
    public static void GenerateAll()
    {
        EnsureFolders();

        // 1) 스킬
        var skillWatercolor = CreateSkill("Skill_수채화번짐", "수채화 번짐",
            "세계의 색이 번져 적을 삼킨다.", mp: 10, cd: 3, dmgMul: 2.2f);
        var skillWhisper = CreateSkill("Skill_달콤한속삭임", "달콤한 속삭임",
            "마시멜로 냄새로 교감한다. 공감 게이지 +25.", mp: 6, cd: 2, dmgMul: 0f, empathy: 25);
        var skillBarrier = CreateSkill("Skill_솜사탕방벽", "솜사탕 방벽",
            "폭신한 환상이 감싼다. 2턴간 방어력 +50%.", mp: 7, cd: 3, dmgMul: 0f,
            target: SkillTargetType.Self,
            buffs: new List<BuffInfo> { new BuffInfo { type = BuffType.DefenseUp, value = 50f, duration = 4f } });
        var skillBreath = CreateSkill("Skill_심호흡", "심호흡",
            "떨리는 손을 진정시킨다. 3턴간 매턴 HP 5 회복.", mp: 4, cd: 2, dmgMul: 0f,
            target: SkillTargetType.Self,
            buffs: new List<BuffInfo> { new BuffInfo { type = BuffType.HealOverTime, value = 5f, duration = 6f } });

        var skillStrike   = LoadRequired<SkillData>($"{SkillFolder}/Skill_강타.asset");
        var skillFirstAid = LoadRequired<SkillData>($"{SkillFolder}/Skill_응급처치.asset");

        // 2) AI 액션 + 프로파일 (weight가 액션 에셋에 있으므로 프로파일별로 분리 생성)
        var meleeBasic = CreateMelee("Melee_Basic", weight: 70, cooldown: 1.5f, range: 1.5f);
        var meleeFast  = CreateMelee("Melee_Fast",  weight: 85, cooldown: 0.8f, range: 1.5f);
        var meleeBoss  = CreateMelee("Melee_Boss",  weight: 55, cooldown: 1.2f, range: 1.6f);
        var idleBasic  = CreateIdle("Idle_Basic", weight: 30, duration: 0.8f);
        var idleFast   = CreateIdle("Idle_Fast",  weight: 15, duration: 0.5f);
        var idleBoss   = CreateIdle("Idle_Boss",  weight: 20, duration: 0.7f);
        var enrageBoss = CreateEnrage("Enrage_Boss", weight: 25, cooldown: 8f,
            attackMul: 1.5f, duration: 4f, hpThreshold: 0.5f);

        var profileBasic      = CreateProfile("Profile_Basic",      meleeBasic, idleBasic);
        var profileAggressive = CreateProfile("Profile_Aggressive", meleeFast,  idleFast);
        var profileBoss       = CreateProfile("Profile_Boss",       meleeBoss,  enrageBoss, idleBoss);

        // 3) 루트테이블 (기존 Resources/Items 아이템 재활용)
        var marshmallow = LoadItem("Marshmallow");
        var hotchoco    = LoadItem("hotchocolate");
        var espresso    = LoadItem("Espresso");
        var anemone     = LoadItem("Anemone");
        var mHotch      = LoadItem("M_hotch");

        var lootCommon = CreateLootTable("LootTable_Common",
            (marshmallow, 0.35f), (hotchoco, 0.20f));
        var lootHound = CreateLootTable("LootTable_Hound",
            (espresso, 0.30f), (marshmallow, 0.20f));
        var lootBoss = CreateLootTable("LootTable_Boss",
            (anemone, 1.0f), (mHotch, 0.5f));

        // 빈 껍데기 LootTable_ 도 Common 구성으로 채움
        FillEmptyLootTable("Assets/Date/Battle/LootTable_.asset",
            (marshmallow, 0.35f), (hotchoco, 0.20f));

        // 4) 적 프리팹 3종 + 미니보스 (Enemy.prefab 복제, 색 틴트로 임시 구분)
        var puppet = CreateEnemyPrefab("enemy_puppet", "실 끊긴 인형",
            hp: 60, atk: 8, def: 3, eva: 5, lv: 1, crit: 5,
            moveSpeed: 2.0f, actionAtk: 8f, actionCd: 1.8f,
            profile: profileBasic, tint: new Color(0.85f, 0.85f, 0.92f));
        var marsh = CreateEnemyPrefab("enemy_marshmallow", "우는 마시멜로",
            hp: 80, atk: 10, def: 5, eva: 10, lv: 2, crit: 5,
            moveSpeed: 2.5f, actionAtk: 10f, actionCd: 1.5f,
            profile: profileBasic, tint: new Color(1f, 0.85f, 0.9f));
        var hound = CreateEnemyPrefab("enemy_ink_hound", "먹빛 사냥개",
            hp: 70, atk: 14, def: 4, eva: 15, lv: 3, crit: 10,
            moveSpeed: 3.5f, actionAtk: 12f, actionCd: 1.0f,
            profile: profileAggressive, tint: new Color(0.4f, 0.4f, 0.5f));
        var keeper = CreateEnemyPrefab("enemy_doll_keeper", "인형 관리인",
            hp: 180, atk: 16, def: 8, eva: 5, lv: 4, crit: 10,
            moveSpeed: 2.2f, actionAtk: 16f, actionCd: 2.0f,
            profile: profileBoss, tint: new Color(0.8f, 0.45f, 0.45f));

        // 5) EnemyDatabase 엔트리 주입
        UpdateEnemyDatabase(new[]
        {
            // 기존 엔트리(enemyID "1")도 보상이 나오도록 채움
            new DbEntry { id = "1",                 prefab = null,   loot = lootCommon, xp = 8  },
            new DbEntry { id = "enemy_puppet",      prefab = puppet, loot = lootCommon, xp = 8  },
            new DbEntry { id = "enemy_marshmallow", prefab = marsh,  loot = lootCommon, xp = 12 },
            new DbEntry { id = "enemy_ink_hound",   prefab = hound,  loot = lootHound,  xp = 18 },
            new DbEntry { id = "enemy_doll_keeper", prefab = keeper, loot = lootBoss,   xp = 40 },
        });

        // 6) BattleUI 프리팹에 기본/해금 스킬 장착
        InjectBattleUISkills(
            defaults:     new[] { skillStrike, skillFirstAid, skillWhisper, skillBreath },
            levelUnlocks: new[] { (skillWatercolor, 2), (skillBarrier, 3) });

        // 7) MapScene EnemySymbol 빈 enemyID 채우기
        FixMapSceneEnemyIDs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CombatContentGenerator] 전투 콘텐츠 에셋 생성 완료");
    }

    // ─────────────────────────────────────────────
    //  스킬
    // ─────────────────────────────────────────────
    static SkillData CreateSkill(string fileName, string displayName, string desc,
        int mp, int cd, float dmgMul, int empathy = 0,
        SkillTargetType target = SkillTargetType.SingleEnemy, List<BuffInfo> buffs = null)
    {
        string path = $"{SkillFolder}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (existing != null) { Debug.Log($"[CombatContentGenerator] 이미 존재 (건너뜀): {path}"); return existing; }

        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.displayName      = displayName;
        skill.description      = desc;
        skill.mpCost           = mp;
        skill.cooldownTurns    = cd;
        skill.damageMultiplier = dmgMul;
        skill.empathyGain      = empathy;
        skill.targetType       = target;
        if (buffs != null) skill.buffs = buffs;
        AssetDatabase.CreateAsset(skill, path);
        return skill;
    }

    // ─────────────────────────────────────────────
    //  AI 액션 / 프로파일
    // ─────────────────────────────────────────────
    static MeleeAttackAction CreateMelee(string name, int weight, float cooldown, float range)
    {
        return CreateActionAsset<MeleeAttackAction>(name, a =>
        {
            a.actionName = name; a.weight = weight; a.cooldown = cooldown; a.maxRange = range;
        });
    }

    static IdleAction CreateIdle(string name, int weight, float duration)
    {
        return CreateActionAsset<IdleAction>(name, a =>
        {
            a.actionName = name; a.weight = weight; a.cooldown = 0.5f; a.idleDuration = duration;
        });
    }

    static BuffSelfAction CreateEnrage(string name, int weight, float cooldown,
        float attackMul, float duration, float hpThreshold)
    {
        return CreateActionAsset<BuffSelfAction>(name, a =>
        {
            a.actionName = name; a.weight = weight; a.cooldown = cooldown;
            a.attackMultiplier = attackMul; a.duration = duration; a.hpRatioThreshold = hpThreshold;
        });
    }

    static T CreateActionAsset<T>(string name, System.Action<T> configure) where T : EnemyAction
    {
        string path = $"{AIFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var action = ScriptableObject.CreateInstance<T>();
        configure(action);
        AssetDatabase.CreateAsset(action, path);
        return action;
    }

    static EnemyAIProfile CreateProfile(string name, params EnemyAction[] actions)
    {
        string path = $"{AIFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<EnemyAIProfile>(path);
        if (existing != null) return existing;

        var profile = ScriptableObject.CreateInstance<EnemyAIProfile>();
        profile.actions.AddRange(actions);
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    // ─────────────────────────────────────────────
    //  루트테이블
    // ─────────────────────────────────────────────
    static LootTable CreateLootTable(string name, params (ItemData item, float chance)[] drops)
    {
        string path = $"{BattleFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<LootTable>(path);
        if (existing != null) return existing;

        var table = ScriptableObject.CreateInstance<LootTable>();
        AddLootEntries(table, drops);
        AssetDatabase.CreateAsset(table, path);
        return table;
    }

    static void FillEmptyLootTable(string path, params (ItemData item, float chance)[] drops)
    {
        var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
        if (table == null) return;
        bool isEmpty = true;
        foreach (var e in table.entries)
            if (e != null && e.item != null) { isEmpty = false; break; }
        if (!isEmpty) return;

        table.entries.Clear();
        AddLootEntries(table, drops);
        EditorUtility.SetDirty(table);
        Debug.Log($"[CombatContentGenerator] 빈 루트테이블 채움: {path}");
    }

    static void AddLootEntries(LootTable table, (ItemData item, float chance)[] drops)
    {
        foreach (var (item, chance) in drops)
        {
            if (item == null) continue;
            table.entries.Add(new LootTable.LootEntry
            {
                item = item, dropChance = chance, minQuantity = 1, maxQuantity = 1,
            });
        }
    }

    static ItemData LoadItem(string name)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Resources/Items/{name}.asset");
        if (item == null)
            Debug.LogWarning($"[CombatContentGenerator] 아이템 없음: {name}");
        return item;
    }

    // ─────────────────────────────────────────────
    //  적 프리팹
    // ─────────────────────────────────────────────
    static GameObject CreateEnemyPrefab(string id, string displayName,
        int hp, int atk, int def, int eva, int lv, int crit,
        float moveSpeed, float actionAtk, float actionCd,
        EnemyAIProfile profile, Color tint)
    {
        string path = $"{EnemyFolder}/Enemy_{id}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) { Debug.Log($"[CombatContentGenerator] 이미 존재 (건너뜀): {path}"); return existing; }

        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy.prefab");
        try
        {
            var unit = root.GetComponentInChildren<Unit>(true);
            if (unit != null)
            {
                unit.unitName  = displayName;
                unit.unitLevel = lv;
                unit.maxHP     = hp;
                unit.currentHP = hp;
                unit.level     = lv;
                unit.attack    = atk;
                unit.defense   = def;
                unit.evasion   = eva;
                unit.critRate  = crit;
            }

            var health = root.GetComponentInChildren<EnemyHealth>(true);
            if (health != null)
            {
                health.maxHealth = hp;
                health.level     = lv;
                health.defense   = def;
                health.evasion   = eva;
            }

            var ai = root.GetComponentInChildren<EnemyAI>(true);
            if (ai != null)
            {
                ai.moveSpeed      = moveSpeed;
                ai.attackDamage   = actionAtk;
                ai.attackCooldown = actionCd;
                ai.aiProfile      = profile;
            }

            // 임시 구분용 색 틴트 (정식 스프라이트 교체 전까지)
            var sprite = root.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null) sprite.color = tint;

            root.name = $"Enemy_{id}";
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ─────────────────────────────────────────────
    //  EnemyDatabase
    // ─────────────────────────────────────────────
    class DbEntry { public string id; public GameObject prefab; public LootTable loot; public int xp; }

    static void UpdateEnemyDatabase(DbEntry[] entries)
    {
        var db = LoadRequired<EnemyDatabase>("Assets/Date/Battle/EnemyDatabase.asset");
        if (db == null) return;

        foreach (var e in entries)
        {
            var existing = db.enemies.Find(x => x != null && x.enemyID == e.id);
            if (existing == null)
            {
                existing = new EnemyDatabase.EnemyEntry { enemyID = e.id };
                db.enemies.Add(existing);
            }
            if (e.prefab != null)        existing.battlePrefab = e.prefab;
            if (existing.lootTable == null) existing.lootTable = e.loot;
            if (existing.xpReward <= 0)  existing.xpReward     = e.xp;
        }
        EditorUtility.SetDirty(db);
        Debug.Log($"[CombatContentGenerator] EnemyDatabase 갱신 — 총 {db.enemies.Count}개 엔트리");
    }

    // ─────────────────────────────────────────────
    //  BattleUI 스킬 장착
    // ─────────────────────────────────────────────
    static void InjectBattleUISkills(SkillData[] defaults, (SkillData skill, int level)[] levelUnlocks)
    {
        const string path = "Assets/Prefabs/BattleUI.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var battle = root.GetComponentInChildren<BattleSystem>(true);
            if (battle == null)
            {
                Debug.LogError("[CombatContentGenerator] BattleUI.prefab에서 BattleSystem을 찾지 못했습니다.");
                return;
            }

            battle.playerDefaultSkills.Clear();
            foreach (var s in defaults)
                if (s != null) battle.playerDefaultSkills.Add(s);

            battle.levelUnlockSkills.Clear();
            foreach (var (skill, level) in levelUnlocks)
                if (skill != null)
                    battle.levelUnlockSkills.Add(
                        new BattleSystem.LevelUnlockSkill { skill = skill, unlockLevel = level });

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[CombatContentGenerator] BattleUI 스킬 장착 — 기본 {battle.playerDefaultSkills.Count}종 + 해금 {battle.levelUnlockSkills.Count}종");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ─────────────────────────────────────────────
    //  MapScene EnemySymbol ID
    // ─────────────────────────────────────────────
    static void FixMapSceneEnemyIDs()
    {
        const string scenePath = "Assets/Scenes/MapScene.unity";
        string[] ids = { "enemy_puppet", "enemy_marshmallow", "enemy_ink_hound", "enemy_doll_keeper" };

        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        int assigned = 0;
        foreach (var symbol in Object.FindObjectsByType<EnemySymbol>(
                     FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
        {
            if (!string.IsNullOrEmpty(symbol.enemyID)) continue;
            symbol.enemyID = ids[assigned % ids.Length];
            EditorUtility.SetDirty(symbol);
            assigned++;
        }

        if (assigned > 0)
            EditorSceneManager.SaveScene(scene);
        if (setup != null && setup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(setup);

        Debug.Log($"[CombatContentGenerator] MapScene EnemySymbol ID 지정 — {assigned}건");
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    static void EnsureFolders()
    {
        foreach (string folder in new[] { SkillFolder, AIFolder, EnemyFolder })
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }

    static T LoadRequired<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"[CombatContentGenerator] 필수 에셋 없음: {path}");
        return asset;
    }
}
#endif
