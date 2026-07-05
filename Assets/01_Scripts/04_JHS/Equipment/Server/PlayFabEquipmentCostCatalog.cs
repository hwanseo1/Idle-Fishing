using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using JHS.Backend;   // UpgradeCostTable / BoatUnlockCostTable (Title Data 비용표 DTO)

namespace JHS.Equipment
{
    // IEquipmentCostCatalog의 PlayFab 구현 — Title Data에서 비용표 3키를 한 번에 받아 파싱·캐시.
    // 기존 PlayFabBackendGateway와 같은 Client GetTitleData 경로. 로그인 세션 전제(없으면 인증오류→폴백).
    // 파싱 DTO는 04_JHS/Backend의 UpgradeCostTable/BoatUnlockCostTable 재사용(패치도구 푸시 검증과 동일 스키마).
    public sealed class PlayFabEquipmentCostCatalog : IEquipmentCostCatalog
    {
        private const string KeyEquipUpgrade = "EquipmentUpgradeCostList";
        private const string KeyBoatUpgrade = "BoatUpgradeCostList";
        private const string KeyBoatUnlock = "BoatUnlockCostList";

        private UpgradeCostTable _equip;
        private UpgradeCostTable _boatUpgrade;
        private BoatUnlockCostTable _boatUnlock;

        public bool IsLoaded { get; private set; }

        // Title Data 3키를 한 번에 받아 파싱·캐시. 실패해도 onDone(false) — 매니저는 SO로 폴백.
        public void Load(Action<bool> onDone = null)
        {
            var req = new GetTitleDataRequest
            {
                Keys = new List<string> { KeyEquipUpgrade, KeyBoatUpgrade, KeyBoatUnlock }
            };

            PlayFabClientAPI.GetTitleData(req,
                result =>
                {
                    try
                    {
                        var data = result.Data;
                        if (data != null)
                        {
                            if (data.TryGetValue(KeyEquipUpgrade, out var j1) && !string.IsNullOrEmpty(j1)) _equip = UpgradeCostTable.Parse(j1);
                            if (data.TryGetValue(KeyBoatUpgrade, out var j2) && !string.IsNullOrEmpty(j2)) _boatUpgrade = UpgradeCostTable.Parse(j2);
                            if (data.TryGetValue(KeyBoatUnlock, out var j3) && !string.IsNullOrEmpty(j3)) _boatUnlock = BoatUnlockCostTable.Parse(j3);
                        }

                        IsLoaded = _equip != null || _boatUpgrade != null || _boatUnlock != null;
                        if (IsLoaded) Debug.Log("[CostCatalog] Title Data 비용표 로드 완료");
                        else Debug.LogWarning("[CostCatalog] Title Data 비용표 비어있음 — SO 폴백 유지");
                        onDone?.Invoke(IsLoaded);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CostCatalog] 비용표 파싱 실패 — SO 폴백 유지: {e.Message}");
                        onDone?.Invoke(false);
                    }
                },
                error =>
                {
                    Debug.LogWarning($"[CostCatalog] GetTitleData 실패 — SO 폴백 유지: {error.Error} - {error.ErrorMessage}");
                    onDone?.Invoke(false);
                });
        }

        public bool TryGetEquipmentUpgradeGold(string equipmentId, int levelIndex, out int gold)
            => TryGetUpgradeGold(_equip, equipmentId, levelIndex, out gold);

        public bool TryGetBoatUpgradeGold(string shipId, int levelIndex, out int gold)
            => TryGetUpgradeGold(_boatUpgrade, shipId, levelIndex, out gold);

        // items[id].levels["levelIndex"].goldCost. 없으면 false(→SO 폴백). 음수 골드는 무효 처리.
        private static bool TryGetUpgradeGold(UpgradeCostTable table, string id, int levelIndex, out int gold)
        {
            gold = 0;
            if (table?.items == null || string.IsNullOrEmpty(id) || levelIndex < 0) return false;
            if (!table.items.TryGetValue(id, out var item) || item?.levels == null) return false;
            if (!item.levels.TryGetValue(levelIndex.ToString(), out var lv) || lv == null) return false;
            if (lv.goldCost < 0) return false;
            gold = lv.goldCost;
            return true;
        }

        public bool TryGetBoatUnlockGold(string shipId, out int gold)
        {
            gold = 0;
            if (_boatUnlock?.items == null || string.IsNullOrEmpty(shipId)) return false;
            if (!_boatUnlock.items.TryGetValue(shipId, out var item) || item == null) return false;
            if (item.goldCost < 0) return false;
            gold = item.goldCost;
            return true;
        }
    }
}
