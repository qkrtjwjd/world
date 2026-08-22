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

        /// <summary>
        /// 카메라 앞에 바닥판을 세운다.
        /// <paramref name="sprite"/> 는 반드시 에셋 스프라이트여야 한다 —
        /// Sprite.Create(Texture2D.whiteTexture, ...) 로 만든 것은 bounds 가 (0,0) 이라
        /// 프러스텀 컬링에 걸려 아예 그려지지 않는다(isVisible=false 로 확인).
        /// </summary>
        public static BattleBackdrop Create(Color color, Sprite sprite)
        {
            if (sprite == null)
            {
                Debug.LogWarning("[BattleBackdrop] 바닥판 스프라이트가 비어 있어 무대를 못 만든다. "
                                 + "BattleUI 프리팹의 backdropSprite 를 확인할 것.");
                return null;
            }

            Camera cam = Camera.main;
            if (cam == null) return null;

            // 카메라의 자식으로 붙이지 않는다 — 카메라 스케일을 그대로 물려받아
            // lossyScale 이 0 이 되면 bounds 가 (0,0) 이 되고 컬링돼 아예 안 그려진다.
            // 대신 매 프레임 카메라 앞으로 옮긴다.
            var go = new GameObject("BattleBackdrop");

            var bd = go.AddComponent<BattleBackdrop>();
            bd._cam = cam;
            bd._sr  = go.AddComponent<SpriteRenderer>();

            bd._sr.sprite       = sprite;
            bd._sr.color        = color;
            bd._sr.sortingOrder = SortingOrder;

            // 기본 재질은 Sprite-Lit-Default 라 2D 라이트가 팔레트 색을 들어올린다.
            // 무대 바닥은 조명을 받으면 안 된다 — 정확히 #14110F 로 깔려야 한다.
            Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            if (unlit != null) bd._sr.material = new Material(unlit);

            bd.Fit();
            return bd;
        }

        void LateUpdate() { Fit(); }

        /// <summary>직교 카메라의 화면 크기에 맞춰 늘린다. 해상도가 바뀌어도 따라간다.</summary>
        void Fit()
        {
            if (_cam == null || _sr == null) return;
            if (!_cam.orthographic) return;   // 원근 카메라는 이 방식이 성립하지 않는다

            // 카메라 앞 한 칸. 직교라 z 는 정렬에 영향을 주지 않지만 클리핑 안쪽에 둔다.
            transform.position = _cam.transform.position
                               + _cam.transform.forward * Mathf.Max(1f, _cam.nearClipPlane + 1f);
            transform.rotation = _cam.transform.rotation;

            // 스프라이트의 월드 크기로 나눠야 한다 — 8x8 짜리를 그대로 늘리면 배율이 어긋난다.
            Vector2 unit = _sr.sprite != null ? _sr.sprite.bounds.size : Vector2.one;
            if (unit.x < 0.0001f || unit.y < 0.0001f) return;

            float h = _cam.orthographicSize * 2f * Margin;
            float w = h * _cam.aspect;
            transform.localScale = new Vector3(w / unit.x, h / unit.y, 1f);
        }
    }
}
