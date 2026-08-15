using System.Collections;
using UnityEngine;

/// <summary>
/// 빵집 NPC. 플레이어 진입 시 자동 루프 대사 재생.
/// 첫 방문 시($빵반죽_획득 == false) Village_Bakery 스토리 1회 재생 후 루프 전환.
/// 단검 파지 중에는 마네킹 스프라이트 + 저음 변질 대사 버전 사용.
/// </summary>
public class BakeryNPC : MonoBehaviour
{
    [Header("스프라이트")]
    [SerializeField] private SpriteRenderer npcRenderer;
    [SerializeField] private Sprite         normalSprite;
    [SerializeField] private Sprite         daggerSprite;

    [Header("Yarn 노드")]
    [SerializeField] private string storyNode      = "Village_Bakery";
    [SerializeField] private string loopNodeNormal = "BakeryNPC_Loop_Normal";
    [SerializeField] private string loopNodeDagger = "BakeryNPC_Loop_Dagger";

    [Header("설정")]
    [SerializeField] private float loopInterval = 4f;

    private bool      _playerNear;
    private Coroutine _loopRoutine;

    void Update()
    {
        if (!_playerNear || npcRenderer == null) return;
        npcRenderer.sprite = DaggerSystem.IsEquipped ? daggerSprite : normalSprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        _playerNear = true;

        bool storyDone = FlagManager.Instance != null && FlagManager.Instance.GetFlag("빵반죽_획득");
        if (!storyDone)
        {
            _loopRoutine = StartCoroutine(PlayStoryThenLoop());
            return;
        }
        _loopRoutine = StartCoroutine(DialogueLoop());
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        _playerNear = false;
        if (_loopRoutine != null) { StopCoroutine(_loopRoutine); _loopRoutine = null; }
        if (npcRenderer != null && normalSprite != null)
            npcRenderer.sprite = normalSprite;
    }

    IEnumerator PlayStoryThenLoop()
    {
        yield return YarnDialogue.PlayAndWait(storyNode, true);
        if (_playerNear)
            _loopRoutine = StartCoroutine(DialogueLoop());
    }

    IEnumerator DialogueLoop()
    {
        while (_playerNear)
        {
            if (!YarnDialogue.IsRunning)
            {
                string node = DaggerSystem.IsEquipped ? loopNodeDagger : loopNodeNormal;
                yield return YarnDialogue.PlayAndWait(node, false);
            }
            yield return new WaitForSeconds(loopInterval);
        }
    }
}
