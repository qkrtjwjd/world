using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 키보드·패드로 선택이 옮겨갈 때 그 항목이 보이도록 <see cref="ScrollRect"/> 를 따라 움직입니다.
/// </summary>
/// <remarks>
/// <para>휠 스크롤은 <c>ScrollRect</c> 가 알아서 하지만 <b>선택 이동은 따라오지 않는다.</b>
/// 뷰포트 밖 항목이 선택되면 화면에는 아무 변화가 없어 조작이 멈춘 것처럼 보인다.
/// 인벤토리 격자(6열 × 8행)가 그런 경우다.</para>
///
/// <para><b>선택이 바뀐 프레임에만</b> 움직인다. 매 프레임 위치를 강제하지 않으므로
/// 사용자가 휠로 굴리는 것을 방해하지 않는다.</para>
///
/// <para>세로 스크롤만 다룬다. 이 프로젝트의 스크롤은 전부 세로다.</para>
/// </remarks>
[RequireComponent(typeof(ScrollRect))]
public class ScrollToSelected : MonoBehaviour
{
    [Tooltip("항목이 가장자리에 딱 붙지 않도록 두는 여백(기준 해상도 640x360 기준 픽셀)")]
    public float padding = 4f;

    ScrollRect _scroll;
    GameObject _last;

    readonly Vector3[] _viewCorners = new Vector3[4];
    readonly Vector3[] _itemCorners = new Vector3[4];

    ScrollRect Scroll => _scroll != null ? _scroll : (_scroll = GetComponent<ScrollRect>());

    void OnDisable() => _last = null;   // 다시 열었을 때 첫 선택도 잡아 준다

    void LateUpdate()
    {
        if (Scroll == null || Scroll.content == null) return;
        if (EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) { _last = null; return; }
        if (selected == _last) return;                       // 선택이 그대로면 손대지 않는다
        if (!selected.transform.IsChildOf(Scroll.content)) return;

        _last = selected;

        var item = selected.transform as RectTransform;
        if (item != null) Reveal(item);
    }

    void Reveal(RectTransform item)
    {
        RectTransform view = Scroll.viewport != null ? Scroll.viewport : (RectTransform)transform;
        RectTransform content = Scroll.content;

        float scrollable = content.rect.height - view.rect.height;
        if (scrollable <= 0f) return;                        // 다 보이면 움직일 이유가 없다

        view.GetWorldCorners(_viewCorners);
        item.GetWorldCorners(_itemCorners);

        // 뷰포트 로컬 좌표로 옮겨 위/아래를 비교한다 (인덱스 0 = 좌하, 1 = 좌상)
        float viewBottom = view.InverseTransformPoint(_viewCorners[0]).y + padding;
        float viewTop    = view.InverseTransformPoint(_viewCorners[1]).y - padding;
        float itemBottom = view.InverseTransformPoint(_itemCorners[0]).y;
        float itemTop    = view.InverseTransformPoint(_itemCorners[1]).y;

        float delta;
        if (itemTop > viewTop)          delta = itemTop - viewTop;         // 위로 벗어났다
        else if (itemBottom < viewBottom) delta = itemBottom - viewBottom; // 아래로 벗어났다
        else return;                                                       // 이미 보인다

        // content 는 위가 고정(pivot y = 1)이라 y 가 커질수록 아래쪽 내용이 보인다.
        Vector2 pos = content.anchoredPosition;
        pos.y = Mathf.Clamp(pos.y - delta, 0f, scrollable);
        content.anchoredPosition = pos;
    }
}
