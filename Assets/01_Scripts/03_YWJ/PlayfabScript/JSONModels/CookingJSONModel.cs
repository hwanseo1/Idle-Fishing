using System;
using System.Collections.Generic;

[Serializable]
public class CookingJSONModel
{
    public Dictionary<string, CookingSlotInfo> cookSlots = new();
}

[Serializable]
public class CookingSlotInfo
{
    public bool isOpened;
    public CookingJobInfo job;
}

[Serializable]
public class CookingJobInfo
{
    public string recipeId;
    public int totalCount;
    public int claimedCount;
    public int durationSec;
    public string startedAtUtc;
}