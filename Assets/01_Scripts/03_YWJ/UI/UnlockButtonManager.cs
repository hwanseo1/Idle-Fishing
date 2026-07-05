using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnlockableFeature
{
    Equipment,
    Ship,
    Crew,
    Cooking,
    Recruit,
    Shop,
    MoveToStage,
    Multiplay
}

[Serializable]
public class UnlockFeatureData
{
    public UnlockableFeature feature;

    [Tooltip("예: 1-2, 2-3, 3-boss")]
    public string unlockStageId;

    public GameObject lockImage;
}


public class UnlockButtonManager : MonoBehaviour
{
    [Header("Unlock Settings")]
    [SerializeField]
    private List<UnlockFeatureData> _unlockFeatures = new();

    private readonly Dictionary<UnlockableFeature, bool> _unlockStates = new();

    public bool IsUnlocked(UnlockableFeature feature)
    {
        return _unlockStates.TryGetValue(feature, out bool unlocked) && unlocked;
    }

    public void RefreshUnlockState()
    {
        if (PlayFabDataStore.Instance == null)
            return;

        PlayerInfo playerInfo = PlayFabDataStore.Instance.GetPlayerInfo();

        if (playerInfo == null || playerInfo.stage == null)
            return;

        string maxStageId = string.IsNullOrEmpty(playerInfo.stage.maxStageId)
            ? playerInfo.stage.currentStageId
            : playerInfo.stage.maxStageId;

        foreach (var data in _unlockFeatures)
        {
            bool unlocked = IsStageReached(maxStageId, data.unlockStageId);

            _unlockStates[data.feature] = unlocked;

            if (data.lockImage != null)
                data.lockImage.SetActive(!unlocked);
        }
    }


    private bool IsStageReached(string currentMaxStageId, string requiredStageId)
    {
        if (!TryParseStageId(currentMaxStageId, out int currentMap, out int currentStage))
            return false;

        if (!TryParseStageId(requiredStageId, out int requiredMap, out int requiredStage))
            return false;

        if (currentMap > requiredMap)
            return true;

        if (currentMap < requiredMap)
            return false;

        return currentStage >= requiredStage;
    }

    private bool TryParseStageId(string stageId, out int map, out int stage)
    {
        map = 1;
        stage = 1;

        if (string.IsNullOrEmpty(stageId))
            return false;

        string[] split = stageId.Split('-');

        if (split.Length != 2)
            return false;

        if (!int.TryParse(split[0], out map))
            return false;

        if (split[1].Equals("boss", System.StringComparison.OrdinalIgnoreCase))
        {
            stage = 999;
            return true;
        }

        return int.TryParse(split[1], out stage);
    }
}