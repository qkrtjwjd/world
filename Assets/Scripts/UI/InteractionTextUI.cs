using UnityEngine;
using TMPro;

public class InteractionTextUI : MonoBehaviour
{
    public static InteractionTextUI Instance;

    public TMP_Text textComponent;

    void Awake()
    {
        Instance = this;
        if (textComponent == null)
            textComponent = GetComponentInChildren<TMP_Text>();
        Hide();
    }

    public void Show(string message)
    {
        if (textComponent == null)
        {
            Debug.LogError("[InteractionTextUI] TMP_Text 컴포넌트가 연결되지 않았습니다.");
            return;
        }
        textComponent.text = message;
        if (!textComponent.gameObject.activeSelf) textComponent.gameObject.SetActive(true);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
