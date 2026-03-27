using UnityEditor;
using UnityEngine;

public class AddColliders
{
    [MenuItem("Tools/선택 오브젝트 콜라이더 추가")]
    static void AddBoxColliders()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("[AddColliders] 선택된 오브젝트가 없습니다.");
            return;
        }

        int addedCount = 0;

        foreach (GameObject obj in selected)
        {
            // 이미 Collider2D(BoxCollider2D 포함 모든 종류)가 있으면 건너뜀
            if (obj.GetComponent<Collider2D>() != null)
            {
                Debug.Log($"[AddColliders] 건너뜀 (이미 콜라이더 있음): {obj.name}");
                continue;
            }

            Undo.AddComponent<BoxCollider2D>(obj);
            Debug.Log($"[AddColliders] BoxCollider2D 추가됨: {obj.name}");
            addedCount++;
        }

        Debug.Log($"[AddColliders] 완료 — {addedCount}개 오브젝트에 BoxCollider2D 추가.");
    }
}
