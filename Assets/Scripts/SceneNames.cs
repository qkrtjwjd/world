/// <summary>
/// 씬 이름 상수 모음.
/// 씬 이름을 바꿀 때 이 파일 한 곳만 수정하면 됩니다.
/// </summary>
public static class SceneNames
{
    public const string Title       = "TitleScene";
    public const string Intro       = "IntroScene";
    public const string Home        = "Home";
    public const string Map         = "MapScene";
    public const string Battle      = "BattleScene";
    public const string Shelter     = "Shelter";
    public const string BadEnding   = "BadEndingScene";
    public const string Credits     = "CreditsScene";

    // 현실/환상은 씬이 아니라 한 씬 안에서 F키로 오간다.
    // 상태는 DaggerFilterController.IsRealityView 를 볼 것. (2026-08-27 DarkReality 씬 폐기)
}
