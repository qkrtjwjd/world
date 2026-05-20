using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 내 WorldObject 컴포넌트가 붙은 모든 오브젝트를 자동 수집하여
/// 게이지 구간에 따라 활성화 상태를 제어한다.
///
/// 구간별 동작:
///   0  ~ 30  : Fantasy만 표시
///   31 ~ 69  : Fantasy + Reality 모두 표시 (글리치 혼재)
///   70 ~ 100 : Reality만 표시
///
/// 사용법: 오브젝트를 다중 선택 후 WorldObject 컴포넌트를 추가하고 ZoneType 설정.
/// </summary>
public class GlitchZoneObjectController : MonoBehaviour
{
    private GameObject[] _fantasyObjects;
    private GameObject[] _realityObjects;

    void Start()
    {
        CollectMarkers();

        var gm = GaugeManager.Instance;
        if (gm != null)
        {
            gm.OnGaugeChanged += HandleGaugeChanged;
            HandleGaugeChanged(gm.fantasyRealityGauge);
        }
    }

    void OnDestroy()
    {
        var gm = GaugeManager.Instance;
        if (gm != null)
            gm.OnGaugeChanged -= HandleGaugeChanged;
    }

    void CollectMarkers()
    {
        var markers = FindObjectsByType<WorldObject>(FindObjectsInactive.Include);

        var fantasy = new List<GameObject>();
        var reality = new List<GameObject>();

        foreach (var marker in markers)
        {
            if (marker.zoneType == WorldObject.ZoneType.Fantasy)
                fantasy.Add(marker.gameObject);
            else
                reality.Add(marker.gameObject);
        }

        _fantasyObjects = fantasy.ToArray();
        _realityObjects = reality.ToArray();
    }

    void HandleGaugeChanged(float gauge)
    {
        if (gauge <= 30f)
        {
            SetAll(_fantasyObjects, true);
            SetAll(_realityObjects, false);
        }
        else if (gauge >= 70f)
        {
            SetAll(_fantasyObjects, false);
            SetAll(_realityObjects, true);
        }
        else
        {
            SetAll(_fantasyObjects, true);
            SetAll(_realityObjects, true);
        }
    }

    void SetAll(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            if (obj != null) obj.SetActive(active);
    }
}
