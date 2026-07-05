using System;
using System.Collections.Generic;



[Serializable]
public class CrewSlotJSONModel
{
    public Dictionary<string, CrewSlotInfo> crewSlots = new();
}

[Serializable]
public class CrewSlotInfo
{
    public bool isUnlocked;
    public string equippedCrewId;
}