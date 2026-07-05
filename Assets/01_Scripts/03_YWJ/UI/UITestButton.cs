using UnityEngine;
using UnityEngine.UI;

public class UITestButton : MonoBehaviour
{
    [SerializeField] private Button _testButton;

    private void Awake()
    {
        _testButton.onClick.AddListener(OnTestButtonClicked);
    }

    private void OnTestButtonClicked()
    {
        DataChangeListener.Instance.Try_UpgradeFishingRodLevel();
        DataChangeListener.Instance.Try_SetFishingChairLevel(10);
        Debug.Log($"Fishing Bobber Level: {DataChangeListener.Instance.Try_GetFishingBobberLevel()}");
    }
}
