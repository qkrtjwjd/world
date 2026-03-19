using UnityEngine;

/// <summary>
/// 환상/현실 필터 상태에 따라 오브젝트 가시성을 제어합니다.
/// DaggerFilterController가 SetFilter()를 일괄 호출합니다.
/// </summary>
public class RealityFilterObject : MonoBehaviour
{
    [Header("환상 상태에서만 보임")]
    public GameObject[] fantasyObjects;

    [Header("현실 상태에서만 보임")]
    public GameObject[] realityObjects;

    void Start()
    {
        SetFilter(false); // 기본값: 환상
    }

    public void SetFilter(bool isReality)
    {
        foreach (var obj in fantasyObjects)
            if (obj != null) obj.SetActive(!isReality);

        foreach (var obj in realityObjects)
            if (obj != null) obj.SetActive(isReality);
    }
}
