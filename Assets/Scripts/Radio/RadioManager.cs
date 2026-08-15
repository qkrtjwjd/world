using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RadioManager : MonoBehaviour
{
    public static RadioManager Instance { get; private set; }

    [Header("라디오 데이터 목록")]
    [SerializeField] private List<RadioData> radioDataList = new List<RadioData>();

    private readonly Dictionary<string, RadioData> _radioMap = new Dictionary<string, RadioData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildMap();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void BuildMap()
    {
        _radioMap.Clear();
        foreach (var data in radioDataList)
        {
            if (data != null && !string.IsNullOrEmpty(data.objectID))
                _radioMap[data.objectID] = data;
        }
    }

    public bool HasRadioData(string objectID) =>
        !string.IsNullOrEmpty(objectID) && _radioMap.ContainsKey(objectID);

    public void PlayRadio(string objectID)
    {
        if (!_radioMap.TryGetValue(objectID, out RadioData data)) return;
        PlayRadioInline(data.yarnNodeName);
    }

    public void PlayRadioInline(string yarnNodeName)
    {
        if (!string.IsNullOrEmpty(yarnNodeName))
            StartCoroutine(YarnDialogue.PlayAndWait(yarnNodeName));
    }
}
