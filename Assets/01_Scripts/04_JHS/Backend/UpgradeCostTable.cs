using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace JHS.Backend
{
    // Title Data 'BoatUpgradeCostList'/'EquipmentUpgradeCostList'의 클라 읽기 + 푸시 검증 공용 DTO.
    // 선호 JSON 스키마와 1:1. dict(맵) 구조라 JsonUtility 불가 → Newtonsoft로 역직렬화.
    //   { "items": { "<id>": { "levels": { "<lv>": { goldCost, materials:[{itemId,count}] } } } } }
    public sealed class UpgradeCostTable
    {
        public Dictionary<string, Item> items;

        public sealed class Item
        {
            public Dictionary<string, Level> levels;   // 키 = 레벨 인덱스 문자열("0","1"…)
        }

        public sealed class Level
        {
            public int goldCost;
            public List<Material> materials;
        }

        public sealed class Material
        {
            public string itemId;
            public int count;
        }

        // JSON을 Newtonsoft로 역직렬화(dict 지원). 실패 시 예외.
        public static UpgradeCostTable Parse(string json) => JsonConvert.DeserializeObject<UpgradeCostTable>(json);

        // 스키마 검증 — 유효하면 null, 아니면 사유 문자열. (PatchService 푸시 전 검증·클라 로드 후 확인 공용)
        public static string Validate(string json)
        {
            UpgradeCostTable t;
            try { t = Parse(json); }
            catch (Exception e) { return $"역직렬화 실패: {e.Message}"; }

            if (t == null || t.items == null || t.items.Count == 0)
                return "items가 비어 있음";

            foreach (var entry in t.items)
            {
                Item item = entry.Value;
                if (item?.levels == null || item.levels.Count == 0)
                    return $"'{entry.Key}'의 levels가 비어 있음";

                foreach (var lv in item.levels)
                {
                    if (lv.Value == null) return $"'{entry.Key}' 레벨 '{lv.Key}'가 null";
                    if (lv.Value.goldCost < 0) return $"'{entry.Key}' 레벨 '{lv.Key}' goldCost 음수";
                }
            }
            return null;
        }
    }
}
