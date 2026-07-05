using UnityEngine;
using UnityEngine.UI;
using Runtime;

public class MainStageUIButton : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _collectionButton;
    [SerializeField] private Button _settingButton;

    [Header("Panels")]
    [SerializeField] private GameObject _debugPanel;

    [Header("RuntimeStateController")]
    [SerializeField] private RuntimeStateController _runtimeStateController;

    private bool _isDebugPanelActive = false;

    private void Awake()
    {
        _collectionButton.onClick.AddListener(OnCollectionButtonClicked);
        _settingButton.onClick.AddListener(OnSettingButtonClicked);
    }

    private void OnCollectionButtonClicked()
    {
        _runtimeStateController.CurrentState = RuntimeState.COLLECTION;
    }

    private void OnSettingButtonClicked()
    {
        // 세팅 팝업을 띄우는 로직
        Debug.Log("Setting Button Clicked - Open Settings Popup");
    }
}
