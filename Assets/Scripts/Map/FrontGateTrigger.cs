using System.Collections;
using UnityEngine;

/// <summary>
/// S#13 정문 — 집 구간의 마지막 지점.
/// 플레이어가 이 트리거를 통과하면 마을 파트(MapScene)로 넘어간다.
///
/// 마당에 정문 오브젝트를 만들고 IsTrigger 콜라이더에 이 컴포넌트를 붙이세요.
///
/// ⚠ 정본 규약 (2026-08-07 D 정본 S#13)
///   - 마당부터 정문까지는 플레이어가 직접 걷는다. 컷신으로 처리하지 않는다.
///     그래서 이 컴포넌트는 대사도 연출도 재생하지 않고, 통과만 감지한다.
///   - 카메라는 루를 따라가지 않고 그 자리에 남는다. 루의 뒷모습이 화면에서 작아지며 전환된다.
///   - 감정을 크게 쓰지 않는다. 음악도 연출도 절제한다.
///     루가 대단한 결심을 한 것이 아니라 아빠를 데리러 가는 것뿐이라는 톤을 유지한다.
///
/// ※ 마당에서 단검을 뽑아 뿌리 없는 꽃을 보는 선택 연출은 **넣지 않기로 결정**했다.
///   결계의 정체는 마을 구간까지 미룬다. (정본이 "결정에 맡긴다"고 명시한 항목)
/// </summary>
public class FrontGateTrigger : MonoBehaviour
{
    [Header("전환할 씬")]
    public string targetScene = SceneNames.Map;

    [Header("카메라를 그 자리에 두고 루가 멀어지는 시간(초)")]
    [Tooltip("0이면 즉시 전환한다. 정본은 루의 뒷모습이 작아지는 것을 보여주라고 지시한다.")]
    public float lingerDuration = 1.5f;

    private bool _triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered || !other.CompareTag("Player")) return;

        _triggered = true;
        StartCoroutine(PassGateRoutine());
    }

    IEnumerator PassGateRoutine()
    {
        // 집을 벗어났다 — 결계의 조임이 여기서 끝난다(C-14-2 성공 경로).
        // 씬 전환을 기다리지 않고 여기서 해제해야 lingerDuration 동안 90초가 다해도 실패하지 않는다.
        HouseEscapePressureController.NotifyEscaped();

        // 카메라를 따라가지 않게 한다 — 루가 화면에서 작아진다.
        // Follow 대상을 떼면 가상 카메라가 그 자리에 그대로 남는다.
        CameraFollow.Instance?.SetTarget(null);

        if (lingerDuration > 0f)
            yield return new WaitForSeconds(lingerDuration);

        TransitionManager.Instance?.DoSceneTransition(targetScene);
    }
}
