using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public float currentPlayTime = 0f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Time.timeScale > 0)
        {
            currentPlayTime += Time.unscaledDeltaTime; 
        }
    }

    // ★ 수정됨: 몇 번 슬롯(slotIndex)에 저장할지 숫자를 받습니다.
    public void SaveGame(int slotIndex)
    {
        SaveData data = new SaveData();

        data.sceneName = SceneManager.GetActiveScene().name;
        data.playTime = currentPlayTime;
        data.saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"); // 날짜

        string json = JsonUtility.ToJson(data);

        // 파일 이름 뒤에 번호를 붙임 (예: SaveFile0, SaveFile1...)
        PlayerPrefs.SetString("SaveFile" + slotIndex, json);
        PlayerPrefs.Save();

        Debug.Log(slotIndex + "번 슬롯에 저장 완료!");
    }

    // ★ 수정됨: 몇 번 슬롯 데이터를 불러올지 숫자를 받습니다.
    public SaveData LoadSaveData(int slotIndex)
    {
        string fileName = "SaveFile" + slotIndex;

        if (PlayerPrefs.HasKey(fileName))
        {
            string json = PlayerPrefs.GetString(fileName);
            return JsonUtility.FromJson<SaveData>(json);
        }
        return null;
    }
}