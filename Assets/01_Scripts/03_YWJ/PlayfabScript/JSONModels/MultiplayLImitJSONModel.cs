using System;
using System.Collections.Generic;

[Serializable]
public class MultiplayLimitJSONModel
{
    public int playCount;
    public int maxPlayCount;
    public bool canPlay;
    public string lastResetAtUtc;
}