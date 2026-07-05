using Crew;
using DG.Tweening;
using Fisher.Data;
using Reward;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GachaUI : MonoBehaviour
{
    [Header("Gold Gacha Panel")]
    [SerializeField] private GameObject _goldGachaPanel;
    [SerializeField] private Button _goldGachaButton_1time;
    [SerializeField] private Button _goldGachaButton_10times;

    [Header("Premium Gacha Panel")]
    [SerializeField] private GameObject _premiumGachaPanel;
    [SerializeField] private Button _premiumGachaButton_1time;
    [SerializeField] private Button _premiumGachaButton_10times;

    [Header("Gacha Result Panel")]
    [SerializeField] private GameObject _gachaResultPanel;
    [SerializeField] private GameObject _closeButton;
    [SerializeField] private GameObject _popupPanel;

    [Header("Prefabs")]
    [SerializeField] private GameObject _crewPrefab;
    [SerializeField] private GameObject _materialPrefab;
    [SerializeField] private Sprite _defaultMaterialSprite;

    [Header("Panel Change Button")]
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;

    [Header("참조")]
    [SerializeField] private GachaSystem _gachaSystem;
    [SerializeField] private GachaDictionaryConvertor _gachaDictionaryConvertor;

    private List<CrewListItemForGacha> _crewCards = new();

    private int _currentGachaPanelIndex = 0;
    // 0 = Gold, 1 = Premium

    private void Awake()
    {
        _gachaDictionaryConvertor = FindFirstObjectByType<GachaDictionaryConvertor>();

        _goldGachaButton_1time.onClick.AddListener(OnClickMaterialSingleDraw);
        _goldGachaButton_10times.onClick.AddListener(OnClickMaterialTenDraw);

        _premiumGachaButton_1time.onClick.AddListener(OnClickCrewSingleDraw);
        _premiumGachaButton_10times.onClick.AddListener(OnClickCrewTenDraw);

        _leftArrowButton.onClick.AddListener(OnClickLeftArrow);
        _rightArrowButton.onClick.AddListener(OnClickRightArrow);
    }

    private void OnEnable()
    {
        foreach (Transform child in _gachaResultPanel.transform)
        {
            Destroy(child.gameObject);
        }

        _gachaResultPanel.SetActive(false);
        _closeButton.SetActive(false);

        _currentGachaPanelIndex = 0;
        RefreshGachaPanel();

        Debug.Log("인벤토리 FlushAll 호출");
        PlayFabGateway.Instance.Inventory.FlushAll(onSuccess: result =>
        {
            Debug.Log($"인벤토리 FlushAll 완료: {result}");
        }, onFailure: error =>
        {
            Debug.LogError($"인벤토리 FlushAll 실패: {error}");
        });
    }

    private void Popup(string text)
    {
        _popupPanel.SetActive(true);
        _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = "재화가 부족합니다.";
        _popupPanel.GetComponentInChildren<Button>().onClick.AddListener(OnclickConfirmButton);
    }

    private void RefreshGachaPanel()
    {
        _goldGachaPanel.SetActive(_currentGachaPanelIndex == 0);
        _premiumGachaPanel.SetActive(_currentGachaPanelIndex == 1);
    }

    private void OnClickLeftArrow()
    {
        _currentGachaPanelIndex--;

        if (_currentGachaPanelIndex < 0)
            _currentGachaPanelIndex = 1;

        RefreshGachaPanel();
    }

    private void OnClickRightArrow()
    {
        _currentGachaPanelIndex++;

        if (_currentGachaPanelIndex > 1)
            _currentGachaPanelIndex = 0;

        RefreshGachaPanel();
    }

    public void OnclickConfirmButton()
    {
        _popupPanel.SetActive(false);
    }

    public void OnClickCrewTenDraw()
    {
        _gachaSystem.CrewDrawTen(
            rewards =>
            {
                ShowResult(rewards);
            },
            error =>
            {
                Debug.LogError(error);

                Popup(error);
            });
    }

    public void OnClickCrewSingleDraw()
    {
        _gachaSystem.CrewDrawSingle(
            reward =>
            {
                ShowResult(new List<GachaReward> { reward });
            },
            error =>
            {
                Debug.LogError(error);

                Popup(error);
            });        
    }


    public void OnClickMaterialTenDraw()
    {
        _gachaSystem.MaterialDrawTen(
            rewards =>
            {
                ShowResult(rewards);
            },
            error =>
            {
                Debug.LogError(error);

                Popup(error);
            });
    }

    public void OnClickMaterialSingleDraw()
    {
        _gachaSystem.MaterialDrawSingle(
            reward =>
            {
                ShowResult(new List<GachaReward> { reward });
            },
            error =>
            {
                Debug.LogError(error);

                Popup(error);
            });
    }

    public void OnClickResultPanelClose()
    {
        foreach (Transform child in _gachaResultPanel.transform)
        {
            Destroy(child.gameObject);
        }
        _gachaResultPanel.SetActive(false);
        _closeButton.SetActive(false);
    }
    private void ShowResult(List<GachaReward> rewards)
    {
        _crewCards.Clear();

        _gachaResultPanel.SetActive(true);
        _closeButton.SetActive(true);

        foreach (Transform child in _gachaResultPanel.transform)
        {
            Destroy(child.gameObject);
        }

        foreach (var reward in rewards)
        {
            switch (reward.RewardType)
            {
                case RewardType.Crew:
                    {
                        GameObject item = Instantiate(_crewPrefab, _gachaResultPanel.transform);

                        CrewData crewData = CrewManager.Instance.CrewDataBase.GetCrewData(reward.RewardId);

                        CrewListItemForGacha card = item.GetComponent<CrewListItemForGacha>();

                        card.Init(crewData, reward);

                        _crewCards.Add(card);
                        break;
                    }

                case RewardType.Materials:
                    {
                        GameObject item = Instantiate(_materialPrefab, _gachaResultPanel.transform);
                        MatItemListForGacha itemElement = item.GetComponent<MatItemListForGacha>();

                        ItemData itemData = _gachaSystem.ItemDataBase.Materials.Find(item => item.ItemId == reward.RewardId);
                        string text = itemData != null
                            ? $"재료 : {itemData.DisplayName}"
                            : "재료를 찾을 수 없습니다.";
                        Sprite sprite = _defaultMaterialSprite;

                        itemElement.Init(sprite, text);
                        break;
                    }

                case RewardType.CrewFragment:
                    {
                        GameObject item = Instantiate(_materialPrefab, _gachaResultPanel.transform);
                        MatItemListForGacha itemElement = item.GetComponent<MatItemListForGacha>();

                        ItemData itemData= _gachaSystem.ItemDataBase.CrewFragments.Find(item => item.ItemId == reward.RewardId);
                        string text = itemData != null
                            ? $"선원 조각 : {itemData.DisplayName}"
                            : "선원 조각을 찾을 수 없습니다.";

                        Sprite sprite = _gachaDictionaryConvertor.TryGetFragmentSprite(reward.RewardId, out Sprite fragmentSprite) ? fragmentSprite : _defaultMaterialSprite;

                        itemElement.Init(sprite, text);
                        break;
                    }
            }
        }
        PlayResultAnimation();
    }

    private void PlayResultAnimation()
    {
        Sequence seq = DOTween.Sequence();

        foreach (var item in _crewCards)
        {
            seq.AppendCallback(() =>
            {
                item.PlayFlip();
            });

            seq.AppendInterval(0.18f);
        }
    }
}
