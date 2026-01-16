using System;
using UnityEngine;

[System.Serializable] // 이 줄이 있어야 저장(직렬화)이 가능합니다!
public class SaveData
{
    public string sceneName;      // 저장한 맵 이름 (예: "House")
    public float playTime;        // 플레이 시간 (초 단위)
    public string saveDate;       // 저장한 실제 날짜 (예: "2024-01-15 14:00")
    
    // 나중에 플레이어 위치도 저장하려면 여기에 x, y, z 추가하면 됩니다.
    public float playerX;
    public float playerY;
}