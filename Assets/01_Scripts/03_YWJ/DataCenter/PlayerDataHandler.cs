using UnityEngine;
using System.Collections.Generic;
using System;


namespace DataCenter
{
	public class PlayerDataHandler : MonoBehaviour
	{
		[SerializeField] private PlayerData _playerData;

		[Header("Cached Player Info")]
		[SerializeField] private int _playerID;
		[SerializeField] private string _playerName;
		[SerializeField] private long _lastLoginTime;

		[Header("Cached Player Money")]
		[SerializeField] private int _gold;
		[SerializeField] private int _prismPearl;
		[SerializeField] private int _pirateCoin;

		[Header("Cached Player Progress")]
		[SerializeField] private int _farthestStage;
		[SerializeField] private int _farthestStageLevel;

		[Header("Cached Player Equipment Levels")]
		[SerializeField] private int _fishingRodLevel;
		[SerializeField] private int _fishingChairLevel;
		[SerializeField] private int _fishingBobberLevel;

		[Header("Cached Player Dictionaries")]
		[SerializeField] private Dictionary<int, int> _shipLevelDictionary;
		[SerializeField] private Dictionary<int, int> _crewLevelDictionary;
		[SerializeField] private Dictionary<int, int> _inventoryDictionary;

		[Header("Level Limits")]
		[SerializeField] private int _maxFarthestStage = 10;
		[SerializeField] private int _maxFarthestStageLevel = 10;
		[SerializeField] private int _maxFishingRodLevel = 20;
		[SerializeField] private int _maxFishingChairLevel = 20;
		[SerializeField] private int _maxFishingBobberLevel = 20;
		[SerializeField] private int _maxShipLevel = 20;

		[Header("Current Player Info")]
		[SerializeField] private int _currentStage;
		[SerializeField] private int _currentStageLevel;
		[SerializeField] private int _currentShipID;


		private void Awake()
		{
			LoadDataFromPlayerData();
		}

		private void OnApplicationQuit()
		{
			// 게임 종료 시 PlayerData에 현재 데이터를 저장
			// 추후 정밀한 저장 타이밍과 방법으로 수정 필요
			//SaveDataToPlayerData();
		}

		#region Sava&Load Player Data

		private void LoadDataFromPlayerData()
		{
			if (_playerData == null)
			{
				Debug.LogError("PlayerData is not assigned!");
				return;
			}

            _playerID = _playerData.PlayerID;
			_playerName = _playerData.PlayerName;
			_lastLoginTime = _playerData.LastLoginTime;
			_gold = _playerData.Gold;
			_prismPearl = _playerData.PrismPearl;
			_pirateCoin = _playerData.PirateCoin;
			_farthestStage = _playerData.FarthestStage;
			_farthestStageLevel = _playerData.FarthestStageLevel;
			_fishingRodLevel = _playerData.FishingRodLevel;
			_fishingChairLevel = _playerData.FishingChairLevel;
			_fishingBobberLevel = _playerData.FishingBobberLevel;

			// Dictionary 초기화
			_shipLevelDictionary = _playerData.ShipLevelDictionary != null
				? new Dictionary<int, int>(_playerData.ShipLevelDictionary)
				: new Dictionary<int, int>();

			_crewLevelDictionary = _playerData.CrewLevelDictionary != null
				? new Dictionary<int, int>(_playerData.CrewLevelDictionary)
				: new Dictionary<int, int>();

			_inventoryDictionary = _playerData.InventoryDictionary != null
				? new Dictionary<int, int>(_playerData.InventoryDictionary)
				: new Dictionary<int, int>();

			_currentStage = _farthestStage; // 초기값은 가장 멀리 간 스테이지로 설정
			_currentStageLevel = _farthestStageLevel; // 초기값은 가장 멀리 간 스테이지 레벨로 설정
			_currentShipID = 0; // 초기값은 기본 배로 설정 (추후 변경 필요)
		}

		public void SaveDataToPlayerData()
		{
			if (_playerData == null)
			{
				Debug.LogError("PlayerData is not assigned!");
				return;
			}

            SetLastLoginTime(); // 마지막 로그인 시간 업데이트

            _playerData.PlayerID = _playerID;
			_playerData.PlayerName = _playerName;
			_playerData.LastLoginTime = _lastLoginTime;
            _playerData.Gold = _gold;
			_playerData.PrismPearl = _prismPearl;
			_playerData.PirateCoin = _pirateCoin;
			_playerData.FarthestStage = _farthestStage;
			_playerData.FarthestStageLevel = _farthestStageLevel;
			_playerData.FishingRodLevel = _fishingRodLevel;
			_playerData.FishingChairLevel = _fishingChairLevel;
			_playerData.FishingBobberLevel = _fishingBobberLevel;

			// Dictionary 저장
			_playerData.ShipLevelDictionary = new Dictionary<int, int>(_shipLevelDictionary);
			_playerData.CrewLevelDictionary = new Dictionary<int, int>(_crewLevelDictionary);
			_playerData.InventoryDictionary = new Dictionary<int, int>(_inventoryDictionary);
		}

		#endregion


		#region Player ID

		public void SetPlayerID(int newPlayerID)
		{
			// 권한 체크 로직 추가 필요
			_playerID = newPlayerID;
		}

		public int GetPlayerID()
		{
			return _playerID;
		}

		#endregion


		#region Player Name

		public void SetPlayerName(string newPlayerName)
		{
			// 권한 체크 로직 추가 필요
			_playerName = newPlayerName;
		}

		public string GetPlayerName()
		{
			return _playerName;
		}

        #endregion


        #region Last Login Time

		public void SetLastLoginTime()
		{
			_lastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

		public long GetLastLoginTime()
		{
			return _lastLoginTime;
		}

        #endregion


        #region Gold

        public void AddGold(int addGold)
		{
			_gold += addGold;
		}

		public void SubtractGold(int subtractGold)
		{
			if (_gold >= subtractGold)
			{
				_gold -= subtractGold;
			}
			else
			{
				Debug.LogWarning("Not enough gold to subtract.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public int GetGold()
		{
			return _gold;
		}

		#endregion


		#region Prism Pearl

		public void AddPrismPearl(int addPrismPearl)
		{
			_prismPearl += addPrismPearl;
		}

		public void SubtractPrismPearl(int subtractPrismPearl)
		{
			if (_prismPearl >= subtractPrismPearl)
			{
				_prismPearl -= subtractPrismPearl;
			}
			else
			{
				Debug.LogWarning("Not enough prism pearls to subtract.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public int GetPrismPearl()
		{
			return _prismPearl;
		}

		#endregion


		#region Pirate Coin

		public void AddPirateCoin(int addPirateCoin)
		{
			_pirateCoin += addPirateCoin;
		}

		public void SubtractPirateCoin(int subtractPirateCoin)
		{
			if (_pirateCoin >= subtractPirateCoin)
			{
				_pirateCoin -= subtractPirateCoin;
			}
			else
			{
				Debug.LogWarning("Not enough pirate coins to subtract.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public int GetPirateCoin()
		{
			return _pirateCoin;
		}

		#endregion


		#region Farthest Stage

		public void SetFarthestStage(int newFarthestStage)
		{
			_farthestStage = Mathf.Clamp(newFarthestStage, 1, _maxFarthestStage);
		}

		public int GetFarthestStage()
		{
			return _farthestStage;
		}

		#endregion


		#region Farthest Stage Level

		public void SetFarthestStageLevel(int newFarthestStageLevel)
		{
			_farthestStageLevel = Mathf.Clamp(newFarthestStageLevel, 1, _maxFarthestStageLevel);
		}

		public int GetFarthestStageLevel()
		{
			return _farthestStageLevel;
		}

		#endregion


		#region Fishing Rod Level

		public void UpgradeFishingRodLevel()
		{
			if (_fishingRodLevel < _maxFishingRodLevel)
			{
				_fishingRodLevel++;
			}
			else
			{
				Debug.LogWarning("Fishing rod is already at max level.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public void SetFishingRodLevel(int newFishingRodLevel)
		{
			_fishingRodLevel = Mathf.Clamp(newFishingRodLevel, 1, _maxFishingRodLevel);
		}

		public int GetFishingRodLevel()
		{
			return _fishingRodLevel;
		}

		#endregion


		#region Fishing Chair Level

		public void UpgradeFishingChairLevel()
		{
			if (_fishingChairLevel < _maxFishingChairLevel)
			{
				_fishingChairLevel++;
			}
			else
			{
				Debug.LogWarning("Fishing chair is already at max level.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public void SetFishingChairLevel(int newFishingChairLevel)
		{
			_fishingChairLevel = Mathf.Clamp(newFishingChairLevel, 1, _maxFishingChairLevel);
		}

		public int GetFishingChairLevel()
		{
			return _fishingChairLevel;
		}

		#endregion


		#region Fishing Bobber Level

		public void UpgradeFishingBobberLevel()
		{
			if (_fishingBobberLevel < _maxFishingBobberLevel)
			{
				_fishingBobberLevel++;
			}
			else
			{
				Debug.LogWarning("Fishing bobber is already at max level.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public void SetFishingBobberLevel(int newFishingBobberLevel)
		{
			_fishingBobberLevel = Mathf.Clamp(newFishingBobberLevel, 1, _maxFishingBobberLevel);
		}

		public int GetFishingBobberLevel()
		{
			return _fishingBobberLevel;
		}

		#endregion


		#region Ship Data Dictionary

		public void UpgradeShipLevel(int shipID)
		{
			if (_shipLevelDictionary.TryGetValue(shipID, out int currentLevel))
			{
				if (currentLevel < _maxShipLevel)
				{
					_shipLevelDictionary[shipID] = currentLevel + 1;
				}
				else
				{
					Debug.LogWarning($"Ship {shipID} is already at max level.");
					// 불가 팝업 또는 다른 처리 로직 추가 필요
				}
			}
			else
			{
				Debug.LogWarning($"Ship {shipID} not found in player data.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}
		}

		public void SetShipLevel(int shipID, int shipLevel)
		{
			int clampedLevel = Mathf.Clamp(shipLevel, 1, _maxShipLevel);
			_shipLevelDictionary[shipID] = clampedLevel;
		}

		public int GetShipLevel(int shipID)
		{
			return _shipLevelDictionary.TryGetValue(shipID, out int shipLevel) ? shipLevel : 0;
		}

		#endregion


		#region Crew Data Dictionary

		public void SetCrewLevel(int crewID, int crewLevel)
		{
			_crewLevelDictionary[crewID] = crewLevel;
		}

		public int GetCrewLevel(int crewID)
		{
			return _crewLevelDictionary.TryGetValue(crewID, out int crewLevel) ? crewLevel : 0;
		}

		#endregion


		#region Inventory Data Dictionary

		public void SetInventoryItemCount(int itemID, int itemCount)
		{
			_inventoryDictionary[itemID] = itemCount;
		}

		public int GetInventoryItemCount(int itemID)
		{
			return _inventoryDictionary.TryGetValue(itemID, out int itemCount) ? itemCount : 0;
		}

		#endregion


		#region Currnet Stage

		public void SetCurrentStage(int newCurrentStage)
		{
			_currentStage = Mathf.Clamp(newCurrentStage, 1, _farthestStage);
		}

		public int GetCurrentStage()
		{
			return _currentStage;
		}

		#endregion


		#region Current Stage Level

		public void SetCurrentStageLevel(int newCurrentStageLevel)
		{
			_currentStageLevel = Mathf.Clamp(newCurrentStageLevel, 1, _farthestStageLevel);
		}

		public int GetCurrentStageLevel()
		{
			return _currentStageLevel;
		}

		#endregion


		#region Current Ship ID

		public void SetCurrentShipID(int newCurrentShipID)
		{
			if (_shipLevelDictionary.ContainsKey(newCurrentShipID))
			{
				_currentShipID = newCurrentShipID;
			}
			else
			{
				Debug.LogWarning($"Ship {newCurrentShipID} not found in player data.");
				// 불가 팝업 또는 다른 처리 로직 추가 필요
			}

		}
		public int GetCurrentShipID()
		{
			return _currentShipID;
		}

        #endregion
    }
}
