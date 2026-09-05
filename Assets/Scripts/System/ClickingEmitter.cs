using System;
using UnityEngine;

/// <summary>
/// 딱딱 소리 발동기 (C-14-3-2 / 수치 F-6).
///
/// 루의 도자기 손가락이 저절로 내는 소리다. 세라 근처에서 발동하면 세라가 그쪽을 돌아본다.
/// 돌아보는 것까지가 발각 판정 전 단계이며, 판정 자체는 <see cref="SeraVision"/> 의 2초 규칙을 따른다.
///
/// ※ 루는 딱딱 소리를 억제하지 못한다. 자기 몸이 자기 말을 안 듣는 것이 인형화다.
///   억제 조작을 넣으면 이 의미가 죽는다 (C-14-3-2).
///
/// ⚠ 발동은 '게임 이벤트' 이고 소리는 그 표현일 뿐이다.
///   AudioManager.PlayClicking 은 접근성 설정(clickingSoundDisabled)으로 무음이 될 수 있는데,
///   발동 자체를 소리에 묶으면 그 설정을 켠 플레이어만 이유 없이 안전해진다.
/// </summary>
public class ClickingEmitter : MonoBehaviour
{
    [Header("발동 주기 (F-6 초안값)")]
    [Tooltip("인형화 0~30 구간. 데모 기본 구간이다. " +
             "F-6 「90초당 1회」 — 순찰 1라운드 110초당 1회꼴이라 " +
             "C-7-2 의 「마을은 라운드당 1~2회」와 맞는다. " +
             "⛔ 옛 값 60초로 되돌리지 말 것 (F v1.18 개정).")]
    public float intervalAutonomy = 90f;
    [Tooltip("인형화 31~60 구간. 31 돌파 시 체감 난도가 눈에 띄게 오른다. " +
             "F-6 「40초당 1회」. ⛔ 옛 값 25초로 되돌리지 말 것 (F v1.18 개정).")]
    public float intervalCrack = 40f;

    [Header("세라 반응")]
    [Tooltip("이 거리 안에서 발동하면 세라가 돌아본다(유닛). 시야 거리보다 넉넉하게 잡는다.")]
    public float attentionRadius = 3.375f;

    [Header("소리")]
    [Tooltip("AudioManager 등록 이름. 등록돼 있으면 그 클립을 쓰고, 없으면 아래 절차 생성음으로 대신한다.")]
    public string sfxClickingName = "ceramic_tap";

    [Header("절차 생성음 (에셋이 없을 때)")]
    [Tooltip("도자기가 부딪히는 기본 주파수(Hz). 배음은 비화성적으로 얹는다 — 그래야 나무나 금속이 아니라 도자기로 들린다.")]
    public float tapHz = 2100f;
    [Tooltip("소리가 잦아드는 시간(초). 도자기는 짧고 마르게 끝난다.")]
    public float tapDecay = 0.09f;

    /// <summary>딱딱이 발동했을 때 발행된다. 인자는 발동 위치.</summary>
    public static event Action<Vector3> OnClicked;

    float _timer;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < CurrentInterval) return;

        _timer = 0f;
        Emit();
    }

    /// <summary>인형화 구간에 따른 현재 발동 주기.</summary>
    float CurrentInterval
    {
        get
        {
            float corruption = CorruptionManager.Instance != null
                ? CorruptionManager.Instance.currentCorruption : 0f;

            // 구간 경계는 CorruptionManager 가 단일 출처다. 여기에 숫자를 적지 않는다(CLAUDE.md §2).
            return CorruptionManager.GetStage(corruption) == CorruptionStage.Autonomy
                ? intervalAutonomy : intervalCrack;
        }
    }

    void Emit()
    {
        Vector3 pos = transform.position;

        // 소리는 표현일 뿐 — 설정으로 무음이 되어도 아래 주의 전환은 그대로 일어난다.
        PlayTap();

        OnClicked?.Invoke(pos);

        // 세라가 그쪽을 돌아본다.
        var patrol = SeraPatrol.Instance;
        if (patrol == null) return;

        if (Vector3.Distance(patrol.transform.position, pos) <= attentionRadius)
            patrol.LookToward(pos);
    }

    /// <summary>
    /// 등록된 클립이 있으면 그것을, 없으면 절차 생성음을 낸다.
    ///
    /// ⚠ 소리가 전혀 안 나면 플레이어는 <b>자기가 소리를 냈다는 사실 자체를 모른다.</b>
    ///   세라가 돌아보는 이유를 알 수 없으니 마을 스텔스가 운으로 바뀐다.
    ///   집 구간의 저음이 같은 이유로 절차 생성 폴백을 갖고 있다
    ///   (HouseEscapePressureController.BuildDroneClip).
    ///
    /// ⚠ 이름을 지어내는 것이 아니다(CLAUDE.md §0-4). <see cref="sfxClickingName"/> 이
    ///   실제로 등록되면 그때부터 그 클립을 쓴다. 폴백은 그때까지의 임시 소리다.
    /// </summary>
    void PlayTap()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        if (am.HasSound(sfxClickingName)) { am.PlayClicking(sfxClickingName); return; }

        if (_fallback == null) _fallback = BuildTapClip();
        am.PlayClicking(_fallback);
    }

    AudioClip _fallback;

    /// <summary>
    /// 도자기 손가락이 부딪히는 짧은 소리를 만든다.
    /// 배음을 1 : 1.63 : 2.34 로 얹는다 — 정수배가 아니어야 금속·나무가 아닌 도자기로 들린다.
    /// </summary>
    AudioClip BuildTapClip()
    {
        const int rate = 44100;
        float dur = Mathf.Max(0.02f, tapDecay * 2.2f);
        int samples = Mathf.RoundToInt(rate * dur);
        var data = new float[samples];

        float f0 = Mathf.Max(200f, tapHz);
        float tau = Mathf.Max(0.005f, tapDecay);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t / tau);
            data[i] = (Mathf.Sin(2f * Mathf.PI * f0 * t)
                     + Mathf.Sin(2f * Mathf.PI * f0 * 1.63f * t) * 0.55f * Mathf.Exp(-t / (tau * 0.6f))
                     + Mathf.Sin(2f * Mathf.PI * f0 * 2.34f * t) * 0.30f * Mathf.Exp(-t / (tau * 0.4f)))
                     * env * 0.28f;
        }

        // 시작을 아주 짧게 띄워 클릭 잡음을 없앤다.
        int fade = Mathf.Min(64, samples);
        for (int i = 0; i < fade; i++) data[i] *= (float)i / fade;

        var clip = AudioClip.Create("ClickingTap (auto)", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
