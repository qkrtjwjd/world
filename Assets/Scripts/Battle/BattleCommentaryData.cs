using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 턴제 전투 캐릭터 대사 데이터.
/// [에디터] 우클릭 → Create → Battle → Commentary Data 로 에셋 생성.
/// characters 리스트에 루/쿠루 등 캐릭터를 추가하고,
/// 각 캐릭터의 taggedComments에 상황별 대사를 입력하세요.
///
/// 지원 태그: turn_start / skill_use / low_hp / ally_down / victory / boss_appear
/// </summary>
[CreateAssetMenu(fileName = "BattleCommentaryData", menuName = "Battle/Commentary Data")]
public class BattleCommentaryData : ScriptableObject
{
    [System.Serializable]
    public class TaggedComments
    {
        [Tooltip("turn_start / skill_use / low_hp / ally_down / victory / boss_appear")]
        public string tag;
        [TextArea(1, 4)]
        public string[] comments;
    }

    [System.Serializable]
    public class CharacterCommentarySet
    {
        public string characterName;
        public Sprite faceSprite;
        public TaggedComments[] taggedComments;

        /// <summary>tag에 해당하는 대사를 랜덤으로 1개 반환. 없으면 null.</summary>
        public string GetRandomComment(string tag)
        {
            if (taggedComments == null) return null;
            foreach (var tc in taggedComments)
            {
                if (tc.tag == tag && tc.comments != null && tc.comments.Length > 0)
                    return tc.comments[UnityEngine.Random.Range(0, tc.comments.Length)];
            }
            return null;
        }
    }

    [Tooltip("등록된 캐릭터 목록. 루/쿠루 기본, 자유롭게 추가 가능.")]
    public List<CharacterCommentarySet> characters = new List<CharacterCommentarySet>();

    /// <summary>이름으로 캐릭터 데이터를 반환. 없으면 null.</summary>
    public CharacterCommentarySet GetCharacter(string characterName)
    {
        foreach (var c in characters)
            if (c.characterName == characterName) return c;
        return null;
    }

    /// <summary>characterName 캐릭터의 tag 대사를 랜덤으로 1개 반환. 없으면 null.</summary>
    public string GetRandomComment(string characterName, string tag)
        => GetCharacter(characterName)?.GetRandomComment(tag);
}
