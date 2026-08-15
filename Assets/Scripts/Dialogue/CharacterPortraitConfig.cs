using UnityEngine;

public enum PortraitSide { Left, Right }
public enum EntryMotion  { SlideIn, FadeIn, None }
public enum ExitMotion   { SlideOut, FadeOut, None }

[CreateAssetMenu(fileName = "NewPortraitConfig", menuName = "무채색낙원/CharacterPortraitConfig")]
public class CharacterPortraitConfig : ScriptableObject
{
    public string      characterId;
    public PortraitSide defaultSide   = PortraitSide.Left;
    public EntryMotion  entryMotion   = EntryMotion.SlideIn;
    public ExitMotion   exitMotion    = ExitMotion.SlideOut;
    public float        entryDuration = 0.3f;
    public float        exitDuration  = 0.3f;
}
