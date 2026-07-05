using JHS.Backend;

namespace JHS.PatchTool
{
    // 패치 도구가 다룰 Title Data 대상 목록. 새 대상은 여기 한 줄 추가(키 + 검증기) — 도구 본체 무수정(OCP).
    // [0]=기본=대상 미선택(빈 키)로 두어, 도구를 열었을 때의 기본 대상이 항상 무해하게(빈 키는 푸시 차단).
    public static class PatchTargets
    {
        public static readonly PatchTarget[] All =
        {
            new PatchTarget("(대상 선택 안 함)",          "",                         false, null),
            new PatchTarget("장비 강화비용",              "EquipmentUpgradeCostList", false, UpgradeCostTable.Validate),
            new PatchTarget("배 강화비용",                "BoatUpgradeCostList",      false, UpgradeCostTable.Validate),
            new PatchTarget("배 해금비용",                "BoatUnlockCostList",       false, BoatUnlockCostTable.Validate),
            new PatchTarget("RemoteConfig (버전/점검)",   "RemoteConfig",              false, RemoteConfig.Validate),
        };

        public static string[] DisplayNames()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].DisplayName;
            return names;
        }
    }
}
