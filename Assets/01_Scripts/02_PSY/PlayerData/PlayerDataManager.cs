using Crew;
using System.IO;
using UnityEngine;
using static PlayerSaveData;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    public PlayerSaveData CurrentPlayerData { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public bool HasPlayerData => CurrentPlayerData != null;

    // 저장 경로
    private string GetPlayerPath(int playerID)
    {
        return Path.Combine(Application.persistentDataPath, $"Player_{playerID}.json");
    }

    // 신규 플레이어 데이터 생성
    public PlayerSaveData CreatePlayerData(int playerID, string playerName)
    {
        PlayerSaveData data = new PlayerSaveData();
        
        data.PlayerID = playerID;
        data.PlayerName = playerName;

        data.Gold = 0;

        CrewInstanceData starterCrew = CrewFactory.CreateStarterCrew();

        data.OwnedCrews.Add(starterCrew);

        data.EquippedCrewSlots.Add(
            new EquippedCrewSlotData
            {
                SlotIndex = 0,
            });

        SavePlayerData(data);

        return data;
    }

    // Json 으로 저장
    public void SavePlayerData(PlayerSaveData data)
    {
        string path = GetPlayerPath(data.PlayerID);

        string json = JsonUtility.ToJson(data, true);

        File.WriteAllText(path, json);
    }

    // 플레이어 데이터 로드
    public PlayerSaveData LoadPlayerData(int playerID)
    {
        string path = GetPlayerPath(playerID);

        if (!File.Exists(path))
            return null;

        string json = File.ReadAllText(path);

        return JsonUtility.FromJson<PlayerSaveData>(json);
    }

    // 현재 플레이어 설정
    public void SetCurrentPlayer(PlayerSaveData playerData)
    {
        CurrentPlayerData = playerData;
    }
}