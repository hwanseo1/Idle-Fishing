using System;
using System.Collections.Generic;



public static class PlayerDataEventBus
{
    #region Player Info Events

    public static event Action<int> OnPlayerIDChanged;
    public static event Action<string> OnPlayerNameChanged;

    public static void InvokePlayerIDChanged(int playerID)
    {
        OnPlayerIDChanged?.Invoke(playerID);
    }

    public static void InvokePlayerNameChanged(string playerName)
    {
        OnPlayerNameChanged?.Invoke(playerName);
    }

    #endregion


    #region Player Money Events

    public static event Action<int> OnGoldChanged;
    public static event Action<int> OnPrismPearlChanged;
    public static event Action<int> OnPirateCoinChanged;

    public static void InvokeGoldChanged(int gold)
    {
        OnGoldChanged?.Invoke(gold);
    }

    public static void InvokePrismPearlChanged(int prismPearl)
    {
        OnPrismPearlChanged?.Invoke(prismPearl);
    }

    public static void InvokePirateCoinChanged(int pirateCoin)
    {
        OnPirateCoinChanged?.Invoke(pirateCoin);
    }

    #endregion


    #region Player Progress Events

    public static event Action<int> OnFarthestStageChanged;
    public static event Action<int> OnFarthestStageLevelChanged;

    public static void InvokeFarthestStageChanged(int farthestStage)
    {
        OnFarthestStageChanged?.Invoke(farthestStage);
    }

    public static void InvokeFarthestStageLevelChanged(int farthestStageLevel)
    {
        OnFarthestStageLevelChanged?.Invoke(farthestStageLevel);
    }

    #endregion


    #region Player Equipment Level Events

    public static event Action<int> OnFishingRodLevelChanged;
    public static event Action<int> OnFishingChairLevelChanged;
    public static event Action<int> OnFishingBobberLevelChanged;

    public static void InvokeFishingRodLevelChanged(int fishingRodLevel)
    {
        OnFishingRodLevelChanged?.Invoke(fishingRodLevel);
    }

    public static void InvokeFishingChairLevelChanged(int fishingChairLevel)
    {
        OnFishingChairLevelChanged?.Invoke(fishingChairLevel);
    }

    public static void InvokeFishingBobberLevelChanged(int fishingBobberLevel)
    {
        OnFishingBobberLevelChanged?.Invoke(fishingBobberLevel);
    }

    #endregion


    #region Player Dictionary Events

    public static event Action<int, int> OnShipLevelChanged;
    public static event Action<Dictionary<int, int>> OnShipLevelDictionaryChanged;

    public static event Action<int, int> OnCrewLevelChanged;
    public static event Action<Dictionary<int, int>> OnCrewLevelDictionaryChanged;

    public static event Action<int, int> OnInventoryItemCountChanged;
    public static event Action<Dictionary<int, int>> OnInventoryDictionaryChanged;

    public static void InvokeShipLevelChanged(int shipID, int shipLevel)
    {
        OnShipLevelChanged?.Invoke(shipID, shipLevel);
    }

    public static void InvokeShipLevelDictionaryChanged(Dictionary<int, int> shipLevelDictionary)
    {
        OnShipLevelDictionaryChanged?.Invoke(shipLevelDictionary);
    }

    public static void InvokeCrewLevelChanged(int crewID, int crewLevel)
    {
        OnCrewLevelChanged?.Invoke(crewID, crewLevel);
    }

    public static void InvokeCrewLevelDictionaryChanged(Dictionary<int, int> crewLevelDictionary)
    {
        OnCrewLevelDictionaryChanged?.Invoke(crewLevelDictionary);
    }

    public static void InvokeInventoryItemCountChanged(int itemID, int itemCount)
    {
        OnInventoryItemCountChanged?.Invoke(itemID, itemCount);
    }

    public static void InvokeInventoryDictionaryChanged(Dictionary<int, int> inventoryDictionary)
    {
        OnInventoryDictionaryChanged?.Invoke(inventoryDictionary);
    }

    #endregion

}