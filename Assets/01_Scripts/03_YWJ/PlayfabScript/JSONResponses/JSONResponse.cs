using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// PlayFab CloudScript에서 반환하는 표준 응답 형식
/// { "success": true, "data": { ... } }
/// </summary>
[Serializable]
public class PlayFabDataResponse<T>
{
    public bool success;
    public T data;
}


/// <summary>
/// 통합 플레이어 데이터 응답 구조
/// 모든 게임 데이터를 한 번에 가져옴
/// </summary>
[Serializable]
public class AllPlayerDataWrapper
{
    public InventoryDataWrapper inventory;
    public Dictionary<string, CrewInfo> crew;
    public CrewSlotJSONModel crewSlot;
    public Dictionary<string, ShipInfo> ship;
    public Dictionary<string, EquipmentInfo> equipment;
    public CookingJSONModel cookSlot;
    public MoneyJSONModel currency;
    public StageJSONModel stage;
    public MultiplayLimitJSONModel multiplayLimit;
    public JObject cshRuntimeState;
    public TutorialJSONModel tutorialData;
}


/// <summary>
/// 크루 데이터 응답의 data 구조
/// "data": { "CrewData": { "crew1": {...}, "crew2": {...} } }
/// </summary>
[Serializable]
public class CrewDataWrapper
{
    public Dictionary<string, CrewInfo> CrewData;
}

[Serializable]
public class PromoteCrewResponse
{
    public bool success;
    public string crewId;
    public string fragmentId;
    public string previousGrade;
    public string currentGrade;
    public int usedFragments;
    public int remainingFragments;
}


/// <summary>
/// 크루 슬롯 데이터 응답의 data 구조
/// "data": {
///   "CrewSlotData": {
///     "CrewSlots": {
///       "0": {
///         "isUnlocked": true,
///         "equippedCrewId": "Crew_Navigator_Jun"
///       },
///       "1": {
///         "isUnlocked": true,
///         "equippedCrewId": null
///       }
///     }
///   }
/// }
/// </summary>
[Serializable]
public class CrewSlotDataWrapper
{
    public CrewSlotJSONModel CrewSlotData = new();
}


[Serializable]
public class UnlockCrewSlotResponse
{
    public bool success;
    public bool allUnlocked;
    public string unlockedSlotIndex;
    public CrewSlotUnlockConsumed consumed;
    public CrewSlotJSONModel crewSlotData;
}

[Serializable]
public class CrewSlotUnlockConsumed
{
    public string currencyCode;
    public int amount;
}


/// <summary>
/// 인벤토리 데이터 응답의 실제 구조
/// "data": { "FishInventory": { "fish_001": 2 }, "FoodInventory": {}, ... }
/// </summary>
[Serializable]
public class InventoryDataWrapper
{
    public Dictionary<string, int> FishInventory;
    public Dictionary<string, int> FoodInventory;
    public Dictionary<string, int> IngredientInventory;
    public Dictionary<string, int> OddmentInventory;
}


/// <summary>
/// 선박 데이터 응답의 data 구조
/// "data": { "ShipData": { "ship1": {...}, "ship2": {...} } }
/// </summary>
[Serializable]
public class ShipDataWrapper
{
    public Dictionary<string, ShipInfo> ShipData;
}


/// <summary>
/// 장비 데이터 응답의 data 구조
/// "data": { "EquipmentData": { "equip1": {...}, "equip2": {...} } }
/// </summary>
[Serializable]
public class EquipmentDataWrapper
{
    public Dictionary<string, EquipmentInfo> EquipmentData;
}


/// <summary>
/// 요리 데이터 응답의 data 구조
/// "data": { "slotInfos": { "0": {...}, "1": {...} } }
/// </summary>
[Serializable]
public class CookingDataWrapper
{
    public CookingJSONModel CookingSlotData = new();
}



/// <summary>
/// 재화 데이터 응답의 data 구조
/// "currency": { "gold": 1000, "prismPearl": 50, "pirateCoin": 200 }
/// </summary>
[Serializable]
public class MoneyDataWrapper
{
    public MoneyJSONModel MoneyData = new();
}


[Serializable]
public class StageDataWrapper
{
    public StageJSONModel StageData = new();
}

[Serializable]
public class ElapsedLoginTimeResponse
{
    public bool success;
    public bool exists;
    public int elapsedSeconds;
    public int elapsedMinutes;
    public float elapsedHours;
    public string nowUtc;
}

[Serializable]
public class SaveLastLoginTimeResponse
{
    public bool success;
    public LastLoginTimeData data;
}

[Serializable]
public class LastLoginTimeData
{
    public string lastLoginAtUtc;
}



[Serializable]
public class ConsumeRecruitCostResponse
{
    public bool success;
    public string error;

    public string recruitType;
    public int drawCount;
    public RecruitConsumedData consumed;
    public RecruitRemainingData remaining;
}

[Serializable]
public class RecruitConsumedData
{
    public string ticketId;
    public int ticketCount;
    public string currencyCode;
    public int currencyAmount;
}

[Serializable]
public class RecruitRemainingData
{
    public int ticketCount;
}

[Serializable]
public class MultiplayLimitResponse
{
    public bool success;
    public bool consumed;
    public MultiplayLimitJSONModel data;
}


[Serializable]
public class MultiplayRewardResponse
{
    public bool success;
    public int personalContribution;
    public int totalContribution;
    public MultiplayRewardResult rewards;
}

[Serializable]
public class MultiplayRewardResult
{
    public MultiplayItemReward[] personalRewards;
    public MultiplayItemReward[] totalRewards;
    public MultiplayGrantedItem[] grantedItems;
    public MultiplayGrantedCurrency[] grantedCurrencies;
}

[Serializable]
public class MultiplayItemReward
{
    public string itemId;
    public int count;

    public string currencyCode;
    public int amount;
}

[Serializable]
public class MultiplayGrantedItem
{
    public string itemId;
    public int count;
}

[Serializable]
public class MultiplayGrantedCurrency
{
    public string currencyCode;
    public int amount;
}


[Serializable]
public class SetCurrentSessionResponse
{
    public bool success;
}

[Serializable]
public class GetCurrentSessionResponse
{
    public bool success;
    public bool exists;
    public string sessionKey;
}