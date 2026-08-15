using System.Collections;
using UnityEngine;

/// <summary>
/// 대화 중 라인 수가 lineThreshold 이상 진행되면 루의 "NeckScratch" Animator 트리거를 실행합니다.
/// 씬 인스턴스당 1회만 발동됩니다.
/// 마을 씬(Village)의 적절한 GameObject에 부착하세요.
/// </summary>
public class DialogueNeckScratchTrigger : MonoBehaviour
{
    [Tooltip("발동 기준 라인 수 (기본 4)")]
    [SerializeField] private int   lineThreshold = 4;
    [Tooltip("NeckScratch 유지 후 자동 복귀까지 대기 시간 (초)")]
    [SerializeField] private float returnDelay   = 2f;

    private bool     _hasFired;
    private Animator _playerAnim;

    void Start()
    {
        var ctrl = FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl != null)
            _playerAnim = ctrl.GetComponentInChildren<Animator>()
                       ?? ctrl.GetComponent<Animator>();
    }

    void OnEnable()  => LineCounter.OnLineAdvanced += OnLineAdvanced;
    void OnDisable() => LineCounter.OnLineAdvanced -= OnLineAdvanced;

    void OnLineAdvanced(int count)
    {
        if (_hasFired || count < lineThreshold) return;
        _hasFired = true;
        StartCoroutine(DoNeckScratch());
    }

    IEnumerator DoNeckScratch()
    {
        _playerAnim?.SetTrigger("NeckScratch");
        yield return new WaitForSecondsRealtime(returnDelay);
        _playerAnim?.ResetTrigger("NeckScratch");
    }
}
