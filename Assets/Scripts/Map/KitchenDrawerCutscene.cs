using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// S#08 부엌 서랍 — 작고 녹슨 열쇠(다락방 열쇠) 획득.
/// InteractionTrigger.onInteract 에 BeginCutscene() 을 연결하세요.
///
/// ⚠ 정본 규약 (2026-08-07 D 정본 S#08)
///   - 지문 한 줄("부엌 서랍 안에 작고 녹슨 열쇠가 나온다. 다락방 열쇠.")은 화면에 나온다.
///     정본이 금지한 것은 **설명**이지 지문이 아니다 — 루가 이것이 다락방 열쇠라는 것을
///     어떻게 아는지는 설명하지 않는다. 이 집에서 27년을 살았다. 열쇠 하나가 어디 것인지는 안다.
///     한 번도 열어본 적이 없을 뿐이다. (구 House_Kitchen_key 의 "어떻게 알았지?" 는 폐기됨)
///   - 카메라가 열쇠를 중앙에 두지 않는다. 살짝 구석에 두고 플레이어가 먼저 찾아내게 한다.
///   - 스프라이트: 환상 필터의 매끄러운 색조 안에서 이것만 질감이 거칠다.
///     각설탕이 '밖에서 들어온 것'이라면 이 열쇠는 '안에서 오래 방치된 것'이다.
///     채도를 올리지 말고 떨어뜨린다.
///   - 세라가 이 열쇠를 숨기지 않았다는 점이 중요하다. 금지도 은닉도 없었다.
/// </summary>
public class KitchenDrawerCutscene : MonoBehaviour
{
    public static KitchenDrawerCutscene Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("Yarn 노드 이름")]
    public string yarnNode = "House_Kitchen_Drawer";

    [Header("루 손 클로즈업 Image (Canvas)")]
    public Image handCloseupImage;

    [Header("효과음")]
    public AudioClip sfxDrawerOpen;
    public AudioClip sfxItemsRattle;

    [Header("획득 아이템")]
    public ItemData atticKeyItem;

    [Header("목표 갱신")]
    public string objectiveHeader = "목표 갱신";
    public string objectiveBody   = "다락방 열쇠를 사용하세요.";

    public void BeginCutscene()
    {
        if (GameState.isAtticKeyFound) return;
        GameState.isAtticKeyFound = true;
        StartCoroutine(PlayCutscene());
    }

    IEnumerator PlayCutscene()
    {
        var ctrl = YarnDialogue.LockPlayer();

        if (handCloseupImage != null)
            yield return StartCoroutine(FadeInImage(handCloseupImage, 1f, 0.3f));

        AudioManager.Instance?.Play(sfxDrawerOpen);
        yield return new WaitForSeconds(0.5f);

        AudioManager.Instance?.Play(sfxItemsRattle);
        yield return new WaitForSeconds(0.5f);

        // PlayIfExists 를 쓴다. S#08 은 정본상 대사가 0줄이라(node_map 기대값 0) 이 노드의
        // 본문이 주석만 남고, Yarn 컴파일러는 실행문이 없는 노드를 산출물에서 제외한다.
        // PlayAndWait 로 부르면 StartDialogue 가 없는 노드를 찾아 에러를 남긴다.
        // 열쇠 획득과 목표 갱신은 아래에서 계속 진행되어야 하므로 여기서 조용히 건너뛴다.
        if (!string.IsNullOrEmpty(yarnNode))
            yield return YarnDialogue.PlayIfExists(yarnNode);

        if (atticKeyItem != null)
            InventoryManager.Instance?.AddItem(atticKeyItem);

        if (handCloseupImage != null)
            yield return StartCoroutine(FadeOutImage(handCloseupImage, 0.3f));

        ObjectiveManager.Instance?.ShowObjective(objectiveHeader, objectiveBody);

        YarnDialogue.UnlockPlayer(ctrl);
    }

    IEnumerator FadeInImage(Image image, float targetAlpha, float duration)
    {
        Color c = image.color;
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, targetAlpha, elapsed / duration);
            image.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        image.color = c;
    }

    IEnumerator FadeOutImage(Image image, float duration)
    {
        Color c = image.color;
        float start = c.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(start, 0f, elapsed / duration);
            image.color = c;
            yield return null;
        }
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(false);
    }
}
