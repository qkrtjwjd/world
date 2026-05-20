using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleSystem에서 분리된 글리치 전환 연출 컨트롤러.
/// 턴제 → 핵앤슬래시 전환 시 시각·청각 연출을 담당합니다.
/// BattleUI 프리팹에서 BattleSystem과 동일 GameObject에 컴포넌트로 추가하세요.
/// </summary>
public class BattleGlitchTransition : MonoBehaviour
{
    [Header("전환 연출 — 오디오")]
    [Tooltip("유리 깨지는 SFX")]
    public AudioClip   glassShatterSE;
    [Tooltip("SFX 전용 AudioSource")]
    public AudioSource sfxSource;

    private static readonly WaitForSecondsRealtime _wait025 = new WaitForSecondsRealtime(0.25f);
    private static readonly WaitForSecondsRealtime _wait043 = new WaitForSecondsRealtime(0.18f + 0.25f);

    /// <summary>
    /// 글리치 전환을 시작합니다. BattleSystem.ForceSwitchToHackSlash() 에서 호출합니다.
    /// </summary>
    public Coroutine StartGlitchSwitch(
        string       triggerMessage,
        GameObject[] panels,
        Text         dialogueText)
    {
        return StartCoroutine(GlitchAndSwitch(triggerMessage, panels, dialogueText));
    }

    IEnumerator GlitchAndSwitch(
        string       triggerMessage,
        GameObject[] panels,
        Text         dialogueText)
    {
        if (dialogueText != null) dialogueText.text = triggerMessage;

        Time.timeScale = 0.3f;
        var vfx = TransitionVFXController.Instance;

        if (vfx != null && vfx.HasDaggerImage)
        {
            // ── Phase 0: 단검 날아옴 ──────────────────────────────────
            Vector2 stabUV = vfx.DaggerStabUV;
            vfx.PlayDaggerStab(0.18f, () =>
            {
                // 박히는 순간: 균열 + 카메라 흔들림 + SFX + 패널 폭발 동시 시작
                vfx.ImpactFlash(stabUV, 0.1f);
                vfx.CameraShake(0.55f, 0.12f);
                vfx.StartScreenCrack(0.2f, stabUV);
                if (sfxSource != null && glassShatterSE != null)
                    sfxSource.PlayOneShot(glassShatterSE);
                StartCoroutine(ExplodePanels(panels, dialogueText));
            });

            // 단검 비행 시간(0.18s) + 균열 전파 대기(0.25s)
            yield return _wait043;
        }
        else
        {
            // ── Phase 0 폴백: 단검 없이 즉시 충격 ───────────────────
            Vector2 impactUV = new Vector2(0.5f, 0.5f);
            if (vfx != null)
            {
                vfx.ImpactFlash(impactUV, 0.1f);
                vfx.CameraShake(0.55f, 0.12f);
                vfx.StartScreenCrack(0.2f, impactUV);
            }
            if (sfxSource != null && glassShatterSE != null)
                sfxSource.PlayOneShot(glassShatterSE);
            StartCoroutine(ExplodePanels(panels, dialogueText));

            yield return _wait025;
        }

        // ── Phase 1: 산산조각 ────────────────────────────────────────
        if (vfx != null)
            vfx.ShatterScreen(0.25f);

        yield return _wait025;

        // ── Phase 2: 전환 완료 ────────────────────────────────────────
        Time.timeScale = 1f;

        var pil = PlayerInputLock.Instance;
        if (pil != null && pil.IsLocked) pil.Unlock();

        // 참조를 미리 확보한 뒤 배틀 UI 제거
        var enc         = EncounterManager.Instance;
        var enemyObj    = enc?.CurrentEnemyObject;
        var enemyPrefab = enc?.enemyPrefabToSpawn;
        var hackSlash   = HackSlashCombatManager.Instance;

        Destroy(gameObject.transform.root.gameObject);

        if (hackSlash != null)
            hackSlash.BeginCombat(enemyObj, null);  // 기존 씬 적만 사용, 새 적 스폰 금지
        else
            Debug.LogError("[BattleGlitchTransition] HackSlashCombatManager가 현재 씬에 없습니다.");
    }

    // ── Phase 1 경직 → Phase 2 찌그러짐 → Phase 3 산산조각
    IEnumerator ExplodePanels(GameObject[] panels, Text dialogueText)
    {
        const float P1 = 0.1f;
        const float P2 = 0.1f;
        const float P3 = 0.3f;

        int count = panels.Length;

        RectTransform[] rts     = new RectTransform[count];
        CanvasGroup[]   cgs     = new CanvasGroup[count];
        Vector2[]       origins = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            if (panels[i] == null) continue;
            panels[i].SetActive(true);
            rts[i] = panels[i].GetComponent<RectTransform>();
            cgs[i] = panels[i].GetComponent<CanvasGroup>();
            if (cgs[i] == null) cgs[i] = panels[i].AddComponent<CanvasGroup>();
            origins[i] = rts[i] != null ? rts[i].anchoredPosition : Vector2.zero;
        }

        // ── Phase 1: 균열 직전 경직 ──────────────────────────────────
        float elapsed = 0f;
        Color grayTarget = new Color(0.5f, 0.5f, 0.5f, 1f);
        while (elapsed < P1)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / P1;
            float sx = t < 0.5f ? Mathf.Lerp(1f, 1.05f, t * 2f)
                                 : Mathf.Lerp(1.05f, 0.95f, (t - 0.5f) * 2f);
            float sy = t < 0.5f ? Mathf.Lerp(1f, 0.95f, t * 2f)
                                 : Mathf.Lerp(0.95f, 1.05f, (t - 0.5f) * 2f);

            for (int i = 0; i < count; i++)
            {
                if (rts[i] == null) continue;
                rts[i].localScale       = new Vector3(sx, sy, 1f);
                rts[i].anchoredPosition = origins[i] + Random.insideUnitCircle * (120f * t);
            }
            if (dialogueText != null)
                dialogueText.color = Color.Lerp(dialogueText.color, grayTarget, t);

            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (rts[i] != null) rts[i].localScale = Vector3.one;

        // ── Phase 2: 찌그러짐 ──────────────────────────────────────────
        Vector3[] squishScales =
        {
            new Vector3(1.3f, 0.7f, 1f),
            new Vector3(0.7f, 1.3f, 1f),
            new Vector3(1.2f, 0.8f, 1f),
        };

        elapsed = 0f;
        while (elapsed < P2)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / P2);

            for (int i = 0; i < count; i++)
            {
                if (rts[i] == null) continue;
                rts[i].localScale       = Vector3.Lerp(Vector3.one, squishScales[i % squishScales.Length], t);
                rts[i].anchoredPosition = origins[i] + Random.insideUnitCircle * 80f;
            }
            yield return null;
        }

        // ── Phase 3: 산산조각 ──────────────────────────────────────────
        float[]   stagger   = { 0f, 0.02f, 0.04f };
        Vector2[] flyVels   = new Vector2[count];
        float[]   rotSpeeds = new float[count];

        for (int i = 0; i < count; i++)
        {
            flyVels[i]   = Random.insideUnitCircle.normalized * Random.Range(1800f, 2600f);
            rotSpeeds[i] = (Random.value > 0.5f ? 1f : -1f) * 2400f;
        }

        elapsed = 0f;
        float lastStagger = stagger[Mathf.Min(count - 1, stagger.Length - 1)];
        while (elapsed < P3 + lastStagger)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < count; i++)
            {
                if (panels[i] == null) continue;
                float local = elapsed - stagger[Mathf.Min(i, stagger.Length - 1)];
                if (local <= 0f) continue;

                float t = Mathf.Clamp01(local / P3);
                if (rts[i] != null)
                {
                    rts[i].anchoredPosition = origins[i] + flyVels[i] * local;
                    rts[i].localEulerAngles = new Vector3(0f, 0f, rotSpeeds[i] * local);
                    rts[i].localScale       = Vector3.Lerp(squishScales[i % squishScales.Length],
                                                           new Vector3(0.1f, 0.1f, 1f), t);
                }
                if (cgs[i] != null) cgs[i].alpha = Mathf.Lerp(1f, 0f, t);
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (panels[i] != null) panels[i].SetActive(false);
    }

}
