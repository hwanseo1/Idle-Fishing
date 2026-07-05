using System;



public static class RuntimeStateEventBus
{
    // GameStart State Events
    public static event Action OnGameStartStateEntered;

    public static event Action OnGameStartStateExited;

    public static void PublishGameStartStateEntered()
    {
        OnGameStartStateEntered?.Invoke();
    }

    public static void PublishGameStartStateExited()
    {
        OnGameStartStateExited?.Invoke();
    }


    // Login State Events
    public static event Action OnLoginStateEntered;

    public static event Action OnLoginStateExited;

    public static void PublishLoginStateEntered()
    {
        OnLoginStateEntered?.Invoke();
    }

    public static void PublishLoginStateExited()
    {
        OnLoginStateExited?.Invoke();
    }


    // OfflineReward State Events
    public static event Action OnOfflineRewardStateEntered;

    public static event Action OnOfflineRewardStateExited;

    public static void PublishOfflineRewardStateEntered()
    {
        OnOfflineRewardStateEntered?.Invoke();
    }

    public static void PublishOfflineRewardStateExited()
    {
        OnOfflineRewardStateExited?.Invoke();
    }


    // EquipmentUpgrade State Events
    public static event Action OnEquipmentUpgradeStateEntered;

    public static event Action OnEquipmentUpgradeStateExited;

    public static void PublishEquipmentUpgradeStateEntered()
    {
        OnEquipmentUpgradeStateEntered?.Invoke();
    }

    public static void PublishEquipmentUpgradeStateExited()
    {
        OnEquipmentUpgradeStateExited?.Invoke();
    }


    // ShipUpgrade State Events
    public static event Action OnShipUpgradeStateEntered;

    public static event Action OnShipUpgradeStateExited;

    public static void PublishShipUpgradeStateEntered()
    {
        OnShipUpgradeStateEntered?.Invoke();
    }

    public static void PublishShipUpgradeStateExited()
    {
        OnShipUpgradeStateExited?.Invoke();
    }


    // SailorUpgrade State Events
    public static event Action OnSailorUpgradeStateEntered;

    public static event Action OnSailorUpgradeStateExited;

    public static void PublishSailorUpgradeStateEntered()
    {
        OnSailorUpgradeStateEntered?.Invoke();
    }

    public static void PublishSailorUpgradeStateExited()
    {
        OnSailorUpgradeStateExited?.Invoke();
    }


    // Cooking State Events
    public static event Action OnCookingStateEntered;

    public static event Action OnCookingStateExited;

    public static void PublishCookingStateEntered()
    {
        OnCookingStateEntered?.Invoke();
    }

    public static void PublishCookingStateExited()
    {
        OnCookingStateExited?.Invoke();
    }


    // Inventory State Events
    public static event Action OnInventoryStateEntered;

    public static event Action OnInventoryStateExited;

    public static void PublishInventoryStateEntered()
    {
        OnInventoryStateEntered?.Invoke();
    }

    public static void PublishInventoryStateExited()
    {
        OnInventoryStateExited?.Invoke();
    }


    // Shop State Events
    public static event Action OnShopStateEntered;

    public static event Action OnShopStateExited;

    public static void PublishShopStateEntered()
    {
        OnShopStateEntered?.Invoke();
    }

    public static void PublishShopStateExited()
    {
        OnShopStateExited?.Invoke();
    }


    // Recruit State Events
    public static event Action OnRecruitStateEntered;

    public static event Action OnRecruitStateExited;

    public static void PublishRecruitStateEntered()
    {
        OnRecruitStateEntered?.Invoke();
    }

    public static void PublishRecruitStateExited()
    {
        OnRecruitStateExited?.Invoke();
    }


    // Collection State Events
    public static event Action OnCollectionStateEntered;

    public static event Action OnCollectionStateExited;

    public static void PublishCollectionStateEntered()
    {
        OnCollectionStateEntered?.Invoke();
    }

    public static void PublishCollectionStateExited()
    {
        OnCollectionStateExited?.Invoke();
    }


    // MainStage State Events
    public static event Action OnMainStageStateEntered;

    public static event Action OnMainStageStateExited;

    public static void PublishMainStageStateEntered()
    {
        OnMainStageStateEntered?.Invoke();
    }

    public static void PublishMainStageStateExited()
    {
        OnMainStageStateExited?.Invoke();
    }


    // StageChange State Events
    public static event Action OnStageChangeStateEntered;

    public static event Action OnStageChangeStateExited;

    public static void PublishStageChangeStateEntered()
    {
        OnStageChangeStateEntered?.Invoke();
    }

    public static void PublishStageChangeStateExited()
    {
        OnStageChangeStateExited?.Invoke();
    }


    // BossStage State Events
    public static event Action OnBossStageStateEntered;

    public static event Action OnBossStageStateExited;

    public static void PublishBossStageStateEntered()
    {
        OnBossStageStateEntered?.Invoke();
    }

    public static void PublishBossStageStateExited()
    {
        OnBossStageStateExited?.Invoke();
    }


    // MultiMatching State Events
    public static event Action OnMultiMatchingStateEntered;

    public static event Action OnMultiMatchingStateExited;

    public static void PublishMultiMatchingStateEntered()
    {
        OnMultiMatchingStateEntered?.Invoke();
    }

    public static void PublishMultiMatchingStateExited()
    {
        OnMultiMatchingStateExited?.Invoke();
    }


    // MultiStage State Events
    public static event Action OnMultiStageStateEntered;

    public static event Action OnMultiStageStateExited;

    public static void PublishMultiStageStateEntered()
    {
        OnMultiStageStateEntered?.Invoke();
    }

    public static void PublishMultiStageStateExited()
    {
        OnMultiStageStateExited?.Invoke();
    }
}
