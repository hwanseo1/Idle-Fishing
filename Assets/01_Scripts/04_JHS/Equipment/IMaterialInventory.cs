using System.Collections.Generic;

namespace JHS.Equipment
{
    // 강화에 필요한 재료 1종 (아이템 ID + 수량).
    // 재료 ID는 string — 추후 어느 인벤토리로 통일되든 어댑터에서 변환한다(예: CSH는 string, YWJ는 int).
    [System.Serializable]
    public struct MaterialCost
    {
        public string materialId;
        public int count;
    }

    // 재료 인벤토리 (실제 구현은 인벤토리 파트). 강화 재료 차감에만 사용.
    // 다른 모듈의 코드는 건드리지 않고, 이 인터페이스를 구현한 어댑터를 주입해서 연결한다.
    public interface IMaterialInventory
    {
        int GetCount(string materialId);
        bool Has(IReadOnlyList<MaterialCost> costs);
        bool TryConsume(IReadOnlyList<MaterialCost> costs);  // 원자적: 전부 차감되거나 하나도 안 됨
    }

    // 재료 비용 정규화 유틸. 같은 materialId가 여러 번 들어와도 합산해 한 항목으로 만든다.
    // (합산하지 않으면 Has가 같은 ID를 개별 검사해 보유량을 중복으로 인정 → 오탐)
    internal static class MaterialCostUtil
    {
        public static List<MaterialCost> Aggregate(IReadOnlyList<MaterialCost> costs)
        {
            var sums = new Dictionary<string, int>();
            if (costs != null)
                foreach (var c in costs)
                    if (c.count > 0 && !string.IsNullOrEmpty(c.materialId))
                        sums[c.materialId] = (sums.TryGetValue(c.materialId, out var n) ? n : 0) + c.count;

            var list = new List<MaterialCost>(sums.Count);
            foreach (var kv in sums)
                list.Add(new MaterialCost { materialId = kv.Key, count = kv.Value });
            return list;
        }
    }

    // 인벤토리 시스템 없을 때 쓰는 더미 (기본적으로 모든 재료가 충분하다고 가정)
    public sealed class DummyMaterialInventory : IMaterialInventory
    {
        private readonly Dictionary<string, int> _stock = new();
        private readonly int _default;

        public DummyMaterialInventory(int defaultStock = 999999) => _default = defaultStock;

        public int GetCount(string materialId)
            => _stock.TryGetValue(materialId, out var n) ? n : _default;

        public bool Has(IReadOnlyList<MaterialCost> costs)
        {
            foreach (var c in MaterialCostUtil.Aggregate(costs))
                if (GetCount(c.materialId) < c.count) return false;
            return true;
        }

        public bool TryConsume(IReadOnlyList<MaterialCost> costs)
        {
            var needs = MaterialCostUtil.Aggregate(costs);
            foreach (var c in needs)
                if (GetCount(c.materialId) < c.count) return false;   // 선확인 (원자성)
            foreach (var c in needs)
                _stock[c.materialId] = GetCount(c.materialId) - c.count;
            return true;
        }
    }
}
