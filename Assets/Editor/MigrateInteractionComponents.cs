#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools → Migrate Interaction Components 를 실행하면
/// 모든 씬의 InteractionDialogueTrigger / PostDialogueItemSpawner 데이터를
/// InteractionTrigger 로 자동 이전하고 구 컴포넌트를 제거합니다.
/// 실행 전 씬을 저장해두세요.
/// </summary>
public static class MigrateInteractionComponents
{
    [MenuItem("Tools/Migrate Interaction Components")]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog(
            "컴포넌트 마이그레이션",
            "모든 씬의 InteractionDialogueTrigger / PostDialogueItemSpawner 를\n" +
            "InteractionTrigger 로 이전합니다.\n\n" +
            "진행 전 씬을 저장하세요.",
            "진행", "취소"))
            return;

        string currentScenePath = SceneManager.GetActiveScene().path;
        int totalMigrated = 0;

        string[] allSceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

        foreach (string guid in allSceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = MigrateScene(scene);

            if (count > 0)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Migration] {path}: {count}개 오브젝트 이전 완료");
                totalMigrated += count;
            }
        }

        if (!string.IsNullOrEmpty(currentScenePath))
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);

        EditorUtility.DisplayDialog(
            "마이그레이션 완료",
            $"총 {totalMigrated}개 오브젝트를 이전했습니다.\n\n" +
            "이제 다음 파일을 삭제해도 됩니다:\n" +
            "• Scripts/Dialogue/InteractionDialogueTrigger.cs\n" +
            "• Scripts/Dialogue/PostDialogueItemSpawner.cs",
            "확인");
    }

    private static int MigrateScene(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            count += MigrateGameObject(root);
        return count;
    }

    private static int MigrateGameObject(GameObject go)
    {
        int count = 0;

        // 타입 이름 문자열로 조회 — 해당 스크립트가 없어도 컴파일 오류 없음
        var dialogue = go.GetComponent("InteractionDialogueTrigger") as Component;
        if (dialogue != null)
        {
            var trigger = go.GetComponent<InteractionTrigger>();
            if (trigger == null)
            {
                Debug.LogWarning($"[Migration] {GetPath(go)}: InteractionTrigger 없음 — 건너뜀");
            }
            else
            {
                var srcSO = new SerializedObject(dialogue);
                var dstSO = new SerializedObject(trigger);

                CopyProperty(srcSO, dstSO, "dialogueData");
                CopyProperty(srcSO, dstSO, "repeatDialogue");
                CopyProperty(srcSO, dstSO, "lockPlayerDuringDialogue");
                CopyProperty(srcSO, dstSO, "playOnce");
                CopyProperty(srcSO, dstSO, "onDialogueComplete");

                dstSO.ApplyModifiedPropertiesWithoutUndo();

                // PostDialogueItemSpawner가 같은 오브젝트에 있으면 함께 이전
                var spawner = go.GetComponent("PostDialogueItemSpawner") as Component;
                if (spawner != null)
                {
                    var spSO = new SerializedObject(spawner);

                    var ppProp  = spSO.FindProperty("pickupPrefab");
                    var itProp  = spSO.FindProperty("itemToSpawn");
                    var spProp  = spSO.FindProperty("spawnPoint");
                    var soProp  = spSO.FindProperty("spawnOnce");
                    var qtyProp = spSO.FindProperty("quantity");

                    if (ppProp  != null) trigger.pickupPrefab  = ppProp.objectReferenceValue  as GameObject;
                    if (itProp  != null) trigger.itemToSpawn   = itProp.objectReferenceValue  as ItemData;
                    if (spProp  != null) trigger.spawnPoint    = spProp.objectReferenceValue  as Transform;
                    if (soProp  != null) trigger.spawnOnce     = soProp.boolValue;
                    if (qtyProp != null) trigger.spawnQuantity = qtyProp.intValue;

                    EditorUtility.SetDirty(trigger);
                    Object.DestroyImmediate(spawner, true);
                }

                Object.DestroyImmediate(dialogue, true);
                EditorUtility.SetDirty(go);
                count++;
            }
        }
        else
        {
            // InteractionDialogueTrigger 없이 PostDialogueItemSpawner만 있는 경우
            var spawner = go.GetComponent("PostDialogueItemSpawner") as Component;
            if (spawner != null)
            {
                var trigger = go.GetComponent<InteractionTrigger>();
                if (trigger != null)
                {
                    var spSO = new SerializedObject(spawner);

                    var ppProp  = spSO.FindProperty("pickupPrefab");
                    var itProp  = spSO.FindProperty("itemToSpawn");
                    var spProp  = spSO.FindProperty("spawnPoint");
                    var soProp  = spSO.FindProperty("spawnOnce");
                    var qtyProp = spSO.FindProperty("quantity");

                    if (ppProp  != null) trigger.pickupPrefab  = ppProp.objectReferenceValue  as GameObject;
                    if (itProp  != null) trigger.itemToSpawn   = itProp.objectReferenceValue  as ItemData;
                    if (spProp  != null) trigger.spawnPoint    = spProp.objectReferenceValue  as Transform;
                    if (soProp  != null) trigger.spawnOnce     = soProp.boolValue;
                    if (qtyProp != null) trigger.spawnQuantity = qtyProp.intValue;

                    EditorUtility.SetDirty(trigger);
                    Object.DestroyImmediate(spawner, true);
                    EditorUtility.SetDirty(go);
                    count++;
                }
            }
        }

        for (int i = 0; i < go.transform.childCount; i++)
            count += MigrateGameObject(go.transform.GetChild(i).gameObject);

        return count;
    }

    private static void CopyProperty(SerializedObject src, SerializedObject dst, string propertyName)
    {
        var srcProp = src.FindProperty(propertyName);
        var dstProp = dst.FindProperty(propertyName);
        if (srcProp == null || dstProp == null) return;
        dst.CopyFromSerializedPropertyIfDifferent(srcProp);
    }

    private static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }
}
#endif
