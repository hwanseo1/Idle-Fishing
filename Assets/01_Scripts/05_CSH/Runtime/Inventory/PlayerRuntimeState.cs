using System;
using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 저장 가능한 인벤토리 한 줄입니다. instanceId가 있으면 고유 인스턴스 아이템으로 취급합니다.
    /// </summary>
    [Serializable]
    public sealed class InventoryEntry
    {
        public string itemId;
        public int count;
        public string instanceId;
        public int levelIndex = -1;
    }

    /// <summary>
    /// Fisher 런타임 서비스들이 공유하는 플레이어 상태입니다.
    /// 서버 동기화 어댑터는 이 DTO를 기준으로 로컬 상태를 읽고 씁니다.
    /// </summary>
    public sealed class PlayerRuntimeState
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
        public ActiveRecipeState activeRecipeState;
        public readonly List<ActiveRecipeState> activeRecipeStates = new List<ActiveRecipeState>();
        public readonly List<InventoryEntry> inventoryEntries = new List<InventoryEntry>();
        public readonly Dictionary<string, int> itemAcquisitionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<string> discoveredCollectionItemIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> claimedRewardIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> lockedItemIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> newItemNoticeIds = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 진행 중인 요리의 레시피와 시작/완료 시각입니다.
    /// </summary>
    public sealed class ActiveRecipeState
    {
        public int slotIndex;
        public string recipeId;
        public long startedUtcTicks;
        public long completesUtcTicks;
        public int queuedCount = 1;
    }
}
