using System.Collections.Generic;
using UnityEngine;
using Fisher.Data;


namespace RMS.Multiplay
{
    // itemId / currencyCode -> 아이콘, 표시 이름 조회용 레지스트리.

    [CreateAssetMenu(fileName = "ItemIconRegistry", menuName = "Scriptable Objects/ItemIconRegistry")]
    public class ItemIconRegistry : ScriptableObject
    {
        [Header("일반 아이템 (itemId 기준)")]
        [Tooltip("Assets/03_Data/05_CSH/Items 폴더의 ItemData를 등록. 컴포넌트 우클릭 -> '폴더에서 자동 수집'으로 채울 수 있다.")]
        [SerializeField] private ItemData[] _items;

        [Header("재화 (currencyCode 기준, GD/PP/PC)")]
        [Tooltip("ItemData로 등록되어 있지 않은 재화 아이콘을 직접 등록한다.")]
        [SerializeField] private CurrencyIconEntry[] _currencyIcons;

        [System.Serializable]
        public class CurrencyIconEntry
        {
            [Tooltip("GD, PP, PC 등 PlayFab VirtualCurrency 코드")]
            public string currencyCode;
            public string displayName;
            public Sprite icon;
        }

        private Dictionary<string, ItemData> _itemLookup;
        private Dictionary<string, CurrencyIconEntry> _currencyLookup;

        // itemId 또는 currencyCode 둘 다 이 메서드 하나로 조회 가능 (호출부에서 구분할 필요 없음).
        public Sprite GetIcon(string id)
        {
            EnsureLookup();

            if (_itemLookup.TryGetValue(id, out var item))
                return item.Icon;

            if (_currencyLookup.TryGetValue(id, out var currency))
                return currency.icon;

            return null;
        }

        public string GetDisplayName(string id)
        {
            EnsureLookup();

            if (_itemLookup.TryGetValue(id, out var item))
                return item.DisplayName;

            if (_currencyLookup.TryGetValue(id, out var currency))
                return string.IsNullOrEmpty(currency.displayName) ? id : currency.displayName;

            return id; // 매핑이 없으면 원본 id를 그대로 표시 (조용히 실패하지 않도록)
        }

        private void EnsureLookup()
        {
            if (_itemLookup != null && _currencyLookup != null) return;

            _itemLookup = new Dictionary<string, ItemData>();
            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                    _itemLookup[item.ItemId] = item;
                }
            }

            _currencyLookup = new Dictionary<string, CurrencyIconEntry>();
            if (_currencyIcons != null)
            {
                foreach (var entry in _currencyIcons)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.currencyCode)) continue;
                    _currencyLookup[entry.currencyCode] = entry;
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("폴더에서 자동 수집 (Assets/03_Data/05_CSH/Items)")]
        private void CollectFromFolder()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/03_Data/05_CSH/Items" });
            var list = new List<ItemData>();

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null) list.Add(item);
            }

            _items = list.ToArray();
            _itemLookup = null; // 다음 조회 시 재구성되도록 캐시 무효화
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ItemIconRegistry] {_items.Length}개 ItemData 수집 완료.");
        }
#endif
    }
}