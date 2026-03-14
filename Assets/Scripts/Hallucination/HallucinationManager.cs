using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace HallucinationSystem
{
    public class HallucinationManager : MonoBehaviour
    {
        public static HallucinationManager Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("전체 화면 이미지를 포함하는 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup overlayCanvasGroup;
        
        [Tooltip("현실적인 사진/장면을 표시할 Image 컴포넌트입니다.")]
        [SerializeField] private Image displayImage;

        [Header("Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        [Header("Player Control")]
        [Tooltip("환각이 시작될 때 실행할 이벤트입니다. (예: 플레이어 이동 비활성화)")]
        public UnityEvent onHallucinationStart;

        [Tooltip("환각이 끝날 때 실행할 이벤트입니다. (예: 플레이어 이동 활성화)")]
        public UnityEvent onHallucinationEnd;

        private bool isHallucinating = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Ensure UI is hidden at start
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 0;
                overlayCanvasGroup.blocksRaycasts = false;
                overlayCanvasGroup.interactable = false;
            }
        }

        /// <summary>
        /// Triggers the hallucination effect.
        /// </summary>
        /// <param name="sprite">The image to show.</param>
        /// <param name="duration">How long the image stays visible (excluding fade times).</param>
        public void TriggerHallucination(Sprite sprite, float duration)
        {
            if (isHallucinating) return; // Prevent overlapping hallucinations
            StartCoroutine(HallucinationRoutine(sprite, duration));
        }

        private IEnumerator HallucinationRoutine(Sprite sprite, float duration)
        {
            isHallucinating = true;

            // 1. Setup Image
            if (displayImage != null)
            {
                displayImage.sprite = sprite;
            }

            // 2. Lock Movement (Fire Event)
            onHallucinationStart?.Invoke();

            // 3. Fade In
            yield return StartCoroutine(FadeCanvas(overlayCanvasGroup, 0f, 1f, fadeInDuration));

            // 4. Wait for the duration
            yield return new WaitForSeconds(duration);

            // 5. Fade Out
            yield return StartCoroutine(FadeCanvas(overlayCanvasGroup, 1f, 0f, fadeOutDuration));

            // 6. Unlock Movement (Fire Event)
            onHallucinationEnd?.Invoke();

            isHallucinating = false;
        }

        private IEnumerator FadeCanvas(CanvasGroup cg, float start, float end, float duration)
        {
            if (cg == null) yield break;

            float elapsed = 0f;
            cg.alpha = start;

            // Block raycasts if visible
            if (end > 0)
            {
                cg.blocksRaycasts = true;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }

            cg.alpha = end;

            // Unblock raycasts if hidden
            if (end == 0)
            {
                cg.blocksRaycasts = false;
            }
        }
    }
}
