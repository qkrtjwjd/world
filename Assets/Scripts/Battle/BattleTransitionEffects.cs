using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// BattleSystem에서 분리된 순수 시각/오디오 전환 코루틴 모음.
/// 필요한 레퍼런스를 파라미터로 받으므로 MonoBehaviour 불필요.
/// </summary>
public static class BattleTransitionEffects
{
    /// <summary>흰 플래시 연출: 빠른 플래시 인 후 느린 페이드 아웃.</summary>
    public static IEnumerator WhiteFlash(Image flashImage, float duration)
    {
        if (flashImage == null) yield break;
        flashImage.gameObject.SetActive(true);
        Color c = flashImage.color;

        const float FLASH_IN = 0.03f;
        float elapsed = 0f;
        c.a = 0f;
        flashImage.color = c;
        while (elapsed < FLASH_IN)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(elapsed / FLASH_IN);
            flashImage.color = c;
            yield return null;
        }
        c.a = 1f;
        flashImage.color = c;

        float fadeOutDur = Mathf.Max(0f, duration - FLASH_IN);
        elapsed = 0f;
        while (elapsed < fadeOutDur)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeOutDur);
            flashImage.color = c;
            yield return null;
        }
        c.a = 0f;
        flashImage.color = c;
        flashImage.gameObject.SetActive(false);
    }

    /// <summary>카메라 흔들기 연출. duration 동안 magnitude 강도로 흔들다 원위치.</summary>
    public static IEnumerator CameraShake(float duration, float magnitude)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;
        Transform camT   = mainCam.transform;
        Vector3   origin = camT.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = magnitude * (1f - elapsed / duration);
            camT.localPosition = origin + (Vector3)(Random.insideUnitCircle * strength);
            yield return null;
        }
        camT.localPosition = origin;
    }

    /// <summary>URP ColorAdjustments saturation/contrast를 duration 동안 보간.</summary>
    public static IEnumerator PostProcessLerp(Volume volume, float targetSaturation, float targetContrast, float duration)
    {
        if (volume == null) yield break;
        if (!volume.profile.TryGet<ColorAdjustments>(out var ca)) yield break;

        float startSat = ca.saturation.value;
        float startCon = ca.contrast.value;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ca.saturation.value = Mathf.Lerp(startSat, targetSaturation, t);
            ca.contrast.value   = Mathf.Lerp(startCon, targetContrast,   t);
            yield return null;
        }
        ca.saturation.value = targetSaturation;
        ca.contrast.value   = targetContrast;
    }

    /// <summary>BGM 크로스페이드: 현재 클립을 페이드 아웃 → incomingClip 페이드 인.</summary>
    public static IEnumerator CrossfadeBGM(AudioSource source, AudioClip incomingClip, float duration)
    {
        if (source == null) yield break;

        float startVol = source.volume;
        float halfDur  = duration * 0.5f;
        float elapsed  = 0f;

        while (elapsed < halfDur)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / halfDur);
            yield return null;
        }
        source.volume = 0f;

        if (incomingClip != null)
        {
            source.clip = incomingClip;
            source.Play();
        }

        elapsed = 0f;
        while (elapsed < halfDur)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, startVol, elapsed / halfDur);
            yield return null;
        }
        source.volume = startVol;
    }
}
