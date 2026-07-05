namespace JHS.Equipment
{
    // Title Data 비용표(EquipmentUpgradeCostList/BoatUpgradeCostList/BoatUnlockCostList) 조회 통로.
    // 강화/해금 "표시 비용"의 권위 출처를 SO → Title Data로 옮기기 위한 인터페이스.
    // 미로드·키없음·세션없음이면 Try*가 false 반환 → 호출측(EquipmentManager)이 SO값으로 폴백.
    // (실제 차감은 이미 서버 권위 — 이건 표시 정합성용. levelIndex는 JHS 0-base = Title Data levels["i"]와 직접 일치.)
    public interface IEquipmentCostCatalog
    {
        bool IsLoaded { get; }

        bool TryGetEquipmentUpgradeGold(string equipmentId, int levelIndex, out int gold);
        bool TryGetBoatUpgradeGold(string shipId, int levelIndex, out int gold);
        bool TryGetBoatUnlockGold(string shipId, out int gold);
    }
}
