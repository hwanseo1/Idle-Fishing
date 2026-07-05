using System;
using System.Collections.Generic;
using PlayFab;
using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// Entry point for teammate reward systems that need to grant CSH itemId rewards.
    /// </summary>
    public static class FisherExternalRewardBridge
    {
        private const string FishInventoryKey = "FishInventory";
        private const string FoodInventoryKey = "FoodInventory";
        private const string IngredientInventoryKey = "IngredientInventory";
        private const string OddmentInventoryKey = "OddmentInventory";
        private const string FragmentPrefix = "fragment_";

        private static readonly Dictionary<string, int> PendingItemAddDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> PendingInventoryKeys = new HashSet<string>(StringComparer.Ordinal);
        private static bool _isFlushingQueuedRewards;

        public static bool TryGrantCrewFragment(
            string crewIdOrFragmentItemId,
            int amount = 1,
            string source = "external",
            bool flushImmediately = false)
        {
            if (!TryResolveCrewFragmentItemId(crewIdOrFragmentItemId, out string fragmentItemId))
            {
                Debug.LogWarning("[FisherExternalRewardBridge] Crew fragment grant failed. Invalid crewIdOrFragmentItemId: " + crewIdOrFragmentItemId);
                return false;
            }

            return TryGrantItem(fragmentItemId, amount, source, flushImmediately);
        }

        public static bool TryGrantItem(
            string itemId,
            int amount,
            string source = "external",
            bool flushImmediately = false)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogWarning("[FisherExternalRewardBridge] Item grant failed. itemId is empty.");
                return false;
            }

            if (amount <= 0)
            {
                Debug.LogWarning("[FisherExternalRewardBridge] Item grant failed. amount must be positive: " + amount);
                return false;
            }

            if (!TryResolveRuntime(out FisherRuntimeContext context))
            {
                return false;
            }

            string normalizedItemId = itemId.Trim();
            ServiceResult result = context.InventoryService.TryAddItem(normalizedItemId, amount);
            if (result == null || !result.Success)
            {
                string reason = result == null ? "result is null" : result.FailureReason;
                Debug.LogWarning("[FisherExternalRewardBridge] Item grant rejected. source=" + source + ", itemId=" + normalizedItemId + ", amount=" + amount + ", reason=" + reason);
                return false;
            }

            bool queuedForServer = QueuePendingItemAdd(normalizedItemId, amount);

            context.NotifyRuntimeChanged();
            Debug.Log("[FisherExternalRewardBridge] Item granted. source=" + source + ", itemId=" + normalizedItemId + ", amount=" + amount + ", queuedForServer=" + queuedForServer);

            if (flushImmediately)
            {
                TryFlushQueuedRewards();
            }

            return queuedForServer;
        }

        public static bool TryFlushQueuedRewards(Action onSuccess = null, Action<string> onFailure = null)
        {
            if (_isFlushingQueuedRewards)
            {
                string message = "CSH external reward flush is already running.";
                Debug.LogWarning("[FisherExternalRewardBridge] " + message);
                onFailure?.Invoke(message);
                return false;
            }

            if (PendingItemAddDeltas.Count == 0 && PendingInventoryKeys.Count == 0)
            {
                onSuccess?.Invoke();
                return true;
            }

            PlayFabGateway gateway = PlayFabGateway.Instance;
            InventoryGateway inventory = gateway == null ? null : gateway.Inventory;
            if (gateway == null || inventory == null)
            {
                string message = "PlayFabGateway.Inventory is not ready. Rewards are applied locally and remain queued.";
                Debug.LogWarning("[FisherExternalRewardBridge] " + message);
                onFailure?.Invoke(message);
                return false;
            }

            if (!IsPlayFabClientLoggedIn())
            {
                string message = "PlayFab client is not logged in. Rewards are applied locally and remain queued.";
                Debug.LogWarning("[FisherExternalRewardBridge] " + message);
                onFailure?.Invoke(message);
                return false;
            }

            if (!QueuePendingAddsToInventoryGateway(inventory, out string queueFailure))
            {
                Debug.LogWarning("[FisherExternalRewardBridge] " + queueFailure);
                onFailure?.Invoke(queueFailure);
                return false;
            }

            if (PendingInventoryKeys.Count == 0)
            {
                onSuccess?.Invoke();
                return true;
            }

            List<string> keys = new List<string>(PendingInventoryKeys);
            PendingInventoryKeys.Clear();
            _isFlushingQueuedRewards = true;
            FlushNextInventoryKey(gateway, inventory, keys, 0, onSuccess, onFailure);
            return true;
        }

        private static void FlushNextInventoryKey(
            PlayFabGateway gateway,
            InventoryGateway inventory,
            List<string> keys,
            int index,
            Action onSuccess,
            Action<string> onFailure)
        {
            if (index >= keys.Count)
            {
                RefreshAfterFlush(gateway, onSuccess, onFailure);
                return;
            }

            string inventoryKey = keys[index];
            inventory.Flush(
                inventoryKey,
                _ =>
                {
                    Debug.Log("[FisherExternalRewardBridge] Flushed external rewards: " + inventoryKey);
                    FlushNextInventoryKey(gateway, inventory, keys, index + 1, onSuccess, onFailure);
                },
                error =>
                {
                    _isFlushingQueuedRewards = false;
                    RequeueRemainingKeys(keys, index);
                    string message = string.IsNullOrWhiteSpace(error)
                        ? "External reward flush failed: " + inventoryKey
                        : "External reward flush failed: " + inventoryKey + " / " + error;
                    Debug.LogWarning("[FisherExternalRewardBridge] " + message);
                    onFailure?.Invoke(message);
                });
        }

        private static void RefreshAfterFlush(PlayFabGateway gateway, Action onSuccess, Action<string> onFailure)
        {
            gateway.RefreshAllPlayerData(
                onSuccess: () =>
                {
                    _isFlushingQueuedRewards = false;
                    PullSnapshotsAndNotify();
                    onSuccess?.Invoke();
                },
                onError: error =>
                {
                    _isFlushingQueuedRewards = false;
                    PullSnapshotsAndNotify();
                    string message = "External rewards flushed, but RefreshAllPlayerData failed.";
                    if (error != null && !string.IsNullOrWhiteSpace(error.ErrorMessage))
                    {
                        message += " " + error.ErrorMessage;
                    }

                    Debug.LogWarning("[FisherExternalRewardBridge] " + message);
                    onFailure?.Invoke(message);
                });
        }

        private static void PullSnapshotsAndNotify()
        {
            FisherPlayerDataBridge bridge = UnityEngine.Object.FindFirstObjectByType<FisherPlayerDataBridge>();
            bridge?.PullInventoryFromPlayFabDataStore(force: true);

            FisherRuntimeContext context = UnityEngine.Object.FindFirstObjectByType<FisherRuntimeContext>();
            context?.NotifyRuntimeChanged();
        }

        private static void RequeueRemainingKeys(List<string> keys, int startIndex)
        {
            for (int i = startIndex; i < keys.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(keys[i]))
                {
                    PendingInventoryKeys.Add(keys[i]);
                }
            }
        }

        private static bool TryResolveRuntime(out FisherRuntimeContext context)
        {
            context = UnityEngine.Object.FindFirstObjectByType<FisherRuntimeContext>();

            if (context == null)
            {
                Debug.LogWarning("[FisherExternalRewardBridge] FisherRuntimeContext is missing.");
                return false;
            }

            if (!context.IsReady || context.InventoryService == null)
            {
                Debug.LogWarning("[FisherExternalRewardBridge] Fisher runtime is not ready. status=" + context.LastStatus);
                return false;
            }

            return true;
        }

        private static bool QueuePendingItemAdd(string itemId, int amount)
        {
            if (!TryGetInventoryKeyByItemId(itemId, out _))
            {
                Debug.LogWarning("[FisherExternalRewardBridge] Unsupported PlayFab inventory itemId prefix. itemId=" + itemId);
                return false;
            }

            if (PendingItemAddDeltas.ContainsKey(itemId))
            {
                PendingItemAddDeltas[itemId] += amount;
            }
            else
            {
                PendingItemAddDeltas[itemId] = amount;
            }

            return true;
        }

        private static bool QueuePendingAddsToInventoryGateway(InventoryGateway inventory, out string failure)
        {
            failure = string.Empty;
            if (PendingItemAddDeltas.Count == 0)
            {
                return true;
            }

            Dictionary<string, int> pendingSnapshot = new Dictionary<string, int>(PendingItemAddDeltas, StringComparer.Ordinal);
            Dictionary<string, string> inventoryKeysByItemId = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, int> pair in pendingSnapshot)
            {
                if (pair.Value <= 0)
                {
                    failure = "Invalid pending external reward amount. itemId=" + pair.Key + ", amount=" + pair.Value;
                    return false;
                }

                if (!TryGetInventoryKeyByItemId(pair.Key, out string inventoryKey))
                {
                    failure = "Unsupported pending external reward itemId prefix. itemId=" + pair.Key;
                    return false;
                }

                inventoryKeysByItemId[pair.Key] = inventoryKey;
            }

            try
            {
                foreach (KeyValuePair<string, int> pair in pendingSnapshot)
                {
                    inventory.Add(pair.Key, pair.Value);
                    PendingInventoryKeys.Add(inventoryKeysByItemId[pair.Key]);
                }

                PendingItemAddDeltas.Clear();
                return true;
            }
            catch (Exception exception)
            {
                failure = "Failed to queue pending external rewards to PlayFab Inventory. " + exception.Message;
                return false;
            }
        }

        private static bool TryResolveCrewFragmentItemId(string crewIdOrFragmentItemId, out string fragmentItemId)
        {
            fragmentItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(crewIdOrFragmentItemId))
            {
                return false;
            }

            string trimmed = crewIdOrFragmentItemId.Trim();
            fragmentItemId = trimmed.StartsWith(FragmentPrefix, StringComparison.Ordinal)
                ? trimmed
                : FragmentPrefix + trimmed;
            return true;
        }

        private static bool TryGetInventoryKeyByItemId(string itemId, out string inventoryKey)
        {
            inventoryKey = string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (itemId.StartsWith("fish_", StringComparison.Ordinal))
            {
                inventoryKey = FishInventoryKey;
                return true;
            }

            if (itemId.StartsWith("food_", StringComparison.Ordinal))
            {
                inventoryKey = FoodInventoryKey;
                return true;
            }

            if (itemId.StartsWith("mat_", StringComparison.Ordinal))
            {
                inventoryKey = IngredientInventoryKey;
                return true;
            }

            if (itemId.StartsWith("ticket_", StringComparison.Ordinal) ||
                itemId.StartsWith("box_", StringComparison.Ordinal) ||
                itemId.StartsWith(FragmentPrefix, StringComparison.Ordinal))
            {
                inventoryKey = OddmentInventoryKey;
                return true;
            }

            return false;
        }

        private static bool IsPlayFabClientLoggedIn()
        {
            try
            {
                return PlayFabSettings.staticPlayer != null && PlayFabSettings.staticPlayer.IsClientLoggedIn();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
