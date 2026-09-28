using System.Collections;
using UnityEngine;

/// <summary>
/// S#13 정문 — 집 구간의 마지막 지점.
/// 플레이어가 이 트리거를 통과하면 마을 파트(MapScene)로 넘어간다.
///
/// 마당에 정문 오브젝트를 만들고 IsTrigger 콜라이더에 이 컴포넌트를 붙이세요.
///
/// ⚠ 정본 규약 (개정 D S#13 문단 425 · 430)
///   - 마당부터 정문까지는 플레이어가 직접 걷는다. 컷신으로 처리하지 않는다.
///     그래서 이 컴포넌트는 대사도 연출도 재생하지 않고, 통과만 감지한다.
///   - [CAM] 정문 앞에서 추적을 끊고 그 자리에 남는다. **루가 정문을 지나 화면 밖으로 걸어 나간 뒤** 전환한다.
///     조작은 잠그지 않는다 — 걸어 나가는 것도 루의 발이다.
///   - 감정을 크게 쓰지 않는다. 음악도 연출도 절제한다.
///     루가 대단한 결심을 한 것이 아니라 아빠를 데리러 가는 것뿐이라는 톤을 유지한다.
///
/// ※ 마당에서 단검을 뽑아 뿌리 없는 꽃을 보는 선택 연출은 개정 D 문단 428 에서 **채택으로 확정**됐다.
///   (예전 주석의 「넣지 않기로 결정」은 낡은 판단이다.) 대사도 UI 알림도 붙이지 않는다 —
///   마당 현실 필터 배경(D 441 · 별도 작화)이 오면 필터 토글만으로 성립한다.
/// </summary>
public class FrontGateTrigger : MonoBehaviour
{
    [Header("전환할 씬")]
    public string targetScene = SceneNames.Map;

    // 2026-09-27: 전에는 통과 1.5초 뒤 무조건 전환했다 — 실측에서 루가 화면 중앙 4유닛 안(반폭 10)에 남은 채 넘어갔다.
    [Header("화면 밖 판정")]
    [Tooltip("루의 발끝이 화면 가장자리에서 이만큼(월드 유닛) 더 나가야 「화면 밖」으로 본다. 캐릭터 폭 1유닛의 절반 이상.")]
    public float offscreenMargin = 0.75f;

    private bool _triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered || !other.CompareTag("Player")) return;

        _triggered = true;
        StartCoroutine(PassGateRoutine(other.transform));
    }

    IEnumerator PassGateRoutine(Transform lu)
    {
        // ⚠ 여기서 탈출 압박을 끄지 않는다. 「출구는 정문이지만 제한 시간은 현관문에서 끝난다」
        //   (F-6 문단 788 · C-14-2-2 문단 1060). 해제는 FrontDoorInteraction.DepartRoutine 이 한다.
        //   마당은 이미 압박 밖이므로 이 지점에 남은 조임도 타이머도 없다.

        // 추적을 끊고 그 자리에 남는다 — 카메라 4종의 「고정」(F-3-9).
        CameraDirector.Instance?.Hold();

        while (lu != null && IsOnScreen(lu.position))
            yield return null;

        TransitionManager.Instance?.DoSceneTransition(targetScene);
    }

    bool IsOnScreen(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        // 보이는 폭은 창 비율이 아니라 Pixel Perfect Camera 의 기준 해상도가 정한다(Windowbox · CLAUDE.md §11).
        // cam.aspect 는 창 크기를 따라가 배치 실행 등에서 어긋났다(실측).
        var ppc = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        float halfH, halfW;
        if (ppc != null && ppc.assetsPPU > 0)
        {
            halfW = ppc.refResolutionX * 0.5f / ppc.assetsPPU;
            halfH = ppc.refResolutionY * 0.5f / ppc.assetsPPU;
        }
        else
        {
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
        }
        Vector3 c = cam.transform.position;
        return Mathf.Abs(p.x - c.x) < halfW + offscreenMargin &&
               Mathf.Abs(p.y - c.y) < halfH + offscreenMargin;
    }
}
