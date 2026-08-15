using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인형화 수치에 따라 거울 속 루 스프라이트를 분기하고 대사를 출력합니다.
/// Collider2D가 붙은 거울 오브젝트에 부착하세요.
/// </summary>
public class TruthMirrorObject : MonoBehaviour
{
    [Header("거울 이미지")]
    [SerializeField] private Image mirrorImage;
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("인형화 구간별 스프라이트")]
    [SerializeField] private Sprite humanSprite;      // 0~30%
    [SerializeField] private Sprite crackedSprite;    // 31~70%
    [SerializeField] private Sprite porcelainSprite;  // 71~99%

    [Header("구간별 대사")]
    [SerializeField] private string humanMonologue     = "...그래도 아직은 나다.";
    [SerializeField] private string crackedMonologue   = "...관절이 삐걱거리는 것 같아.";
    [SerializeField] private string porcelainMonologue = "...저게 나인가. 웃고 있는데 눈이 비어있어.";

    [Header("카메라 클로즈업 설정")]
    [SerializeField] private Transform closeUpTarget;    // null이면 this.transform
    [SerializeField] private float     closeUpDuration = 2.5f;
    [SerializeField] private float     closeUpZoom     = 2.5f;

    private bool      _isPlayerNear;
    private bool      _isOpen;
    private Coroutine _sequence;

    void Awake()
    {
        if (mirrorImage != null)
        {
            mirrorImage.gameObject.SetActive(false);
            SetAlpha(0f);
        }
    }

    void Update()
    {
        KeyCode daggerKey = SettingsManager.Instance?.keyDagger ?? KeyCode.F;
        if (!_isPlayerNear || !Input.GetKeyDown(daggerKey)) return;
        if (YarnDialogue.IsRunning) return;
        if (!DaggerKeyRegistry.IsClosest(this)) return;
        if (_isOpen) CloseSequence();
        else         OpenSequence();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = true;
        DaggerKeyRegistry.Register(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = false;
        DaggerKeyRegistry.Unregister(this);
        if (_isOpen) CloseSequence();
    }

    void OnDisable()
    {
        DaggerKeyRegistry.Unregister(this);
    }

    void OpenSequence()
    {
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = StartCoroutine(DoMirrorSequence());
    }

    void CloseSequence()
    {
        if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
        _isOpen = false;

        if (mirrorImage != null) mirrorImage.gameObject.SetActive(false);
        InteractionTextUI.Instance?.Hide();
        CameraDirector.Instance?.RestoreDefault();
        PlayerInputLock.Instance?.Unlock();
    }

    IEnumerator DoMirrorSequence()
    {
        _isOpen = true;
        PlayerInputLock.Instance?.Lock();

        float doll = PuppetizationManager.Instance != null
            ? PuppetizationManager.Instance.GetValue()
            : 0f;

        Sprite sprite;
        string monologue;

        switch (CorruptionManager.GetStage(doll))
        {
            case CorruptionStage.Autonomy:
                sprite = humanSprite;   monologue = humanMonologue;    break;
            case CorruptionStage.Crack:
                sprite = crackedSprite; monologue = crackedMonologue;  break;
            default: // Backfire, Loss, Doll
                // TODO: 데모 이후 backfireSprite / lossSprite 전용 에셋 추가
                sprite = crackedSprite; monologue = porcelainMonologue; break;
        }

        // 카메라 클로즈업
        Transform target = closeUpTarget != null ? closeUpTarget : transform;
        CameraDirector.Instance?.TriggerCloseUp(target, closeUpDuration, closeUpZoom);

        yield return new WaitForSeconds(0.35f); // 줌 도입부 대기

        // 거울 스프라이트 페이드인
        if (mirrorImage != null && sprite != null)
        {
            mirrorImage.sprite = sprite;
            mirrorImage.gameObject.SetActive(true);
            SetAlpha(0f);
            yield return StartCoroutine(FadeImage(1f, fadeInDuration));
        }

        InteractionTextUI.Instance?.Show(monologue);

        // CameraDirector의 closeUpDuration 종료까지 대기
        float remaining = closeUpDuration - 0.35f;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        CloseSequence();
    }

    IEnumerator FadeImage(float targetAlpha, float duration)
    {
        float start   = mirrorImage.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(start, targetAlpha, elapsed / duration));
            yield return null;
        }
        SetAlpha(targetAlpha);
    }

    void SetAlpha(float a)
    {
        if (mirrorImage == null) return;
        Color c = mirrorImage.color;
        c.a = a;
        mirrorImage.color = c;
    }
}
