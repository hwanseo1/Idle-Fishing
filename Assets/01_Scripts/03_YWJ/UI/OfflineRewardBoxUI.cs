using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OfflineRewardBoxUI : MonoBehaviour
{
    [Header("Offline Reward UI Elements")]
    [SerializeField] private Image _fishIconImage;
    [SerializeField] private TMP_Text _fishText;

    [Header("Offline Reward Box Values")]
    [SerializeField] private Sprite _fishIcon;
    [SerializeField] private string _fishName;
    [SerializeField] private int _fishCount;

    public void SetOfflineRewardBoxValue(Sprite fishIcon, string fishName, int fishCount)
    {
        _fishIcon = fishIcon;
        _fishName = fishName;
        _fishCount = fishCount;

        RefreshOfflineRewardBoxUI();
    }

    private void RefreshOfflineRewardBoxUI()
    {
        if (_fishIconImage != null)
        {
            _fishIconImage.sprite = _fishIcon;
        }

        if (_fishText != null)
        {
            _fishText.text = $"{_fishName} x{_fishCount}";
        }
    }
}
