using System;
using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// PlayerRuntimeState를 저장 파일이나 외부 provider로 넘기기 위한 루트 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class PlayerStateSaveData
    {
        public long softCurrency;
        public long prismPearl;
        public long pirateCoin;
        public long cash;
        public long crewExp;
        public int bagCapacity;
        public int bagCapacityLevel;
        public int cookingSlotLimit;
        public int cookingSlotLevel;
        public int currentStage = 1;
        public int farthestStage = 1;
        public long lastTrustedServerUtcTicks;
        public ActiveRecipeSaveData activeRecipeState;
        public List<ActiveRecipeSaveData> activeRecipeStates = new List<ActiveRecipeSaveData>();
        public List<InventoryEntrySaveData> inventoryEntries = new List<InventoryEntrySaveData>();
        public List<ItemAcquisitionSaveData> itemAcquisitionCounts = new List<ItemAcquisitionSaveData>();
        public List<string> discoveredCollectionItemIds = new List<string>();
        public List<string> claimedRewardIds = new List<string>();
        public List<string> lockedItemIds = new List<string>();
        public List<string> newItemNoticeIds = new List<string>();
    }

    /// <summary>
    /// 저장용 인벤토리 한 줄 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class InventoryEntrySaveData
    {
        public string itemId;
        public int count;
        public string instanceId;
        public int levelIndex;
    }

    /// <summary>
    /// 저장용 아이템 누적 획득량 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class ItemAcquisitionSaveData
    {
        public string itemId;
        public int acquiredCount;
    }

    /// <summary>
    /// 저장용 진행 중 요리 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class ActiveRecipeSaveData
    {
        public int slotIndex;
        public string recipeId;
        public long startedUtcTicks;
        public long completesUtcTicks;
        public int queuedCount;
    }

    /// <summary>
    /// 런타임 상태와 저장 DTO 사이의 변환을 담당합니다.
    /// </summary>
    public static class PlayerStateSaveMapper
    {
        #region Capture

        /// <summary>
        /// 현재 런타임 상태를 저장 가능한 DTO로 복사합니다.
        /// </summary>
        public static PlayerStateSaveData Capture(PlayerRuntimeState state, long lastTrustedServerUtcTicks = 0)
        {
            PlayerStateSaveData saveData = new PlayerStateSaveData
            {
                lastTrustedServerUtcTicks = lastTrustedServerUtcTicks
            };

            if (state == null)
            {
                return saveData;
            }

            saveData.softCurrency = state.softCurrency;
            saveData.prismPearl = state.prismPearl;
            saveData.pirateCoin = state.pirateCoin;
            saveData.cash = state.cash;
            saveData.crewExp = state.crewExp;
            saveData.bagCapacity = state.bagCapacity;
            saveData.bagCapacityLevel = state.bagCapacityLevel;
            saveData.cookingSlotLimit = state.cookingSlotLimit;
            saveData.cookingSlotLevel = state.cookingSlotLevel;
            saveData.currentStage = state.currentStage;
            saveData.farthestStage = state.farthestStage;
            if (state.activeRecipeState != null)
            {
                saveData.activeRecipeState = CaptureActiveRecipe(state.activeRecipeState);
            }

            if (state.activeRecipeStates.Count > 0)
            {
                for (int i = 0; i < state.activeRecipeStates.Count; i++)
                {
                    ActiveRecipeState active = state.activeRecipeStates[i];
                    if (active != null && !string.IsNullOrEmpty(active.recipeId))
                    {
                        saveData.activeRecipeStates.Add(CaptureActiveRecipe(active));
                    }
                }
            }
            else if (saveData.activeRecipeState != null)
            {
                saveData.activeRecipeStates.Add(saveData.activeRecipeState);
            }

            HashSet<string> capturedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.inventoryEntries.Count; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count <= 0)
                {
                    continue;
                }

                string instanceId = entry.instanceId ?? string.Empty;
                if (!string.IsNullOrEmpty(instanceId) && !capturedInstanceIds.Add(instanceId))
                {
                    continue;
                }

                saveData.inventoryEntries.Add(new InventoryEntrySaveData
                {
                    itemId = entry.itemId,
                    count = entry.count,
                    instanceId = instanceId,
                    levelIndex = entry.levelIndex
                });
            }

            foreach (KeyValuePair<string, int> pair in state.itemAcquisitionCounts)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                {
                    continue;
                }

                saveData.itemAcquisitionCounts.Add(new ItemAcquisitionSaveData
                {
                    itemId = pair.Key,
                    acquiredCount = pair.Value
                });
            }

            AddIds(saveData.discoveredCollectionItemIds, state.discoveredCollectionItemIds);
            AddIds(saveData.claimedRewardIds, state.claimedRewardIds);
            AddIds(saveData.lockedItemIds, state.lockedItemIds);
            AddIds(saveData.newItemNoticeIds, state.newItemNoticeIds);
            return saveData;
        }

        #endregion

        #region Restore

        /// <summary>
        /// 저장 DTO를 새 PlayerRuntimeState로 복원합니다.
        /// </summary>
        public static PlayerRuntimeState Restore(PlayerStateSaveData saveData)
        {
            PlayerRuntimeState state = new PlayerRuntimeState();
            Apply(saveData, state);
            return state;
        }

        /// <summary>
        /// 기존 PlayerRuntimeState를 비운 뒤 저장 DTO 값을 적용합니다.
        /// </summary>
        public static void Apply(PlayerStateSaveData saveData, PlayerRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            state.softCurrency = 0;
            state.prismPearl = 0;
            state.pirateCoin = 0;
            state.cash = 0;
            state.crewExp = 0;
            state.bagCapacity = 0;
            state.bagCapacityLevel = 0;
            state.cookingSlotLimit = 0;
            state.cookingSlotLevel = 0;
            state.currentStage = 1;
            state.farthestStage = 1;
            state.activeRecipeState = null;
            state.activeRecipeStates.Clear();
            state.inventoryEntries.Clear();
            state.itemAcquisitionCounts.Clear();
            state.discoveredCollectionItemIds.Clear();
            state.claimedRewardIds.Clear();
            state.lockedItemIds.Clear();
            state.newItemNoticeIds.Clear();

            if (saveData == null)
            {
                return;
            }

            state.softCurrency = saveData.softCurrency;
            state.prismPearl = saveData.prismPearl;
            state.pirateCoin = saveData.pirateCoin;
            state.cash = saveData.cash;
            state.crewExp = saveData.crewExp;
            state.bagCapacity = saveData.bagCapacity <= 0 ? 0 : saveData.bagCapacity;
            state.bagCapacityLevel = saveData.bagCapacityLevel <= 0 ? 0 : saveData.bagCapacityLevel;
            state.cookingSlotLimit = saveData.cookingSlotLimit <= 0 ? 0 : saveData.cookingSlotLimit;
            state.cookingSlotLevel = saveData.cookingSlotLevel <= 0 ? 0 : saveData.cookingSlotLevel;
            state.currentStage = saveData.currentStage <= 0 ? 1 : saveData.currentStage;
            state.farthestStage = saveData.farthestStage <= 0 ? state.currentStage : saveData.farthestStage;
            if (saveData.activeRecipeStates != null && saveData.activeRecipeStates.Count > 0)
            {
                for (int i = 0; i < saveData.activeRecipeStates.Count; i++)
                {
                    ActiveRecipeState active = RestoreActiveRecipe(saveData.activeRecipeStates[i], i);
                    if (active != null)
                    {
                        state.activeRecipeStates.Add(active);
                    }
                }
            }
            else if (saveData.activeRecipeState != null)
            {
                ActiveRecipeState active = RestoreActiveRecipe(saveData.activeRecipeState, 0);
                if (active != null)
                {
                    state.activeRecipeStates.Add(active);
                }
            }

            if (state.activeRecipeStates.Count > 0)
            {
                state.activeRecipeState = state.activeRecipeStates[0];
            }

            if (saveData.inventoryEntries != null)
            {
                HashSet<string> restoredInstanceIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < saveData.inventoryEntries.Count; i++)
                {
                    InventoryEntrySaveData entry = saveData.inventoryEntries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count <= 0)
                    {
                        continue;
                    }

                    string instanceId = entry.instanceId ?? string.Empty;
                    if (!string.IsNullOrEmpty(instanceId) && !restoredInstanceIds.Add(instanceId))
                    {
                        continue;
                    }

                    state.inventoryEntries.Add(new InventoryEntry
                    {
                        itemId = entry.itemId,
                        count = entry.count,
                        instanceId = instanceId,
                        levelIndex = entry.levelIndex
                    });
                }
            }

            if (saveData.itemAcquisitionCounts != null)
            {
                for (int i = 0; i < saveData.itemAcquisitionCounts.Count; i++)
                {
                    ItemAcquisitionSaveData acquired = saveData.itemAcquisitionCounts[i];
                    if (acquired == null || string.IsNullOrEmpty(acquired.itemId) || acquired.acquiredCount <= 0)
                    {
                        continue;
                    }

                    state.itemAcquisitionCounts[acquired.itemId] = acquired.acquiredCount;
                }
            }

            AddIds(state.discoveredCollectionItemIds, saveData.discoveredCollectionItemIds);
            AddIds(state.claimedRewardIds, saveData.claimedRewardIds);
            AddIds(state.lockedItemIds, saveData.lockedItemIds);
            AddIds(state.newItemNoticeIds, saveData.newItemNoticeIds);
        }

        #endregion

        #region Id Merge

        private static ActiveRecipeSaveData CaptureActiveRecipe(ActiveRecipeState active)
        {
            return new ActiveRecipeSaveData
            {
                slotIndex = active.slotIndex,
                recipeId = active.recipeId,
                startedUtcTicks = active.startedUtcTicks,
                completesUtcTicks = active.completesUtcTicks,
                queuedCount = active.queuedCount
            };
        }

        private static ActiveRecipeState RestoreActiveRecipe(ActiveRecipeSaveData active, int fallbackSlotIndex)
        {
            if (active == null || string.IsNullOrEmpty(active.recipeId))
            {
                return null;
            }

            return new ActiveRecipeState
            {
                slotIndex = active.slotIndex < 0 ? fallbackSlotIndex : active.slotIndex,
                recipeId = active.recipeId,
                startedUtcTicks = active.startedUtcTicks,
                completesUtcTicks = active.completesUtcTicks,
                queuedCount = active.queuedCount <= 0 ? 1 : active.queuedCount
            };
        }

        private static void AddIds(List<string> target, IEnumerable<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            HashSet<string> existing = new HashSet<string>(target, StringComparer.Ordinal);
            foreach (string id in source)
            {
                if (!string.IsNullOrEmpty(id) && existing.Add(id))
                {
                    target.Add(id);
                }
            }
        }

        private static void AddIds(HashSet<string> target, IEnumerable<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (string id in source)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    target.Add(id);
                }
            }
        }

        #endregion
    }
}
