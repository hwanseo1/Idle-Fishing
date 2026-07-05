using System;
using System.Collections.Generic;

[Serializable]
public class EquipmentJSONModel
{
    public Dictionary<string, EquipmentInfo> equipments = new();
}

[Serializable]
public class EquipmentInfo
{
    public int level;
}