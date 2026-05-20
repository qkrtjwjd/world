using UnityEditor;
using UnityEngine;

public class AddColliders
{
    /// <summary>
    /// 씬 내 InteractionTrigger가 붙은 오브젝트 중
    /// isTrigger=true 인 Collider2D가 없는 것에 BoxCollider2D(isTrigger=true)를 자동 추가합니다.
    /// 크기는 interactionRange * 2 로 설정합니다.
    /// </summary>
    [MenuItem("Tools/InteractionTrigger — 트리거 콜라이더 자동 추가")]
    static void AddTriggerCollidersToInteractionTriggers()
    {
        var triggers = Object.FindObjectsByType<InteractionTrigger>(FindObjectsInactive.Include);

        if (triggers.Length == 0)
        {
            Debug.LogWarning("[AddColliders] 씬에 InteractionTrigger 컴포넌트가 없습니다.");
            return;
        }

        int added = 0;
        foreach (var trigger in triggers)
        {
            // isTrigger=true 인 콜라이더가 이미 있으면 건너뜀
            bool hasTriggerCollider = false;
            foreach (var col in trigger.GetComponents<Collider2D>())
            {
                if (col.isTrigger) { hasTriggerCollider = true; break; }
            }
            if (hasTriggerCollider) continue;

            var box = Undo.AddComponent<BoxCollider2D>(trigger.gameObject);
            box.isTrigger = true;
            box.size      = new Vector2(1f, 1f);

            EditorUtility.SetDirty(trigger.gameObject);
            Debug.Log($"[AddColliders] BoxCollider2D(trigger, size=1x1) 추가됨: {trigger.gameObject.name}");
            added++;
        }

        if (added > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[AddColliders] 완료 — {added}/{triggers.Length}개에 트리거 콜라이더 추가.");
    }

    [MenuItem("Tools/선택 오브젝트 콜라이더 추가")]
    static void AddBoxColliders()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("[AddColliders] 선택된 오브젝트가 없습니다.");
            return;
        }

        int addedColliders   = 0;
        int addedWorldObject = 0;

        foreach (GameObject obj in selected)
        {
            if (obj.GetComponent<Collider2D>() == null)
            {
                Undo.AddComponent<BoxCollider2D>(obj);
                Debug.Log($"[AddColliders] BoxCollider2D 추가됨: {obj.name}");
                addedColliders++;
            }
            else
            {
                Debug.Log($"[AddColliders] 콜라이더 건너뜀 (이미 있음): {obj.name}");
            }

            // SpriteRenderer / UI Image / UI RawImage 중 하나라도 있으면 WorldObject 추가
            if ((obj.GetComponent<SpriteRenderer>()         != null ||
                 obj.GetComponent<UnityEngine.UI.Image>()    != null ||
                 obj.GetComponent<UnityEngine.UI.RawImage>() != null) &&
                obj.GetComponent<WorldObject>() == null)
            {
                Undo.AddComponent<WorldObject>(obj);
                Debug.Log($"[AddColliders] WorldObject 추가됨: {obj.name}");
                addedWorldObject++;
            }
        }

        Debug.Log($"[AddColliders] 완료 — BoxCollider2D {addedColliders}개 / WorldObject {addedWorldObject}개 추가.");
    }
}
