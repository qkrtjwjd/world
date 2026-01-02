using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동을 위해 필수!

namespace ClearSky
{
    public class SimplePlayerController : MonoBehaviour
    {
        // 이동할 씬의 정확한 이름을 적어주세요 (대소문자 구분)
        public string sceneName = "MainScene";
        public float walkSpeed = 4f;          // 걷기 속도
        public float runMultiplier = 1.8f;    // Shift를 누를 때 곱해줄 배율
        
        private Rigidbody2D rb;
        private Animator anim;
        
        // 방향 제어 변수
        private int direction = 1;
        private bool alive = true;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            anim = GetComponent<Animator>();

            // 점프를 없애므로 중력은 0으로 유지 (탑다운 방식이라면 필수)
            rb.gravityScale = 0f;
            if (GameState.hasPositionSaved)
            {
                transform.position = GameState.lastPosition;
            }
        }

        private void Update()
        {
            Restart();
            if (alive)
            {
                Hurt();
                Die();
                Attack();
                Run();
            }
        }

        void Run()
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float verticalInput = Input.GetAxisRaw("Vertical");

            // 유니티 버전에 따라 linearVelocity가 없을 수 있어 안전하게 velocity로 변경함
            Vector2 moveVelocity = rb.linearVelocity; 
            
            // Shift 키로 달리기 여부 판단
            bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float currentSpeed = walkSpeed * (isRunning ? runMultiplier : 1f);

            bool isMoving = false; 

            // --- [핵심 수정 부분: 좌우 이동 및 반전] ---
            if (horizontalInput < 0)
            {
                direction = -1;
                moveVelocity.x = -currentSpeed;
                isMoving = true;

                // ★ 중요: flipX 대신 스케일을 -1로 변경 (뒤틀림 방지)
                transform.localScale = new Vector3(-1, 1, 1);
            }
            else if (horizontalInput > 0)
            {
                direction = 1;
                moveVelocity.x = currentSpeed;
                isMoving = true;

                // ★ 중요: 스케일을 다시 1로 복구
                transform.localScale = new Vector3(1, 1, 1);
            }
            else
            {
                moveVelocity.x = 0; 
            }
            // ---------------------------------------

            // 위/아래 이동
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

            // 애니메이션 설정
            if (isMoving)
                anim.SetBool("isRun", true);
            else
                anim.SetBool("isRun", false);

            rb.linearVelocity = moveVelocity; // 속도 적용
        }

        void Attack()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                anim.SetTrigger("attack");
            }
        }

        void Hurt()
        {
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                anim.SetTrigger("hurt");
                // 넉백 효과
                if (direction == 1)
                    rb.AddForce(new Vector2(-5f, 1f), ForceMode2D.Impulse);
                else
                    rb.AddForce(new Vector2(5f, 1f), ForceMode2D.Impulse);
            }
        }

        void Die()
        {
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                anim.SetTrigger("die");
                alive = false;
            }
        }

        void Restart()
        {
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                anim.SetTrigger("idle");
                alive = true;
            }
        }
        // 플레이어가 닿았을 때 실행됨
        private void OnTriggerEnter2D(Collider2D other)
        {
        // 부딪힌 게 플레이어인지 확인 (태그가 Player여야 함)
            if (other.CompareTag("Enemy"))
        {
            // 현재 위치 저장 (나중에 돌아오기 위해 필요하지만 일단은 이동부터!)
            Debug.Log("전투 시작! 3D 씬으로 이동합니다.");
            
            // 씬 로드
            SceneManager.LoadScene(sceneName);
        }
    }
    }
}