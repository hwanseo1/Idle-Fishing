using RMS.Data;
using RMS.Fishing;
using System;
using UnityEngine;
using UnityEngine.UI;

public class StageBackgroundController : MonoBehaviour
{
    [Serializable]
    public class MapBackground
    {
        public int mapId;
        public Sprite normalBackground;
        public Sprite bossBackground;
    }

    [Header("Background")]
    [SerializeField] private SpriteRenderer _backgroundImage;

    [Header("Map Backgrounds")]
    [SerializeField] private MapBackground[] _backgrounds;

    [Header("Fish Spawn Manager")]
    [SerializeField] private FishSpawnManager _fishSpawnManager;

    private void Awake()
    {
        _backgroundImage = GetComponent<SpriteRenderer>();

        _fishSpawnManager = FindFirstObjectByType<FishSpawnManager>();
    }

    private void OnEnable()
    {
        if (_fishSpawnManager != null)
        {
            _fishSpawnManager.OnStageChanged += OnStageChanged;
        }
    }

    private void OnDisable()
    {
        if (_fishSpawnManager != null)
        {
            _fishSpawnManager.OnStageChanged -= OnStageChanged;
        }
    }

    private void OnStageChanged(StageData stageData)
    {
        if (stageData == null)
            return;

        SetBackground(stageData.StageId);
    }

    public void SetBackground(string stageId)
    {
        if (!TryParseStage(stageId, out int mapId, out bool isBoss))
        {
            Debug.LogWarning($"잘못된 StageId : {stageId}");
            return;
        }

        foreach (var bg in _backgrounds)
        {
            if (bg.mapId != mapId)
                continue;

            _backgroundImage.sprite =
                isBoss ? bg.bossBackground : bg.normalBackground;

            return;
        }

        Debug.LogWarning($"Map {mapId}의 배경이 없습니다.");
    }

    private bool TryParseStage(string stageId, out int mapId, out bool isBoss)
    {
        mapId = 0;
        isBoss = false;

        string[] split = stageId.Split('-');

        if (split.Length != 2)
            return false;

        if (!int.TryParse(split[0], out mapId))
            return false;

        isBoss = split[1].Equals("boss", StringComparison.OrdinalIgnoreCase);

        return true;
    }
}
