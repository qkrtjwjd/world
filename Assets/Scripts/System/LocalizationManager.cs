using System.Collections.Generic;
using UnityEngine;
using System.IO;
// using Newtonsoft.Json.Linq; // 만약 복잡한 JSON 파싱이 필요하면 Newtonsoft 추천, 여기서는 간단하게 Dictionary로 직접 파싱하거나 JsonUtility 사용

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;

    public enum Language { KO, EN, JP }
    public Language currentLanguage = Language.KO;

    // 키-값 쌍을 저장할 딕셔너리
    // 예: "interaction.pickup" -> "{0} 획득하기"
    private Dictionary<string, string> localizedText = new Dictionary<string, string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 넘어가도 유지
            LoadLanguage(currentLanguage);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeLanguage(Language lang)
    {
        currentLanguage = lang;
        LoadLanguage(lang);
        
        // 언어가 바뀌면 UI 갱신 이벤트를 뿌려야 함 (옵저버 패턴 등 사용)
        // 여기서는 간단하게 로그만
        Debug.Log($"언어가 {lang}으로 변경되었습니다.");
    }

    void LoadLanguage(Language lang)
    {
        localizedText.Clear();

        string fileName = lang.ToString().ToLower(); // ko, en, jp
        
        // Resources 폴더에서 불러오기
        TextAsset jsonFile = Resources.Load<TextAsset>($"Localization/{fileName}");
        
        if (jsonFile != null)
        {
            // JsonUtility는 중첩 구조(Nested)를 바로 Dictionary로 못 바꿈.
            // 그래서 간단한 파서(Simple Parser)를 쓰거나 구조체를 만들어야 함.
            // 여기서는 간단하게 'MiniJSON' 같은 걸 쓰거나, 직접 구조체를 만듭니다.
            // 편의상 JsonUtility + Wrapper 클래스 방식을 씁니다.
            
            // 하지만 키가 가변적이라면 구조체로 만들기 힘듦.
            // 일단은 간단하게 문자열 파싱으로 처리하거나, 유니티용 SimpleJSON을 쓰는 게 좋음.
            // 여기서는 "간단 구현"을 위해 수동으로 키를 매핑하는 구조를 예시로 듭니다.
            
            // 실제 상용 게임에서는 Newtonsoft.Json (Json.NET) 에셋을 추천합니다.
            // 지금은 외부 에셋 없이 작동하도록 간단한 구조체 매핑을 하겠습니다.
            
            LocalizationData data = JsonUtility.FromJson<LocalizationData>(jsonFile.text);
            
            // 데이터 딕셔너리에 넣기 (구조체 -> 딕셔너리 평탄화)
            // items
            if (data.items != null)
            {
                // JsonUtility는 Dictionary 직렬화를 지원 안 하므로, List로 받거나 별도 처리가 필요.
                // 이 부분은 유니티 기본 기능만으로는 꽤 번거롭습니다.
                // 따라서 가장 쉬운 방법: "키:값" 형태의 리스트로 JSON을 만들거나, 
                // 지금처럼 구조체를 딱 정해서 쓰는 것입니다.
                
                localizedText["items.apple"] = data.items.apple;
                localizedText["items.red_potion"] = data.items.red_potion;
            }
            
            if (data.interaction != null)
            {
                localizedText["interaction.pickup"] = data.interaction.pickup;
                localizedText["interaction.feed"] = data.interaction.feed;
                localizedText["interaction.talk"] = data.interaction.talk;
            }

            if (data.ui != null)
            {
                localizedText["ui.inventory"] = data.ui.inventory;
                localizedText["ui.save"] = data.ui.save;
                localizedText["ui.load"] = data.ui.load;
                localizedText["ui.close"] = data.ui.close;
            }

             if (data.messages != null)
            {
                localizedText["messages.inventory_full"] = data.messages.inventory_full;
                localizedText["messages.item_get"] = data.messages.item_get;
            }

            if (data.battle != null)
            {
                localizedText["battle.attack"] = data.battle.attack;
                localizedText["battle.defend"] = data.battle.defend;
                localizedText["battle.escape"] = data.battle.escape;
                localizedText["battle.item"] = data.battle.item;
                localizedText["battle.win"] = data.battle.win;
                localizedText["battle.lose"] = data.battle.lose;
                localizedText["battle.wild_enemy_appear"] = data.battle.wild_enemy_appear;
                localizedText["battle.player_turn_prompt"] = data.battle.player_turn_prompt;
                localizedText["battle.enemy_turn_action"] = data.battle.enemy_turn_action;
                localizedText["battle.escape_success"] = data.battle.escape_success;
                localizedText["battle.escape_fail"] = data.battle.escape_fail;
                localizedText["battle.damage_dealt"] = data.battle.damage_dealt;
                localizedText["battle.damage_taken"] = data.battle.damage_taken;
                localizedText["battle.heal"] = data.battle.heal;
                localizedText["battle.defend_action"] = data.battle.defend_action;
            }
        }
        else
        {
            Debug.LogError($"언어 파일 로드 실패: Localization/{fileName}");
        }
    }

    public string GetText(string key)
    {
        if (localizedText.ContainsKey(key))
        {
            return localizedText[key];
        }
        return key; // 없으면 키 그대로 반환
    }

    // 포맷팅 지원 (예: "{0} 획득")
    public string GetText(string key, params object[] args)
    {
        if (localizedText.ContainsKey(key))
        {
            return string.Format(localizedText[key], args);
        }
        return key;
    }
}

// JSON 파싱용 클래스들 (구조가 고정되어 있어야 함)
[System.Serializable]
public class LocalizationData
{
    public ItemTexts items;
    public InteractionTexts interaction;
    public UITexts ui;
    public MessageTexts messages;
    public BattleTexts battle;
}

[System.Serializable]
public class ItemTexts
{
    public string apple;
    public string red_potion;
}

[System.Serializable]
public class InteractionTexts
{
    public string pickup;
    public string feed;
    public string talk;
}

[System.Serializable]
public class UITexts
{
    public string inventory;
    public string save;
    public string load;
    public string close;
}

[System.Serializable]
public class MessageTexts
{
    public string inventory_full;
    public string item_get;
}

[System.Serializable]
public class BattleTexts
{
    public string attack;
    public string defend;
    public string escape;
    public string item;
    public string win;
    public string lose;
    public string wild_enemy_appear;
    public string player_turn_prompt;
    public string enemy_turn_action;
    public string escape_success;
    public string escape_fail;
    public string damage_dealt;
    public string damage_taken;
    public string heal;
    public string defend_action;
}
