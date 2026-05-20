using Yarn.Unity;

public static class DialogueRunnerExtensions
{
    public static void SetLanguage(this DialogueRunner runner, string languageCode)
    {
        if (LocalizationManager.Instance == null) return;

        var lang = languageCode.ToLowerInvariant() switch
        {
            "en" => LocalizationManager.Language.EN,
            "jp" => LocalizationManager.Language.JP,
            _    => LocalizationManager.Language.KO,
        };
        LocalizationManager.Instance.ChangeLanguage(lang);
    }
}
