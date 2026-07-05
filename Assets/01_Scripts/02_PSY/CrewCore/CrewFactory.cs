using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crew
{
    public static class CrewFactory
    {
        // 선원 생성 팩토리
        public static CrewInstanceData CreateCrew(string crewId, CrewGrade grade)
        {
            CrewInstanceData crew = new CrewInstanceData();

            crew.CrewId = crewId;

            crew.Grade = grade;

            crew.Passives = GeneratePassives(grade);

            return crew;
        }

        public static List<CrewPassiveData> GeneratePassives(CrewGrade grade)
        {
            List<CrewPassiveData> passives = new List<CrewPassiveData>();

            List<CrewPassiveType> types = new List<CrewPassiveType>(
                (CrewPassiveType[])Enum.GetValues(typeof(CrewPassiveType)));

            for (int i = 0; i < 3; i++)
            {
                int index = UnityEngine.Random.Range(0, types.Count);

                passives.Add(
                    new CrewPassiveData
                    {
                        Type = types[index],
                        Level = 1
                    });

                types.RemoveAt(index);
            }

            return passives;
        }

        public static int GetMaxPassiveLevel(CrewGrade grade)
        {
            switch (grade)
            {
                case CrewGrade.R:
                    return 5;

                case CrewGrade.SR:
                    return 10;

                case CrewGrade.SSR:
                    return 15;

                default:
                    return 5;
            }
        }

        public static CrewInstanceData CreateStarterCrew()
        {
            return new CrewInstanceData
            {
                CrewId = "Crew_Navigator_Jun",

                Grade = CrewGrade.R,

                DuplicateCount = 0,

                Passives = new List<CrewPassiveData>
                    {
                        new CrewPassiveData
                        {
                            Type = CrewPassiveType.FishRarityIncrease,
                            Level = 1
                        },

                        new CrewPassiveData
                        {
                            Type = CrewPassiveType.AutoFishingSpeedIncrease,
                            Level = 1
                        },

                        new CrewPassiveData
                        {
                            Type = CrewPassiveType.OfflineRewardEfficiency,
                            Level = 1
                        }
                    }
            };
        }

        public static CrewInstanceData CreateFromJson(string crewId, CrewInfo info)
        {
            CrewInstanceData crew = new();

            crew.CrewId = crewId;

            crew.Grade = Enum.Parse<CrewGrade>(info.grade);

            crew.DuplicateCount = info.duplicateCount;

            crew.Passives = new List<CrewPassiveData>();

            void Add(CrewPassiveType type, PassiveInfo p)
            {
                crew.Passives.Add(new CrewPassiveData
                {
                    Type = type,
                    Level = p.level,
                    LevelProgress = p.levelProgress
                });
            }

            Add(CrewPassiveType.BossFishSpecialization,
                info.passives.BossFishSpecialization);

            Add(CrewPassiveType.FishRarityIncrease,
                info.passives.FishRarityIncrease);

            Add(CrewPassiveType.AutoFishingSpeedIncrease,
                info.passives.AutoFishingSpeedIncrease);

            Add(CrewPassiveType.OfflineRewardEfficiency,
                info.passives.OfflineRewardEfficiency);

            Add(CrewPassiveType.MultiplayerContributionIncrease,
                info.passives.MultiplayerContributionIncrease);

            return crew;
        }
    }
}