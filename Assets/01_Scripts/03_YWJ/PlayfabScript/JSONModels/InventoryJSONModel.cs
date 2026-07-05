using System;
using System.Collections.Generic;

[Serializable]
public class InventoryJSONModel
{
    public Dictionary<string, InventoryInfo> inventoryItems = new();
}

[Serializable]
public class InventoryInfo
{
    public int itemCount;
}