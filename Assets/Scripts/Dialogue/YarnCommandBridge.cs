using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class YarnCommandBridge : MonoBehaviour
{
    [Header("Yarn Spinner 연결")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private InMemoryVariableStorage variableStorage;

    [Header("포트레이트 UI")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image portraitImageRight;

    [Header("스프라이트 데이터")]
    [SerializeField] private CharacterSpriteData spriteData;

    private static readonly GaugeTrigger[] _allTriggers =
        (GaugeTrigger[])Enum.GetValues(typeof(GaugeTrigger));

    private static readonly Dictionary<string, Sprite> _spriteCache =
        new Dictionary<string, Sprite>();

    private const string VAR_GAUGE      = "$심리게이지";
    private const string VAR_CORRUPTION = "$인형화";
    private const string VAR_RESOLVE    = "$결심";

    private void Awake()
    {
        YarnDialogue.Register(dialogueRunner);
        dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }

    private void OnDestroy()
    {
        dialogueRunner.onDialogueStart.RemoveListener(OnDialogueStart);
        dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
    }

    // ─── 대화 시작: C# → Yarn 변수 주입 ──────────────────────────
    private void OnDialogueStart()
    {
        DialogueEvents.RaiseStarted();

        if (GaugeManager.Instance != null)
            variableStorage.SetValue(VAR_GAUGE, GaugeManager.Instance.fantasyRealityGauge);

        if (CorruptionManager.Instance != null)
            variableStorage.SetValue(VAR_CORRUPTION, CorruptionManager.Instance.currentCorruption);

        variableStorage.SetValue(VAR_RESOLVE, GameState.isResolved);
    }

    // ─── 대화 종료: Yarn 변수 → C# 반영 ─────────────────────────
    private void OnDialogueComplete()
    {
        if (variableStorage.TryGetValue(VAR_GAUGE, out float gauge) && GaugeManager.Instance != null)
            GaugeManager.Instance.SetGaugeValue(gauge);

        if (variableStorage.TryGetValue(VAR_RESOLVE, out bool resolved))
            GameState.isResolved = resolved;

        DialogueEvents.RaiseEnded();
    }

    // ─── <<showSprite "캐릭터명" "감정코드" ["left"|"right"] ["auto"|"fixed"]>> ────
    // mode="auto"(기본): 게이지 >= 70이면 emotion+"_real" 버전 자동 선택
    // mode="fixed": 게이지 무관하게 지정된 emotion 고정
    [YarnCommand("showSprite")]
    public void ShowSprite(string character, string emotion, string side = "left", string mode = "auto")
    {
        string effectiveEmotion = emotion;

        if (!mode.Equals("fixed", System.StringComparison.OrdinalIgnoreCase) &&
            variableStorage != null &&
            variableStorage.TryGetValue(VAR_GAUGE, out float gauge) &&
            gauge >= 70f)
        {
            string realEmotion = emotion + "_real";
            string realKey     = $"{character}_{realEmotion}";
            if (!_spriteCache.TryGetValue(realKey, out Sprite realSprite))
            {
                realSprite = Resources.Load<Sprite>($"Sprites/{realKey}");
                if (realSprite == null && spriteData != null)
                    realSprite = spriteData.GetSprite(character, realEmotion);
                _spriteCache[realKey] = realSprite;
            }
            if (realSprite != null)
                effectiveEmotion = realEmotion;
        }

        string key = $"{character}_{effectiveEmotion}";
        if (!_spriteCache.TryGetValue(key, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>($"Sprites/{key}");
            if (sprite == null && spriteData != null)
                sprite = spriteData.GetSprite(character, effectiveEmotion);
            _spriteCache[key] = sprite;
        }

        bool useRight  = side.Equals("right", System.StringComparison.OrdinalIgnoreCase);
        Image active   = useRight ? portraitImageRight : portraitImage;
        Image inactive = useRight ? portraitImage      : portraitImageRight;

        if (inactive != null) inactive.gameObject.SetActive(false);
        if (active == null)   return;

        if (sprite != null)
        {
            active.sprite = sprite;
            active.gameObject.SetActive(true);
        }
        else
        {
            active.gameObject.SetActive(false);
        }
    }

    // ─── <<hideSprite ["left"|"right"|"both"]>> ────────────────
    [YarnCommand("hideSprite")]
    public void HideSprite(string side = "both")
    {
        bool hideLeft  = !side.Equals("right", System.StringComparison.OrdinalIgnoreCase);
        bool hideRight = !side.Equals("left",  System.StringComparison.OrdinalIgnoreCase);
        if (hideLeft  && portraitImage      != null) portraitImage.gameObject.SetActive(false);
        if (hideRight && portraitImageRight != null) portraitImageRight.gameObject.SetActive(false);
    }

    // ─── <<triggerGoodEnding>> ──────────────────────────────────
    [YarnCommand("triggerGoodEnding")]
    public void TriggerGoodEnding() => EndingManager.TriggerGoodEnding();

    // ─── <<applyTrigger "트리거이름">> ───────────────────────────
    [YarnCommand("applyTrigger")]
    public void ApplyTrigger(string triggerName)
    {
        if (GaugeManager.Instance == null)
        {
            Debug.LogWarning($"[YarnCommandBridge] ApplyTrigger '{triggerName}': GaugeManager 인스턴스가 없습니다.");
            return;
        }

        bool found = false;
        foreach (GaugeTrigger t in _allTriggers)
        {
            if (t.ToString().StartsWith(triggerName + "__"))
            {
                GaugeManager.Instance.ApplyTrigger(t);
                // 같은 대화 안의 <<if $심리게이지 >= N>> 조건이 변경값을 반영하도록 즉시 동기화
                variableStorage.SetValue(VAR_GAUGE, GaugeManager.Instance.fantasyRealityGauge);
                found = true;
                break;
            }
        }

        if (!found)
            Debug.LogWarning($"[YarnCommandBridge] ApplyTrigger: '{triggerName}'에 해당하는 GaugeTrigger를 찾지 못했습니다.");
    }
}
