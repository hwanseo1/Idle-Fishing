using System;
using System.Collections.Generic;

[Serializable]
public class PlayerJSONModel
{
    public Dictionary<string, PlayerInfo> players = new();

    // ✅ 생성자 추가 - 기본 플레이어 자동 생성
    public PlayerJSONModel()
    {
        // 기본 player1이 없으면 생성
        if (!players.ContainsKey("player1"))
        {
            players["player1"] = new PlayerInfo();
        }
    }

    // ✅ 플레이어가 존재하는지 확인하는 헬퍼 메서드
    public bool HasPlayer(string playerId)
    {
        return players != null && players.ContainsKey(playerId);
    }

    // ✅ 플레이어가 없으면 생성하는 헬퍼 메서드
    public void EnsurePlayer(string playerId)
    {
        if (players == null)
        {
            players = new Dictionary<string, PlayerInfo>();
        }

        if (!players.ContainsKey(playerId))
        {
            players[playerId] = new PlayerInfo();
        }
    }
}

[Serializable]
public class PlayerInfo
{
    public InventoryJSONModel inventory = new();
    public EquipmentJSONModel equipment = new();
    public CrewJSONModel crew = new();
    public CrewSlotJSONModel crewSlot = new();
    public ShipJSONModel ship = new();
    public CookingJSONModel cookSlot = new();
    public MoneyJSONModel currency = new();
    public StageJSONModel stage = new();
    public MultiplayLimitJSONModel multiplayLimit = new();
    public TutorialJSONModel tutorialData = new();
}