using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class PlayFabDataStore : MonoBehaviour
{
    public static PlayFabDataStore Instance { get; private set; }

    private const string FileName = "Player_Playfab.json";
    private const string DefaultPlayerId = "player1";

    public PlayerJSONModel Data { get; private set; } = new PlayerJSONModel();
    public bool HasFreshCookingSnapshotThisSession { get; private set; }

    private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = GetPersistentRootForDontDestroyOnLoad();
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;
        GameObject persistentRoot = GetPersistentRootForDontDestroyOnLoad();
        DontDestroyOnLoad(persistentRoot);

        LoadLocal();
    }

    public void LoadLocal()
    {
        try
        {
            HasFreshCookingSnapshotThisSession = false;

            if (!File.Exists(FilePath))
            {
                Debug.Log("저장된 파일이 없습니다. 새로운 데이터를 생성합니다.");
                Data = new PlayerJSONModel();
                SaveLocal();
                return;
            }

            string json = File.ReadAllText(FilePath);
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("파일이 비어있습니다. 새로운 데이터를 생성합니다.");
                Data = new PlayerJSONModel();
                SaveLocal();
                return;
            }

            Data = JsonConvert.DeserializeObject<PlayerJSONModel>(json);
            
            if (Data == null)
            {
                Debug.LogError("JSON 파싱 실패. 새로운 데이터를 생성합니다.");
                Data = new PlayerJSONModel();
                SaveLocal();
            }
            else
            {
                Data.EnsurePlayer(DefaultPlayerId);
                Debug.Log("데이터 로드 성공");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 로드 실패: {e.Message}\n{e.StackTrace}");
            Data = new PlayerJSONModel();
        }
    }

    public void MarkServerSnapshotRefreshPending()
    {
        HasFreshCookingSnapshotThisSession = false;
    }

    public void SaveLocal()
    {
        try
        {
            if (Data == null)
            {
                Debug.LogError("저장할 데이터가 null입니다.");
                return;
            }

            Data.EnsurePlayer(DefaultPlayerId);

            string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(FilePath, json);
            Debug.Log($"데이터 저장 완료: {FilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 저장 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 모든 플레이어 데이터를 한 번에 업데이트
    /// </summary>
    public void UpdateAllPlayerData(
        Dictionary<string, InventoryInfo> inventoryData,
        Dictionary<string, CrewInfo> crewData,
        Dictionary<string, CrewSlotInfo> crewSlotData,
        Dictionary<string, ShipInfo> shipData,
        Dictionary<string, EquipmentInfo> equipmentData,
        Dictionary<string, CookingSlotInfo> cookingData,
        MoneyJSONModel moneyData,
        StageJSONModel stageData,
        TutorialJSONModel tutorialData,
        string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            // 인벤토리 업데이트
            if (inventoryData != null)
            {
                if (Data.players[playerId].inventory == null)
                {
                    Data.players[playerId].inventory = new InventoryJSONModel();
                }
                Data.players[playerId].inventory.inventoryItems = inventoryData;
                Debug.Log($"[UpdateAll] 인벤토리: {inventoryData.Count}개");
            }

            // 크루 업데이트
            if (crewData != null)
            {
                if (Data.players[playerId].crew == null)
                {
                    Data.players[playerId].crew = new CrewJSONModel();
                }
                Data.players[playerId].crew.crews = crewData;
                Debug.Log($"[UpdateAll] 크루: {crewData.Count}명");
            }

            // 크루 슬롯 업데이트
            if (crewSlotData != null)
            {
                if (Data.players[playerId].crewSlot == null)
                {
                    Data.players[playerId].crewSlot = new CrewSlotJSONModel();
                }
                Data.players[playerId].crewSlot.crewSlots = crewSlotData;
                Debug.Log($"[UpdateAll] 크루 슬롯: {crewSlotData.Count}개");
            }

            // 선박 업데이트
            if (shipData != null)
            {
                if (Data.players[playerId].ship == null)
                {
                    Data.players[playerId].ship = new ShipJSONModel();
                }
                Data.players[playerId].ship.ships = shipData;
                Debug.Log($"[UpdateAll] 선박: {shipData.Count}척");
            }

            // 장비 업데이트
            if (equipmentData != null)
            {
                if (Data.players[playerId].equipment == null)
                {
                    Data.players[playerId].equipment = new EquipmentJSONModel();
                }
                Data.players[playerId].equipment.equipments = equipmentData;
                Debug.Log($"[UpdateAll] 장비: {equipmentData.Count}개");
            }

            // 요리 슬롯 업데이트
            if (cookingData != null)
            {
                if (Data.players[playerId].cookSlot == null)
                {
                    Data.players[playerId].cookSlot = new CookingJSONModel();
                }
                Data.players[playerId].cookSlot.cookSlots = cookingData;
                HasFreshCookingSnapshotThisSession = true;
                Debug.Log($"[UpdateAll] 요리 슬롯: {cookingData.Count}개");
            }

            // 화폐 업데이트
            if (moneyData != null)
            {
                if (Data.players[playerId].currency == null)
                {
                    Data.players[playerId].currency = new MoneyJSONModel();
                }
                Data.players[playerId].currency = moneyData;
                Debug.Log($"[UpdateAll] 화폐: 골드 {moneyData.gold}, 진주 {moneyData.prismPearl}, 해적 코인 {moneyData.pirateCoin}");
            }

            // 스테이지 업데이트
            if (stageData != null)
            {
                if (Data.players[playerId].stage == null)
                {
                    Data.players[playerId].stage = new StageJSONModel();
                }
                Data.players[playerId].stage = stageData;
                Debug.Log($"[UpdateAll] 스테이지: 현재 스테이지 {stageData.currentStageId}, 기여도 {stageData.contribution}, 최대 스테이지 {stageData.maxStageId}");
            }

            // 튜토리얼 데이터 업데이트
            if (tutorialData != null)
            {
                if (Data.players[playerId].tutorialData == null)
                {
                    Data.players[playerId].tutorialData = new TutorialJSONModel();
                }
                Data.players[playerId].tutorialData = tutorialData;
                Debug.Log($"[UpdateAll] 튜토리얼 데이터 업데이트 완료");
            }

            SaveLocal();
            Debug.Log($"모든 플레이어 데이터 업데이트 완료 (플레이어: {playerId})");
        }
        catch (Exception e)
        {
            Debug.LogError($"모든 플레이어 데이터 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateInventory(Dictionary<string, InventoryInfo> inventoryData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            if (Data.players[playerId].inventory == null)
            {
                Data.players[playerId].inventory = new InventoryJSONModel();
            }

            Data.players[playerId].inventory.inventoryItems = inventoryData;
            SaveLocal();
            Debug.Log($"인벤토리 업데이트 완료 (플레이어: {playerId}, 아이템 수: {inventoryData?.Count ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"인벤토리 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    private GameObject GetPersistentRootForDontDestroyOnLoad()
    {
        return transform.root == null ? gameObject : transform.root.gameObject;
    }

    public void UpdateCrew(Dictionary<string, CrewInfo> crewData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            if (Data.players[playerId].crew == null)
            {
                Data.players[playerId].crew = new CrewJSONModel();
            }

            Data.players[playerId].crew.crews = crewData;
            SaveLocal();
            Debug.Log($"크루 업데이트 완료 (플레이어: {playerId}, 크루 수: {crewData?.Count ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"크루 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateCrewSlot(Dictionary<string, CrewSlotInfo> crewSlotData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            if (Data.players[playerId].crewSlot == null)
            {
                Data.players[playerId].crewSlot = new CrewSlotJSONModel();
            }

            Data.players[playerId].crewSlot.crewSlots = crewSlotData;
            SaveLocal();
            Debug.Log($"크루 슬롯 업데이트 완료 (플레이어: {playerId}, 슬롯 수: {crewSlotData?.Count ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"크루 슬롯 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateShip(Dictionary<string, ShipInfo> shipData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            if (Data.players[playerId].ship == null)
            {
                Data.players[playerId].ship = new ShipJSONModel();
            }

            Data.players[playerId].ship.ships = shipData;
            SaveLocal();
            Debug.Log($"선박 업데이트 완료 (플레이어: {playerId}, 선박 수: {shipData?.Count ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"선박 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateEquipment(Dictionary<string, EquipmentInfo> equipmentData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            if (Data.players[playerId].equipment == null)
            {
                Data.players[playerId].equipment = new EquipmentJSONModel();
            }

            Data.players[playerId].equipment.equipments = equipmentData;
            SaveLocal();
            Debug.Log($"장비 업데이트 완료 (플레이어: {playerId}, 장비 수: {equipmentData?.Count ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"장비 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }


    public void UpdateCooking(Dictionary<string, CookingSlotInfo> cookingData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }

            Data.EnsurePlayer(playerId);

            if (Data.players[playerId].cookSlot == null)
            {
                Data.players[playerId].cookSlot = new CookingJSONModel();
            }

            Data.players[playerId].cookSlot.cookSlots = cookingData;
            HasFreshCookingSnapshotThisSession = true;
            SaveLocal();
            Debug.Log($"요리 업데이트 완료 (플레이어: {playerId}, 슬롯 수: {cookingData?.Count ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"요리 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateMoney(MoneyJSONModel moneyData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }
            Data.EnsurePlayer(playerId);
            Data.players[playerId].currency = moneyData;
            SaveLocal();
            Debug.Log($"화폐 업데이트 완료 (플레이어: {playerId}, 골드: {moneyData.gold}, 진주: {moneyData.prismPearl}, 해적 코인: {moneyData.pirateCoin})");
        }
        catch (Exception e)
        {
            Debug.LogError($"화폐 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }


    public void UpdateStage(StageJSONModel stageData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }
            Data.EnsurePlayer(playerId);
            Data.players[playerId].stage = stageData;
            SaveLocal();
            Debug.Log($"스테이지 업데이트 완료 (플레이어: {playerId}, 현재 스테이지: {stageData.currentStageId}, 기여도: {stageData.contribution}, 최대 스테이지: {stageData.maxStageId})");
        }
        catch (Exception e)
        {
            Debug.LogError($"스테이지 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }


    public void UpdateMultiplayLimit(MultiplayLimitJSONModel multiplayLimitData, string playerId = DefaultPlayerId)
    {
        try
        {
            if (Data == null)
            {
                Data = new PlayerJSONModel();
            }
            Data.EnsurePlayer(playerId);
            Data.players[playerId].multiplayLimit = multiplayLimitData;
            SaveLocal();
            Debug.Log($"멀티플레이 제한 업데이트 완료 (플레이어: {playerId}, 제한 수: {multiplayLimitData?.playCount ?? 0})");
        }
        catch (Exception e)
        {
            Debug.LogError($"멀티플레이 제한 업데이트 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void UpdateTutorial(TutorialJSONModel tutorialData, string playerId = "player1")
    {
        if (Data == null)
            Data = new PlayerJSONModel();

        Data.EnsurePlayer(playerId);

        if (tutorialData == null)
            tutorialData = new TutorialJSONModel();

        Data.players[playerId].tutorialData = tutorialData;
        SaveLocal();

        Debug.Log("[PlayFabDataStore] 튜토리얼 데이터 업데이트 완료");
    }


    public PlayerInfo GetPlayerInfo(string playerId = DefaultPlayerId)
    {
        Data.EnsurePlayer(playerId);
        return Data.players[playerId];
    }

    [ContextMenu("Debug: Print Player Data")]
    public void DebugPrintPlayerData()
    {
        if (Data?.players == null)
        {
            Debug.Log("플레이어 데이터가 없습니다.");
            return;
        }

        Debug.Log($"총 플레이어 수: {Data.players.Count}");
        foreach (var kvp in Data.players)
        {
            Debug.Log($"플레이어 ID: {kvp.Key}");
            Debug.Log($"  - 인벤토리 아이템: {kvp.Value.inventory?.inventoryItems?.Count ?? 0}");
            Debug.Log($"  - 크루: {kvp.Value.crew?.crews?.Count ?? 0}");
            Debug.Log($"  - 선박: {kvp.Value.ship?.ships?.Count ?? 0}");
            Debug.Log($"  - 장비: {kvp.Value.equipment?.equipments?.Count ?? 0}");
            Debug.Log($"  - 요리 슬롯: {kvp.Value.cookSlot?.cookSlots?.Count ?? 0}");
            Debug.Log($"  - 화폐: 골드 {kvp.Value.currency?.gold ?? 0}, 진주 {kvp.Value.currency?.prismPearl ?? 0}, 해적 코인 {kvp.Value.currency?.pirateCoin ?? 0}");
            Debug.Log($"  - 스테이지: 현재 스테이지 {kvp.Value.stage?.currentStageId ?? "N/A"}, 기여도 {kvp.Value.stage?.contribution ?? 0}");
        }
    }

    [ContextMenu("Debug: Print JSON")]
    public void DebugPrintJSON()
    {
        if (Data == null)
        {
            Debug.Log("Data가 null입니다.");
            return;
        }

        string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
        Debug.Log($"현재 데이터:\n{json}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
