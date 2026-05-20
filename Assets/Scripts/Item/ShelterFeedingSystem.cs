using System.Collections;
using UnityEngine;

public class ShelterFeedingSystem : MonoBehaviour
{
    [Header("먹이 설정")]
    [Tooltip("먹이로 줄 아이템 데이터 (ItemData)")]
    public ItemData foodItem; // 먹이로 줄 아이템 데이터
    [Tooltip("먹이를 줬을 때 증가할 인형화 수치")]
    public float puppetizationAmount = 10f;

    [Header("피드백")]
    [Tooltip("먹이 주기 성공 시 띄울 메시지")]
    public string successMessage = "정체불명의 존재가 먹이를 받아먹었다. (인형화 상승)";
    [Tooltip("먹이가 없을 때 띄울 메시지")]
    public string failMessage = "줄 수 있는 먹이가 없다.";
    [Tooltip("먹이 주기 성공 이펙트")]
    public GameObject feedEffect;
    
    // InteractionTrigger의 UnityEvent에 이 함수를 연결하세요.
    public void FeedCreature()
    {
        if (InventoryManager.Instance == null) return;

        // foodItem 미연결 방어
        if (foodItem == null)
        {
            Debug.LogWarning("[ShelterFeedingSystem] foodItem이 설정되지 않았습니다. 인스펙터를 확인하세요.");
            return;
        }

        // 먹일 수 없는 아이템이면 조용히 종료
        if (!foodItem.canFeed)
        {
            if (InteractionTextUI.Instance != null)
            {
                InteractionTextUI.Instance.Show(failMessage);
                StartCoroutine(HideTextDelayed(2f));
            }
            return;
        }

        // 인벤토리에서 해당 아이템 찾기
        ItemData foundItem = InventoryManager.Instance.inventoryItems.Find(x => x.itemName == foodItem.itemName);

        if (foundItem != null)
        {
            // 아이템 소모
            InventoryManager.Instance.RemoveItem(foundItem);

            // 스탯 적용
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddPuppetization(puppetizationAmount);
            }

            // 피드백 (UI 매니저가 있다면 사용, 여기선 디버그/임시)
            Dbg.Log(successMessage);
            if (InteractionTextUI.Instance != null)
            {
                InteractionTextUI.Instance.Show(successMessage);
                StartCoroutine(HideTextDelayed(2f));
            }

            // 이펙트
            if (feedEffect != null)
            {
                Instantiate(feedEffect, transform.position, Quaternion.identity);
            }
        }
        else
        {
            Dbg.Log(failMessage);
            if (InteractionTextUI.Instance != null)
            {
                InteractionTextUI.Instance.Show(failMessage);
                StartCoroutine(HideTextDelayed(2f));
            }
        }
    }

    IEnumerator HideTextDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideText();
    }

    void HideText()
    {
        if (InteractionTextUI.Instance != null)
            InteractionTextUI.Instance.Hide();
    }
}
