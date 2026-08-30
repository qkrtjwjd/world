using UnityEngine;

namespace ClearSky
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
    public class SimplePlayerController : MonoBehaviour
    {
        public float walkSpeed = 4f;
        public float runMultiplier = 1.8f;

        private Rigidbody2D rb;
        private Animator anim;

        // Update에서 읽은 입력을 FixedUpdate까지 보존
        private Vector2 _moveInput;
        private bool _isRunning;

        // 코드에서 이동을 잠그는 플래그 (직렬화 제외 → 도메인 리로드 시 자동 해제)
        [System.NonSerialized] private bool _lockedByCode;

        // 바라보는 방향 — Animator 의 dir 파라미터와 같은 값. 0=아래 · 1=옆 · 2=위.
        // 입력이 없으면 마지막 방향을 유지한다 (제자리 대기도 방향을 가진다).
        private int _dir;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            anim = GetComponent<Animator>();
            rb.gravityScale = 0f;
            // 물리 스텝 사이 렌더링 위치를 보간해 달릴 때 버벅임 제거
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        /// <summary>코드에서 플레이어 이동을 잠급니다.</summary>
        public void Lock()
        {
            _lockedByCode = true;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>코드에서 걸었던 이동 잠금을 해제합니다.</summary>
        public void Unlock()
        {
            _lockedByCode = false;
            enabled = true; // 이전 방식(enabled=false)으로 잠겼던 경우도 복구
        }

        private void Update()
        {
            // 턴제 배틀 오버레이 중(timeScale=0)이거나 코드 잠금 상태에는 입력 차단
            if (Time.timeScale == 0f || _lockedByCode)
            {
                _moveInput = Vector2.zero;
                return;
            }

            // 기절 디버프 — 이동 불가
            if (BuffManager.Instance != null && BuffManager.Instance.IsStunned)
            {
                _moveInput = Vector2.zero;
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(h, v);
            _isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 스프라이트 방향·애니메이션은 프레임 단위로 즉시 반영
            // ⚠ 크기는 건드리지 않고 부호만 뒤집는다. (-1,1,1) 을 통째로 대입하면
            //    씬에 박힌 스케일(도트 규격 전환 이후 0.5625)이 지워져, 좌/우를 처음
            //    누르는 순간 캐릭터가 1.78배로 커진 채 돌아오지 않는다.
            //    CompanionFollow 도 같은 형태로 방향만 바꾼다.
            //    방향은 0=아래 · 1=옆 · 2=위 로 Animator 의 dir 에 넘긴다. right 스프라이트는
            //    만들지 않고 left 를 flipX 로 뒤집는다(§11) — left 가 왼쪽을 보므로
            //    왼쪽이 +, 오른쪽이 - 다.
            if (h != 0)
            {
                _dir = 1;
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x) * (h > 0 ? -1f : 1f);
                transform.localScale = s;
            }
            else if (v != 0)
            {
                _dir = v > 0 ? 2 : 0;
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x);   // 위·아래 스프라이트는 뒤집지 않는다
                transform.localScale = s;
            }

            anim.SetInteger("dir", _dir);
            anim.SetBool("isRun", _moveInput != Vector2.zero);
        }

        // 속도 적용은 물리 엔진과 동기화된 FixedUpdate에서 처리
        private void FixedUpdate()
        {
            float currentSpeed = walkSpeed * (_isRunning ? runMultiplier : 1f);
            // 이동속도 버프 (SpeedUp/SpeedDown) 반영
            if (BuffManager.Instance != null)
                currentSpeed *= Mathf.Max(0f, BuffManager.Instance.SpeedMultiplier);
            rb.linearVelocity = _moveInput * currentSpeed;
        }

        // 전투 진입은 EncounterManager / EnemyEncounterTrigger 에서 처리합니다.
        // (이전 코드 제거 — 비가산 LoadScene 이 EncounterManager 와 충돌하던 원인)
    }
}
