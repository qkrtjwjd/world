using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GlitchManager : MonoBehaviour
{
    public static GlitchManager Instance;

    [Header("연결 필수")]
    [Tooltip("글리치 쉐이더가 적용된 UI 패널 (Image)")]
    public Image glitchPanel;

    [Header("기본 설정")]
    [Range(0, 1)] public float defaultIntensity = 0.5f;

    private Material _glitchMat;
    private Coroutine _activeCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 이동해도 유지
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (glitchPanel != null)
        {
            // 재질 인스턴스 가져오기 (원본 훼손 방지)
            _glitchMat = glitchPanel.material;
            
            // 시작할 땐 꺼두기
            glitchPanel.gameObject.SetActive(false);
        }
    }

    // ★ 외부에서 호출하는 함수: "0.5초 동안 글리치 효과 줘!"
    // 사용법: GlitchManager.Instance.PlayGlitch(0.5f);
    public void PlayGlitch(float duration, float intensity = -1f)
    {
        if (glitchPanel == null) return;

        if (intensity < 0) intensity = defaultIntensity;

        // 이미 실행 중이면 멈추고 새로 시작
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(GlitchRoutine(duration, intensity));
    }

    // ★ 외부에서 호출하는 함수: "계속 글리치 상태 유지해! (끄기 전까지)"
    // 사용법: GlitchManager.Instance.SetGlitchLoop(true);
    public void SetGlitchLoop(bool isActive, float intensity = -1f)
    {
        if (glitchPanel == null) return;

        if (isActive)
        {
            if (intensity < 0) intensity = defaultIntensity;
            glitchPanel.gameObject.SetActive(true);
            _glitchMat.SetFloat("_Intensity", intensity);
        }
        else
        {
            glitchPanel.gameObject.SetActive(false);
        }
    }

    IEnumerator GlitchRoutine(float duration, float targetIntensity)
    {
        glitchPanel.gameObject.SetActive(true);
        
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            
            // 랜덤하게 강도를 조절해서 더 불안정한 느낌 주기
            float noise = Random.Range(0.5f, 1.5f);
            _glitchMat.SetFloat("_Intensity", targetIntensity * noise);

            yield return null;
        }

        glitchPanel.gameObject.SetActive(false);
        _activeCoroutine = null;
    }
}