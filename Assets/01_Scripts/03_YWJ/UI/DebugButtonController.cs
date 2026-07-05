using UnityEngine;
using UnityEngine.UI;

public class DebugButtonController : MonoBehaviour
{
    [Header("Debug Buttons")]
    [SerializeField] private Button _onSyncroButton;

    [SerializeField] private Button _onInitItemsButton;
    [SerializeField] private Button _onItemRemoveButton;
    [SerializeField] private Button _onItemSellButton;

    [SerializeField] private Button _onRodLevelUpButton;
    [SerializeField] private Button _onChairLevelUpButton;
    [SerializeField] private Button _onFloatLevelUpButton;

    [SerializeField] private Button _onGetFragmentButton;
    [SerializeField] private Button _onCrewPromoteButton;

    [SerializeField] private Button _onCookingStartButton;
    [SerializeField] private Button _onCookingClaimButton;
    [SerializeField] private Button _onCookingCancelButton;


    private void Awake()
    {
        _onSyncroButton.onClick.AddListener(OnSyncroButtonClicked);
        _onInitItemsButton.onClick.AddListener(OnInitItemsClicked);
        _onItemRemoveButton.onClick.AddListener(OnItemRemoveClicked);
        _onItemSellButton.onClick.AddListener(OnItemSellClicked);

        _onRodLevelUpButton.onClick.AddListener(OnRodLevelUpClicked);
        _onChairLevelUpButton.onClick.AddListener(OnChairLevelUpClicked);
        _onFloatLevelUpButton.onClick.AddListener(OnFloatLevelUpClicked);

        _onGetFragmentButton.onClick.AddListener(OnGetFragmentClicked);
        _onCrewPromoteButton.onClick.AddListener(OnCrewPromoteClicked);

        _onCookingStartButton.onClick.AddListener(OnCookingStartClicked);
        _onCookingClaimButton.onClick.AddListener(OnCookingClaimClicked);
        _onCookingCancelButton.onClick.AddListener(OnCookingCancelClicked);
    }


    private void OnSyncroButtonClicked()
    {
        PlayFabGateway.Instance.RefreshAllPlayerData();
    }

    private void OnInitItemsClicked()
    {
        PlayFabGateway.Instance.Inventory.Add("fish_anchovy", 5);
        PlayFabGateway.Instance.Inventory.Add("fish_saury", 5);
        PlayFabGateway.Instance.Inventory.Add("fish_mackerel", 3);

        PlayFabGateway.Instance.Inventory.Add("ticket_speedup_10m", 1);

        PlayFabGateway.Instance.Inventory.Add("mat_upgrade_common", 200);

        PlayFabGateway.Instance.Inventory.FlushAll();
    }

    private void OnItemRemoveClicked()
    {
        PlayFabGateway.Instance.Inventory.Remove("fish_anchovy", 1);

        PlayFabGateway.Instance.Inventory.Flush("FishInventory");
    }

    private void OnItemSellClicked()
    {
        PlayFabGateway.Instance.Inventory.Sell("fish_mackerel", 1);

        PlayFabGateway.Instance.Inventory.Flush("FishInventory");
    }

    private void OnRodLevelUpClicked()
    {
        PlayFabGateway.Instance.Equipment.LevelUp("equip_rod");
    }

    private void OnChairLevelUpClicked()
    {
        PlayFabGateway.Instance.Equipment.LevelUp("equip_chair");
    }

    private void OnFloatLevelUpClicked()
    {
        PlayFabGateway.Instance.Equipment.LevelUp("equip_float");
    }

    private void OnGetFragmentClicked()
    {
        PlayFabGateway.Instance.Inventory.Add("fragment_Crew_Navigator_Jun", 100);

        PlayFabGateway.Instance.Inventory.Flush("OddmentInventory");
    }

    private void OnCrewPromoteClicked()
    {
        PlayFabGateway.Instance.Crew.Promote("Crew_Navigator_Jun");
    }

    private void OnCookingStartClicked()
    {
        PlayFabGateway.Instance.Cooking.StartCooking(0, "recipe_grilled_anchovy_g1", 2);
    }

    private void OnCookingClaimClicked()
    {
        PlayFabGateway.Instance.Cooking.ClaimCooking(0);
    }

    private void OnCookingCancelClicked()
    {
        PlayFabGateway.Instance.Cooking.CancelCooking(0);
    }
}
