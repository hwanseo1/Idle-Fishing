using System;
using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 아이템 지급, 소비, 판매, 잠금, 가방 capacity, 획득 누적량 기록을 담당합니다.
    /// 상점/요리/도감은 이 서비스를 통해 아이템 수량을 바꿉니다.
    /// </summary>
    public sealed class InventoryService
    {
        #region Dependencies

        private readonly BalanceCatalog catalog;
        private readonly PlayerRuntimeState state;

        /// <summary>
        /// 카탈로그와 플레이어 상태를 기준으로 인벤토리 서비스를 생성합니다.
        /// </summary>
        public InventoryService(BalanceCatalog catalog, PlayerRuntimeState state)
        {
            this.catalog = catalog;
            this.state = state;
        }

        #endregion

        #region Query

        /// <summary>
        /// 현재 인벤토리 목록을 읽기 전용 snapshot으로 반환합니다.
        /// </summary>
        public IReadOnlyList<InventoryEntry> GetInventorySnapshot()
        {
            if (state == null)
            {
                return new List<InventoryEntry>().AsReadOnly();
            }

            return state.inventoryEntries.AsReadOnly();
        }

        /// <summary>
        /// 도감 보상 조건 등에 사용할 누적 획득량을 반환합니다.
        /// </summary>
        public int GetAcquiredCount(string itemId)
        {
            if (state == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            return state.itemAcquisitionCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        /// <summary>
        /// itemId 기준 현재 보유 수량을 스택과 인스턴스 항목까지 합산해 반환합니다.
        /// </summary>
        public int CountItem(string itemId)
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
                if (total >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }

            return total <= 0 ? 0 : (int)total;
        }

        /// <summary>
        /// 현재 인벤토리에서 점유 중인 가방 row 수입니다.
        /// </summary>
        public int OccupiedBagRows => CountOccupiedRows();

        /// <summary>
        /// 현재 가방 row capacity입니다. 0 이하는 아직 capacity 계약을 적용하지 않은 상태로 취급합니다.
        /// </summary>
        public int BagCapacity => state == null ? 0 : state.bagCapacity;

        /// <summary>
        /// 잠금 상태인지 확인합니다. 잠금은 선택 판매와 탭 전체판매에서 적용합니다.
        /// </summary>
        public bool IsItemLocked(string itemId)
        {
            return state != null && !string.IsNullOrEmpty(itemId) && state.lockedItemIds.Contains(itemId);
        }

        /// <summary>
        /// 서버/PlayFab 캐시에서 받은 itemId 수량 snapshot을 로컬 가방 표시 상태로 교체합니다.
        /// 고유 인스턴스 아이템은 유지하고, count-only 스택 항목만 교체합니다.
        /// </summary>
        public ServiceResult ReplaceStackedInventorySnapshot(IReadOnlyDictionary<string, int> itemCounts)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            if (itemCounts == null)
            {
                return ServiceResult.Fail("Inventory snapshot is null.", "inventory.snapshot_null");
            }

            List<InventoryEntry> snapshotEntries = new List<InventoryEntry>();
            List<string> affectedIds = new List<string>();
            foreach (KeyValuePair<string, int> pair in itemCounts)
            {
                string itemId = pair.Key;
                int count = pair.Value;
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    return ServiceResult.Fail("Inventory snapshot contains empty itemId.", "inventory.snapshot_empty_item");
                }

                if (count < 0)
                {
                    return ServiceResult.Fail("Inventory snapshot contains negative count: " + itemId, "inventory.snapshot_invalid_count");
                }

                if (count == 0)
                {
                    continue;
                }

                if (!catalog.TryGetItem(itemId, out ItemDefinition item))
                {
                    return ServiceResult.Fail("Unknown itemId in inventory snapshot: " + itemId, "inventory.snapshot_unknown_item");
                }

                if (!item.IsEnabled)
                {
                    return ServiceResult.Fail("Disabled item in inventory snapshot: " + itemId, "inventory.snapshot_item_disabled");
                }

                if (!item.Stackable)
                {
                    return ServiceResult.Fail("Non-stackable item in count snapshot: " + itemId, "inventory.snapshot_non_stackable");
                }

                int remaining = count;
                int maxStack = Math.Max(1, catalog.ResolveMaxStack(item));
                while (remaining > 0)
                {
                    int add = Math.Min(maxStack, remaining);
                    snapshotEntries.Add(new InventoryEntry
                    {
                        itemId = itemId,
                        count = add,
                        instanceId = string.Empty,
                        levelIndex = -1
                    });
                    remaining -= add;
                }

                affectedIds.Add(itemId);
            }

            for (int i = state.inventoryEntries.Count - 1; i >= 0; i--)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry != null &&
                    string.IsNullOrEmpty(entry.instanceId) &&
                    entry.levelIndex < 0)
                {
                    state.inventoryEntries.RemoveAt(i);
                }
            }

            for (int i = 0; i < snapshotEntries.Count; i++)
            {
                InventoryEntry entry = snapshotEntries[i];
                state.inventoryEntries.Add(entry);
                if (state.itemAcquisitionCounts.TryGetValue(entry.itemId, out int acquired))
                {
                    state.itemAcquisitionCounts[entry.itemId] = Math.Max(acquired, CountItemInSnapshot(itemCounts, entry.itemId));
                }
                else
                {
                    state.itemAcquisitionCounts.Add(entry.itemId, CountItemInSnapshot(itemCounts, entry.itemId));
                }

                state.discoveredCollectionItemIds.Add(entry.itemId);
            }

            ServiceResult result = ServiceResult.Ok("inventory.snapshot_applied");
            for (int i = 0; i < affectedIds.Count; i++)
            {
                result.AffectedIds.Add(affectedIds[i]);
            }

            return result;
        }

        /// <summary>
        /// 가방에서 새 아이템 배지를 확인한 것으로 처리합니다.
        /// </summary>
        public bool AcknowledgeNewItemNotice(string itemId)
        {
            if (state == null || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            return state.newItemNoticeIds.Remove(itemId);
        }

        /// <summary>
        /// 가방 오픈 snapshot에 노출된 새 아이템 배지를 한 번에 확인 처리합니다.
        /// </summary>
        public int AcknowledgeNewItemNotices(IEnumerable<string> itemIds)
        {
            if (state == null || itemIds == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (string itemId in itemIds)
            {
                if (!string.IsNullOrEmpty(itemId) && state.newItemNoticeIds.Remove(itemId))
                {
                    removed++;
                }
            }

            return removed;
        }

        #endregion

        #region Mutations

        /// <summary>
        /// 카탈로그에 존재하는 아이템을 지급합니다. 고유 아이템은 count 1과 instanceId가 필요합니다.
        /// </summary>
        public ServiceResult TryAddItem(string itemId, int count, string instanceId = "", int levelIndex = -1)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            if (!catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return ServiceResult.Fail("Unknown itemId: " + itemId, "inventory.unknown_item");
            }

            if (!item.IsEnabled)
            {
                return ServiceResult.Fail("Item is disabled: " + itemId, "inventory.item_disabled");
            }

            if (count <= 0)
            {
                return ServiceResult.Fail("Add count must be positive.", "inventory.invalid_count");
            }

            if (!item.Stackable)
            {
                string normalizedInstanceId = instanceId == null ? string.Empty : instanceId.Trim();
                if (count != 1 || string.IsNullOrWhiteSpace(normalizedInstanceId))
                {
                    return ServiceResult.Fail("Non-stackable items require count 1 and instanceId.", "inventory.instance_required");
                }

                if (HasInstanceId(normalizedInstanceId))
                {
                    return ServiceResult.Fail("Duplicate instanceId: " + normalizedInstanceId, "inventory.duplicate_instance");
                }

                if (!CanAddOccupiedRows(1))
                {
                    return ServiceResult.Fail("Bag capacity is full.", "inventory.bag_capacity_full");
                }

                if (!CanRecordAcquired(itemId, 1))
                {
                    return ServiceResult.Fail("Acquired count would overflow.", "inventory.acquisition_overflow");
                }

                state.inventoryEntries.Add(new InventoryEntry
                {
                    itemId = itemId,
                    count = 1,
                    instanceId = normalizedInstanceId,
                    levelIndex = levelIndex
                });
                RecordAcquired(itemId, 1);
                RegisterItemDiscovery(itemId);
                RegisterNewItemNotice(itemId);

                ServiceResult result = ServiceResult.Ok("inventory.add_success");
                result.ItemDeltas.Add(new ItemDelta(itemId, 1, normalizedInstanceId, levelIndex));
                result.AffectedIds.Add(itemId);
                return result;
            }

            if (!string.IsNullOrWhiteSpace(instanceId) || levelIndex >= 0)
            {
                return ServiceResult.Fail(
                    "Stackable item runtime identity is not supported by the count-only operation.",
                    "inventory.stack_identity_not_supported");
            }

            if (!CanRecordAcquired(itemId, count))
            {
                return ServiceResult.Fail("Acquired count would overflow.", "inventory.acquisition_overflow");
            }

            int remaining = count;
            int maxStack = Math.Max(1, catalog.ResolveMaxStack(item));
            int newRowsNeeded = CountNewStackRowsNeeded(itemId, count, maxStack);
            if (!CanAddOccupiedRows(newRowsNeeded))
            {
                return ServiceResult.Fail("Bag capacity is full.", "inventory.bag_capacity_full");
            }

            for (int i = 0; i < state.inventoryEntries.Count && remaining > 0; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null ||
                    entry.itemId != itemId ||
                    !string.IsNullOrEmpty(entry.instanceId) ||
                    entry.levelIndex >= 0 ||
                    entry.count <= 0)
                {
                    continue;
                }

                int free = maxStack - entry.count;
                if (free <= 0)
                {
                    continue;
                }

                int add = Math.Min(free, remaining);
                entry.count += add;
                remaining -= add;
            }

            while (remaining > 0)
            {
                int add = Math.Min(maxStack, remaining);
                state.inventoryEntries.Add(new InventoryEntry
                {
                    itemId = itemId,
                    count = add,
                    instanceId = string.Empty,
                    levelIndex = -1
                });
                remaining -= add;
            }

            RecordAcquired(itemId, count);
            RegisterItemDiscovery(itemId);
            RegisterNewItemNotice(itemId);

            ServiceResult success = ServiceResult.Ok("inventory.add_success");
            success.ItemDeltas.Add(new ItemDelta(itemId, count));
            success.AffectedIds.Add(itemId);
            return success;
        }

        /// <summary>
        /// 이미 소비 처리된 스택형 아이템을 환불합니다. 획득량, 도감 발견, 신규 알림에는 반영하지 않습니다.
        /// </summary>
        public ServiceResult TryReturnConsumedItem(string itemId, int count)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            if (!catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return ServiceResult.Fail("Unknown itemId: " + itemId, "inventory.unknown_item");
            }

            if (!item.IsEnabled)
            {
                return ServiceResult.Fail("Item is disabled: " + itemId, "inventory.item_disabled");
            }

            if (count <= 0)
            {
                return ServiceResult.Fail("Return count must be positive.", "inventory.invalid_count");
            }

            if (!item.Stackable)
            {
                return ServiceResult.Fail("Only stackable consumed items can be returned by count.", "inventory.instance_required");
            }

            int remaining = count;
            int maxStack = Math.Max(1, catalog.ResolveMaxStack(item));
            int newRowsNeeded = CountNewStackRowsNeeded(itemId, count, maxStack);
            if (!CanAddOccupiedRows(newRowsNeeded))
            {
                return ServiceResult.Fail("Bag capacity is full.", "inventory.bag_capacity_full");
            }

            for (int i = 0; i < state.inventoryEntries.Count && remaining > 0; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null ||
                    entry.itemId != itemId ||
                    !string.IsNullOrEmpty(entry.instanceId) ||
                    entry.levelIndex >= 0 ||
                    entry.count <= 0)
                {
                    continue;
                }

                int free = maxStack - entry.count;
                if (free <= 0)
                {
                    continue;
                }

                int add = Math.Min(free, remaining);
                entry.count += add;
                remaining -= add;
            }

            while (remaining > 0)
            {
                int add = Math.Min(maxStack, remaining);
                state.inventoryEntries.Add(new InventoryEntry
                {
                    itemId = itemId,
                    count = add,
                    instanceId = string.Empty,
                    levelIndex = -1
                });
                remaining -= add;
            }

            ServiceResult success = ServiceResult.Ok("inventory.return_success");
            success.ItemDeltas.Add(new ItemDelta(itemId, count));
            success.AffectedIds.Add(itemId);
            return success;
        }

        /// <summary>
        /// 카탈로그 경제 파라미터 또는 CSH 기본 경제값에 따라 softCurrency를 소모해 가방 capacity를 늘립니다.
        /// </summary>
        public ServiceResult TryPurchaseBagCapacityExpansion()
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            int currentCapacity = state.bagCapacity;
            if (currentCapacity <= 0)
            {
                currentCapacity = ReadEconomyInt("initial_bag_capacity", 0);
            }

            int step = ReadEconomyInt("bag_capacity_step", 0);
            int max = ReadEconomyInt("bag_capacity_max", 0);
            long baseCost = ReadEconomyLong("bag_capacity_gold_cost_base", 0);
            if (currentCapacity <= 0 || step <= 0 || max <= 0 || baseCost <= 0)
            {
                return ServiceResult.Fail("Bag capacity economy params are missing.", "inventory.bag_capacity_config_missing");
            }

            if (currentCapacity >= max)
            {
                return ServiceResult.Fail("Bag capacity is already maxed.", "inventory.bag_capacity_max");
            }

            if (state.bagCapacityLevel == int.MaxValue)
            {
                return ServiceResult.Fail("Bag capacity level would overflow.", "inventory.bag_capacity_level_overflow");
            }

            int levelMultiplier = Math.Max(1, state.bagCapacityLevel + 1);
            if (!CurrencyMath.TryMultiply(baseCost, levelMultiplier, out long cost))
            {
                return ServiceResult.Fail("Bag capacity cost would overflow.", "inventory.currency_overflow");
            }

            if (state.softCurrency < cost)
            {
                return ServiceResult.Fail("Not enough softCurrency.", "inventory.not_enough_currency");
            }

            if (!CurrencyMath.TryAdd(state.softCurrency, -cost, out long nextGold))
            {
                return ServiceResult.Fail("Currency would overflow on bag capacity purchase.", "inventory.currency_overflow");
            }

            long expanded = (long)currentCapacity + step;
            state.softCurrency = nextGold;
            state.bagCapacity = expanded >= max ? max : (int)expanded;
            state.bagCapacityLevel = levelMultiplier;

            ServiceResult result = ServiceResult.Ok("inventory.bag_capacity_expand_success");
            result.CurrencyDelta = -cost;
            result.AffectedIds.Add("bag_capacity");
            return result;
        }

        /// <summary>
        /// 특정 itemId를 판매 보호용 잠금 상태로 설정하거나 해제합니다.
        /// </summary>
        public ServiceResult SetItemLock(string itemId, bool locked)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            if (!catalog.TryGetItem(itemId, out _))
            {
                return ServiceResult.Fail("Unknown itemId: " + itemId, "inventory.unknown_item");
            }

            if (locked)
            {
                state.lockedItemIds.Add(itemId);
            }
            else
            {
                state.lockedItemIds.Remove(itemId);
            }

            ServiceResult result = ServiceResult.Ok(locked ? "inventory.item_locked" : "inventory.item_unlocked");
            result.AffectedIds.Add(itemId);
            return result;
        }

        /// <summary>
        /// 스택형 아이템을 지정 수량만큼 소비합니다.
        /// </summary>
        public ServiceResult TryConsumeItem(string itemId, int count)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            if (!catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return ServiceResult.Fail("Unknown itemId: " + itemId, "inventory.unknown_item");
            }

            if (!item.IsEnabled)
            {
                return ServiceResult.Fail("Item is disabled: " + itemId, "inventory.item_disabled");
            }

            if (!item.Stackable)
            {
                return ServiceResult.Fail("Instance item consume requires an instance-specific operation.", "inventory.instance_required");
            }

            if (count <= 0)
            {
                return ServiceResult.Fail("Consume count must be positive.", "inventory.invalid_count");
            }

            int owned = CountStacked(itemId);
            if (owned < count)
            {
                return ServiceResult.Fail("Not enough item count.", "inventory.not_enough_item");
            }

            int remaining = count;
            for (int i = state.inventoryEntries.Count - 1; i >= 0 && remaining > 0; i--)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null ||
                    entry.itemId != itemId ||
                    !string.IsNullOrEmpty(entry.instanceId) ||
                    entry.levelIndex >= 0)
                {
                    continue;
                }

                int consume = Math.Min(entry.count, remaining);
                entry.count -= consume;
                remaining -= consume;

                if (entry.count <= 0)
                {
                    state.inventoryEntries.RemoveAt(i);
                }
            }

            ServiceResult result = ServiceResult.Ok("inventory.consume_success");
            result.ItemDeltas.Add(new ItemDelta(itemId, -count));
            result.AffectedIds.Add(itemId);
            return result;
        }

        /// <summary>
        /// 판매 가능한 아이템을 소비하고 판매가만큼 softCurrency를 지급합니다.
        /// </summary>
        public ServiceResult TrySellItem(string itemId, int count)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Inventory service is not initialized.", "inventory.not_initialized");
            }

            if (!catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return ServiceResult.Fail("Unknown itemId: " + itemId, "inventory.unknown_item");
            }

            if (!item.IsEnabled)
            {
                return ServiceResult.Fail("Item is disabled: " + itemId, "inventory.item_disabled");
            }

            if (count <= 0)
            {
                return ServiceResult.Fail("Sell count must be positive.", "inventory.invalid_count");
            }

            if (!ItemSellPolicy.IsSellable(item))
            {
                return ServiceResult.Fail("Item is not sellable.", "inventory.not_sellable");
            }

            if (IsItemLocked(itemId))
            {
                return ServiceResult.Fail("Item is locked for sale.", "inventory.item_locked");
            }

            if (!CurrencyMath.TryMultiply(item.SellPrice, count, out long gain) ||
                !CurrencyMath.TryAdd(state.softCurrency, gain, out long nextCurrency))
            {
                return ServiceResult.Fail("Currency would overflow on sell.", "inventory.currency_overflow");
            }

            ServiceResult consume = item.Stackable ? TryConsumeItem(itemId, count) : TryConsumeInstanceItems(itemId, count);
            if (!consume.Success)
            {
                return consume;
            }

            state.softCurrency = nextCurrency;

            ServiceResult result = ServiceResult.Ok("inventory.sell_success");
            result.ItemDeltas.Add(new ItemDelta(itemId, -count));
            result.CurrencyDelta = gain;
            result.AffectedIds.Add(itemId);
            return result;
        }

        #endregion

        #region Acquisition Tracking

        private void RecordAcquired(string itemId, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (state.itemAcquisitionCounts.TryGetValue(itemId, out int current))
            {
                state.itemAcquisitionCounts[itemId] = current + count;
                return;
            }

            state.itemAcquisitionCounts.Add(itemId, count);
        }

        private void RegisterItemDiscovery(string itemId)
        {
            if (state == null || string.IsNullOrEmpty(itemId))
            {
                return;
            }

            state.discoveredCollectionItemIds.Add(itemId);
        }

        private void RegisterNewItemNotice(string itemId)
        {
            if (state == null || string.IsNullOrEmpty(itemId))
            {
                return;
            }

            state.newItemNoticeIds.Add(itemId);
        }

        private bool CanRecordAcquired(string itemId, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (!state.itemAcquisitionCounts.TryGetValue(itemId, out int current))
            {
                return true;
            }

            try
            {
                int ignored = checked(current + count);
                return ignored >= 0;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        #endregion

        #region Count Helpers

        private int CountOccupiedRows()
        {
            if (state == null)
            {
                return 0;
            }

            int occupied = 0;
            for (int i = 0; i < state.inventoryEntries.Count; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry != null && !string.IsNullOrEmpty(entry.itemId) && entry.count > 0)
                {
                    occupied++;
                }
            }

            return occupied;
        }

        private static int CountItemInSnapshot(IReadOnlyDictionary<string, int> itemCounts, string itemId)
        {
            if (itemCounts == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            return itemCounts.TryGetValue(itemId, out int count) && count > 0 ? count : 0;
        }

        private bool CanAddOccupiedRows(int newRows)
        {
            if (state == null || state.bagCapacity <= 0 || newRows <= 0)
            {
                return true;
            }

            int occupied = CountOccupiedRows();
            return occupied <= state.bagCapacity && state.bagCapacity - occupied >= newRows;
        }

        private int CountNewStackRowsNeeded(string itemId, int count, int maxStack)
        {
            int remaining = count;
            for (int i = 0; i < state.inventoryEntries.Count && remaining > 0; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null ||
                    entry.itemId != itemId ||
                    !string.IsNullOrEmpty(entry.instanceId) ||
                    entry.levelIndex >= 0 ||
                    entry.count <= 0)
                {
                    continue;
                }

                int free = maxStack - entry.count;
                if (free <= 0)
                {
                    continue;
                }

                remaining -= Math.Min(free, remaining);
            }

            if (remaining <= 0)
            {
                return 0;
            }

            long rows = ((long)remaining + maxStack - 1) / maxStack;
            return rows >= int.MaxValue ? int.MaxValue : (int)rows;
        }

        private int CountStacked(string itemId)
        {
            long total = 0;
            for (int i = 0; i < state.inventoryEntries.Count; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry != null &&
                    entry.itemId == itemId &&
                    string.IsNullOrEmpty(entry.instanceId) &&
                    entry.levelIndex < 0)
                {
                    total += entry.count;
                    if (total >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }
            }

            return total <= 0 ? 0 : (int)total;
        }

        #endregion

        #region Instance Helpers

        private ServiceResult TryConsumeInstanceItems(string itemId, int count)
        {
            int owned = CountInstances(itemId);
            if (owned < count)
            {
                return ServiceResult.Fail("Not enough item count.", "inventory.not_enough_item");
            }

            int remaining = count;
            for (int i = state.inventoryEntries.Count - 1; i >= 0 && remaining > 0; i--)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry == null ||
                    entry.itemId != itemId ||
                    string.IsNullOrEmpty(entry.instanceId))
                {
                    continue;
                }

                state.inventoryEntries.RemoveAt(i);
                remaining--;
            }

            ServiceResult result = ServiceResult.Ok("inventory.consume_success");
            result.ItemDeltas.Add(new ItemDelta(itemId, -count));
            result.AffectedIds.Add(itemId);
            return result;
        }

        private int CountInstances(string itemId)
        {
            int total = 0;
            for (int i = 0; i < state.inventoryEntries.Count; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry != null &&
                    entry.itemId == itemId &&
                    !string.IsNullOrEmpty(entry.instanceId))
                {
                    total++;
                }
            }

            return total;
        }

        private bool HasInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            for (int i = 0; i < state.inventoryEntries.Count; i++)
            {
                InventoryEntry entry = state.inventoryEntries[i];
                if (entry != null && entry.instanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Economy Param Helpers

        private int ReadEconomyInt(string key, int fallback)
        {
            long value = ReadEconomyLong(key, fallback);
            if (value <= 0)
            {
                return fallback;
            }

            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private long ReadEconomyLong(string key, long fallback)
        {
            long resolvedFallback = FisherEconomyDefaults.ResolveLong(key, fallback);
            if (catalog == null ||
                string.IsNullOrEmpty(key) ||
                !catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param) ||
                param == null ||
                !param.IsEnabled ||
                !long.TryParse(param.Value, out long value))
            {
                return resolvedFallback;
            }

            return value;
        }

        #endregion
    }
}
