using UnityEngine;
using UnityEngine.UI;

public class MirrorReflection : MonoBehaviour
{
    [Header("내 모습을 보여줄 이미지")]
    public Image reflectionImage; 

    [Header("상태별 색상 설정")]
    public Color pureColor = Color.cyan;     // 0~19% (아주 깨끗함 - 하늘색 추천)
    public Color normalColor = Color.white;  // 20~49% (보통 - 흰색)
    public Color warningColor = Color.yellow;// 50~79% (불안 - 노랑)
    public Color dangerColor = Color.red;    // 80~99% (위험 - 빨강)

    private void OnEnable()
    {
        UpdateReflection();
    }

    void UpdateReflection()
    {
        if (CorruptionManager.instance == null) return;

        float val = CorruptionManager.instance.currentCorruption;

        // 구간별 색상 변경
        if (val < 20)
        {
            // 0 ~ 19.99... (아주 깨끗함)
            reflectionImage.color = pureColor;
        }
        else if (val < 50) 
        {
            // 20 ~ 49.99... (보통)
            reflectionImage.color = normalColor;
        }
        else if (val < 80)
        {
            // 50 ~ 79.99... (불안)
            reflectionImage.color = warningColor;
        }
        else
        {
            // 80 ~ 99.99... (위험)
            reflectionImage.color = dangerColor;
        }
    }

    public void CloseMirror()
    {
        gameObject.SetActive(false);
    }
}