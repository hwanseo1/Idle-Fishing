using System.Collections.Generic;
using UnityEngine;
using Fisher.PlayerSystems;   // CSH 인벤토리 (코드 수정 없이 public API만 호출)

namespace JHS.Equipment
{
    // 강화 재료 차감을 CSH InventoryService에 연결하는 어댑터.
    // - 선호님(CSH) 코드는 건드리지 않고, 공개된 FisherRuntimeContext / InventoryService.public API만 호출한다.
    // - 씬에 FisherRuntimeContext가 없거나(내 씬 등) 준비 안 됐으면 _fallback(기본 더미)으로 자동 폴백한다.
    //   → 통합 씬에 인벤토리가 올라오고 items.csv에 재료가 등록되는 순간, 코드 변경 없이 진짜 인벤토리로 전환됨.
    public sealed class CshMaterialInventoryAdapter : IMaterialInventory
    {
        private readonly IMaterialInventory _fallback;
        private FisherRuntimeContext _context;

        public CshMaterialInventoryAdapter(IMaterialInventory fallback = null)
            => _fallback = fallback ?? new DummyMaterialInventory();

        // 실제 인벤토리에 연결돼 있으면 true, 아니면 폴백 사용 중.
        public bool IsLive => Resolve() != null;

        // 살아있는 FisherRuntimeContext를 찾아 캐시. 없거나 준비 안 됐으면 null.
        private FisherRuntimeContext Resolve()
        {
            if (_context != null && _context.IsReady) return _context;
            // 씬 전환 등으로 캐시가 죽었으면 다시 탐색 (강화는 유저 조작이라 호출 빈도 낮음)
            _context = Object.FindFirstObjectByType<FisherRuntimeContext>();
            return (_context != null && _context.IsReady) ? _context : null;
        }

        private InventoryService Service => Resolve()?.InventoryService;

        public int GetCount(string materialId)
        {
            var svc = Service;
            if (svc == null) return _fallback.GetCount(materialId);
            return CountOwned(svc, materialId);
        }

        public bool Has(IReadOnlyList<MaterialCost> costs)
        {
            var svc = Service;
            if (svc == null) return _fallback.Has(costs);

            foreach (var c in MaterialCostUtil.Aggregate(costs))
                if (CountOwned(svc, c.materialId) < c.count) return false;
            return true;
        }

        public bool TryConsume(IReadOnlyList<MaterialCost> costs)
        {
            var svc = Service;
            if (svc == null) return _fallback.TryConsume(costs);

            // 같은 재료 ID를 합산해 정규화 (중복 항목 오탐 방지)
            var needs = MaterialCostUtil.Aggregate(costs);

            // 원자적 보장: 먼저 전부 충분한지 확인
            foreach (var c in needs)
                if (CountOwned(svc, c.materialId) < c.count) return false;

            // 차감 — 도중 실패 시 이미 차감한 재료를 되돌린다(방어용; 선확인 후엔 거의 안 일어남)
            var consumed = new List<MaterialCost>();
            foreach (var c in needs)
            {
                var r = svc.TryConsumeItem(c.materialId, c.count);
                if (r == null || !r.Success)
                {
                    foreach (var done in consumed)
                        svc.TryAddItem(done.materialId, done.count);   // 롤백
                    return false;
                }
                consumed.Add(c);
            }
            return true;
        }

        // CSH InventoryService에는 "현재 보유 개수" 공개 메서드가 없어, 스냅샷에서 직접 합산한다.
        // (스택형 기준: instanceId 없고 levelIndex<0 인 엔트리만 — TryConsumeItem이 소비하는 대상과 동일)
        private static int CountOwned(InventoryService svc, string materialId)
        {
            var snapshot = svc.GetInventorySnapshot();
            if (snapshot == null) return 0;

            long total = 0;
            foreach (var e in snapshot)
            {
                if (e == null || e.itemId != materialId) continue;
                if (!string.IsNullOrEmpty(e.instanceId) || e.levelIndex >= 0) continue;
                total += e.count;
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return total <= 0 ? 0 : (int)total;
        }
    }
}
