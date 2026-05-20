public static class LocalizationHelper
{
    /// <summary>현재 언어 설정에 따라 ko/en/jp 중 하나를 반환합니다. 선택된 언어가 비어 있으면 ko로 폴백합니다.</summary>
    public static string Get(string ko, string en, string jp)
    {
        if (LocalizationManager.Instance == null)
            return ko;

        switch (LocalizationManager.Instance.currentLanguage)
        {
            case LocalizationManager.Language.EN:
                return string.IsNullOrEmpty(en) ? ko : en;
            case LocalizationManager.Language.JP:
                return string.IsNullOrEmpty(jp) ? ko : jp;
            default:
                return ko;
        }
    }
}
