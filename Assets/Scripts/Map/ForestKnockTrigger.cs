using System.Collections;
using UnityEngine;

/// <summary>
/// ForestEntrance 태그 진입 시: BGM 페이드 아웃(0.5초) → 딱 SFX 1회 → BGM 복귀.
/// 씬 인스턴스당 1회만 발동됩니다.
/// Collider2D를 가진 ForestEntrance 오브젝트에 부착하세요.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ForestKnockTrigger : MonoBehaviour
{
    [Tooltip("씬의 BGM AudioSource")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("딱 소리 AudioClip")]
    [SerializeField] private AudioClip knockClip;

    [Tooltip("BGM 페이드 아웃/인 시간 (초)")]
    [SerializeField] private float fadeDuration = 0.5f;

    private bool _triggered;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered || !other.CompareTag("Player")) return;
        _triggered = true;
        StartCoroutine(DoKnock());
    }

    IEnumerator DoKnock()
    {
        if (bgmSource == null) yield break;

        float origVolume = bgmSource.volume;

        yield return StartCoroutine(FadeBGM(bgmSource, 0f, fadeDuration));

        if (knockClip != null)
            bgmSource.PlayOneShot(knockClip, 1f);

        yield return new WaitForSeconds(knockClip != null ? knockClip.length : 0.5f);

        yield return StartCoroutine(FadeBGM(bgmSource, origVolume, fadeDuration));
    }

    IEnumerator FadeBGM(AudioSource src, float targetVol, float duration)
    {
        float start   = src.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            src.volume  = Mathf.Lerp(start, targetVol, elapsed / duration);
            yield return null;
        }

        src.volume = targetVol;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawWireCube(transform.position, col.bounds.size);
    }
}
