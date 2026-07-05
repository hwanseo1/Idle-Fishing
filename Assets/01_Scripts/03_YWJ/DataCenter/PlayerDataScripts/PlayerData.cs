using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Data/PlayerData")]
public class PlayerData : ScriptableObject
{
    [Header("Player Info")]
    [SerializeField] public int PlayerID;
    [SerializeField] public string PlayerName;
    [SerializeField] public long LastLoginTime; // Unix timestamp

    [Header("Money")]
    [SerializeField] public int Gold;
    [SerializeField] public int PrismPearl;
    [SerializeField] public int PirateCoin;

    [Header("Progress")]
    [SerializeField] public int FarthestStage;
    [SerializeField] public int FarthestStageLevel;

    [Header("Equipment Level")]
    [SerializeField] public int FishingRodLevel;
    [SerializeField] public int FishingChairLevel;
    [SerializeField] public int FishingBobberLevel;

    [Header("Ship Dictionary")]
    [SerializeField] public Dictionary<int, int> ShipLevelDictionary; // Key: ShipID, Value: ShipLevel

    [Header("Crew Dictionary")]
    [SerializeField] public Dictionary<int, int> CrewLevelDictionary; // Key: CrewID, Value: CrewLevel

    [Header("Inventory Dictionary")]
    [SerializeField] public Dictionary<int, int> InventoryDictionary; // Key: ItemID, Value: ItemCount
}
