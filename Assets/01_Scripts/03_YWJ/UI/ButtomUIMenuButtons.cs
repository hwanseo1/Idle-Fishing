using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Runtime;


namespace UI
{
    public class ButtomUIMenuButtons : MonoBehaviour
    {
        [Header("Buttom UI Menu Buttons")]
        [SerializeField] private Button _equipmentMenuButton;
        [SerializeField] private Button _shipMenuButton;
        [SerializeField] private Button _sailorMenuButton;
        [SerializeField] private Button _cookingMenuButton;
        [SerializeField] private Button _homeMenuButton;
        [SerializeField] private Button _inventoryMenuButton;
        [SerializeField] private Button _recruitMenuButton;
        [SerializeField] private Button _shopMenuButton;

        [Header("Toggle Buttons")]
        [SerializeField] private Button _managementToggleButton;
        [SerializeField] private Button _dockToggleButton;

        [Header("Additional Buttons")]
        [SerializeField] private Button[] _managementSubButtons;
        [SerializeField] private Button[] _dockSubButtons;

        [Header("Runtime State Controller")]
        [SerializeField] private RuntimeStateController _runtimeStateController;
        
        [Header("Unlock Button Manager")]
        [SerializeField] private UnlockButtonManager _unlockButtonManager;

        private bool _isManagementMenuOpen = false;
        private bool _isDockMenuOpen = false;

        private void Awake()
        {
            _managementSubButtons = new Button[] { _equipmentMenuButton, _shipMenuButton, _sailorMenuButton };
            _dockSubButtons = new Button[] { _recruitMenuButton, _shopMenuButton };

            // 버튼 리스너 함수
            _equipmentMenuButton.onClick.AddListener(OnEquipmentMenuButtonClick);
            _shipMenuButton.onClick.AddListener(OnShipMenuButtonClicked);
            _sailorMenuButton.onClick.AddListener(OnSailorMenuButtonClicked);
            _cookingMenuButton.onClick.AddListener(OnCookingMenuButtonClicked);
            _homeMenuButton.onClick.AddListener(OnHomeMenuButtonClicked);
            _inventoryMenuButton.onClick.AddListener(OnInventoryMenuButtonClicked);
            _recruitMenuButton.onClick.AddListener(OnRecruitMenuButtonClicked);
            _shopMenuButton.onClick.AddListener(OnShopMenuButtonClicked);

            // 토글 버튼 리스너 함수
            _managementToggleButton.onClick.AddListener(ToggleManagementMenu);
            _dockToggleButton.onClick.AddListener(ToggleDockMenu);
        }

        #region 버튼 클릭 이벤트 함수

        public void OnEquipmentMenuButtonClick()
        {
            if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.Equipment))
            {
                Debug.Log("Equipment Menu Button is locked.");
                return;
            }

            Debug.Log("Equipment Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.EQUIPMENTUPGRADE;
        }

        public void OnShipMenuButtonClicked()
        {
            if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.Ship))
            {
                Debug.Log("Ship Menu Button is locked.");
                return;
            }

            Debug.Log("Ship Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.SHIPUPGRADE;
        }

        public void OnSailorMenuButtonClicked()
        {
            if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.Crew))
            {
                Debug.Log("Sailor Menu Button is locked.");
                return;
            }

            Debug.Log("Sailor Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.SAILORUPGRADE;
        }

        public void OnCookingMenuButtonClicked()
        {
            if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.Cooking))
            {
                Debug.Log("Cooking Menu Button is locked.");
                return;
            }

            Debug.Log("Cooking Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.COOKING;
        }

        public void OnHomeMenuButtonClicked()
        {
            Debug.Log("Home Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
        }

        public void OnInventoryMenuButtonClicked()
        {
            Debug.Log("Inventory Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.INVENTORY;
        }

        public void OnRecruitMenuButtonClicked()
        {
            if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.Recruit))
            {
                Debug.Log("Recruit Menu Button is locked.");
                return;
            }

            Debug.Log("Recruit Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.RECRUIT;
        }

        public void OnShopMenuButtonClicked()
        {
            if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.Shop))
            {
                Debug.Log("Shop Menu Button is locked.");
                return;
            }

            Debug.Log("Shop Menu Button Clicked");
            _runtimeStateController.CurrentState = RuntimeState.SHOP;
        }

        public void ToggleManagementMenu()
        {
            _isManagementMenuOpen = !_isManagementMenuOpen;

            for (int i = 0; i < _managementSubButtons.Length; i++)
            {
                Vector2 target;
                RectTransform buttonTransform = _managementSubButtons[i].GetComponent<RectTransform>();

                if (_isManagementMenuOpen)
                    target = new Vector2(buttonTransform.anchoredPosition.x, 60 + 190 * (i + 1));
                else
                    target = new Vector2(buttonTransform.anchoredPosition.x, 50);

                buttonTransform.DOAnchorPos(target, 0.3f);
            }
        }

        public void ToggleDockMenu()
        {
            _isDockMenuOpen = !_isDockMenuOpen;

            for (int i = 0; i < _dockSubButtons.Length; i++)
            {
                Vector2 target;
                RectTransform buttonTransform = _dockSubButtons[i].GetComponent<RectTransform>();

                if (_isDockMenuOpen)
                    target = new Vector2(buttonTransform.anchoredPosition.x, 60 + 190 * (i + 1));
                else
                    target = new Vector2(buttonTransform.anchoredPosition.x, 50);

                buttonTransform.DOAnchorPos(target, 0.3f);
            }
        }

        #endregion
    }
}
