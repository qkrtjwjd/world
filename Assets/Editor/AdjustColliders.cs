using UnityEditor;
using UnityEngine;

public class AdjustColliders
{
    private const float AlphaThreshold = 0.01f; // 안티앨리어싱 픽셀 제외

    [MenuItem("Tools/콜라이더 크기 자동 맞춤")]
    static void AdjustToSprite()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("[AdjustColliders] 선택된 오브젝트가 없습니다.");
            return;
        }

        int adjustedCount = 0;

        foreach (GameObject obj in selected)
        {
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            BoxCollider2D  bc = obj.GetComponent<BoxCollider2D>();

            if (sr == null)
            {
                Debug.LogWarning($"[AdjustColliders] SpriteRenderer 없음, 건너뜀: {obj.name}");
                continue;
            }
            if (bc == null)
            {
                Debug.LogWarning($"[AdjustColliders] BoxCollider2D 없음, 건너뜀: {obj.name}");
                continue;
            }
            if (sr.sprite == null)
            {
                Debug.LogWarning($"[AdjustColliders] Sprite가 없음, 건너뜀: {obj.name}");
                continue;
            }

            if (ApplyPixelPerfectCollider(obj, sr, bc))
                adjustedCount++;
        }

        Debug.Log($"[AdjustColliders] 완료 — {adjustedCount}개 오브젝트 콜라이더 조정.");
    }

    static bool ApplyPixelPerfectCollider(GameObject obj, SpriteRenderer sr, BoxCollider2D bc)
    {
        Sprite  sprite   = sr.sprite;
        Texture2D tex    = sprite.texture;
        string assetPath = AssetDatabase.GetAssetPath(tex);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        bool wasReadable = tex.isReadable;

        // 텍스처 읽기 가능하도록 임시 활성화 (importer가 없는 내장 에셋은 폴백)
        if (!wasReadable)
        {
            if (importer != null)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                // 내장 에셋 등 importer가 없는 경우 sprite.bounds로 폴백
                Debug.LogWarning($"[AdjustColliders] 읽기 불가능한 내장 텍스처, sprite.bounds로 대체: {obj.name}");
                Undo.RecordObject(bc, "Adjust Collider Size");
                bc.offset = sprite.bounds.center;
                bc.size   = sprite.bounds.size;
                Debug.Log($"[AdjustColliders] 조정 완료(폴백): {obj.name}  size={bc.size}  offset={bc.offset}");
                return true;
            }
        }

        bool success = false;
        try
        {
            // 스프라이트 rect (아틀라스 내 절대 픽셀 좌표)
            Rect    texRect = sprite.textureRect;
            Vector2 pivot   = sprite.pivot; // 스프라이트 rect 내 좌하단 기준 픽셀 좌표
            float   ppu     = sprite.pixelsPerUnit;

            int rx = (int)texRect.x,  ry = (int)texRect.y;
            int rw = (int)texRect.width, rh = (int)texRect.height;

            Color[] pixels;
            try
            {
                pixels = tex.GetPixels(rx, ry, rw, rh);
            }
            catch
            {
                // 내장 텍스처 등 GetPixels 불가 시 sprite.bounds로 폴백
                Debug.LogWarning($"[AdjustColliders] 픽셀 읽기 실패, sprite.bounds로 대체: {obj.name}");
                Undo.RecordObject(bc, "Adjust Collider Size");
                bc.offset = sprite.bounds.center;
                bc.size   = sprite.bounds.size;
                Debug.Log($"[AdjustColliders] 조정 완료(폴백): {obj.name}  size={bc.size}  offset={bc.offset}");
                success = true;
                return true;
            }

            // 불투명 픽셀의 AABB 계산
            int minX = rw, maxX = -1, minY = rh, maxY = -1;
            for (int y = 0; y < rh; y++)
            {
                for (int x = 0; x < rw; x++)
                {
                    if (pixels[y * rw + x].a > AlphaThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
            {
                Debug.LogWarning($"[AdjustColliders] 완전히 투명한 스프라이트, 건너뜀: {obj.name}");
                return false;
            }

            // 픽셀 좌표 → Unity 유닛, pivot 기준 offset
            float width   = (maxX - minX + 1) / ppu;
            float height  = (maxY - minY + 1) / ppu;
            float offsetX = ((minX + maxX + 1) * 0.5f - pivot.x) / ppu;
            float offsetY = ((minY + maxY + 1) * 0.5f - pivot.y) / ppu;

            Undo.RecordObject(bc, "Adjust Collider Size");
            bc.size   = new Vector2(width, height);
            bc.offset = new Vector2(offsetX, offsetY);

            Debug.Log($"[AdjustColliders] 조정 완료: {obj.name}  size={bc.size}  offset={bc.offset}");
            success = true;
        }
        finally
        {
            // isReadable 원래 상태로 복원
            if (!wasReadable && importer != null)
            {
                importer.isReadable = false;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        return success;
    }
}
