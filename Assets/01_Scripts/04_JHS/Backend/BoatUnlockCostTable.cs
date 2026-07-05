using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace JHS.Backend
{
    // Title Data 'BoatUnlockCostList'의 클라 읽기 + 푸시 검증 공용 DTO.
    // 배 1척당 "1회 해금 골드 비용"(레벨 개념 없음). 강화비용(UpgradeCostTable)과 같은 items 봉투 규약.
    //   { "items": { "<boatId>": { "goldCost": N } } }   // 키 = boat_* 공통 ID
    // ⚠️ 서버(원준)가 이 키를 읽어 해금 권위검증/골드차감에 쓰는 게 Phase 2 목표 — 필드명은 서버와 합의 시 고정.
    public sealed class BoatUnlockCostTable
    {
        public Dictionary<string, Item> items;

        public sealed class Item
        {
            public int goldCost;   // 해금 1회 골드 비용 (음수 불가)
        }

        // JSON을 Newtonsoft로 역직렬화(dict 지원). 실패 시 예외.
        public static BoatUnlockCostTable Parse(string json) => JsonConvert.DeserializeObject<BoatUnlockCostTable>(json);

        // 스키마 검증 — 유효하면 null, 아니면 사유 문자열. (푸시 전 검증·클라 로드 후 확인 공용)
        public static string Validate(string json)
        {
            BoatUnlockCostTable t;
            try { t = Parse(json); }
            catch (Exception e) { return $"역직렬화 실패: {e.Message}"; }

            if (t == null || t.items == null || t.items.Count == 0)
                return "items가 비어 있음";

            foreach (var entry in t.items)
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) return "빈 배 ID 존재";
                if (entry.Value == null) return $"'{entry.Key}' 항목이 null";
                if (entry.Value.goldCost < 0) return $"'{entry.Key}' goldCost 음수";
            }
            return null;
        }
    }
}
