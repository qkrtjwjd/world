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

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(h, v);
            _isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 스프라이트 방향·애니메이션은 프레임 단위로 즉시 반영
            if (h < 0)      transform.localScale = new Vector3(-1, 1, 1);
            else if (h > 0) transform.localScale = new Vector3(1,  1, 1);

            anim.SetBool("isRun", _moveInput != Vector2.zero);
        }

        // 속도 적용은 물리 엔진과 동기화된 FixedUpdate에서 처리
        private void FixedUpdate()
        {
            float currentSpeed = walkSpeed * (_isRunning ? runMultiplier : 1f);
            rb.linearVelocity = _moveInput * currentSpeed;
        }

        // 전투 진입은 EncounterManager / EnemyEncounterTrigger 에서 처리합니다.
        // (이전 코드 제거 — 비가산 LoadScene 이 EncounterManager 와 충돌하던 원인)
    }
}
