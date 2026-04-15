using System.Collections;
using UnityEngine;

/// <summary>
/// ItemData.specialEffectCode (SpecialEffectType enum)에 따라 특수 시각 효과를 실행합니다.
///
/// 새 효과 추가 방법:
///   1. SpecialEffectType.cs 에 enum 값 추가
///   2. HandleEffect()의 switch에 case 추가
///   3. 해당 효과 코루틴/메서드 작성
///   4. 아이템 에셋 인스펙터에서 specialEffectCode 드롭다운으로 선택
/// </summary>
public class ItemEffectHandler : MonoBehaviour
{
    public static ItemEffectHandler Instance { get; private set; }

    [Header("블러 효과")]
    [Tooltip("화면 블러 효과용 오버레이 패널 (Canvas 하위 Full-screen Image 오브젝트를 연결)")]
    [SerializeField] private GameObject blurOverlayPanel;
    [Tooltip("블러 효과 지속 시간(초)")]
    [SerializeField] private float blurDuration = 2f;

    // 효과를 추가할 때 여기에 [SerializeField] 필드를 추가하세요.

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (blurOverlayPanel != null) blurOverlayPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  공개 메서드
    // ─────────────────────────────────────────────

    /// <summary>
    /// SpecialEffectType에 따라 특수 시각 효과를 실행합니다.
    /// None이면 아무 효과도 없습니다.
    /// 새 효과 추가: SpecialEffectType enum에 값 추가 → 여기에 case 추가.
    /// </summary>
    public void HandleEffect(SpecialEffectType effectType)
    {
        switch (effectType)
        {
            case SpecialEffectType.None:
                break;
            case SpecialEffectType.Blur:
                StartCoroutine(BlurEffect());
                break;
            case SpecialEffectType.CaffeineStack:
                CaffeineManager.Instance?.AddStack();
                break;
            // 새 효과: SpecialEffectType에 값 추가 후 case를 여기에 추가하세요.
        }
    }

    // ─────────────────────────────────────────────
    //  효과 코루틴
    // ─────────────────────────────────────────────

    private IEnumerator BlurEffect()
    {
        if (blurOverlayPanel == null)
        {
            Debug.LogWarning("[ItemEffectHandler] blurOverlayPanel이 연결되지 않았습니다. 인스펙터에서 연결해주세요.");
            yield break;
        }

        blurOverlayPanel.SetActive(true);
        yield return new WaitForSecondsRealtime(blurDuration);
        blurOverlayPanel.SetActive(false);
    }

    // 새 효과 메서드를 여기에 추가하세요.
}
