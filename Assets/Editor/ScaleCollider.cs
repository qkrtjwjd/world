using UnityEditor;
using UnityEngine;

public class ScaleCollider
{
    [MenuItem("Tools/콜라이더 Scale 1:1 맞춤")]
    static void FitToScale()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("[ScaleCollider] 선택된 오브젝트가 없습니다.");
            return;
        }

        int count = 0;
        foreach (GameObject obj in selected)
        {
            BoxCollider2D bc = obj.GetComponent<BoxCollider2D>();
            if (bc == null)
            {
                Debug.LogWarning($"[ScaleCollider] BoxCollider2D 없음, 건너뜀: {obj.name}");
                continue;
            }

            Undo.RecordObject(bc, "Fit Collider To Scale");
            bc.size   = Vector2.one;   // 로컬 1×1 → 월드에서 Scale과 1:1
            bc.offset = Vector2.zero;  // Transform 피벗 기준 정중앙
            Debug.Log($"[ScaleCollider] 적용 완료: {obj.name}  scale={obj.transform.localScale}");
            count++;
        }

        Debug.Log($"[ScaleCollider] 완료 — {count}개 오브젝트 처리.");
    }
}
