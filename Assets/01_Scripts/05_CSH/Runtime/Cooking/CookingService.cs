using System;
using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 레시피 큐 등록, 완료 가능 시간 검증, 결과 아이템 지급을 담당합니다.
    /// </summary>
    public sealed class CookingService
    {
        #region Constants

        public const int MaxQueueCount = 99;
        public const int FixedCookingSlotCount = 3;

        #endregion

        #region Dependencies

        private readonly BalanceCatalog catalog;
        private readonly PlayerRuntimeState state;
        private readonly InventoryService inventoryService;
        private readonly IClock clock;

        /// <summary>
        /// 카탈로그, 플레이어 상태, 인벤토리 서비스, 시계를 기준으로 요리 서비스를 생성합니다.
        /// </summary>
        public CookingService(
            BalanceCatalog catalog,
            PlayerRuntimeState state,
            InventoryService inventoryService,
            IClock clock)
        {
            this.catalog = catalog;
            this.state = state;
            this.inventoryService = inventoryService;
            this.clock = clock;
        }

        #endregion

        #region Slot Contract

        /// <summary>
        /// 현재 진행 중인 요리 슬롯 수입니다.
        /// </summary>
        public int ActiveCookingSlotCount
        {
            get
            {
                NormalizeActiveRecipeStates();
                return state == null ? 0 : state.activeRecipeStates.Count;
            }
        }

        /// <summary>
        /// 현재 요리 슬롯 제한입니다. 요리 큐는 확장 없이 3칸 고정입니다.
        /// </summary>
        public int CookingSlotLimit => FixedCookingSlotCount;

        /// <summary>
        /// 요리 슬롯 확장 단계입니다. fixed-3 계약에서는 항상 0입니다.
        /// </summary>
        public int CookingSlotLevel => 0;

        /// <summary>
        /// UI 표시용 활성 요리 슬롯 목록입니다. 반환된 목록 자체를 수정하지 않습니다.
        /// </summary>
        public IReadOnlyList<ActiveRecipeState> ActiveRecipeStates
        {
            get
            {
                NormalizeActiveRecipeStates();
                return state == null ? Array.Empty<ActiveRecipeState>() : state.activeRecipeStates;
            }
        }

        /// <summary>
        /// 서버가 검증한 요리 슬롯 snapshot을 표시 상태에 반영합니다.
        /// 재료 차감, 완료 보상, 환불은 서버 권한이므로 여기서는 진행 상태만 교체합니다.
        /// </summary>
        public ServiceResult ReplaceServerCookingSnapshot(IReadOnlyList<ActiveRecipeState> activeRecipes, int openedSlotCount)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
            }

            state.activeRecipeStates.Clear();
            state.activeRecipeState = null;
            state.cookingSlotLimit = FixedCookingSlotCount;
            state.cookingSlotLevel = 0;

            if (activeRecipes != null)
            {
                for (int i = 0; i < activeRecipes.Count; i++)
                {
                    ActiveRecipeState active = activeRecipes[i];
                    if (active == null || string.IsNullOrWhiteSpace(active.recipeId))
                    {
                        continue;
                    }

                    if (!catalog.TryGetRecipe(active.recipeId, out RecipeDefinition recipe) ||
                        recipe == null ||
                        !recipe.IsEnabled)
                    {
                        continue;
                    }

                    int slotIndex = active.slotIndex < 0 ? i : active.slotIndex;
                    if (!IsFixedCookingSlotIndex(slotIndex))
                    {
                        continue;
                    }

                    state.activeRecipeStates.Add(new ActiveRecipeState
                    {
                        slotIndex = slotIndex,
                        recipeId = active.recipeId,
                        startedUtcTicks = active.startedUtcTicks,
                        completesUtcTicks = active.completesUtcTicks,
                        queuedCount = NormalizeQueueCount(active.queuedCount)
                    });
                }
            }

            // Server snapshots are authoritative; do not import the legacy singleton here.
            SyncPrimaryActiveRecipe();
            return ServiceResult.Ok("cooking.server_snapshot_applied");
        }

        /// <summary>
        /// 서버가 해당 요리 슬롯에 작업이 없다고 확정한 경우 로컬 표시 상태의 낡은 job을 제거합니다.
        /// </summary>
        public ServiceResult ClearServerCookingSlot(int slotIndex, string recipeId = null)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
            }

            if (slotIndex < 0 && string.IsNullOrWhiteSpace(recipeId))
            {
                return ServiceResult.Fail("Cooking slot identity is missing.", "cooking.invalid_slot");
            }

            NormalizeActiveRecipeStates();

            bool removed = false;
            for (int i = state.activeRecipeStates.Count - 1; i >= 0; i--)
            {
                ActiveRecipeState active = state.activeRecipeStates[i];
                if (active == null)
                {
                    state.activeRecipeStates.RemoveAt(i);
                    removed = true;
                    continue;
                }

                bool sameSlot = slotIndex >= 0 && active.slotIndex == slotIndex;
                bool sameRecipe = !string.IsNullOrWhiteSpace(recipeId) &&
                                  string.Equals(active.recipeId, recipeId, StringComparison.Ordinal);
                if (!sameSlot && !sameRecipe)
                {
                    continue;
                }

                state.activeRecipeStates.RemoveAt(i);
                removed = true;
            }

            state.cookingSlotLimit = FixedCookingSlotCount;
            state.cookingSlotLevel = 0;

            SyncPrimaryActiveRecipe();

            ServiceResult result = ServiceResult.Ok(removed
                ? "cooking.server_slot_cleared"
                : "cooking.server_slot_already_empty");
            if (removed)
            {
                result.AffectedIds.Add(string.IsNullOrWhiteSpace(recipeId) ? "cooking_slot_" + slotIndex : recipeId);
            }

            return result;
        }

        /// <summary>
        /// 서버 mutation이 성공한 특정 요리 job을 slot/recipe/start identity로 로컬 표시 상태에서 제거합니다.
        /// </summary>
        public ServiceResult ClearServerCookingJob(int slotIndex, string recipeId, long startedUtcTicks)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
            }

            bool hasSlot = slotIndex >= 0;
            bool hasRecipe = !string.IsNullOrWhiteSpace(recipeId);
            bool hasStartedAt = startedUtcTicks > 0L;
            if (!hasSlot && !hasRecipe && !hasStartedAt)
            {
                return ServiceResult.Fail("Cooking job identity is missing.", "cooking.invalid_identity");
            }

            NormalizeActiveRecipeStates();

            int matchedIndex = -1;
            int matchCount = 0;
            for (int i = 0; i < state.activeRecipeStates.Count; i++)
            {
                ActiveRecipeState active = state.activeRecipeStates[i];
                if (active == null)
                {
                    continue;
                }

                if (hasSlot && active.slotIndex != slotIndex)
                {
                    continue;
                }

                if (hasRecipe && !string.Equals(active.recipeId, recipeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (hasStartedAt && active.startedUtcTicks != startedUtcTicks)
                {
                    continue;
                }

                matchedIndex = i;
                matchCount++;
            }

            if (matchCount > 1)
            {
                return ServiceResult.Fail("Cooking job identity matched multiple active jobs.", "cooking.identity_ambiguous");
            }

            bool removed = false;
            if (matchedIndex >= 0)
            {
                state.activeRecipeStates.RemoveAt(matchedIndex);
                removed = true;
            }

            state.cookingSlotLimit = FixedCookingSlotCount;
            state.cookingSlotLevel = 0;

            SyncPrimaryActiveRecipe();

            ServiceResult result = ServiceResult.Ok(removed
                ? "cooking.server_job_cleared"
                : "cooking.server_job_already_empty");
            if (removed)
            {
                result.AffectedIds.Add(hasRecipe ? recipeId : "cooking_slot_" + slotIndex);
            }

            return result;
        }

        /// <summary>
        /// 요리 큐는 3칸 고정 계약이므로 확장 구매는 항상 실패합니다.
        /// </summary>
        public ServiceResult TryPurchaseCookingSlotExpansion()
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
            }

            state.cookingSlotLimit = FixedCookingSlotCount;
            state.cookingSlotLevel = 0;
            return ServiceResult.Fail("Cooking slot expansion is disabled.", "cooking.slot_expansion_disabled");
        }

        #endregion

        #region Queue Flow

        /// <summary>
        /// 레시피 1개를 시작하거나, 같은 레시피가 이미 진행 중이면 큐에 1개 추가합니다.
        /// </summary>
        public ServiceResult TryStartRecipe(string recipeId)
        {
            return TryQueueRecipe(recipeId, 1);
        }

        /// <summary>
        /// 같은 레시피를 최대 99개까지 제작 대기열에 추가합니다.
        /// </summary>
        public ServiceResult TryQueueRecipe(string recipeId, int count)
        {
            if (!TryValidateQueueRequest(recipeId, count, out RecipeDefinition recipe, out ServiceResult failed))
            {
                return failed;
            }

            NormalizeActiveRecipeStates();
            ActiveRecipeState targetState = FindActiveRecipe(recipe.RecipeId);
            int currentQueue = targetState == null ? 0 : NormalizeQueueCount(targetState.queuedCount);
            if (targetState == null && state.activeRecipeStates.Count >= CookingSlotLimit)
            {
                string messageKey = CookingSlotLimit <= 1 ? "cooking.different_recipe_active" : "cooking.slot_full";
                return ServiceResult.Fail("No cooking slot is available for another recipe.", messageKey);
            }

            if (currentQueue + count > MaxQueueCount)
            {
                return ServiceResult.Fail("Recipe queue exceeds max count.", "cooking.queue_full");
            }

            if (!TryMultiply(recipe.InputCount, count, out int inputTotal1) ||
                !TryMultiply(recipe.InputCount2, count, out int inputTotal2))
            {
                return ServiceResult.Fail("Recipe input count would overflow.", "cooking.input_count_overflow");
            }

            if (inventoryService.CountItem(recipe.InputItemId) < inputTotal1 ||
                inventoryService.CountItem(recipe.InputItemId2) < inputTotal2)
            {
                return ServiceResult.Fail("Not enough recipe ingredients.", "inventory.not_enough_item");
            }

            RewardBundleRollbackState rollback = RewardBundleRollbackState.Capture(state);
            ServiceResult consumeFirst = inventoryService.TryConsumeItem(recipe.InputItemId, inputTotal1);
            if (!consumeFirst.Success)
            {
                rollback.Restore(state);
                return consumeFirst;
            }

            ServiceResult consumeSecond = inventoryService.TryConsumeItem(recipe.InputItemId2, inputTotal2);
            if (!consumeSecond.Success)
            {
                rollback.Restore(state);
                return consumeSecond;
            }

            DateTime now = clock.UtcNow;
            if (targetState == null)
            {
                if (!TryAddSeconds(now, recipe.DurationSec, out long completesUtcTicks))
                {
                    rollback.Restore(state);
                    return ServiceResult.Fail("Recipe completion time would exceed DateTime range.", "cooking.time_overflow");
                }

                targetState = new ActiveRecipeState
                {
                    slotIndex = NextCookingSlotIndex(),
                    recipeId = recipe.RecipeId,
                    startedUtcTicks = now.Ticks,
                    completesUtcTicks = completesUtcTicks,
                    queuedCount = count
                };
                state.activeRecipeStates.Add(targetState);
            }
            else
            {
                targetState.queuedCount = currentQueue + count;
            }

            SyncPrimaryActiveRecipe();

            ServiceResult result = ServiceResult.Ok(currentQueue == 0 ? "cooking.start_success" : "cooking.queue_success");
            result.ItemDeltas.Add(new ItemDelta(recipe.InputItemId, -inputTotal1));
            result.ItemDeltas.Add(new ItemDelta(recipe.InputItemId2, -inputTotal2));
            result.AffectedIds.Add(recipe.RecipeId);
            result.AffectedIds.Add(recipe.InputItemId);
            result.AffectedIds.Add(recipe.InputItemId2);
            return result;
        }

        /// <summary>
        /// 현재 진행 중인 요리를 취소하고, 소비한 재료를 환불합니다. 환불 실패 시 진행 상태를 유지합니다.
        /// </summary>
        public ServiceResult TryCancelActiveRecipe()
        {
            NormalizeActiveRecipeStates();
            return TryCancelRecipeState(FirstActiveRecipeState());
        }

        /// <summary>
        /// 지정한 레시피의 진행 슬롯을 취소하고 소비한 재료를 환불합니다.
        /// </summary>
        public ServiceResult TryCancelRecipe(string recipeId)
        {
            NormalizeActiveRecipeStates();
            return TryCancelRecipeState(FindActiveRecipe(recipeId));
        }

        private ServiceResult TryCancelRecipeState(ActiveRecipeState active)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
            }

            if (active == null)
            {
                return ServiceResult.Fail("No active recipe.", "cooking.no_active_recipe");
            }

            string recipeId = active.recipeId;
            if (!catalog.TryGetRecipe(recipeId, out RecipeDefinition recipe))
            {
                return ServiceResult.Fail("Active recipe definition is missing: " + recipeId, "cooking.missing_active_recipe");
            }

            int queuedCount = NormalizeQueueCount(active.queuedCount);
            if (!TryMultiply(recipe.InputCount, queuedCount, out int refundTotal1) ||
                !TryMultiply(recipe.InputCount2, queuedCount, out int refundTotal2))
            {
                return ServiceResult.Fail("Recipe refund count would overflow.", "cooking.refund_count_overflow");
            }

            RewardBundleRollbackState rollback = RewardBundleRollbackState.Capture(state);
            ServiceResult refundFirst = TryRefundRecipeInput(recipe.InputItemId, refundTotal1);
            if (!refundFirst.Success)
            {
                rollback.Restore(state);
                return ServiceResult.Fail("Recipe cancel refund failed: " + refundFirst.FailureReason, "cooking.cancel_refund_failed");
            }

            ServiceResult refundSecond = TryRefundRecipeInput(recipe.InputItemId2, refundTotal2);
            if (!refundSecond.Success)
            {
                rollback.Restore(state);
                return ServiceResult.Fail("Recipe cancel refund failed: " + refundSecond.FailureReason, "cooking.cancel_refund_failed");
            }

            RemoveActiveRecipe(active);

            ServiceResult result = ServiceResult.Ok("cooking.cancel_success");
            result.ItemDeltas.AddRange(refundFirst.ItemDeltas);
            result.ItemDeltas.AddRange(refundSecond.ItemDeltas);
            result.AffectedIds.Add(recipeId);
            result.AffectedIds.AddRange(refundFirst.AffectedIds);
            result.AffectedIds.AddRange(refundSecond.AffectedIds);
            return result;
        }

        #endregion

        #region Acceleration Flow

        /// <summary>
        /// 현재 진행 중인 요리의 다음 완료 시간을 지정 초만큼 앞당깁니다.
        /// </summary>
        public ServiceResult TryAccelerateActiveRecipe(int seconds)
        {
            NormalizeActiveRecipeStates();
            return TryAccelerateRecipeState(FirstActiveRecipeState(), seconds);
        }

        /// <summary>
        /// 지정한 레시피 슬롯의 다음 완료 시간을 지정 초만큼 앞당깁니다.
        /// </summary>
        public ServiceResult TryAccelerateRecipe(string recipeId, int seconds)
        {
            NormalizeActiveRecipeStates();
            return TryAccelerateRecipeState(FindActiveRecipe(recipeId), seconds);
        }

        private ServiceResult TryAccelerateRecipeState(ActiveRecipeState active, int seconds)
        {
            if (!TryBuildAcceleratedState(active, seconds, out ActiveRecipeState acceleratedState, out ServiceResult failed))
            {
                return failed;
            }

            ReplaceActiveRecipe(active, acceleratedState);
            ServiceResult result = ServiceResult.Ok("cooking.accelerate_success");
            result.AffectedIds.Add(acceleratedState.recipeId);
            return result;
        }

        /// <summary>
        /// 가속권 아이템 1개를 소비해 현재 진행 중인 요리 시간을 앞당깁니다.
        /// </summary>
        public ServiceResult TryUseSpeedupItem(string itemId, int seconds)
        {
            NormalizeActiveRecipeStates();
            return TryUseSpeedupItemForRecipeState(FirstActiveRecipeState(), itemId, seconds);
        }

        /// <summary>
        /// 가속권 아이템 1개를 소비해 지정한 레시피 슬롯의 시간을 앞당깁니다.
        /// </summary>
        public ServiceResult TryUseSpeedupItemForRecipe(string recipeId, string itemId, int seconds)
        {
            NormalizeActiveRecipeStates();
            return TryUseSpeedupItemForRecipeState(FindActiveRecipe(recipeId), itemId, seconds);
        }

        private ServiceResult TryUseSpeedupItemForRecipeState(ActiveRecipeState active, string itemId, int seconds)
        {
            if (!TryBuildAcceleratedState(active, seconds, out ActiveRecipeState acceleratedState, out ServiceResult failed))
            {
                return failed;
            }

            if (string.IsNullOrWhiteSpace(itemId) || !catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return ServiceResult.Fail("Unknown speedup item: " + itemId, "cooking.unknown_speedup_item");
            }

            if (!item.IsEnabled)
            {
                return ServiceResult.Fail("Speedup item is disabled: " + itemId, "cooking.speedup_item_disabled");
            }

            if (item.Category != "Ticket")
            {
                return ServiceResult.Fail("Speedup item must be a Ticket category item.", "cooking.invalid_speedup_item");
            }

            ServiceResult consume = inventoryService.TryConsumeItem(itemId, 1);
            if (!consume.Success)
            {
                return consume;
            }

            ReplaceActiveRecipe(active, acceleratedState);
            ServiceResult result = ServiceResult.Ok("cooking.speedup_success");
            result.ItemDeltas.Add(new ItemDelta(itemId, -1));
            result.AffectedIds.Add(acceleratedState.recipeId);
            result.AffectedIds.Add(itemId);
            return result;
        }

        #endregion

        #region Completion Flow

        /// <summary>
        /// 현재 시각 기준 완료된 요리를 정산합니다.
        /// </summary>
        public ServiceResult TryCompleteRecipe()
        {
            return TryCompleteReadyRecipes();
        }

        /// <summary>
        /// 완료 시간이 지난 큐를 가능한 만큼 한 번에 정산합니다.
        /// </summary>
        public ServiceResult TryCompleteReadyRecipes()
        {
            if (catalog == null || state == null || inventoryService == null || clock == null)
            {
                return ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
            }

            NormalizeActiveRecipeStates();
            if (state.activeRecipeStates.Count == 0)
            {
                return ServiceResult.Fail("No active recipe.", "cooking.no_active_recipe");
            }

            ActiveRecipeState readyState = FirstReadyActiveRecipeState();
            if (readyState == null)
            {
                return ServiceResult.Fail("Recipe is not complete yet.", "cooking.not_ready");
            }

            string recipeId = readyState.recipeId;
            if (!catalog.TryGetRecipe(recipeId, out RecipeDefinition recipe))
            {
                return ServiceResult.Fail("Active recipe is missing from catalog: " + recipeId, "cooking.missing_active_recipe");
            }

            int queuedCount = NormalizeQueueCount(readyState.queuedCount);
            int completedCount = CountReadyRecipes(readyState, recipe, queuedCount);
            if (completedCount <= 0)
            {
                return ServiceResult.Fail("Recipe is not complete yet.", "cooking.not_ready");
            }

            if (!TryMultiply(recipe.OutputCount, completedCount, out int outputTotal) ||
                !TryMultiply(recipe.CrewExp, completedCount, out int crewExpTotal))
            {
                return ServiceResult.Fail("Recipe reward count would overflow.", "cooking.reward_overflow");
            }

            if (!CurrencyMath.TryAdd(state.crewExp, crewExpTotal, out long nextCrewExp))
            {
                return ServiceResult.Fail("Crew exp would overflow. Active recipe is kept for retry.", "cooking.crew_exp_overflow");
            }

            int remainingQueue = queuedCount - completedCount;
            ActiveRecipeState nextRecipeState = BuildNextRecipeState(readyState, recipe, completedCount, remainingQueue);
            if (remainingQueue > 0 && nextRecipeState == null)
            {
                return ServiceResult.Fail("Recipe queue time would overflow. Active recipe is kept for retry.", "cooking.time_overflow");
            }

            ServiceResult add = inventoryService.TryAddItem(recipe.OutputItemId, outputTotal);
            if (!add.Success)
            {
                return ServiceResult.Fail("Recipe output apply failed. Active recipe is kept for retry. " + add.FailureReason, "cooking.output_apply_failed");
            }

            ReplaceActiveRecipe(readyState, nextRecipeState);
            state.crewExp = nextCrewExp;

            ServiceResult result = ServiceResult.Ok("cooking.complete_success");
            result.ItemDeltas.Add(new ItemDelta(recipe.OutputItemId, outputTotal));
            result.AffectedIds.Add(recipe.RecipeId);
            result.AffectedIds.Add(recipe.OutputItemId);
            return result;
        }

        #endregion

        #region Active Slot Helpers

        private void NormalizeActiveRecipeStates()
        {
            if (state == null)
            {
                return;
            }

            if (state.activeRecipeStates.Count == 0 && state.activeRecipeState != null)
            {
                state.activeRecipeStates.Add(state.activeRecipeState);
            }

            for (int i = state.activeRecipeStates.Count - 1; i >= 0; i--)
            {
                ActiveRecipeState active = state.activeRecipeStates[i];
                if (active == null || string.IsNullOrEmpty(active.recipeId))
                {
                    state.activeRecipeStates.RemoveAt(i);
                    continue;
                }

                if (active.slotIndex < 0)
                {
                    active.slotIndex = i;
                }

                if (!IsFixedCookingSlotIndex(active.slotIndex))
                {
                    state.activeRecipeStates.RemoveAt(i);
                    continue;
                }
            }

            state.activeRecipeStates.Sort(CompareActiveRecipeSlot);
            SyncPrimaryActiveRecipe();
        }

        private ActiveRecipeState FindActiveRecipe(string recipeId)
        {
            if (state == null || string.IsNullOrEmpty(recipeId))
            {
                return null;
            }

            for (int i = 0; i < state.activeRecipeStates.Count; i++)
            {
                ActiveRecipeState active = state.activeRecipeStates[i];
                if (active != null && string.Equals(active.recipeId, recipeId, StringComparison.Ordinal))
                {
                    return active;
                }
            }

            return null;
        }

        private ActiveRecipeState FirstActiveRecipeState()
        {
            if (state == null || state.activeRecipeStates.Count == 0)
            {
                return null;
            }

            return state.activeRecipeStates[0];
        }

        private ActiveRecipeState FirstReadyActiveRecipeState()
        {
            if (state == null || clock == null)
            {
                return null;
            }

            long nowTicks = clock.UtcNow.Ticks;
            for (int i = 0; i < state.activeRecipeStates.Count; i++)
            {
                ActiveRecipeState active = state.activeRecipeStates[i];
                if (active != null && nowTicks >= active.completesUtcTicks)
                {
                    return active;
                }
            }

            return null;
        }

        private int NextCookingSlotIndex()
        {
            int limit = CookingSlotLimit;
            for (int slotIndex = 0; slotIndex < limit; slotIndex++)
            {
                bool used = false;
                for (int i = 0; i < state.activeRecipeStates.Count; i++)
                {
                    if (state.activeRecipeStates[i] != null && state.activeRecipeStates[i].slotIndex == slotIndex)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    return slotIndex;
                }
            }

            return state.activeRecipeStates.Count;
        }

        private void RemoveActiveRecipe(ActiveRecipeState active)
        {
            if (state == null || active == null)
            {
                return;
            }

            for (int i = state.activeRecipeStates.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(state.activeRecipeStates[i], active))
                {
                    state.activeRecipeStates.RemoveAt(i);
                    break;
                }
            }

            SyncPrimaryActiveRecipe();
        }

        private void ReplaceActiveRecipe(ActiveRecipeState previous, ActiveRecipeState next)
        {
            if (state == null || previous == null)
            {
                return;
            }

            for (int i = 0; i < state.activeRecipeStates.Count; i++)
            {
                if (!ReferenceEquals(state.activeRecipeStates[i], previous))
                {
                    continue;
                }

                if (next == null)
                {
                    state.activeRecipeStates.RemoveAt(i);
                }
                else
                {
                    next.slotIndex = previous.slotIndex;
                    state.activeRecipeStates[i] = next;
                }

                SyncPrimaryActiveRecipe();
                return;
            }

            if (next != null)
            {
                state.activeRecipeStates.Add(next);
            }

            SyncPrimaryActiveRecipe();
        }

        private void SyncPrimaryActiveRecipe()
        {
            if (state == null)
            {
                return;
            }

            state.activeRecipeStates.Sort(CompareActiveRecipeSlot);
            state.activeRecipeState = state.activeRecipeStates.Count == 0 ? null : state.activeRecipeStates[0];
        }

        private static int CompareActiveRecipeSlot(ActiveRecipeState left, ActiveRecipeState right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.slotIndex.CompareTo(right.slotIndex);
        }

        #endregion

        #region Validation

        private bool TryValidateQueueRequest(string recipeId, int count, out RecipeDefinition recipe, out ServiceResult failed)
        {
            recipe = null;
            failed = null;

            if (catalog == null || state == null || inventoryService == null || clock == null)
            {
                failed = ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
                return false;
            }

            if (count <= 0)
            {
                failed = ServiceResult.Fail("Queue count must be positive.", "cooking.invalid_queue_count");
                return false;
            }

            if (count > MaxQueueCount)
            {
                failed = ServiceResult.Fail("Recipe queue exceeds max count.", "cooking.queue_full");
                return false;
            }

            if (!catalog.TryGetRecipe(recipeId, out recipe))
            {
                failed = ServiceResult.Fail("Unknown recipeId: " + recipeId, "cooking.unknown_recipe");
                return false;
            }

            if (!recipe.IsEnabled)
            {
                failed = ServiceResult.Fail("Recipe is disabled.", "cooking.disabled");
                return false;
            }

            if (recipe.InputCount <= 0 ||
                recipe.InputCount2 <= 0 ||
                recipe.OutputCount <= 0 ||
                recipe.DurationSec < 0 ||
                recipe.CrewExp < 0)
            {
                failed = ServiceResult.Fail("Recipe definition has invalid counts, duration, or crew exp.", "cooking.invalid_definition");
                return false;
            }

            if (string.IsNullOrEmpty(recipe.InputItemId2) || recipe.InputItemId == recipe.InputItemId2)
            {
                failed = ServiceResult.Fail("Recipe must use two different fish inputs.", "cooking.invalid_inputs");
                return false;
            }

            if (!catalog.TryGetItem(recipe.OutputItemId, out ItemDefinition outputItem))
            {
                failed = ServiceResult.Fail("Recipe output item is missing: " + recipe.OutputItemId, "cooking.output_item_missing");
                return false;
            }

            if (!outputItem.IsEnabled)
            {
                failed = ServiceResult.Fail("Recipe output item is disabled: " + recipe.OutputItemId, "cooking.output_item_disabled");
                return false;
            }

            return true;
        }

        #endregion

        #region Time Helpers

        private bool TryBuildAcceleratedState(ActiveRecipeState active, int seconds, out ActiveRecipeState acceleratedState, out ServiceResult failed)
        {
            acceleratedState = null;
            failed = null;

            if (catalog == null || state == null || inventoryService == null || clock == null)
            {
                failed = ServiceResult.Fail("Cooking service is not initialized.", "cooking.not_initialized");
                return false;
            }

            if (active == null)
            {
                failed = ServiceResult.Fail("No active recipe.", "cooking.no_active_recipe");
                return false;
            }

            if (seconds <= 0)
            {
                failed = ServiceResult.Fail("Acceleration seconds must be positive.", "cooking.invalid_acceleration");
                return false;
            }

            if (!catalog.TryGetRecipe(active.recipeId, out RecipeDefinition recipe))
            {
                failed = ServiceResult.Fail("Active recipe is missing from catalog: " + active.recipeId, "cooking.missing_active_recipe");
                return false;
            }

            long speedupTicks;
            try
            {
                speedupTicks = TimeSpan.FromSeconds(seconds).Ticks;
            }
            catch (OverflowException)
            {
                failed = ServiceResult.Fail("Acceleration seconds would overflow.", "cooking.acceleration_overflow");
                return false;
            }

            long nowTicks = clock.UtcNow.Ticks;
            long nextCompletesTicks = active.completesUtcTicks - speedupTicks;
            if (nextCompletesTicks < nowTicks)
            {
                nextCompletesTicks = nowTicks;
            }

            long durationTicks = recipe.DurationSec <= 0 ? 0 : TimeSpan.FromSeconds(recipe.DurationSec).Ticks;
            acceleratedState = new ActiveRecipeState
            {
                slotIndex = active.slotIndex,
                recipeId = active.recipeId,
                startedUtcTicks = durationTicks <= 0 ? active.startedUtcTicks : nextCompletesTicks - durationTicks,
                completesUtcTicks = nextCompletesTicks,
                queuedCount = NormalizeQueueCount(active.queuedCount)
            };
            return true;
        }

        private int CountReadyRecipes(ActiveRecipeState activeRecipe, RecipeDefinition recipe, int queuedCount)
        {
            if (recipe.DurationSec <= 0)
            {
                return queuedCount;
            }

            long durationTicks = TimeSpan.FromSeconds(recipe.DurationSec).Ticks;
            long elapsedAfterFirst = Math.Max(0L, clock.UtcNow.Ticks - activeRecipe.completesUtcTicks);
            long readyCount = 1L + elapsedAfterFirst / durationTicks;
            return readyCount >= queuedCount ? queuedCount : (int)readyCount;
        }

        private static ActiveRecipeState BuildNextRecipeState(ActiveRecipeState activeRecipe, RecipeDefinition recipe, int completedCount, int remainingQueue)
        {
            if (remainingQueue <= 0)
            {
                return null;
            }

            try
            {
                long durationTicks = TimeSpan.FromSeconds(recipe.DurationSec).Ticks;
                long nextCompletesUtcTicks = checked(activeRecipe.completesUtcTicks + durationTicks * completedCount);
                return new ActiveRecipeState
                {
                    recipeId = activeRecipe.recipeId,
                    startedUtcTicks = checked(nextCompletesUtcTicks - durationTicks),
                    completesUtcTicks = nextCompletesUtcTicks,
                    queuedCount = remainingQueue
                };
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        private static bool TryAddSeconds(DateTime started, int seconds, out long ticks)
        {
            try
            {
                ticks = started.AddSeconds(seconds).Ticks;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                ticks = started.Ticks;
                return false;
            }
        }

        private static int NormalizeQueueCount(int queuedCount)
        {
            if (queuedCount <= 0)
            {
                return 1;
            }

            return queuedCount > MaxQueueCount ? MaxQueueCount : queuedCount;
        }

        private static bool IsFixedCookingSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < FixedCookingSlotCount;
        }

        private static bool TryMultiply(int value, int multiplier, out int result)
        {
            try
            {
                result = checked(value * multiplier);
                return result >= 0;
            }
            catch (OverflowException)
            {
                result = 0;
                return false;
            }
        }

        private ServiceResult TryRefundRecipeInput(string itemId, int count)
        {
            if (count <= 0)
            {
                return ServiceResult.Ok("inventory.return_skipped");
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return ServiceResult.Fail("Recipe refund itemId is empty.", "cooking.invalid_refund_item");
            }

            return inventoryService.TryReturnConsumedItem(itemId, count);
        }

        #endregion
    }
}
