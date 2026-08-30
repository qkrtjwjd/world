using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionTextUI : MonoBehaviour
{
    public static InteractionTextUI Instance;

    public TMP_Text textComponent;

    [Tooltip("문구 뒤에 깔 배경. 비워 두면 글자만 나온다 — 예전 동작 그대로다. " +
             "도트 더미는 Assets/Images/UI/_dummy/ui_prompt.png 다.")]
    public Image background;

    void Awake()
    {
        Instance = this;
        if (textComponent == null)
            textComponent = GetComponentInChildren<TMP_Text>();
        // 인스펙터에서 안 꽂았으면 자식에서 찾아 본다. 없으면 배경 없이 동작한다.
        if (background == null)
            background = GetComponentInChildren<Image>(true);
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
        if (background != null && !background.gameObject.activeSelf)
            background.gameObject.SetActive(true);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
