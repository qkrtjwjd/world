using System;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string sceneName;      // 맵 이름
    public float playTime;        // 플레이 시간
    public string saveDate;       // 날짜
    public float corruption;      // 타락 수치

    // ★ [추가됨] 플레이어 위치 (X, Y, Z)
    public float playerX;
    public float playerY;
    public float playerZ;

    // ★ [추가됨] 인벤토리 아이템 이름 목록 (Resources/Items/ 경로에 있는 파일 이름)
    public System.Collections.Generic.List<string> inventoryItemNames = new System.Collections.Generic.List<string>();

    // ★ [추가됨] 플레이어 스탯 및 게이지 저장
    public float health;
    public float mental;
    public float hunger;
    public float realityGauge; // RealitySystem 게이지
}