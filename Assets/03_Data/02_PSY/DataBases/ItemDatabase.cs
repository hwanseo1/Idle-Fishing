using Fisher.Data;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Scriptable Objects/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> CrewFragments;
    public List<ItemData> Materials;
    public List<ItemData> Foods;
}