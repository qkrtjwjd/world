using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 턴제 전투의 무대 바닥판. 뒤에 있는 필드(MapScene)를 완전히 가린다.
    ///
    /// <para><b>왜 UI 가 아니라 월드에 세우는가.</b> BattleUI 는 Screen Space - Overlay 캔버스라
    /// 월드에 그려지는 것 전부를 덮는다. 그런데 전투 중인 적은 UI 가 아니라 월드의
    /// <see cref="SpriteRenderer"/> 다(<c>SpawnUnit</c> 이 씬 루트에 세운다). 그래서 캔버스 쪽
    /// 배경을 불투명하게 만들면 필드와 함께 적까지 사라진다. 바닥판은 필드 위·적 아래,
    /// 즉 월드의 정렬 순서 사이에 끼워야 한다.</para>
    ///
    /// <para>정렬 순서는 <see cref="SortingOrder"/> = 500. MapScene 의 스프라이트는 최대 100,
    /// 전투용 적 클론은 <see cref="EnemySortingOrder"/> = 600 으로 올려 세운다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleBackdrop : MonoBehaviour
    {
        public const int SortingOrder      = 500;
        public const int EnemySortingOrder = 600;

        /// <summary>화면 밖까지 넉넉히 덮는다. 카메라가 조금 움직여도 가장자리가 새지 않게.</summary>
        const float Margin = 1.35f;

        SpriteRenderer _sr;
        Camera         _cam;

        /// <summary>카메라 앞에 바닥판을 세운다. 이미 있으면 그것을 돌려준다.</summary>
        public static BattleBackdrop Create(Color color)
        {
            Camera cam = Camera.main;
            if (cam == null) return null;

            var go = new GameObject("BattleBackdrop");
            go.transform.SetParent(cam.transform, worldPositionStays: false);
            // 카메라 앞 — 직교 카메라라 z 값은 정렬에 영향을 주지 않지만, 클리핑 안쪽에 둔다.
            go.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(1f, cam.nearClipPlane + 1f));
            go.transform.localRotation = Quaternion.identity;

            var bd = go.AddComponent<BattleBackdrop>();
            bd._cam = cam;
            bd._sr  = go.AddComponent<SpriteRenderer>();

            // 1x1 흰 스프라이트를 코드로 만든다 — 에셋 참조 없이 어느 씬에서나 뜬다.
            bd._sr.sprite = Sprite.Create(
                Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            bd._sr.color        = color;
            bd._sr.sortingOrder = SortingOrder;

            bd.Fit();
            return bd;
        }

        void LateUpdate() { Fit(); }

        /// <summary>직교 카메라의 화면 크기에 맞춰 늘린다. 해상도가 바뀌어도 따라간다.</summary>
        void Fit()
        {
            if (_cam == null || _sr == null) return;
            if (!_cam.orthographic) return;   // 원근 카메라는 이 방식이 성립하지 않는다

            float h = _cam.orthographicSize * 2f * Margin;
            float w = h * _cam.aspect;
            transform.localScale = new Vector3(w, h, 1f);
        }
    }
}
