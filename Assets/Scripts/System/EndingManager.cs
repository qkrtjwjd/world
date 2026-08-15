using UnityEngine.SceneManagement;

/// <summary>배드 엔딩 종류. 되감기 지점과 표시 문구가 종류별로 다릅니다.</summary>
public enum BadEndingType
{
    /// <summary>집 S#12 — 90초 안에 나가지 못해 현관문이 영원히 닫힌다. 복귀: S#11 직후 (C-14-2)</summary>
    HouseSealed,
    /// <summary>마을 — 순찰 2회차 이후 발각되어 감금된다. 복귀: 마을 진입 지점 (C-14-3-4)</summary>
    Captured,
    /// <summary>인형화 100 — 자아 소멸. 데모 범위(상한 32)에서는 도달하지 않는다 (C-2-6)</summary>
    Doll,
}

public static class EndingManager
{
    /// <summary>
    /// BadEndingScene 이 로드된 뒤 <see cref="BadEndingSequence"/> 가 읽어갈 엔딩 종류.
    /// 씬 전환을 사이에 두므로 정적 필드로 넘깁니다.
    /// </summary>
    public static BadEndingType PendingBadEnding { get; private set; } = BadEndingType.Doll;

    public static void TriggerGoodEnding()
    {
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(SceneNames.Credits);
        else
            SceneManager.LoadScene(SceneNames.Credits);
    }

    /// <summary>
    /// 배드 엔딩 씬으로 이동합니다. 연출과 복귀는 BadEndingScene 의 <see cref="BadEndingSequence"/> 가 맡습니다.
    /// </summary>
    /// <remarks>
    /// 인형화 페널티를 붙이지 않습니다 — 되감기가 있으므로 이중 처벌이 됩니다(CLAUDE.md §2 · C-14-2).
    /// 턴제 전투(timeScale 0) 중에 호출돼도 연출이 정지하지 않도록 timeScale 을 되돌립니다.
    /// </remarks>
    public static void TriggerBadEnding(BadEndingType type)
    {
        PendingBadEnding = type;
        UnityEngine.Time.timeScale = 1f;

        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(SceneNames.BadEnding);
        else
            SceneManager.LoadScene(SceneNames.BadEnding);
    }
}
