#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GaugeTriggerAssetGenerator
{
    [MenuItem("무채색낙원/게이지 트리거 에셋 생성")]
    static void GenerateAll()
    {
        string folder = "Assets/Resources/GaugeTriggers";
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var triggers = new (string id, float amount, bool isReality)[]
        {
            ("무서운것_목격",      15f,  true),
            ("신체_고통",          10f,  true),
            ("쿠루_직접_대화",     10f,  true),
            ("아버지_유품_접촉",   10f,  true),
            ("루_감정_폭발",       10f,  true),
            ("환상_평화주의_성공",  5f,  true),
            ("세라_목소리_들림",  -25f, false),
            ("무서운것_회피",     -15f, false),
            ("NPC_눈_마주침",     -10f, false),
            ("쿠루_부재",         -10f, false),
            ("마시멜로_냄새",      -5f, false),
            ("세라_흔적_발견",     -5f, false),
        };

        int created = 0;
        foreach (var (id, amount, isReality) in triggers)
        {
            string path = $"{folder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<GaugeTriggerDefinition>(path) != null)
            {
                Debug.Log($"[GaugeTriggerAssetGenerator] 이미 존재 (건너뜀): {id}");
                continue;
            }
            var def = ScriptableObject.CreateInstance<GaugeTriggerDefinition>();
            def.triggerId         = id;
            def.amount            = amount;
            def.isRealityDirection = isReality;
            AssetDatabase.CreateAsset(def, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[GaugeTriggerAssetGenerator] 완료 — {created}개 에셋 생성 (Resources/GaugeTriggers/)");
    }
}
#endif
