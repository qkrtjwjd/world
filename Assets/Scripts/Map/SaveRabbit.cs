using UnityEngine;

/// <summary>
/// 그림자 토끼 — 월드에서 저장을 여는 매개다 (CLAUDE.md §8).
///
/// <para>슬롯을 코드가 고르지 않는다. <see cref="PauseSystem.OpenSave"/> 로 저장 패널을 열어
/// 사람이 슬롯 3개 중에서 고른다. 실제 저장과 「중단 저장 삭제」는 <c>SaveManager.SaveGame</c> 가 한다.</para>
///
/// <para>붙이는 법: 같은 오브젝트의 <c>InteractionTrigger.onInteract</c> 에 이 컴포넌트의
/// <see cref="Save"/> 를 연결한다. E키 판정·범위·프롬프트는 InteractionTrigger 가 이미 갖고 있다.</para>
///
/// <para>⚠ 정본 F-145 — 토끼는 <b>필터 무관·광원 무관</b>이다. SpriteRenderer 머티리얼을
/// Sprite-Unlit-Default 로 두어야 환상·현실에서 같게 보이고 주변 광원을 타지 않는다.</para>
/// </summary>
public class SaveRabbit : MonoBehaviour
{
    [Tooltip("저장 패널을 여는 PauseSystem. 비워 두면 씬에서 찾는다.")]
    public PauseSystem pauseSystem;

    /// <summary>InteractionTrigger.onInteract 에 연결한다.</summary>
    public void Save()
    {
        var ps = pauseSystem != null
            ? pauseSystem
            : FindAnyObjectByType<PauseSystem>(FindObjectsInactive.Include);

        if (ps == null)
        {
            Debug.LogWarning("[SaveRabbit] PauseSystem 을 찾지 못해 저장 패널을 열 수 없습니다. " +
                             "씬에 PauseSystem 이 있는지 확인하세요.", this);
            return;
        }

        ps.OpenSave();
    }
}
