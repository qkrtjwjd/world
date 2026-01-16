using UnityEngine;

public class MirrorObject : MonoBehaviour
{
    [Header("연결할 것")]
    public GameObject mirrorUIPanel;

    [Header("설정")]
    public KeyCode interactKey = KeyCode.F;

    private bool isPlayerNear = false;

    void Update()
    {
        if (mirrorUIPanel == null) return;

        // 플레이어가 근처에 있고 F키를 눌렀을 때
        if (isPlayerNear && Input.GetKeyDown(interactKey))
        {
            // 현재 꺼져있으면 -> 켜야 함 (true)
            // 현재 켜져있으면 -> 꺼야 함 (false)
            bool willOpen = !mirrorUIPanel.activeSelf;
            
            ToggleMirror(willOpen);
        }
    }

    // ★ 거울을 켜고 끄는 기능을 함수로 따로 뺌 (깔끔하게 관리하기 위해)
    void ToggleMirror(bool isOpen)
    {
        mirrorUIPanel.SetActive(isOpen);

        if (isOpen)
        {
            // 창을 열 때 -> 시간을 멈춘다 (게이지 멈춤!)
            Time.timeScale = 0f;
            Debug.Log("거울 보기: 시간 정지");
        }
        else
        {
            // 창을 닫을 때 -> 시간을 다시 흐르게 한다
            Time.timeScale = 1f;
            Debug.Log("거울 닫기: 시간 재개");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;

            // ★ 중요: 거울을 켜둔 채로 멀어지면?
            // 강제로 끄면서 시간도 다시 흐르게 해줘야 함! (안 그러면 영원히 시간 멈춤)
            if (mirrorUIPanel.activeSelf)
            {
                ToggleMirror(false); // 끄기 실행
            }
        }
    }
}