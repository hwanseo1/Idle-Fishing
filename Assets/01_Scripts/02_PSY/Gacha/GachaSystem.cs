using Crew;
using Fisher.Data;
using Reward;
using System;
using System.Collections.Generic;
using UnityEngine;
using static JHS.Backend.UpgradeCostTable;

public class GachaSystem : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ItemDatabase _itemDataBase;
    [SerializeField] private RewardManager _rewardManager;

    [Header("선원 가챠 확률")]
    [SerializeField] private float _rRate = 80f;
    [SerializeField] private float _srRate = 17f;
    [SerializeField] private float _ssrRate = 3f;

    [Header("재료 가챠 확률")]
    [SerializeField] private float _materialRate = 76.5f;
    [SerializeField] private float _crewFregmentsRate = 20f;
    [SerializeField] private float _crewRate = 3.5f;


    public ItemDatabase ItemDataBase => _itemDataBase;

    private string _recruitType1 = "basic";
    private string _recruitType2 = "premium";

    #region 선원 뽑기
    /// 10연 뽑기
    public void CrewDrawTen(Action<List<GachaReward>> onSuccess, Action<string> onFail)
    {
        PlayFabGateway.Instance.Recruit.ConsumeRecruitCost(
            _recruitType2,
            10,
            response =>
            {
                List<GachaReward> results = new();

                for (int i = 0; i < 10; i++)
                {
                    GachaReward reward = CrewDraw();

                    if (reward == null)
                        continue;
                    _rewardManager.GiveReward(reward);
                    results.Add(reward);
                }

                onSuccess?.Invoke(results);
            },
            onFail);
    }

    /// 단일 뽑기
    public void CrewDrawSingle(Action<GachaReward> onSuccess, Action<string> onFail)
    {
        PlayFabGateway.Instance.Recruit.ConsumeRecruitCost(
            _recruitType2,
            1,
            response =>
            {
                GachaReward reward = CrewDraw();

                if (reward == null) return;

                _rewardManager.GiveReward(reward);

                onSuccess?.Invoke(reward);
            },
            onFail);
    }

    private GachaReward CrewDraw()
    {
        CrewGrade grade = RollCrewGrade();

        List<CrewData> candidates = CrewManager.Instance.CrewDataBase.GetCrewsByGrade(grade);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[{grade}] 등급의 CrewData가 없습니다.");

            return null;
        }

        CrewData selectedCrew = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        return new GachaReward
        {
            RewardType = RewardType.Crew,
            RewardId = selectedCrew.CrewId,
            Amount = 1
        };
    }
    private CrewGrade RollCrewGrade()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);

        if (roll < _ssrRate)
        {
            return CrewGrade.SSR;
        }

        if (roll < _ssrRate + _srRate)
        {
            return CrewGrade.SR;
        }

        if (roll < _ssrRate + _srRate + _rRate)
        {
            return CrewGrade.R;
        }

        return CrewGrade.R;
    }
    #endregion

    #region 재료 뽑기
    // 10연 뽑기
    public void MaterialDrawTen(Action<List<GachaReward>> onSuccess, Action<string> onFail)
    {
        PlayFabGateway.Instance.Recruit.ConsumeRecruitCost(
            _recruitType1,
            10,
            response =>
            {
                List<GachaReward> results = new();
                bool hasCrewFragment = false;
                
                // 처음 9개
                for (int i = 0; i < 9; i++)
                {
                    GachaReward reward = MaterialDraw();

                    if (reward != null)
                    {
                        if (reward.RewardType == RewardType.CrewFragment)
                            hasCrewFragment = true;
                        _rewardManager.GiveReward(reward);
                        results.Add(reward);
                    }
                }

                // 선원 조각 1개 보장 로직
                // 마지막 1개
                if (hasCrewFragment)
                {
                    GachaReward reward = MaterialDraw();
                    _rewardManager.GiveReward(reward);
                    results.Add(reward);
                }
                else
                {
                    ItemData fragment = GetRandomItem(RewardType.CrewFragment);

                    results.Add(new GachaReward
                    {
                        RewardType = RewardType.CrewFragment,
                        RewardId = fragment.ItemId,
                        Amount = 1
                    });
                }

                onSuccess?.Invoke(results);
            },
            onFail);
    }

    // 단일 뽑기
    public void MaterialDrawSingle(Action<GachaReward> onSuccess, Action<string> onFail)
    {
        PlayFabGateway.Instance.Recruit.ConsumeRecruitCost(
            _recruitType1,
            1,
            response =>
            {
                GachaReward reward = MaterialDraw();

                if (reward == null) return;

                _rewardManager.GiveReward(reward);

                onSuccess?.Invoke(reward);
            },
            onFail);
    }
    private GachaReward MaterialDraw()
    {
        RewardType rewardType = RollRewardType();

        switch (rewardType)
        {
            case RewardType.Materials:
                {
                    ItemData material = GetRandomItem(RewardType.Materials);

                    if (material == null)
                        return null;

                    return new GachaReward
                    {
                        RewardType = RewardType.Materials,
                        RewardId = material.ItemId,
                        Amount = 1
                    };
                }

            case RewardType.CrewFragment:
                {
                    ItemData crewFragment = GetRandomItem(RewardType.CrewFragment);

                    if (crewFragment == null)
                        return null;

                    return new GachaReward
                    {
                        RewardType = RewardType.CrewFragment,
                        RewardId = crewFragment.ItemId,
                        Amount = 1
                    };
                }

            case RewardType.Crew:
                {
                    return CrewDraw();
                }
            default:
                Debug.LogError("알 수 없는 보상 유형입니다.");
                break;
        }

        return null;
    }

    // 재료 뽑기 등급 결정
    private RewardType RollRewardType()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);

        if (roll < _crewRate)
            return RewardType.Crew;
        if (roll < _crewRate + _crewFregmentsRate)
            return RewardType.CrewFragment;
        if (roll < _crewRate + _crewFregmentsRate + _materialRate)
            return RewardType.Materials;

        return RewardType.Materials;
    }

    private ItemData GetRandomItem(RewardType rewardType)
    {
        int randomIndex = 0;
        switch (rewardType)
        {
            case RewardType.CrewFragment:
                List<ItemData> crewFragments = _itemDataBase.CrewFragments;
                randomIndex = UnityEngine.Random.Range(0, crewFragments.Count);
                return crewFragments[randomIndex];

            case RewardType.Materials:
                List<ItemData> materials = _itemDataBase.Materials;
                randomIndex = UnityEngine.Random.Range(0, materials.Count);
                return materials[randomIndex];

            case RewardType.Crew:
                break;
        }

        return null;
    }
    #endregion
}
