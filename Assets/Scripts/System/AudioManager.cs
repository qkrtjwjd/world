using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이름 기반 효과음 재생 싱글톤.
/// DontDestroyOnLoad로 씬 전환 후에도 유지된다.
/// Inspector에서 SoundEntry 배열로 클립을 등록하고,
/// AudioManager.Instance.Play("이름") 으로 재생한다.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class SoundEntry
    {
        public string    name;
        public AudioClip clip;
    }

    [Header("효과음 목록")]
    [SerializeField] private SoundEntry[] sounds;

    [Header("재생용 AudioSource (PlayOneShot)")]
    [SerializeField] private AudioSource audioSource;

    private Dictionary<string, AudioClip> _lookup;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            BuildLookup();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void BuildLookup()
    {
        _lookup = new Dictionary<string, AudioClip>();
        if (sounds == null) return;
        foreach (var entry in sounds)
        {
            if (string.IsNullOrEmpty(entry.name) || entry.clip == null) continue;
            if (!_lookup.ContainsKey(entry.name))
                _lookup[entry.name] = entry.clip;
            else
                Debug.LogWarning($"[AudioManager] 중복 이름 무시됨: '{entry.name}'");
        }
    }

    /// <summary>등록된 이름의 효과음을 PlayOneShot으로 재생한다.</summary>
    public void Play(string soundName)
    {
        if (_lookup == null || !_lookup.TryGetValue(soundName, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] 등록되지 않은 사운드: '{soundName}'");
            return;
        }
        audioSource.PlayOneShot(clip);
    }
}
