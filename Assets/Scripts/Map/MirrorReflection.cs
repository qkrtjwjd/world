using UnityEngine;
using UnityEngine.UI;

public class MirrorReflection : MonoBehaviour
{
    [Header("내 모습을 보여줄 이미지")]
    public Image reflectionImage; 

    [Header("상태별 이미지 설정")]
    public Sprite pureSprite;    // 0~19% (아주 깨끗함)
    public Sprite normalSprite;  // 20~49% (보통)
    public Sprite warningSprite; // 50~79% (불안)
    public Sprite dangerSprite;  // 80~99% (위험)

    private void OnEnable()
    {
        UpdateReflection();
    }

    void UpdateReflection()
    {
        if (CorruptionManager.instance == null) return;

        float val = CorruptionManager.instance.currentCorruption;

        // 색상은 원래대로(흰색) 돌려놔야 스프라이트 본연의 색이 보임
        reflectionImage.color = Color.white;

        // 구간별 이미지 변경
        if (val < 20)
        {
            // 0 ~ 19.99... (아주 깨끗함)
            if (pureSprite != null) reflectionImage.sprite = pureSprite;
        }
        else if (val < 50) 
        {
            // 20 ~ 49.99... (보통)
            if (normalSprite != null) reflectionImage.sprite = normalSprite;
        }
        else if (val < 80)
        {
            // 50 ~ 79.99... (불안)
            if (warningSprite != null) reflectionImage.sprite = warningSprite;
        }
        else
        {
            // 80 ~ 99.99... (위험)
            if (dangerSprite != null) reflectionImage.sprite = dangerSprite;
        }
    }

    public void CloseMirror()
    {
        gameObject.SetActive(false);
    }
}