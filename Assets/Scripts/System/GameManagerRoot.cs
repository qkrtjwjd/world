using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GameManager 프리팹 루트 — 씬마다 놓인 GameManager 사본이 같은 일을 두 번 하지 않게 한다(2026-10-05).
///
/// <para><b>왜 필요한가.</b> GameManager 는 씬마다 놓여 있고, 안의 싱글톤들이 첫 것을 DontDestroyOnLoad 로 남긴다.
/// 사본에서는 싱글톤 가드가 컴포넌트만 지우므로 비싱글톤 스크립트(<see cref="PauseSystem"/> · <see cref="PostProcessingController"/> ·
/// <see cref="DebugGaugeController"/> · <see cref="GlitchZoneObjectController"/> · <see cref="RealityGaugeDriver"/>)와
/// 자식(글리치 · 패널 캔버스, 아이템 효과, 전역 후처리 볼륨 <c>Volume_Screen</c>)이 씬마다 하나씩 더 돌았다
/// (배치 실측 — 마을에서 GameManager 3개). 전역 볼륨이 여럿이면 설정의 밝기 · 채도가 어느 볼륨에 걸리느냐에 따라 먹지 않는다.</para>
///
/// <para><b>규칙.</b> 처음 깬 GameManager 가 기준이다. 사본은 다른 스크립트가 깨기 전에 기준에도 있는 비싱글톤 스크립트와
/// 같은 이름의 자식을 지운다. 기준에 없는 매니저(Home 의 GameStateManager · SFXManager · FilterManager · FlagManager)는 남긴다 —
/// 그 사본이 그 매니저들의 첫 인스턴스다. 남은 것이 없으면 오브젝트째 지운다.
/// 사본 볼륨을 가리키던 씬 참조(마을 <c>GlitchZoneVolume</c>)는 기준의 볼륨으로 옮긴다.</para>
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameManagerRoot : MonoBehaviour
{
    static GameManagerRoot _primary;

    static readonly System.Type[] DuplicateScripts =
    {
        typeof(PauseSystem), typeof(PostProcessingController), typeof(DebugGaugeController),
        typeof(GlitchZoneObjectController), typeof(RealityGaugeDriver),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _primary = null;

    void Awake()
    {
        if (_primary == null) { _primary = this; return; }
        if (_primary == this) return;
        PruneDuplicate();
        StartCoroutine(DestroyIfEmpty());
    }

    void OnDestroy()
    {
        if (_primary == this) _primary = null;
    }

    void PruneDuplicate()
    {
        foreach (var t in DuplicateScripts)
        {
            var mine = GetComponent(t);
            if (mine != null && _primary.GetComponent(t) != null) DestroyImmediate(mine);
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            var twin  = _primary.transform.Find(child.name);
            if (twin == null) continue;
            RebindVolumes(child, twin);
            DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>사본 자식의 Volume 을 가리키던 PostProcessingController 를 기준 쪽 Volume 으로 옮긴다.</summary>
    static void RebindVolumes(Transform from, Transform to)
    {
        var src = from.GetComponentsInChildren<Volume>(true);
        if (src.Length == 0) return;
        var dst = to.GetComponentInChildren<Volume>(true);
        foreach (var ppc in FindObjectsByType<PostProcessingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (src.Contains(ppc.brightnessVolume)) ppc.brightnessVolume = dst;
            if (src.Contains(ppc.saturationVolume)) ppc.saturationVolume = dst;
            if (src.Contains(ppc.fantasyVolume))    ppc.fantasyVolume    = dst;
            if (src.Contains(ppc.realityVolume))    ppc.realityVolume    = dst;
            if (src.Contains(ppc.glitchVolume))     ppc.glitchVolume     = dst;
        }
    }

    /// <summary>싱글톤 가드가 사본 컴포넌트를 지운 뒤(프레임 끝) 남은 게 없으면 껍데기를 치운다.</summary>
    IEnumerator DestroyIfEmpty()
    {
        yield return null;
        bool empty = transform.childCount == 0
                     && GetComponents<MonoBehaviour>().All(m => m == null || m == this);
        if (empty) Destroy(gameObject);
    }
}
