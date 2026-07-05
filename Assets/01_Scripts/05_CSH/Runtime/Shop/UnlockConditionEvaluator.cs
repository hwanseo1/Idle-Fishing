using System;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// CSV unlockCondition/visibilityCondition 문자열을 현재 런타임 상태와 경제 파라미터 기준으로 평가합니다.
    /// 서버 상품 노출 조건과 로컬 CSV 조건식이 같은 문법을 쓰도록 이 파일을 기준 계약으로 둡니다.
    /// </summary>
    internal static class UnlockConditionEvaluator
    {
        #region Evaluation

        public static bool IsUnlocked(string condition, BalanceCatalog catalog, PlayerRuntimeState state)
        {
            string normalized = string.IsNullOrWhiteSpace(condition) ? string.Empty : condition.Trim();
            return AreConditionsMet(normalized, catalog, state);
        }

        public static bool IsVisible(ShopItemDefinition shopItem, BalanceCatalog catalog, PlayerRuntimeState state)
        {
            return shopItem != null && AreConditionsMet(shopItem.VisibilityCondition, catalog, state);
        }

        private static bool AreConditionsMet(string condition, BalanceCatalog catalog, PlayerRuntimeState state)
        {
            string normalized = string.IsNullOrWhiteSpace(condition) ? string.Empty : condition.Trim();
            if (normalized.Length == 0 ||
                normalized.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("always", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = normalized.Split(new[] { "&&", ";" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!AreConditionsMet(parts[i], catalog, state))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (normalized.StartsWith("stage>=", StringComparison.Ordinal))
            {
                return IsStageUnlocked(normalized, state);
            }

            if (normalized.StartsWith("currentStage>=", StringComparison.Ordinal))
            {
                return IsSpecificStageUnlocked(normalized, "currentStage>=", state == null ? 0 : state.currentStage);
            }

            if (normalized.StartsWith("farthestStage>=", StringComparison.Ordinal))
            {
                return IsSpecificStageUnlocked(normalized, "farthestStage>=", state == null ? 0 : state.farthestStage);
            }

            if (normalized.Equals("bm_placeholder", StringComparison.Ordinal))
            {
                return ReadEconomyBool(catalog, "bm_placeholder_enabled", false);
            }

            if (normalized.StartsWith("param:", StringComparison.Ordinal))
            {
                return ReadEconomyBool(catalog, normalized.Substring("param:".Length), false);
            }

            if (TryParseThreshold(normalized, "currency:", out string currency, out long requiredCurrency))
            {
                return FisherCurrencyContract.GetBalance(state, currency) >= requiredCurrency;
            }

            if (TryParseThreshold(normalized, "owned:", out string ownedItemId, out long requiredOwned))
            {
                return CountOwnedItem(state, ownedItemId) >= requiredOwned;
            }

            if (TryParseThreshold(normalized, "acquired:", out string acquiredItemId, out long requiredAcquired))
            {
                return CountAcquiredItem(state, acquiredItemId) >= requiredAcquired;
            }

            return false;
        }

        #endregion

        #region Helpers

        private static bool IsStageUnlocked(string condition, PlayerRuntimeState state)
        {
            if (state == null)
            {
                return false;
            }

            string rawStage = condition.Substring("stage>=".Length);
            if (!int.TryParse(rawStage, out int requiredStage))
            {
                return false;
            }

            int reachedStage = Math.Max(state.currentStage, state.farthestStage);
            return reachedStage >= requiredStage;
        }

        private static bool IsSpecificStageUnlocked(string condition, string prefix, int stageValue)
        {
            string rawStage = condition.Substring(prefix.Length);
            return int.TryParse(rawStage, out int requiredStage) && stageValue >= requiredStage;
        }

        private static bool TryParseThreshold(string condition, string prefix, out string key, out long required)
        {
            key = string.Empty;
            required = 0;

            if (!condition.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string body = condition.Substring(prefix.Length);
            int separator = body.IndexOf(">=", StringComparison.Ordinal);
            if (separator <= 0 || separator + 2 >= body.Length)
            {
                return false;
            }

            key = body.Substring(0, separator);
            string rawRequired = body.Substring(separator + 2);
            return !string.IsNullOrEmpty(key) && long.TryParse(rawRequired, out required);
        }

        private static long CountOwnedItem(PlayerRuntimeState state, string itemId)
        {
            if (state == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            long total = 0;
            for (int i = 0; i < state.inventoryEntries.Count; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null || entry.itemId != itemId || entry.count <= 0)
                {
                    continue;
                }

                total += entry.count;
                if (total >= long.MaxValue - int.MaxValue)
                {
                    return long.MaxValue;
                }
            }

            return total;
        }

        private static long CountAcquiredItem(PlayerRuntimeState state, string itemId)
        {
            if (state == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            return state.itemAcquisitionCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        private static bool ReadEconomyBool(BalanceCatalog catalog, string key, bool fallback)
        {
            if (catalog == null ||
                !catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param) ||
                param == null ||
                !param.IsEnabled)
            {
                return fallback;
            }

            return bool.TryParse(param.Value, out bool value) ? value : fallback;
        }

        #endregion
    }
}
