using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GachaDictionaryConvertor : MonoBehaviour
{
    [Serializable]
    public class FragmentPrefabEntry
    {
        public string itemId;
        public Sprite fragmentSprite;
    }

    [Header("Crew Fragment Prefabs")]
    [SerializeField] private List<FragmentPrefabEntry> _fragmentPrefabs = new();

    private readonly Dictionary<string, Sprite> _fragmentPrefabDictionary = new();

    private void Awake()
    {
        BuildDictionary();
    }

    private void BuildDictionary()
    {
        _fragmentPrefabDictionary.Clear();

        foreach (var entry in _fragmentPrefabs)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.itemId) ||
                entry.fragmentSprite == null)
            {
                Debug.LogWarning("[GachaDictionaryConvertor] 잘못된 Entry가 있습니다.");
                continue;
            }

            if (_fragmentPrefabDictionary.ContainsKey(entry.itemId))
            {
                Debug.LogWarning($"[GachaDictionaryConvertor] 중복 ItemId : {entry.itemId}");
                continue;
            }

            _fragmentPrefabDictionary.Add(entry.itemId, entry.fragmentSprite);
        }

        Debug.Log($"[GachaDictionaryConvertor] 등록 완료 : {_fragmentPrefabDictionary.Count}개");
    }

    public Sprite GetFragmentSprite(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        if (_fragmentPrefabDictionary.TryGetValue(itemId, out Sprite fragmentSprite))
            return fragmentSprite;

        Debug.LogWarning($"[GachaDictionaryConvertor] 등록되지 않은 ItemId : {itemId}");
        return null;
    }

    public bool TryGetFragmentSprite(string itemId, out Sprite fragmentSprite)
    {
        return _fragmentPrefabDictionary.TryGetValue(itemId, out fragmentSprite);
    }
}