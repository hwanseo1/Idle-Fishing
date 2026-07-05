using RMS.Data;
using UnityEngine;
using System.Collections.Generic;

public class StageDictionaryConvertor : MonoBehaviour
{
    [Header("1 스테이지")]
    [SerializeField] private StageData[] _stage1Datas;

    [Header("2 스테이지")]
    [SerializeField] private StageData[] _stage2Datas;

    [Header("3 스테이지")]
    [SerializeField] private StageData[] _stage3Datas;


    private Dictionary<string, StageData> _allStageDataDictionary;

    private void Awake()
    {
        StageDataToDictionary();
    }


    private void StageDataToDictionary()
    {
        _allStageDataDictionary = new Dictionary<string, StageData>();

        foreach (var stageData in _stage1Datas)
        {
            _allStageDataDictionary[stageData.StageId] = stageData;
        }

        foreach (var stageData in _stage2Datas)
        {
            _allStageDataDictionary[stageData.StageId] = stageData;
        }

        foreach (var stageData in _stage3Datas)
        {
            _allStageDataDictionary[stageData.StageId] = stageData;
        }
    }

    public StageData GetStageDataById(string stageId)
    {
        if (_allStageDataDictionary.TryGetValue(stageId, out var stageData))
        {
            return stageData;
        }
        else
        {
            Debug.LogWarning($"Stage ID '{stageId}' not found in the dictionary.");
            return null;
        }
    }
}
