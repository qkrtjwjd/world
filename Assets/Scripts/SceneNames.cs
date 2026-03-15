/// <summary>
/// 씬 이름 상수 모음.
/// 씬 이름을 바꿀 때 이 파일 한 곳만 수정하면 됩니다.
/// </summary>
public static class SceneNames
{
    public const string Title       = "TitleScene";
    public const string Home        = "Home";
    public const string Map         = "MapScene";
    public const string DarkReality = "DarkReality";
    public const string Battle      = "BattleScene";
    public const string Shelter     = "Shelter";
    public const string BadEnding   = "BadEndingScene";

    // 현실(Dark) <-> 환상(Fantasy) 짝꿍 매핑
    public static string GetFantasyScene(string realityScene)
    {
        switch (realityScene)
        {
            case DarkReality: return Map;
            // 추후 씬 추가 시 여기에 케이스만 추가
            default:          return Map;
        }
    }

    public static bool IsRealityScene(string sceneName)
    {
        return sceneName == DarkReality || sceneName.Contains("Dark");
    }
}
