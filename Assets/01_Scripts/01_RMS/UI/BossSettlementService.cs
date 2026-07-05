using Fisher.PlayerSystems;
using RMS.Data;
using System.Collections.Generic;
using UnityEngine;


namespace RMS.UI
{
    // 보스 클리어 보상 결과 1건. UI 정산 패널 표시용.
    public struct BossRewardResult
    {
        public string FishId;
        public int Amount;
        public bool IsLegendary;
        public UnityEngine.Sprite Icon; // FishData.Icon에서 직접 가져옴
    }

    // 싱글 보스(StageBoss) 클리어 정산.
    // 보스 스테이지가 속한 지역의 물고기 풀을 StageData 체인에서 읽어
    // - 일반/희귀/에픽 물고기 → 전 종 x25 확정 지급
    // - 레전더리 물고기    → 15% 확률로 각 종 3~5마리 지급
    // 아이템 지급은 FisherExternalRewardBridge를 통해 CSH 가방/서버에 반영한다.
    public static class BossSettlementService
    {
        private const float LegendaryDropChance = 0.15f;
        private const int NormalFishAmount = 25;
        private const int LegendaryMinAmount = 3;
        private const int LegendaryMaxAmount = 5;
        private const string RewardSource = "boss_clear";

        // 보스 클리어 보상을 지급하고, UI 표시용 결과 목록을 반환한다.
        // 반환값이 비어 있으면 지급 가능한 물고기가 없거나 조건 미충족.
        public static List<BossRewardResult> SettleClear(BossData boss, StageData bossStage, StageData firstStage)
        {
            var results = new List<BossRewardResult>();

            if (boss == null)
            {
                Debug.LogWarning("[BossSettlementService] boss가 null입니다.");
                return results;
            }
            if (boss.BossType != BossType.StageBoss)
            {
                Debug.LogWarning($"[BossSettlementService] {boss.BossId}는 StageBoss가 아닙니다.");
                return results;
            }
            if (bossStage == null || firstStage == null)
            {
                Debug.LogWarning("[BossSettlementService] bossStage 또는 firstStage가 null입니다.");
                return results;
            }

            FishEntry[] entries = StageData.GetFishEntriesUpToStage(firstStage, bossStage.StageId);

            var normalFishIds = new List<(string fishId, UnityEngine.Sprite icon)>();
            var legendaryFishIds = new List<(string fishId, UnityEngine.Sprite icon)>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (FishEntry entry in entries)
            {
                if (entry == null || entry.fishData == null) continue;
                string fishId = entry.fishData.FishId;
                if (string.IsNullOrEmpty(fishId) || !seen.Add(fishId)) continue;

                if (entry.fishData.Rarity == FishRarity.Legendary)
                    legendaryFishIds.Add((fishId, entry.fishData.Icon));
                else
                    normalFishIds.Add((fishId, entry.fishData.Icon));
            }

            // 일반/희귀/에픽 → 전 종 x25 확정 지급
            foreach (var (fishId, icon) in normalFishIds)
            {
                FisherExternalRewardBridge.TryGrantItem(fishId, NormalFishAmount, RewardSource);
                results.Add(new BossRewardResult { FishId = fishId, Amount = NormalFishAmount, IsLegendary = false, Icon = icon });
            }

            // 레전더리 → 15% 확률로 각 종 3~5마리
            bool legendaryDropped = legendaryFishIds.Count > 0 && Random.value < LegendaryDropChance;
            if (legendaryDropped)
            {
                foreach (var (fishId, icon) in legendaryFishIds)
                {
                    int amount = Random.Range(LegendaryMinAmount, LegendaryMaxAmount + 1);
                    FisherExternalRewardBridge.TryGrantItem(fishId, amount, RewardSource + "_legendary");
                    results.Add(new BossRewardResult { FishId = fishId, Amount = amount, IsLegendary = true, Icon = icon });
                }
            }

            FisherExternalRewardBridge.TryFlushQueuedRewards();

            Debug.Log($"<color=#FFD700>[BossSettlementService] {boss.BossId} 클리어 보상 지급 완료 — 일반 {normalFishIds.Count}종 x{NormalFishAmount}, 레전더리 드롭: {legendaryDropped}</color>");
            return results;
        }
    }
}