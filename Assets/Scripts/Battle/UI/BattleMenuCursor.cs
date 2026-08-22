using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Battle.UI
{
    /// <summary>
    /// 전투 커맨드 앞에 붙는 선택 커서(▶).
    ///
    /// <para>버튼 배경만 밝게 바꾸는 방식(Unity Button 의 ColorTint)은 글자 색까지 못 바꿔서
    /// 흰 배경에 흰 글자가 되어 버린다. 커서를 옮기는 쪽이 싸고 확실하다 — 언더테일의 하트,
    /// 드퀘·포켓몬의 ▶ 가 같은 방식이다.</para>
    ///
    /// <para>커서는 EventSystem 이 지금 고른 오브젝트를 따라간다. 마우스로 눌러도 키보드로
    /// 옮겨도 같은 값을 보므로 입력 방식을 가리지 않는다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleMenuCursor : MonoBehaviour
    {
        [Tooltip("커서 글자. 비워 두면 자기 자신에서 찾는다.")]
        public TextMeshProUGUI cursorText;

        [Tooltip("선택된 버튼의 왼쪽 변에서 이 값만큼 더 왼쪽에 놓는다.")]
        public float gap = 28f;

        RectTransform _rt;
        RectTransform _parent;

        void Awake()
        {
            _rt     = transform as RectTransform;
            _parent = transform.parent as RectTransform;
            if (cursorText == null) cursorText = GetComponent<TextMeshProUGUI>();
            Hide();
        }

        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;

            // 선택이 없거나, 꺼졌거나, 버튼이 아니면 커서를 숨긴다.
            if (sel == null || !sel.activeInHierarchy)
            {
                Hide();
                return;
            }

            var target = sel.transform as RectTransform;
            if (target == null || _rt == null || _parent == null)
            {
                Hide();
                return;
            }

            // 대상의 왼쪽 변 중앙을 커서 부모의 좌표계로 옮긴다.
            // 커맨드가 HorizontalLayoutGroup 으로 배치돼 폭이 런타임에 정해지므로
            // anchoredPosition 을 그대로 읽으면 안 되고 월드 코너를 거쳐야 한다.
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);        // 0 좌하 · 1 좌상 · 2 우상 · 3 우하
            Vector3 leftMid = (corners[0] + corners[1]) * 0.5f;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent, RectTransformUtility.WorldToScreenPoint(null, leftMid), null, out local);

            // local 은 부모 피벗을 원점으로 한 좌표이고, anchoredPosition 은 앵커를 원점으로 한다.
            // 둘이 같다고 가정하면 안 된다 — Canvas 는 런타임에 루트 RectTransform 의 피벗을
            // 프리팹에 저장된 값과 다르게 잡는다(프리팹은 (0,0) 인데 런타임은 (0.5,0.5) 였다).
            // 그래서 앵커가 피벗에서 얼마나 떨어져 있는지를 매번 빼 준다.
            Rect    pr     = _parent.rect;
            Vector2 anchor = (_rt.anchorMin + _rt.anchorMax) * 0.5f;
            Vector2 anchorInLocal = new Vector2(
                (anchor.x - _parent.pivot.x) * pr.width,
                (anchor.y - _parent.pivot.y) * pr.height);

            _rt.anchoredPosition = new Vector2(local.x - gap, local.y) - anchorInLocal;
            Show();
        }

        void Show()
        {
            if (cursorText != null && !cursorText.enabled) cursorText.enabled = true;
        }

        void Hide()
        {
            if (cursorText != null && cursorText.enabled) cursorText.enabled = false;
        }
    }
}
