using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// S#08 부엌 서랍 — 작고 낡은 열쇠(다락방 열쇠) 획득.
/// InteractionTrigger.onInteract 에 BeginCutscene() 을 연결하세요.
///
/// ⚠ 정본 규약 (D 정본 S#08)
///   - 「녹슨 열쇠」는 루가 8살 때 만든 쉼터 열쇠 전용 명칭이다(D 문단 328). 이것은 「다락방 열쇠」다.
///   - 지문 한 줄("부엌 서랍 안에 작고 낡은 열쇠가 나온다. 다락방 열쇠.")은 **화면에 나오지 않는다.**
///     v3 화이트리스트가 지문 전체를 표시 금지로 본다(House_Kitchen_Drawer 주석 · node_map S#08 = 0줄).
///     루가 이것이 다락방 열쇠라는 것을
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

    [Header("서랍 안 클로즈업 (D S#08 문단 324 · 433)")]
    [Tooltip("전체화면 클로즈업을 띄울 Image (Canvas). 아래 스프라이트가 있을 때만 쓴다.")]
    public Image handCloseupImage;
    [Tooltip("「부엌 서랍 — 열린 상태 내부 클로즈업」. 잡동사니 사이 열쇠 하나를 **구석에** 그려 넣는다\n" +
             "(카메라가 열쇠를 중앙에 두지 않는다 — 플레이어가 먼저 찾아내게). 비우면 월드 카메라 클로즈업으로 대신한다.")]
    public Sprite drawerInteriorSprite;
    [Tooltip("⛔ 쓰지 않는다 — 2026-09-27 카메라 대체 클로즈업 폐기(E-64).")]
    public Transform drawerCloseupTarget;

    // 2026-09-27: 예전에는 handCloseupImage 를 스프라이트 확인 없이 알파 1 로 띄웠다.
    //   그 Image 는 S#03 도자기 손가락과 공용이고 스프라이트가 비어 있어(흰색) 서랍을 열면
    //   1.5초간 화면 전체가 흰 사각형으로 덮였다. 스프라이트가 있을 때만 띄운다.
    //   같은 날 넣었던 「그림이 없으면 월드 카메라 클로즈업」은 줌 폐기(E-64)로 걷어냈다.
    //   서랍 안은 오버레이 컷(house_overlay_*, F-3-9)으로 옮길 자리다 — 매핑표 확정 대기.

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

        bool useImage   = handCloseupImage != null && drawerInteriorSprite != null;
        if (useImage)
        {
            handCloseupImage.sprite = drawerInteriorSprite;
            yield return StartCoroutine(FadeInImage(handCloseupImage, 1f, 0.3f));
        }
        // 그림이 없으면 카메라로 대신하지 않는다 — 클로즈업은 카메라가 아니라 오버레이 컷이다(개정 D 문단 336 · E-64).

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

        if (useImage)
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
