using UnityEngine;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [Header("설정")]
    public int  slotIndex;
    public TMP_Text slotInfoText;

    private void OnEnable() => UpdateSlotInfo();

    public void UpdateSlotInfo()
    {
        if (SaveManager.Instance == null || slotInfoText == null) return;

        SaveData data = SaveManager.Instance.LoadSaveData(slotIndex);
        slotInfoText.text = data != null ? FormatSlotInfo(data) : "빈 슬롯";
    }

    static string FormatSlotInfo(SaveData data)
    {
        int total   = (int)data.playTime;
        int hours   = total / 3600;
        int minutes = (total % 3600) / 60;

        string time = hours > 0 ? $"{hours}시간 {minutes}분" : $"{minutes}분";
        // v6 이하 세이브는 playerName이 비어 있으므로 기본 이름으로 표시
        string name = string.IsNullOrWhiteSpace(data.playerName) ? PlayerIdentity.DefaultName : data.playerName;
        return $"{name}  Lv.{data.playerLevel}\n장소: {data.sceneName}\n플레이: {time}\n{data.saveDate}";
    }

    public void OnClickSave()
    {
        SaveManager.Instance?.SaveGame(slotIndex);
        UpdateSlotInfo();
    }

    public void OnClickLoad()
    {
        SaveManager.Instance?.LoadGame(slotIndex);
    }
}