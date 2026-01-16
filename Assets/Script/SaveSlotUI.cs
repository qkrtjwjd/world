using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    [Header("슬롯 설정")]
    public int slotIndex; // ★ 인스펙터에서 0, 1, 2... 입력할 변수
    public Text slotInfoText; 

    private void OnEnable()
    {
        UpdateSlotInfo();
    }

    public void UpdateSlotInfo()
    {
        if (SaveManager.instance == null) return;

        // 내 번호(slotIndex)에 저장된 데이터를 가져옴
        SaveData data = SaveManager.instance.LoadSaveData(slotIndex);

        if (data != null)
        {
            // ★ 여기가 핵심입니다! (초 단위 시간을 -> 분 단위로 변환)
            // data.playTime은 게임 켜져있던 총 시간(초)입니다.
            int totalSeconds = (int)data.playTime;
            
            int hours = totalSeconds / 3600;        // 시간 계산 (3600초 = 1시간)
            int minutes = (totalSeconds % 3600) / 60; // 분 계산

            // 글자 만들기: "0시간 5분" 또는 그냥 "5분"
            string timeString = "";
            if (hours > 0) 
            {
                timeString += $"{hours}시간 "; // 1시간 이상일 때만 '시간' 표시
            }
            timeString += $"{minutes}분"; // 분은 항상 표시

            // 화면에 보여주기
            // 예시: 
            // 장소: House
            // 플레이 경과: 5분
            slotInfoText.text = $"장소: {data.sceneName}\n플레이 경과: {timeString}";
        }
        else
        {
            slotInfoText.text = "빈 슬롯";
        }
    }

    // 버튼을 누르면 내 번호(slotIndex)에 저장하라고 명령
    public void OnClickSave()
    {
        SaveManager.instance.SaveGame(slotIndex); // ★ 내 번호 전달
        UpdateSlotInfo(); // 즉시 화면 갱신
    }
}