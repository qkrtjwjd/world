using System.Collections;
using UnityEngine;

/// <summary>
/// 집_루의방_장난감상자 (house_lu_room_toybox) — 대비 (D-4-1 · C-16-9 둘째 예외 · E-76).
///
///   환상(F)          루 「예전에는 가지고 놀았지만, 지금은 먼지만 쌓였네」 — 대비 노드 _F
///   현실(R) · 밀기 전  대사 없음. 상자가 아래로 밀린다 · 끄는 소리 · 인형화 −1 · 야광별이 드러난다
///   민 뒤 F · R      같은 줄 — _F · _R (변환기가 둘 다 낸다)
///
/// 민 자리는 두 필터 모두 그대로다 — 좌표와 콜라이더를 옮긴다. 필터 교체가 아니라 세계에서 일어난 일이다.
/// 밀기는 한 번뿐이다. 상태는 플래그(obj.house_lu_room_toybox.pushed) 하나이고 저장 슬롯 · 되감기 지점에 같이 들어간다 —
/// BE#01 로 S#11 직후에 돌아가면 그 시점 값(밀기 전)으로 돌아가며 인형화도 함께 돌아간다(F-6).
/// 단검은 S#12 에서 얻으므로 밀기는 90초 제한 시간 안에서만 가능하고 시간은 그대로 흐른다(E-76 결정 20).
///
/// 「현실」은 대비 버전과 같은 기준(심리 게이지 70 이상 · F-2-2)으로 판정한다.
/// 🔴 민 흔적은 현실 스프라이트에만 그린다 — 그림 대기.
/// </summary>
[RequireComponent(typeof(InspectableObject))]
public class LuRoomToybox : MonoBehaviour, IInspectOverride
{
    [Tooltip("민 뒤 상자가 놓이는 자리(월드 좌표 · 아래쪽). 책상 · 문 쪽 동선을 막지 않는 곳.")]
    public Vector2 pushedPosition;
    [Tooltip("상자에 가려 있던 야광별. 밀면 켠다.")]
    public GameObject glowStars;
    [Tooltip("상자 끄는 소리(AudioManager 등록 이름).")]
    public string sfxDrag = "toybox_drag";
    [Tooltip("밀 때 인형화 변동(데모 고정값 · C-2-2 · F-2-6).")]
    public float pushCorruption = -1f;
    [Tooltip("미는 데 걸리는 시간(초).")]
    public float pushSeconds = 0.4f;

    public static string PushedKey(string id) => $"obj.{id}.pushed";

    Vector2 _home;
    bool _busy;
    InspectableObject _obj;

    public bool Pushed =>
        GameStateManager.Instance != null && GameStateManager.Instance.GetFlag(PushedKey(_obj != null ? _obj.CodeId : ""));

    void Awake()
    {
        _obj = GetComponent<InspectableObject>();
        _home = transform.position;
    }

    int _applied = -1;   // 마지막으로 맞춘 상태 (-1 = 아직)

    // 되감기 · 불러오기로 플래그가 바뀌는 경우까지 — 값만 비교하므로 가볍다(YardSugarState 와 같은 방식).
    void Update()
    {
        if (_busy) return;
        int now = Pushed ? 1 : 0;
        if (now != _applied) Apply();
    }

    /// <summary>플래그에 맞춰 상자 자리와 야광별을 맞춘다.</summary>
    public void Apply()
    {
        bool pushed = Pushed;
        _applied = pushed ? 1 : 0;
        SetPosition(pushed ? pushedPosition : _home);
        if (glowStars != null) glowStars.SetActive(pushed);
    }

    public bool TryInspect(InspectableObject obj)
    {
        if (_busy) return true;
        if (Pushed || InspectableObject.VersionSuffix() != "R") return false;   // 대비 노드 그대로(_F · 민 뒤 _R)
        StartCoroutine(Push());
        return true;
    }

    IEnumerator Push()
    {
        _busy = true;
        GameStateManager.Instance?.SetFlag(PushedKey(_obj.CodeId), true);
        if (!string.IsNullOrEmpty(sfxDrag)) AudioManager.Instance?.Play(sfxDrag);
        CorruptionManager.Instance?.AddCorruption(pushCorruption);

        var ctrl = YarnDialogue.LockPlayer();
        Vector2 from = transform.position;
        for (float t = 0f; t < pushSeconds; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            SetPosition(Vector2.Lerp(from, pushedPosition, t / pushSeconds));
        }
        SetPosition(pushedPosition);
        if (glowStars != null) glowStars.SetActive(true);
        YarnDialogue.UnlockPlayer(ctrl);
        Dbg.Log("[루의 방] 장난감 상자를 밀었다 — 인형화 " + pushCorruption + " · 야광별이 드러난다");
        _busy = false;
    }

    void SetPosition(Vector2 p)
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = p;
        transform.position = new Vector3(p.x, p.y, transform.position.z);
        Physics2D.SyncTransforms();
    }
}
