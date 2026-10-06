using System.Threading;
using TMPro;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;

/// <summary>
/// 대사 글자 소리 — 글자가 하나 나올 때마다 화자별 짧은 소리를 낸다(언더테일식 · 2026-10-06 사용자 결정 「인물마다 다르게」).
///
/// <para><b>붙는 자리.</b> <see cref="LinePresenter"/> 의 Event Handlers 목록(Dialogue.prefab).
/// 타이프라이터가 글자마다 <see cref="OnCharacterWillAppear"/> 를 부른다. 빨리 넘기기로 남은 글자가 한 번에 뜨면
/// 그 글자들에는 불리지 않으므로 소리도 거기서 멈춘다.</para>
///
/// <para><b>화자.</b> 줄의 화자 ID 는 <see cref="SpeakerStylePresenter.CurrentSpeakerId"/> 에서 받는다 — 같은 줄의
/// 서식을 입히는 쪽과 판정이 어긋나지 않게 한 곳에서 읽는다. 표에 없는 화자는 루다(루의 이름은 <c>{$이름}</c> 변수라
/// 고정 ID 가 없다 — SpeakerStylePresenter 와 같은 규칙).</para>
///
/// <para>소리는 <c>AudioManager</c> 에 등록된 <c>blip_*</c> 이며 음량은 설정의 「음성」(Voice)을 따른다.
/// 지금은 절차 합성 임시음 — 진짜 소리가 오면 프리팹 clip 만 갈아끼운다.</para>
/// </summary>
public class DialogueBlipHandler : ActionMarkupHandler
{
    [System.Serializable]
    public class Voice
    {
        [Tooltip("화자 ID (SpeakerStyle.json 의 id). 「루」는 미등록 화자 전체를 뜻한다.")]
        public string speakerId;
        [Tooltip("AudioManager 에 등록된 소리 이름. 비우면 이 화자는 무음.")]
        public string sound;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitch = 1f;
    }

    const string LuId = "루";
    const string NarrationId = "나레이션";

    [Tooltip("화자별 글자 소리. 정해지지 않은 셋(루독백 · 라디오 유 · 세라목소리)은 2026-10-06 임시값이다.")]
    [SerializeField] Voice[] voices =
    {
        new Voice { speakerId = LuId,         sound = "blip_lu",      volume = 1f   },
        new Voice { speakerId = "루독백",      sound = "blip_lu",      volume = 0.45f, pitch = 0.95f },
        new Voice { speakerId = "세라",        sound = "blip_sera",    volume = 1f   },
        new Voice { speakerId = "세라목소리",   sound = "blip_sera",    volume = 0.8f, pitch = 0.85f },
        new Voice { speakerId = "쿠루",        sound = "blip_kuru",    volume = 1f   },
        new Voice { speakerId = "솔",          sound = "blip_sol",     volume = 1f   },
        new Voice { speakerId = "radio_yu",   sound = "blip_radio",   volume = 0.9f },
        new Voice { speakerId = "미루",        sound = "blip_default", volume = 0.9f, pitch = 1.18f },
        new Voice { speakerId = "아모",        sound = "blip_default", volume = 0.9f, pitch = 1.08f },
        new Voice { speakerId = "부엉이",      sound = "blip_default", volume = 0.7f, pitch = 0.75f },
        new Voice { speakerId = NarrationId,  sound = "blip_default", volume = 0.5f },
    };

    [Tooltip("몇 글자마다 한 번 울릴지. 1 = 매 글자.")]
    [Min(1)] [SerializeField] int everyNthLetter = 2;

    [Tooltip("한 번 울릴 때마다 음높이를 이만큼 흔든다(±). 0 이면 고정.")]
    [Range(0f, 0.2f)] [SerializeField] float pitchJitter = 0.03f;

    /// <summary>글자 소리가 날 때 (화자 ID, 소리 이름)으로 발행된다. 배치 검증용.</summary>
    public static event System.Action<string, string> OnBlip;

    AudioSource _src;
    Voice _voice;
    AudioClip _clip;
    int _counted;

    void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;
        AudioManager.RegisterVoice(_src);
    }

    void OnDestroy() => AudioManager.UnregisterVoice(_src);

    public override void OnPrepareForLine(MarkupParseResult line, TMP_Text text) { }

    public override void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text)
    {
        _counted = 0;
        _voice = null;
        _clip = null;
    }

    public override YarnTask OnCharacterWillAppear(int currentCharacterIndex, MarkupParseResult line, CancellationToken cancellationToken)
    {
        // 화자는 첫 글자에서 정한다 — 이 시점이면 SpeakerStylePresenter 가 같은 줄을 이미 받았다.
        if (_voice == null) ResolveVoice();
        string text = line.Text;
        if (_clip == null || text == null || currentCharacterIndex >= text.Length) return YarnTask.CompletedTask;

        char c = text[currentCharacterIndex];
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c)) return YarnTask.CompletedTask;

        if (_counted++ % everyNthLetter != 0) return YarnTask.CompletedTask;

        _src.pitch = _voice.pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        _src.PlayOneShot(_clip, _voice.volume * AudioManager.MuffleFactor);
        OnBlip?.Invoke(_voice.speakerId, _voice.sound);
        return YarnTask.CompletedTask;
    }

    public override void OnLineDisplayComplete() { }

    public override void OnLineWillDismiss() { }

    void ResolveVoice()
    {
        string id = SpeakerStylePresenter.CurrentSpeakerId;
        if (string.IsNullOrWhiteSpace(id)) id = NarrationId;

        Voice fallback = null;
        _voice = null;
        foreach (var v in voices)
        {
            if (v.speakerId == id) { _voice = v; break; }
            if (v.speakerId == LuId) fallback = v;
        }
        // 표에 없는 화자 = 루({$이름}). 나레이션 등 등록된 무명 ID 는 위에서 이미 잡혔다.
        if (_voice == null && !SpeakerStylePresenter.IsMappedSpeaker(id)) _voice = fallback;
        if (_voice == null) { _voice = new Voice(); return; }   // 등록 화자인데 표에 없음 → 무음

        var am = AudioManager.Instance;
        if (am == null || !am.TryGetSound(_voice.sound, out _clip)) _clip = null;
    }
}
