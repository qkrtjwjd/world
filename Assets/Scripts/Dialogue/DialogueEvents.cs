public static class DialogueEvents
{
    public static event System.Action OnDialogueStarted;
    public static event System.Action OnDialogueEnded;

    /// <summary>
    /// 초상화의 감정이 지정됐을 때 발행된다. 인자는 (화자 ID, 감정 ID).
    ///
    /// 감정 ID 는 <c>Assets/Date/Dialogue/CharacterSpriteData.asset</c> 의 등록값이며
    /// yarn 의 <c>&lt;&lt;showSprite&gt;&gt;</c> 가 준 <b>원본 인자</b>다 —
    /// 심리게이지 70 이상에서 갈아끼우는 <c>_real</c> 변형은 스프라이트 교체용이라 여기 오지 않는다.
    ///
    /// ⚠ <c>hideSprite</c> 에서는 발행하지 않는다. 초상화가 내려간 것이 곧
    ///   「그 인물의 기분이 사라졌다」는 뜻은 아니기 때문이다.
    /// </summary>
    public static event System.Action<string, string> OnPortraitEmotion;

    internal static void RaiseStarted() => OnDialogueStarted?.Invoke();
    internal static void RaiseEnded()   => OnDialogueEnded?.Invoke();

    internal static void RaisePortraitEmotion(string character, string emotionId)
        => OnPortraitEmotion?.Invoke(character, emotionId);
}
