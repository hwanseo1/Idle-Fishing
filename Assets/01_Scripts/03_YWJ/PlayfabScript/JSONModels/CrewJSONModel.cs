using System;
using System.Collections.Generic;


[Serializable]
public class CrewJSONModel
{
    public Dictionary<string, CrewInfo> crews = new();
}

[Serializable]
public class CrewInfo
{
    public string grade;

    public bool equipped;

    public int duplicateCount;

    public string lastPromotedAtUtc;

    public PassiveLevels passives = new();
}

[Serializable]
public class PassiveLevels
{
    public PassiveInfo BossFishSpecialization = new();

    public PassiveInfo FishRarityIncrease = new();

    public PassiveInfo AutoFishingSpeedIncrease = new();

    public PassiveInfo OfflineRewardEfficiency = new();

    public PassiveInfo MultiplayerContributionIncrease = new();
}

[Serializable]
public class PassiveInfo
{
    public int level;

    public int levelProgress;
}