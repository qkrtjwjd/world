using UnityEngine;

/// <summary>
/// 라디오 키 (E-75-5 · C-16-7 · F-8-4 · F-2-5). 키는 Q.
///
/// 조사 범위 안에 라디오 반응 대상이 있으면 유의 목소리 한 줄(화자 radio_yu), 없으면 잡음 SFX 만 낸다 —
/// 대화창을 띄우지 않는다. 조사 결과 뒤에 자동으로 붙지 않는다. 라디오 획득(S#11) 이후 탐험 중에만 받으며
/// 전투 · 컷신(입력 잠금) · 대화창 표시 중에는 반응하지 않는다. 필터 · 인형화를 참조하지 않는다.
///
/// 반응 대상을 표시하지 않는다(C-16-9) — 어디서나 누를 수 있고 기본 반응이 잡음이므로
/// 어떤 물건에 반응이 있는지를 UI 가 먼저 알려주지 않는다(E-75-5 근거).
///
/// 조사 물건이 없는 마을 · 숲에서도 잡음이 나야 하므로 조사 매니저와 따로 상시 둔다.
/// </summary>
public class RadioKey : MonoBehaviour
{
    public const KeyCode Key = KeyCode.Q;

    /// <summary>잡음 SFX(AudioManager 등록 이름). S#11 지지직 잡음과 같은 계열의 짧은 판 — 임시 합성음(🔴).</summary>
    public const string NoiseSfx = "radio_noise_short";

    const string ItemRadio = "라디오";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("RadioKey [Auto]");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<RadioKey>();
    }

    float _cooldown;

    void Update()
    {
        if (_cooldown > 0f) { _cooldown -= Time.unscaledDeltaTime; return; }
        if (!Input.GetKeyDown(Key)) return;
        if (!CanUse()) return;

        _cooldown = 0.4f;
        var lu = LuPose.Lu;
        var target = lu != null ? InspectableObjectManager.FindRadioTarget(lu.position) : null;
        if (target != null)
            StartCoroutine(target.PlayRadio());
        else
            AudioManager.Instance?.Play(NoiseSfx);
    }

    static bool CanUse()
    {
        if (Time.timeScale == 0f) return false;
        if (YarnDialogue.IsRunning) return false;
        if (OverlayCut.IsOpen || ReadableOverlay.IsOpen) return false;
        if (PlayerInputLock.Instance != null && PlayerInputLock.Instance.IsLocked) return false;
        if (BattleSystem.IsActive || HackSlashCombatManager.IsActive) return false;
        var inv = InventoryManager.Instance;
        return inv != null && inv.HasItem(ItemRadio);
    }
}
