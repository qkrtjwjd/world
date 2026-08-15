using System.Collections.Generic;
using UnityEngine;

/// <summary>환상/현실 오브젝트 쌍을 SetActive로 전환합니다.</summary>
[System.Serializable]
public class SwapPair
{
    [Tooltip("환상 모드에서 활성화되는 오브젝트")]
    public GameObject fantasyObject;

    [Tooltip("현실 모드에서 활성화되는 오브젝트")]
    public GameObject realityObject;
}

public class ObjectSwapController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static ObjectSwapController Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("ObjectSwapController [Auto]");
                _instance = go.AddComponent<ObjectSwapController>();
            }
            return _instance;
        }
    }
    private static ObjectSwapController _instance;

    // ─────────────────────────────────────────────
    //  Inspector 설정
    // ─────────────────────────────────────────────
    [Header("전환 오브젝트 쌍 목록")]
    public List<SwapPair> swapPairs = new List<SwapPair>();

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 컴포넌트만 파괴 — 매니저 루트 오브젝트에 함께 붙은 다른 컴포넌트 보호
            Destroy(this);
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────
    /// <summary>현실 오브젝트를 활성화하고 환상 오브젝트를 비활성화합니다.</summary>
    public void SwapToReality()
    {
        foreach (SwapPair pair in swapPairs)
        {
            pair.fantasyObject?.SetActive(false);
            pair.realityObject?.SetActive(true);
        }
        Dbg.Log("[ObjectSwapController] 현실 오브젝트로 스왑 완료");
    }

    /// <summary>환상 오브젝트를 활성화하고 현실 오브젝트를 비활성화합니다.</summary>
    public void SwapToFantasy()
    {
        foreach (SwapPair pair in swapPairs)
        {
            pair.fantasyObject?.SetActive(true);
            pair.realityObject?.SetActive(false);
        }
        Dbg.Log("[ObjectSwapController] 환상 오브젝트로 스왑 완료");
    }
}
