using UnityEngine;
using TMPro;
using UnityEngine.UI; // Legacy Text용

public class InteractionTextUI : MonoBehaviour
{
    public static InteractionTextUI Instance;

    [Header("TextMeshPro 사용 시 연결")]
    public TextMeshProUGUI textComponent;

    [Header("Legacy Text (일반 텍스트) 사용 시 연결")]
    public Text legacyTextComponent;

    void Awake()
    {
        Instance = this;

        // 1. TextMeshPro 찾기
        if (textComponent == null)
            textComponent = GetComponentInChildren<TextMeshProUGUI>();

        // 2. 만약 없으면 Legacy Text 찾기
        if (textComponent == null && legacyTextComponent == null)
            legacyTextComponent = GetComponentInChildren<Text>();
        
        // 시작할 땐 안 보이게 끄기
        Hide();
    }

    public void Show(string message)
    {
        // 둘 다 없으면 에러 로그 띄우고 중단 (NullReference 방지)
        if (textComponent == null && legacyTextComponent == null)
        {            Debug.LogError("[InteractionTextUI] 텍스트 컴포넌트가 연결되지 않았습니다! 인스펙터를 확인하세요.");
            return; 
        }

        // 둘 중 연결된 것에 텍스트 설정하고, 해당 텍스트 오브젝트도 켜주기
        if (textComponent != null)
        {
            textComponent.text = message;
            if (!textComponent.gameObject.activeSelf) textComponent.gameObject.SetActive(true);
        }
        else if (legacyTextComponent != null)
        {            legacyTextComponent.text = message;
            if (!legacyTextComponent.gameObject.activeSelf) legacyTextComponent.gameObject.SetActive(true);
        }
        
        // 캔버스(또는 이 스크립트가 붙은 오브젝트)를 켬 (최적화: 꺼져있을 때만 켬)
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        // 최적화: 켜져있을 때만 끔
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}
