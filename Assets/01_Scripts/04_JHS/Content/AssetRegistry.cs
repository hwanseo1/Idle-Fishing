using System.Collections.Generic;
using UnityEngine;

namespace JHS.Content
{
    // 공통 종류 ID → 그 ID의 실제 데이터(정의 SO)를 에디터에서 손으로 매핑하는 레지스트리(인앱 동봉).
    // 한 ID = 한 진짜 데이터 엔티티(equip_rod = 낚싯대 데이터). 아이콘 등은 그 데이터(SO) 안에 들어있다 —
    // sprite/png를 ID로 따로 구분하지 않는다. RegistryAssetProvider가 이걸 읽어 동기 조회한다(데이터드리븐).
    [CreateAssetMenu(menuName = "Data/Asset Registry", fileName = "AssetRegistry")]
    public sealed class AssetRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string id;       // 공통 종류 ID (equip_* / boat_* / fish_* …) = 데이터 엔티티 ID
            public Object asset;     // 그 ID의 실제 데이터(정의 SO)
        }

        [SerializeField] private Entry[] _entries;

        // id → 그 id로 등록된 에셋들 (지연 빌드)
        private Dictionary<string, List<Object>> _map;

        // id로 등록된 에셋 중 타입 T인 첫 항목. 없으면 null.
        public T Get<T>(string id) where T : Object
        {
            if (_map == null) Build();
            if (!string.IsNullOrEmpty(id) && _map.TryGetValue(id, out var list))
                foreach (var a in list)
                    if (a is T t) return t;
            return null;
        }

        private void Build()
        {
            _map = new Dictionary<string, List<Object>>();
            if (_entries == null) return;
            foreach (var e in _entries)
            {
                if (string.IsNullOrEmpty(e.id) || e.asset == null) continue;
                if (!_map.TryGetValue(e.id, out var list))
                    _map[e.id] = list = new List<Object>();
                list.Add(e.asset);
            }
        }
    }
}
