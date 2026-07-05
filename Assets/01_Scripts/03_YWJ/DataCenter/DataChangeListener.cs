using UnityEngine;
using DataCenter;


public class DataChangeListener : MonoBehaviour
{
	[SerializeField] private PlayerDataHandler _playerDataHandler;

	public static DataChangeListener Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);

		_playerDataHandler = FindFirstObjectByType<PlayerDataHandler>();
	}


	#region Player ID

	public void Try_SetPlayerID(int newPlayerID)
	{
		_playerDataHandler.SetPlayerID(newPlayerID);
	}

	public int Try_GetPlayerID()
	{
		return _playerDataHandler.GetPlayerID();
	}

	#endregion


	#region Player Name

	public void Try_SetPlayerName(string newPlayerName)
	{
		_playerDataHandler.SetPlayerName(newPlayerName);
	}

	public string Try_GetPlayerName()
	{
		return _playerDataHandler.GetPlayerName();
	}

    #endregion


    #region Last Login Time

	public void Try_SetLastLoginTime()
	{
		_playerDataHandler.SetLastLoginTime();
	}

	public long Try_GetLastLoginTime()
	{
		return _playerDataHandler.GetLastLoginTime();
	}

    #endregion


    #region Gold

    public void Try_AddGold(int addGold)
	{
		_playerDataHandler.AddGold(addGold);
	}

	public void Try_SubtractGold(int subtractGold)
	{
		_playerDataHandler.SubtractGold(subtractGold);
	}

	public int Try_GetGold()
	{
		return _playerDataHandler.GetGold();
	}

	#endregion


	#region Prism Pearl

	public void Try_AddPrismPearl(int addPrismPearl)
	{
		_playerDataHandler.AddPrismPearl(addPrismPearl);
	}

	public void Try_SubtractPrismPearl(int subtractPrismPearl)
	{
		_playerDataHandler.SubtractPrismPearl(subtractPrismPearl);
	}

	public int Try_GetPrismPearl()
	{
		return _playerDataHandler.GetPrismPearl();
	}

	#endregion


	#region Pirate Coin

	public void Try_AddPirateCoin(int addPirateCoin)
	{
		_playerDataHandler.AddPirateCoin(addPirateCoin);
	}

	public void Try_SubtractPirateCoin(int subtractPirateCoin)
	{
		_playerDataHandler.SubtractPirateCoin(subtractPirateCoin);
	}

	public int Try_GetPirateCoin()
	{
		return _playerDataHandler.GetPirateCoin();
	}

	#endregion


	#region Farthest Stage

	public void Try_SetFarthestStage(int newFarthestStage)
	{
		_playerDataHandler.SetFarthestStage(newFarthestStage);
	}

	public int Try_GetFarthestStage()
	{
		return _playerDataHandler.GetFarthestStage();
	}

	#endregion


	#region Farthest Stage Level

	public void Try_SetFarthestStageLevel(int newFarthestStageLevel)
	{
		_playerDataHandler.SetFarthestStageLevel(newFarthestStageLevel);
	}

	public int Try_GetFarthestStageLevel()
	{
		return _playerDataHandler.GetFarthestStageLevel();
	}

	#endregion


	#region Fishing Rod Level

	public void Try_UpgradeFishingRodLevel()
	{
		_playerDataHandler.UpgradeFishingRodLevel();
	}

	public void Try_SetFishingRodLevel(int newFishingRodLevel)
	{
		_playerDataHandler.SetFishingRodLevel(newFishingRodLevel);
	}

	public int Try_GetFishingRodLevel()
	{
		return _playerDataHandler.GetFishingRodLevel();
	}

	#endregion


	#region Fishing Chair Level

	public void Try_UpgradeFishingChairLevel()
	{
		_playerDataHandler.UpgradeFishingChairLevel();
	}

	public void Try_SetFishingChairLevel(int newFishingChairLevel)
	{
		_playerDataHandler.SetFishingChairLevel(newFishingChairLevel);
	}

	public int Try_GetFishingChairLevel()
	{
		return _playerDataHandler.GetFishingChairLevel();
	}

	#endregion


	#region Fishing Bobber Level

	public void Try_UpgradeFishingBobberLevel()
	{
		_playerDataHandler.UpgradeFishingBobberLevel();
	}

	public void Try_SetFishingBobberLevel(int newFishingBobberLevel)
	{
		_playerDataHandler.SetFishingBobberLevel(newFishingBobberLevel);
	}

	public int Try_GetFishingBobberLevel()
	{
		return _playerDataHandler.GetFishingBobberLevel();
	}

	#endregion


	#region Ship Data Dictionary

	public void Try_UpgradeShipLevel(int shipID)
	{
		_playerDataHandler.UpgradeShipLevel(shipID);
	}

	public void Try_SetShipLevel(int shipID, int shipLevel)
	{
		_playerDataHandler.SetShipLevel(shipID, shipLevel);
	}

	public int Try_GetShipLevel(int shipID)
	{
		return _playerDataHandler.GetShipLevel(shipID);
	}

	#endregion


	#region Crew Data Dictionary

	public void Try_SetCrewLevel(int crewID, int crewLevel)
	{
		_playerDataHandler.SetCrewLevel(crewID, crewLevel);
	}

	public int Try_GetCrewLevel(int crewID)
	{
		return _playerDataHandler.GetCrewLevel(crewID);
	}

	#endregion


	#region Inventory Data Dictionary

	public void Try_SetInventoryItemCount(int itemID, int itemCount)
	{
		_playerDataHandler.SetInventoryItemCount(itemID, itemCount);
	}

	public int Try_GetInventoryItemCount(int itemID)
	{
		return _playerDataHandler.GetInventoryItemCount(itemID);
	}

    #endregion


    #region Current Stage

	public void Try_SetCurrentStage(int newCurrentStage)
	{
        _playerDataHandler.SetCurrentStage(newCurrentStage);
    }

	public int Try_GetCurrentStage()
	{
		return _playerDataHandler.GetCurrentStage();
	}

    #endregion


    #region Current Stage Level

	public void Try_SetCurrentStageLevel(int newCurrentStageLevel)
	{
        _playerDataHandler.SetCurrentStageLevel(newCurrentStageLevel);
    }

	public int Try_GetCurrentStageLevel()
	{
		return _playerDataHandler.GetCurrentStageLevel();
	}

    #endregion


    #region Current Ship ID

	public void Try_SetCurrentShipID(int newCurrentShipID)
	{
        _playerDataHandler.SetCurrentShipID(newCurrentShipID);
    }

	public int Try_GetCurrentShipID()
	{
		return _playerDataHandler.GetCurrentShipID();
	}

    #endregion
}