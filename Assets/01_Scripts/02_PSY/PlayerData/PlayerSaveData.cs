using Crew;
using System;
using System.Collections.Generic;

[Serializable]
public class PlayerSaveData
{
    public int PlayerID;
    public string PlayerName;

    public long LastLoginTime;

    public int Gold;
    public int PrismPearl;
    public int PirateCoin;

    public int FarthestStage;
    public int FarthestStageLevel;

    public int FishingRodLevel;
    public int FishingChairLevel;
    public int FishingBobberLevel;

    public Dictionary<int, int> ShipLevelDictionary = new();

    public Dictionary<int, int> CrewLevelDictionary = new();

    public Dictionary<int, int> InventoryDictionary = new();

    // 보유 선원
    public List<CrewInstanceData> OwnedCrews = new();

    // 장착 슬롯
    public List<EquippedCrewSlotData> EquippedCrewSlots = new();

    [Serializable]
    public class EquippedCrewSlotData
    {
        public int SlotIndex;
        public string CrewUniqueId;
    }
}