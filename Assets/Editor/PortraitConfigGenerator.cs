#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PortraitConfigGenerator
{
    [MenuItem("무채색낙원/포트레이트 설정 에셋 생성")]
    static void GenerateAll()
    {
        string folder = "Assets/Resources/PortraitConfigs";
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var configs = new (string id, PortraitSide side)[]
        {
            ("루",     PortraitSide.Right),
            ("세라",   PortraitSide.Left),
            ("쿠루",   PortraitSide.Left),
            ("부엉이", PortraitSide.Left),
            ("솔",     PortraitSide.Left),
            // "상인" 은 폐기된 구 명칭이다 (CLAUDE.md §5). 다시 넣지 말 것 — 넣으면 상인.asset 이 되살아난다.
            ("미루",   PortraitSide.Left),
            ("아모",   PortraitSide.Left),
        };

        int created = 0;
        foreach (var (id, side) in configs)
        {
            string path = $"{folder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<CharacterPortraitConfig>(path) != null)
            {
                Debug.Log($"[PortraitConfigGenerator] 이미 존재 (건너뜀): {id}");
                continue;
            }
            var cfg = ScriptableObject.CreateInstance<CharacterPortraitConfig>();
            cfg.characterId   = id;
            cfg.defaultSide   = side;
            cfg.entryMotion   = EntryMotion.SlideIn;
            cfg.exitMotion    = ExitMotion.SlideOut;
            cfg.entryDuration = 0.3f;
            cfg.exitDuration  = 0.3f;
            AssetDatabase.CreateAsset(cfg, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PortraitConfigGenerator] 완료 — {created}개 에셋 생성 (Resources/PortraitConfigs/)");
    }
}
#endif
