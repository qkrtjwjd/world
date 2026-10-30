using UnityEngine;

/// <summary>
/// 식탁의 세 접시(루 · 세라 · 유) — E-77-1 · E-77-2 · F-3-2.
///
///   밤(S#01~S#03)        없다
///   S#04A 시작           세라가 차린다 — KitchenTriggerCutscene 이 켠다
///   그 뒤 집을 떠날 때까지  그대로 둔다(세라 외출 뒤에도). D-S#07 「유의 접시만 손대지 않은 채로 남아 있다」
///
/// 씬을 다시 불러오는 복귀(세라 외출 직후 · S#11 직후)와 불러오기에서도 아침을 본 뒤면 차려진 상태로 연다.
/// 식기는 한 벌이다 — 배드 엔딩도 같은 접시를 쓰며, BE#02-c 의 「루 자리만 비움」은
/// BadEndingDirector.SetPlates 가 맡는다(platesThree = 이 오브젝트 · platesTwo = 세라 · 유 묶음).
/// 유의 접시는 집_식탁_유의접시(house_kitchen_yu_plate) 오브젝트 그대로다 — 따로 그리지 않는다(E-77-2).
/// </summary>
public class DiningTablePlates : MonoBehaviour
{
    [Tooltip("루 자리 접시. BE#02-c 에서 이것만 빠진다.")]
    public GameObject luPlate;
    [Tooltip("세라 · 유 자리 접시 묶음. 유의 접시(집_식탁_유의접시)가 이 안에 있다.")]
    public GameObject seraYuPlates;

    void Start() => Show(GameState.isBreakfastWatched);

    public void Show(bool on)
    {
        if (luPlate != null) luPlate.SetActive(on);
        if (seraYuPlates != null) seraYuPlates.SetActive(on);
    }
}
