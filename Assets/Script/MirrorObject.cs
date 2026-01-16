using UnityEngine;

public class MirrorObject : MonoBehaviour
{
    public GameObject mirrorUIPanel; // 띄워줄 UI 패널

    // 마우스로 클릭하면 실행됨 (Collider 필수!)
    private void OnMouseDown()
    {
        // UI가 꺼져있으면 켠다
        if (mirrorUIPanel != null)
        {
            mirrorUIPanel.SetActive(true);
        }
    }
}