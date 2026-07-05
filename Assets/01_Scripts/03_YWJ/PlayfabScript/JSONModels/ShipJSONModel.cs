using System;
using System.Collections.Generic;

[Serializable]
public class ShipJSONModel
{
    public Dictionary<string, ShipInfo> ships = new();
}

[Serializable]
public class ShipInfo
{
    public int level;
    public bool isOpened;
    public bool equipped;
}