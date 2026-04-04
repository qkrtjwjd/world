using UnityEngine;

/// <summary>
/// HUD/목표 패널을 원하는 타이밍에 제어하는 범용 컴포넌트.
/// 트리거존 진입, 버튼 클릭, UnityEvent, 스크립트 직접 호출 등 어디서든 사용 가능.
/// </summary>
public class ObjectiveControl : MonoBehaviour
{
    public enum Action { Hide, Show, Complete }

    [Header("동작 설정")]
    public Action action = Action.Hide;

    [Header("Show 선택 시 — 표시할 목표 텍스트")]
    public string header = "현재 목표";
    [TextArea(2, 4)]
    public string body = "";

    [Header("트리거존 진입 시 자동 실행")]
    public bool triggerOnPlayerEnter = false;

    // ─────────────────────────────────────────────

    /// <summary>
    /// 버튼 onClick, UnityEvent.onInteract, 스크립트 등 어디서든 호출.
    /// </summary>
    public void Execute()
    {
        switch (action)
        {
            case Action.Hide:
                ObjectiveManager.Instance?.HideHUD();
                break;
            case Action.Show:
                ObjectiveManager.Instance?.ShowObjective(header, body);
                break;
            case Action.Complete:
                ObjectiveManager.Instance?.CompleteObjective();
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnPlayerEnter && other.CompareTag("Player"))
            Execute();
    }
}
