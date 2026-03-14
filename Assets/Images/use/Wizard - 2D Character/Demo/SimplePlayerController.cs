using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace ClearSky
{
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

            // 저장된 위치가 있으면 그 자리로 이동 (배틀 씬이 아닐 때만 실행)
            if (GameState.hasPositionSaved && SceneManager.GetActiveScene().name != "BattleScene")
            {
                transform.position = GameState.lastPosition;
                
                // ★ 전투 직후 무적 시간 부여 (1초 뒤에 해제) ★
                StartCoroutine(ResetBattleCooldown());
            }
        }

        private IEnumerator ResetBattleCooldown()
        {
            yield return new WaitForSeconds(1.0f);
            GameState.isComingFromBattle = false;
        }

        private void Update()
        {
            Run(); // 걷기는 계속 해야 함
        }

        void Run()
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float verticalInput = Input.GetAxisRaw("Vertical");

            // velocity 문제 해결 (최신/구버전 호환)
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

            // 애니메이션
            if (isMoving)
                anim.SetBool("isRun", true);
            else
                anim.SetBool("isRun", false);

            rb.linearVelocity = moveVelocity; 
        }

        // ⭐ 핵심: 적과 닿으면 전투 화면으로 이동!
        private void OnTriggerEnter2D(Collider2D other)
        {
            // ★ 전투에서 방금 돌아온 상태라면 충돌 무시 ★
            if (GameState.isComingFromBattle) return;

            if (other.CompareTag("Enemy"))
            {
                Debug.Log("전투 시작!");

                // ★ 전투 진입 직후 락 걸기 (맵에 돌아오자마자 다시 전투되는 것 방지) ★
                GameState.isComingFromBattle = true;

                // 현재 위치 저장 (전투 끝나고 돌아올 자리)
                GameState.lastPosition = transform.position;
                GameState.hasPositionSaved = true;

                // 돌아올 씬 이름 기억
                GameState.returnSceneName = SceneManager.GetActiveScene().name;
                
                // 전투 씬으로 이동
                SceneManager.LoadScene("BattleScene"); 
            }
        }
    }
}