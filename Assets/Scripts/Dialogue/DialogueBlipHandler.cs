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

    /// <summary>한 줄의 글자 소리 상태. 다른 타이프라이터(전투 동료 대사)도 <see cref="BeginLine"/> 으로 받아 쓴다.</summary>
    public sealed class LineState
    {
        internal Voice voice;
        internal AudioClip clip;
        internal int counted;
    }

    // 소리는 DialoguePanel 이 아니라 따로 둔 상시 오브젝트에서 낸다. 대화가 끝나면 YarnCommandBridge 가 패널을 꺼서,
    // 패널의 AudioSource 로는 전투 중 대사(필드 대화창이 꺼진 채 도는 줄)에 소리를 낼 수 없다.
    AudioSource _src;
    LineState _line;

    AudioSource Source
    {
        get
        {
            if (_src != null) return _src;
            var go = new GameObject("DialogueBlipAudio");
            DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            AudioManager.RegisterVoice(_src);
            return _src;
        }
    }

    void OnDestroy()
    {
        if (_src == null) return;
        AudioManager.UnregisterVoice(_src);
        Destroy(_src.gameObject);
    }

    public override void OnPrepareForLine(MarkupParseResult line, TMP_Text text) { }

    public override void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text) => _line = null;

    public override YarnTask OnCharacterWillAppear(int currentCharacterIndex, MarkupParseResult line, CancellationToken cancellationToken)
    {
        // 화자는 첫 글자에서 정한다 — 이 시점이면 SpeakerStylePresenter 가 같은 줄을 이미 받았다.
        _line ??= BeginLine(SpeakerStylePresenter.CurrentSpeakerId);
        string text = line.Text;
        if (text != null && currentCharacterIndex < text.Length) Letter(_line, text[currentCharacterIndex]);
        return YarnTask.CompletedTask;
    }

    public override void OnLineDisplayComplete() { }

    public override void OnLineWillDismiss() { }

    /// <summary>화자 ID 로 한 줄을 시작한다. null · 공백 = 나레이션, 표에 없는 화자 = 루.</summary>
    public LineState BeginLine(string speakerId)
    {
        string id = string.IsNullOrWhiteSpace(speakerId) ? NarrationId : speakerId;

        Voice fallback = null, voice = null;
        foreach (var v in voices)
        {
            if (v.speakerId == id) { voice = v; break; }
            if (v.speakerId == LuId) fallback = v;
        }
        // 표에 없는 화자 = 루({$이름}). 나레이션 등 등록된 무명 ID 는 위에서 이미 잡혔다.
        if (voice == null && !SpeakerStylePresenter.IsMappedSpeaker(id)) voice = fallback;

        var st = new LineState { voice = voice };
        var am = AudioManager.Instance;
        if (voice != null && am != null && am.TryGetSound(voice.sound, out var clip)) st.clip = clip;
        return st;   // voice 나 clip 이 없으면 무음 줄
    }

    /// <summary>글자 하나가 화면에 나타날 때 부른다. 공백 · 문장부호는 건너뛰고 N 글자마다 한 번 울린다.</summary>
    public void Letter(LineState st, char c)
    {
        if (st?.clip == null) return;
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c)) return;
        if (st.counted++ % everyNthLetter != 0) return;

        var src = Source;
        src.pitch = st.voice.pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        src.PlayOneShot(st.clip, st.voice.volume * AudioManager.MuffleFactor);
        OnBlip?.Invoke(st.voice.speakerId, st.voice.sound);
    }
}
