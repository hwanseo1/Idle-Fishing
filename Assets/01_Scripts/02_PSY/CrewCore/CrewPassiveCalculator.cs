using System.Collections.Generic;

namespace Crew
{
    public static class CrewPassiveCalculator
    {
        // 선원 패시브 효과 계산
        public static int GetTotalPassiveLevel(List<CrewInstanceData> crews, CrewPassiveType passiveType)
        {
            int total = 0;

            foreach (var crew in crews)
            {
                if (crew.Passives == null)
                    continue;

                foreach (var passive in crew.Passives)
                {
                    if (passive.Type == passiveType)
                    {
                        total += passive.Level;
                    }
                }
            }

            return total;
        }

        public static float GetFishRarityBonus(List<CrewInstanceData> crews)
        {
            int level = GetTotalPassiveLevel(crews, CrewPassiveType.FishRarityIncrease);

            return level * 0.02f;
        }

        public static float GetOfflineRewardBonus(List<CrewInstanceData> crews)
        {
            int level = GetTotalPassiveLevel(crews, CrewPassiveType.OfflineRewardEfficiency);

            return level * 0.05f;
        }

        public static float GetBossFishBonus(List<CrewInstanceData> crews)
        {
            int level = GetTotalPassiveLevel(crews, CrewPassiveType.BossFishSpecialization);

            return level * 0.05f;
        }

        public static float GetMultiplayerContributionBonus(List<CrewInstanceData> crews)
        {
            int level = GetTotalPassiveLevel(crews, CrewPassiveType.MultiplayerContributionIncrease);
            return level * 0.03f;
        }

        public static float GetAutoFishingSpeedBonus(List<CrewInstanceData> crews)
        {
            int level = GetTotalPassiveLevel(crews, CrewPassiveType.AutoFishingSpeedIncrease);
            return level * 0.02f;
        }
    }
}