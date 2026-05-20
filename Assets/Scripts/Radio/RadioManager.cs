using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RadioManager : MonoBehaviour
{
    public static RadioManager Instance { get; private set; }

    [Header("라디오 데이터 목록")]
    [SerializeField] private List<RadioData> radioDataList = new List<RadioData>();

    private readonly Dictionary<string, RadioData> _radioMap = new Dictionary<string, RadioData>();
    private AudioSource _audioSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildMap();
            DialogueEvents.OnDialogueEnded += StopRadio;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            DialogueEvents.OnDialogueEnded -= StopRadio;
            Instance = null;
        }
    }

    void BuildMap()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _radioMap.Clear();
        foreach (var data in radioDataList)
        {
            if (data != null && !string.IsNullOrEmpty(data.objectID))
                _radioMap[data.objectID] = data;
        }
    }

    public void StopRadio()
    {
        if (_audioSource.isPlaying)
            _audioSource.Stop();
    }

    public bool HasRadioData(string objectID) =>
        !string.IsNullOrEmpty(objectID) && _radioMap.ContainsKey(objectID);

    public void PlayRadio(string objectID)
    {
        if (!_radioMap.TryGetValue(objectID, out RadioData data)) return;

        if (data.voiceClip != null)
        {
            _audioSource.Stop();
            _audioSource.clip = data.voiceClip;
            _audioSource.Play();
        }

        if (!string.IsNullOrEmpty(data.yarnNodeName))
            StartCoroutine(YarnDialogue.PlayAndWait(data.yarnNodeName));
    }
}
