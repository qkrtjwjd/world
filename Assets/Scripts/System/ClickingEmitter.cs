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
    [Tooltip("인형화 0~30 구간. 데모 기본 구간이다.")]
    public float intervalAutonomy = 60f;
    [Tooltip("인형화 31~60 구간. 31 돌파 시 체감 난도가 눈에 띄게 오른다.")]
    public float intervalCrack = 25f;

    [Header("세라 반응")]
    [Tooltip("이 거리 안에서 발동하면 세라가 돌아본다(유닛). 시야 거리보다 넉넉하게 잡는다.")]
    public float attentionRadius = 3.375f;

    [Header("소리")]
    [Tooltip("AudioManager 등록 이름. 비우면 무음이지만 발동 자체는 그대로 일어난다.")]
    public string sfxClickingName = "ceramic_tap";

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
        if (!string.IsNullOrEmpty(sfxClickingName))
            AudioManager.Instance?.PlayClicking(sfxClickingName);

        OnClicked?.Invoke(pos);

        // 세라가 그쪽을 돌아본다.
        var patrol = SeraPatrol.Instance;
        if (patrol == null) return;

        if (Vector3.Distance(patrol.transform.position, pos) <= attentionRadius)
            patrol.LookToward(pos);
    }
}
