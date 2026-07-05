using Crew;
using UnityEngine;
using Reward;

public class RewardManager : MonoBehaviour
{
    public void GiveReward(GachaReward reward)
    {
        switch (reward.RewardType)
        {
            case RewardType.Crew:
                GiveCrew(reward);
                break;

            case RewardType.CrewFragment:
                GiveCrewFragment(reward);
                break;

            case RewardType.Materials:
                GiveMaterials(reward);
                break;
        }
    }

    private void GiveCrew(GachaReward reward)
    {
        CrewData data = CrewManager.Instance.CrewDataBase.GetCrewData(reward.RewardId);

        if (data == null)
            return;

        // 먼저 중복 여부 판단
        reward.IsDuplicate = CrewManager.Instance.HasCrew(data.CrewId);

        if (reward.IsDuplicate)
        {
            string fragmentId = $"fragment_{data.CrewId}";

            // 지급 전 개수
            int before = CrewManager.Instance.GetFragmentCount(fragmentId);

            int amount = data.CrewGrade switch
            {
                CrewGrade.R => 70,
                CrewGrade.SR => 90,
                CrewGrade.SSR => 120,
                _ => 0
            };

            reward.PreviousFragment = before;
            reward.CurrentFragment = before + amount;

            PlayFabGateway.Instance.Inventory.Add(fragmentId, amount);

            return;
        }

        CrewInstanceData crew = CrewFactory.CreateCrew(data.CrewId, data.CrewGrade);

        CrewManager.Instance.AddCrew(crew);
    }

    private void GiveCrewFragment(GachaReward reward)
    {
        PlayFabGateway.Instance.Inventory.Add(reward.RewardId, 1);
        Debug.Log($"선원 조각 지급 : {reward.RewardId}");
    }

    private void GiveMaterials(GachaReward reward)
    {
        PlayFabGateway.Instance.Inventory.Add(reward.RewardId, 1);
        Debug.Log($"재료 지급 : {reward.RewardId}");
    }
}