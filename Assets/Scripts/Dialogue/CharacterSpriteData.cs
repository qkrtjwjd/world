using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpriteEntry
{
    public string emotionId;
    public Sprite sprite;
}

[System.Serializable]
public class CharacterPortrait
{
    public string characterName;
    public List<SpriteEntry> sprites = new List<SpriteEntry>();

    public Sprite GetSprite(string emotion)
    {
        foreach (var e in sprites)
            if (e.emotionId == emotion && e.sprite != null)
                return e.sprite;
        return sprites.Count > 0 ? sprites[0].sprite : null;
    }
}

[CreateAssetMenu(fileName = "CharacterSpriteData", menuName = "Dialogue/Character Sprite Data")]
public class CharacterSpriteData : ScriptableObject
{
    public List<CharacterPortrait> characters = new List<CharacterPortrait>();

    private Dictionary<string, CharacterPortrait> _map;

    void OnEnable() => BuildMap();

    void BuildMap()
    {
        _map = new Dictionary<string, CharacterPortrait>(characters.Count);
        foreach (var c in characters)
            if (!string.IsNullOrEmpty(c.characterName))
                _map[c.characterName] = c;
    }

    public Sprite GetSprite(string character, string emotion)
    {
        if (_map == null) BuildMap();
        if (_map.TryGetValue(character, out var portrait))
            return portrait.GetSprite(emotion);
        Debug.LogWarning($"[CharacterSpriteData] '{character}' 캐릭터를 찾을 수 없습니다.");
        return null;
    }
}
