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

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            anim = GetComponent<Animator>();
            rb.gravityScale = 0f;
        }

        private void Update()
        {
            // 턴제 배틀 오버레이 중(timeScale=0)에는 입력 차단
            if (Time.timeScale == 0f) return;
            Run();
        }

        void Run()
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float verticalInput = Input.GetAxisRaw("Vertical");

            Vector2 moveVelocity = rb.linearVelocity;
            
            bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float currentSpeed = walkSpeed * (isRunning ? runMultiplier : 1f);

            bool isMoving = false; 

            // 좌우 이동
            if (horizontalInput < 0)
            {
                moveVelocity.x = -currentSpeed;
                isMoving = true;
                transform.localScale = new Vector3(-1, 1, 1);
            }
            else if (horizontalInput > 0)
            {
                moveVelocity.x = currentSpeed;
                isMoving = true;
                transform.localScale = new Vector3(1, 1, 1);
            }
            else
            {
                moveVelocity.x = 0; 
            }

            // 상하 이동
            if (verticalInput > 0)
            {
                moveVelocity.y = currentSpeed;
                isMoving = true;
            }
            else if (verticalInput < 0)
            {
                moveVelocity.y = -currentSpeed;
                isMoving = true;
            }
            else
            {
                moveVelocity.y = 0f;
            }

            anim.SetBool("isRun", isMoving);
            rb.linearVelocity = moveVelocity;
        }

        // 전투 진입은 EncounterManager / EnemyEncounterTrigger 에서 처리합니다.
        // (이전 코드 제거 — 비가산 LoadScene 이 EncounterManager 와 충돌하던 원인)
    }
}