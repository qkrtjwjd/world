using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    [Header("설정")]
    public int  slotIndex;
    public Text slotInfoText;

    private void OnEnable() => UpdateSlotInfo();

    public void UpdateSlotInfo()
    {
        if (SaveManager.instance == null || slotInfoText == null) return;

        SaveData data = SaveManager.instance.LoadSaveData(slotIndex);
        slotInfoText.text = data != null ? FormatSlotInfo(data) : "빈 슬롯";
    }

    static string FormatSlotInfo(SaveData data)
    {
        int total   = (int)data.playTime;
        int hours   = total / 3600;
        int minutes = (total % 3600) / 60;

        string time = hours > 0 ? $"{hours}시간 {minutes}분" : $"{minutes}분";
        return $"장소: {data.sceneName}\n플레이: {time}\n{data.saveDate}";
    }

    public void OnClickSave()
    {
        SaveManager.instance?.SaveGame(slotIndex);
        UpdateSlotInfo();
    }

    public void OnClickLoad()
    {
        SaveManager.instance?.LoadGame(slotIndex);
    }
}