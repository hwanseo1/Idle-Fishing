using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using System;
using OfflineReward;

[System.Serializable]
public class FishInventoryData
{
    public List<FishStack> fishes = new();
}

[System.Serializable]
public class FishStack
{
    public string itemId;
    public int count;
}

public class TestInventoryService : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _loadButton;
    [SerializeField] private Button _addFishButton1;
    [SerializeField] private Button _addFishButton2;
    [SerializeField] private Button _addFishButton3;
    [SerializeField] private Button _flushButton;

    private OfflineRewardCaculator _offlineRewardCaculator;

    private void Awake()
    {
        _offlineRewardCaculator = FindFirstObjectByType<OfflineRewardCaculator>();
    }


    private void Start()
    {
        _loadButton.onClick.AddListener(UpdateAllData);
        _addFishButton1.onClick.AddListener(AddFishes);
        _addFishButton2.onClick.AddListener(AddFragment_test);
        _addFishButton3.onClick.AddListener(PromoteCrew_test);
    }

    public void LoadData()
    {
        PlayFabDataStore.Instance.LoadLocal();
    }

    public void UpdateInventory()
    {
        PlayFabGateway.Instance.Inventory.RefreshInventoryData(() =>
        {
            Debug.Log("인벤토리 데이터 새로고침 완료");
        });
    }

    public void UpdateCrewData()
    {
        PlayFabGateway.Instance.Crew.RefreshCrewData(() =>
        {
            Debug.Log("크루 데이터 새로고침 완료");
        });
    }

    public void UpdateShip()
    {
        PlayFabGateway.Instance.Ship.RefreshShipData(() =>
        {
            Debug.Log("선박 데이터 새로고침 완료");
        });
    }

    public void UpdateEquipment()
    {
        PlayFabGateway.Instance.Equipment.RefreshEquipmentData(() =>
        {
            Debug.Log("장비 데이터 새로고침 완료");
        });
    }

    public void UpdateAllData()
    {
      PlayFabGateway.Instance.RefreshAllPlayerData(() =>
      {
          Debug.Log("모든 플레이어 데이터 새로고침 완료");
      });
    }

    public void AddFishes()
    {
        PlayFabGateway.Instance.Inventory.Add("fish_anchovy", 3);
        PlayFabGateway.Instance.Inventory.Add("fish_bluefinTuna", 5);
        PlayFabGateway.Instance.Inventory.Add("fish_squid", 2);

    PlayFabGateway.Instance.Inventory.Flush("FishInventory", (result) =>
    {
        Debug.Log("물고기 추가 완료");
    });
    }

    public void StartCook()
    {
        PlayFabGateway.Instance.Cooking.StartCooking(0,"recipe_grilled_anchovy_g1", 2, (result) =>
        {
            Debug.Log("요리 시작 완료");
        });
    }

    public void AddFragment_test()
    {
        PlayFabGateway.Instance.Inventory.Add("fragment_Crew_Navigator_Jun", 100);

        PlayFabGateway.Instance.Inventory.Flush("OddmentInventory", (result) =>
        {
            Debug.Log("조각 추가 완료");
        });
    }

    public void PromoteCrew_test()
    {
        PlayFabGateway.Instance.Crew.Promote("Crew_Navigator_Jun", (result) =>
        {
            Debug.Log("크루 승급 시도");
        });
    }
}