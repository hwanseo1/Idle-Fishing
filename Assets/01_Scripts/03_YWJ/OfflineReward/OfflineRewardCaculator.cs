using UnityEngine;
using System;
using RMS.Data;
using RMS.Fishing;
using System.Collections.Generic;
using System.Text;
using Crew;
using Fisher.PlayerSystems;
using JHS.Equipment;
using System.Threading.Tasks;


namespace OfflineReward
{
    public class OfflineRewardCaculator : MonoBehaviour
    {
        [Header("Offline Reward Cost")]
        [SerializeField] private int _offlineRewardCost = 0;

        [Header("Offline Reward Hours")]
        [SerializeField] private int _currentOfflineHours = 0;
        [SerializeField] private int _baseMaxOfflineHours = 8;
        [SerializeField] private int _maxOfflineHours = 8;
        [SerializeField] private int _fishingChairLevel = 0;

        [Header("Offline Reward Rate")]
        [SerializeField] private int _offlineRewardFishPerHour = 3;

        [Header("Offline Reward Bonus")]
        [SerializeField] private float _finalRewardBonusRatio = 1.0f;
        [SerializeField] private float _crewBonusBonusRatio = 0;

        [Header("Fish Spawn")]
        [SerializeField] private FishSpawnManager _fishSpawnManager;

        private bool _hasCalculated = false;

        private Dictionary<string, int> _fishCountDictionary = new Dictionary<string, int>();

        private CrewManager _crewManager;
        private EquipmentManager _equipmentManager;

        public int OfflineRewardCost
        {
            get { return _offlineRewardCost; }
        }

        public int CurrentOfflineHours
        {
            get { return _currentOfflineHours; }
        }

        public float FinalRewardBonusRatio
        {
            get { return _finalRewardBonusRatio; }
        }

        public Dictionary<string, int> FishCountDictionary
        {
            get { return _fishCountDictionary; }
        }

        private void Awake()
        {
            _crewManager = FindFirstObjectByType<CrewManager>();
            _equipmentManager = FindFirstObjectByType<EquipmentManager>();
        }


        #region Calculate Function

        private void CalculateOfflineRewardCost()
        { 
            _offlineRewardCost = Mathf.RoundToInt(_currentOfflineHours * _offlineRewardFishPerHour * _finalRewardBonusRatio);
        }

        private void CalculateMaxOfflineHours()
        {
            _fishingChairLevel = _equipmentManager.GetLevel(EquipmentType.Chair);
            // 오프라인 한계 = 기본 4h + 의자 OfflineCapBonus 스탯(데이터 주도). 기존 하드코딩(lv*2) 대체.
            _maxOfflineHours = _baseMaxOfflineHours + (int)_equipmentManager.GetStat(EquipmentStat.OfflineCapBonus, 0f);
        }

        private Task CaculateOfflineHoursAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            PlayFabGateway.Instance.LoginTime.GetElapsedTimeSinceLastLogin(
                response =>
                {
                    double offlineDurationHours = response.elapsedHours;
                    _currentOfflineHours = Mathf.Clamp(
                        (int)offlineDurationHours,
                        0,
                        _maxOfflineHours);

                    tcs.SetResult(true);
                },
                error =>
                {
                    Debug.LogWarning("Failed to get elapsed time since last login: " + error);
                    _currentOfflineHours = 0;
                    tcs.SetResult(false);
                });

            return tcs.Task;
        }

        private void CalculateRewardBonusRatio()
        {
            _crewBonusBonusRatio = _crewManager.GetOfflineRewardBonus();
            _finalRewardBonusRatio = 1.0f + _crewBonusBonusRatio;
        }

        #endregion



        #region Reward Function

        private FishData GetFishFromRewardTable()
        {
            if (_fishSpawnManager == null)
            {
                Debug.LogWarning("FishSpawnManager is not assigned.");
                return null;
            }

            FishData fish = _fishSpawnManager.SelectFish();
            return fish;
        }

        private void GiveOfflineReward()
        {
            _fishCountDictionary.Clear();

            if (_offlineRewardCost < 1)
            {
                Debug.Log("No offline reward to give.");
                return;
            }

            for (int i = 0; i < _offlineRewardCost; i++)
            {
                FishData fish = GetFishFromRewardTable();

                if (fish == null)
                    continue;

                if (_fishCountDictionary.ContainsKey(fish.FishId))
                    _fishCountDictionary[fish.FishId]++;
                else
                    _fishCountDictionary[fish.FishId] = 1;
            }

            foreach (var fishEntry in _fishCountDictionary)
            {
                PlayFabGateway.Instance.Inventory.Add(fishEntry.Key, fishEntry.Value);
            }

            PlayFabGateway.Instance.Inventory.FlushAll();

            PlayFabGateway.Instance.LoginTime.SaveLastLoginTime();

            Debug.Log("오프라인 보상 처리 완료");
        }

        public async Task ProcessOfflineReward()
        {
            if (_hasCalculated)
            {
                GiveOfflineReward();
            }
            else
            {
                await CalculateOfflineRewardAsync();
                GiveOfflineReward();
            }
        }

        public async Task CalculateOfflineRewardAsync()
        {
            CalculateMaxOfflineHours();
            await CaculateOfflineHoursAsync();
            CalculateRewardBonusRatio();
            CalculateOfflineRewardCost();

            _hasCalculated = true;
        }

        #endregion
    }
}
