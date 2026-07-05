using JHS.Fishing;
using RMS.Data;
using RMS.Fishing;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using OfflineReward;
using Runtime;

public class OfflineRewardUIController : MonoBehaviour
{
    [Header("Offline Reward Result UI")]
    [SerializeField] private Transform _rewardContent;
    [SerializeField] private OfflineRewardBoxUI _rewardBoxPrefab;

    [Header("Offline Reward TMP UI")]
    [SerializeField] private TextMeshProUGUI _rewardHoursText;
    [SerializeField] private TextMeshProUGUI _rewardBonusRatioText;
    [SerializeField] private TextMeshProUGUI _rewardCostText;

    [Header("Exit Button")]
    [SerializeField] private Button _exitButton;

    private FishSpawnManager _fishSpawnManager;
    private OfflineRewardCaculator _rewardCaculator;
    private RuntimeStateController _runtimeStateController;

    private void Awake()
    {
        _fishSpawnManager = FindFirstObjectByType<FishSpawnManager>();
        _rewardCaculator = FindFirstObjectByType<OfflineRewardCaculator>();
        _runtimeStateController = FindFirstObjectByType<RuntimeStateController>();


        _exitButton.onClick.AddListener(OnExitButtonClicked);
    }

    private void OnEnable()
    {
        CreateRewardBoxUI(_rewardCaculator.FishCountDictionary);
        SetOfflineRewardTMPUI(_rewardCaculator.CurrentOfflineHours, _rewardCaculator.FinalRewardBonusRatio, _rewardCaculator.OfflineRewardCost);
    }

    private void CreateRewardBoxUI(Dictionary<string, int> fishCountDictionary)
    {
        foreach (Transform child in _rewardContent)
        {
            Destroy(child.gameObject);
        }

        FishEntry[] _fishEntry = _fishSpawnManager.CurrentStage.FishEntries;

        foreach (var fishEntry in fishCountDictionary)
        {
            string fishId = fishEntry.Key;
            int fishCount = fishEntry.Value;



            FishData fishData = null;
            foreach (FishEntry entry in _fishEntry)
            {
                if (entry.fishData.FishId == fishId)
                {
                    fishData = entry.fishData;
                    break;
                }
            }

            if (fishData == null)
            {
                Debug.LogWarning($"FishData를 찾을 수 없음: {fishId}");
                continue;
            }

            OfflineRewardBoxUI box =
                Instantiate(_rewardBoxPrefab, _rewardContent);

            box.SetOfflineRewardBoxValue(
                fishData.Icon,
                fishData.DisplayName,
                fishCount);
        }
    }

    private void SetOfflineRewardTMPUI(int offlineRewardHours, float offlineRewardBonusRatio, int offlineRewardCost)
    {
        _rewardHoursText.text = $"미접속 시간: {offlineRewardHours} hours";
        _rewardBonusRatioText.text = $"오프라인 보상 보너스: {offlineRewardBonusRatio * 100}%";
        _rewardCostText.text = $"오프라인 보상 비용: {offlineRewardCost}";
    }

    private void OnExitButtonClicked()
    {
        _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
    }
}
