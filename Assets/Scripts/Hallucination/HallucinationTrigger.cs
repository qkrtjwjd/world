using System.Collections;
using UnityEngine;

namespace HallucinationSystem
{
    public class HallucinationTrigger : MonoBehaviour
    {
        [Header("환각 데이터 (Hallucination Data)")]
        [Tooltip("화면에 표시할 현실적인 이미지입니다.")]
        [SerializeField] private Sprite hallucinationImage;

        [Tooltip("이미지가 화면에 유지되는 시간(초)입니다. 페이드 인/아웃 시간은 포함되지 않습니다.")]
        [SerializeField] private float duration = 3.0f;

        [Header("실행 설정 (Trigger Settings)")]
        [Tooltip("체크하면 게임 시작 시(Start) 자동으로 실행됩니다.")]
        [SerializeField] private bool playOnStart = false;

        [Tooltip("자동 실행 시, 시작 후 몇 초 뒤에 실행할지 설정합니다.")]
        [SerializeField] private float startDelay = 0f;

        [Tooltip("체크하면 플레이어가 닿았을 때 실행됩니다.")]
        [SerializeField] private bool triggerByCollision = true;

        [Header("옵션 (Options)")]
        [Tooltip("체크하면 한 번만 실행되고, 이후에는 비활성화됩니다.")]
        [SerializeField] private bool triggerOnce = true;

        [Tooltip("충돌을 감지할 플레이어의 태그입니다.")]
        [SerializeField] private string playerTag = "Player";

        private bool hasTriggered = false;

        private void Start()
        {
            if (playOnStart)
            {
                StartCoroutine(AutoStartRoutine());
            }
        }

        private IEnumerator AutoStartRoutine()
        {
            if (startDelay > 0)
            {
                yield return new WaitForSeconds(startDelay);
            }
            TriggerEffect();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerByCollision || hasTriggered) return;

            if (other.CompareTag(playerTag))
            {
                TriggerEffect();
            }
        }

        public void TriggerEffect()
        {
            if (hasTriggered && triggerOnce) return;

            if (HallucinationManager.Instance != null)
            {
                HallucinationManager.Instance.TriggerHallucination(hallucinationImage, duration);
                
                hasTriggered = true;

                if (triggerOnce)
                {
                    // 충돌로 인한 재실행 방지를 위해 콜라이더 끄기 (충돌 트리거인 경우)
                    if (triggerByCollision)
                    {
                        Collider col = GetComponent<Collider>();
                        if (col != null) col.enabled = false;
                    }
                    
                    // 게임 오브젝트를 파괴하고 싶다면 아래 주석 해제
                    // Destroy(gameObject);
                }
            }
            else
            {
                Debug.LogWarning("HallucinationManager 인스턴스를 찾을 수 없습니다! 씬에 매니저가 있는지 확인하세요.");
            }
        }
    }
}