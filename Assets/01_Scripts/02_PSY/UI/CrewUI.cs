using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crew.UI
{
    public class CrewUI : MonoBehaviour
    {
        [Header("Crew Info Panel")]
        [SerializeField] private GameObject _crewInfoPanel;

        [SerializeField] private Image _crewInfoImage;
        [SerializeField] private Image _crewInfoNameSpace;
        [SerializeField] private Image _crewInfoGrade;
        [SerializeField] private Image _crewInfoOutline;
        [SerializeField] private TextMeshProUGUI _crewInfoName;

        [SerializeField] private List<CrewPassiveObject> _crewPassives;
        [SerializeField] private Slider _crewFragmentSlider;
        [SerializeField] private Button _crewUpgradeButton;

        [Header("Grade Sprite")]
        [SerializeField] private Sprite _gradeR;
        [SerializeField] private Sprite _gradeSR;
        [SerializeField] private Sprite _gradeSSR;

        [Header("Outline Sprite")]
        [SerializeField] private Sprite _outlineR;
        [SerializeField] private Sprite _outlineSR;
        [SerializeField] private Image _outlineSSR;

        [Header("Name Space Image")]
        [SerializeField] private Sprite _nameSpaceR;
        [SerializeField] private Sprite _nameSpaceSR;
        [SerializeField] private Sprite _nameSpaceSSR;

        [Header("Crew Slots Panel")]
        [SerializeField] private GameObject _crewSlotPanel;
        [SerializeField] private List<CrewSlotUI> _crewSlots;
        [SerializeField] private GameObject _unlockButton;

        [Header("Total Crew Passive")]
        [SerializeField] private List<TextMeshProUGUI> _passives = new();

        [Header("Crew List Panel")]
        [SerializeField] private GameObject _crewListPanel;
        [SerializeField] private GameObject _crewListPrefab;
        [SerializeField] private List<CrewListItem> _crewList = new();

        [Header("Popup Panel")]
        [SerializeField] private GameObject _popupPanel;


        [Header("연출용")]
        [SerializeField] private Image _upgradeFlash;

        [Serializable]
        public class CrewObject
        {
            public Image CrewImage;
            public TextMeshProUGUI CrewName;
            public Button EquipButton;
        }

        [Serializable]
        public class CrewPassiveObject
        {
            public TextMeshProUGUI PassiveName;
            public Slider PassiveSlider;
        }

        private CrewInstanceData _selectedCrew;

        private List<CrewInstanceData> _currentCrews;

        private bool _isFeeding;

        private Sequence _upgradeSequence;

        private void OnEnable()
        {
            Debug.Log("3. CrewUI OnEnable");

            CrewManager.Instance.OnCrewLoaded += RefreshUI;

            // 이미 데이터가 있다면 즉시 갱신
            if (CrewManager.Instance.GetOwnedCrews().Count > 0)
            {
                RefreshUI();
            }
        }

        private void OnDisable()
        {
            CrewManager.Instance.OnCrewLoaded -= RefreshUI;
            _selectedCrew = null;
        }
        public void RefreshUI()
        {
            PlayFabGateway.Instance.Inventory.FlushAll(onSuccess: result =>
            {
                Debug.Log("4. RefreshUI");
                RefreshCrewSlots();
                RefreshCrewList();
                RefreshTotalPassive();
                _crewInfoPanel.SetActive(false);

                if (_selectedCrew != null)
                {
                    ShowCrewInfo(_selectedCrew);
                    _crewInfoPanel.SetActive(true);
                    return;
                }
            }, onFailure: error =>
            {
                Debug.LogError("Failed to flush inventory.");
            });
        }

        #region UI 초기화
        private void RefreshCrewSlots()
        {
            Debug.Log("5. RefreshCrewSlots");
            List<CrewManager.CrewSlot> slots = CrewManager.Instance.CrewSlots;

            _unlockButton.SetActive(!slots[3].IsUnlocked);

            for (int i = 0; i < slots.Count; i++)
            {
                _crewSlots[i].Refresh(slots[i]);
            }
        }

        private void ClearCrewList()
        {
            foreach (Transform child in _crewListPanel.transform)
            {
                Destroy(child.gameObject);
            }

            _crewList.Clear();
        }

        private void RefreshCrewList()
        {
            Debug.Log("6. RefreshCrewList");

            _currentCrews = CrewManager.Instance.GetOwnedCrews();

            ClearCrewList();

            foreach (CrewInstanceData crew in _currentCrews)
            {
                CrewData crewData = CrewManager.Instance.CrewDataBase.GetCrewData(crew.CrewId);

                GameObject obj = Instantiate(_crewListPrefab, _crewListPanel.transform);

                CrewListItem item = obj.GetComponent<CrewListItem>();

                item.Init(crew, crewData);

                item.SelectButton.onClick.AddListener(() =>
                {
                    OnClickEquip(crew);
                });

                item.InfoButton.onClick.AddListener(() =>
                {
                    OnClickCrewInfo(crew);
                });

                _crewList.Add(item);
            }
        }

        private void RefreshTotalPassive()
        {
            Debug.Log("7. RefreshTotalPassive");
            List<CrewInstanceData> equipped = CrewManager.Instance.GetEquippedCrews();

            float rarity = CrewPassiveCalculator.GetFishRarityBonus(equipped);

            float offline = CrewPassiveCalculator.GetOfflineRewardBonus(equipped);

            float boss = CrewPassiveCalculator.GetBossFishBonus(equipped);

            float multi = CrewPassiveCalculator.GetMultiplayerContributionBonus(equipped);

            float auto = CrewPassiveCalculator.GetAutoFishingSpeedBonus(equipped);

            _passives[0].text = $"희귀도 증가 +{rarity:P0}";
            _passives[1].text = $"오프라인 보상 +{offline:P0}";
            _passives[2].text = $"보스 물고기 보너스 +{boss:P0}";
            _passives[3].text = $"협동 기여도 +{multi:P0}";
            _passives[4].text = $"자동 낚시 속도 +{auto:P0}";
        }

        private void ShowCrewInfo(CrewInstanceData crew)
        {
            _crewFragmentSlider.gameObject.SetActive(crew.Grade != CrewGrade.SSR);
            _crewUpgradeButton.gameObject.SetActive(crew.Grade != CrewGrade.SSR);

            _crewInfoOutline.gameObject.SetActive(crew.Grade != CrewGrade.SSR);
            _outlineSSR.gameObject.SetActive(crew.Grade == CrewGrade.SSR);

            Debug.Log("ShowCrewInfo");
            _crewInfoPanel.SetActive(true);
            _crewInfoName.text = CrewManager.Instance.CrewDataBase.GetCrewData(crew.CrewId).CrewName;
            _crewInfoImage.sprite = CrewManager.Instance.CrewDataBase.GetCrewData(crew.CrewId).CrewSprite;
            switch (crew.Grade)
            {
                case CrewGrade.R:
                    _crewInfoGrade.sprite = _gradeR;
                    _crewInfoNameSpace.sprite = _nameSpaceR;
                    _crewInfoOutline.sprite = _outlineR;
                    SetCrewFregmentSlider(crew);
                    break;
                case CrewGrade.SR:
                    _crewInfoGrade.sprite = _gradeSR;
                    _crewInfoNameSpace.sprite = _nameSpaceSR;
                    _crewInfoOutline.sprite = _outlineSR;
                    SetCrewFregmentSlider(crew);
                    break;
                case CrewGrade.SSR:
                    _crewInfoGrade.sprite = _gradeSSR;
                    _crewInfoNameSpace.sprite = _nameSpaceSSR;
                    break;
            }

            int uiIndex = 0;
            int maxValue = 0;
            int value = 0;
            foreach (var passive in crew.Passives)
            {
                if (passive.Level <= 0)
                    continue;

                _crewPassives[uiIndex].PassiveName.text = $"{GetPassiveName(passive.Type)}\nLv.{passive.Level}";
                maxValue = CrewManager.Instance.GetRequiredCrewExp(crew.Grade, passive.Level);
                value = passive.LevelProgress; ;
                Slider slider = _crewPassives[uiIndex].PassiveSlider;

                PlayPassiveSliderAnimation(
                    slider,
                    (int)slider.value,     // 현재 값에서 시작
                    value,                 // 목표 값
                    maxValue);

                uiIndex++;
            }

            foreach (var passive in crew.Passives)
            {
                Debug.Log($"{passive.Type} Level={passive.Level}");
            }
        }

        private void SetCrewFregmentSlider(CrewInstanceData crew)
        {
            Debug.Log("9. SetCrewFregmentSlider");

            _crewFragmentSlider.minValue = 0;
            int fragmentCount = CrewManager.Instance.GetFragmentCount(crew.CrewId);
            int requiredCount = 0;

            switch (crew.Grade)
            {
                case CrewGrade.R:
                    _crewFragmentSlider.fillRect.GetComponent<Image>().color = new Color32(29, 58, 90, 255);
                    requiredCount = CrewManager.Instance.RtoSR;

                    break;
                case CrewGrade.SR:
                    _crewFragmentSlider.fillRect.GetComponent<Image>().color = new Color32(81, 52, 107, 255);
                    requiredCount = CrewManager.Instance.SRtoSSR;

                    break;
                case CrewGrade.SSR:

                    break;
            }
            _crewFragmentSlider.maxValue = requiredCount;

            int targetValue = Mathf.Min(fragmentCount, requiredCount);

            _crewFragmentSlider.value = targetValue;
            _crewFragmentSlider.GetComponentInChildren<TextMeshProUGUI>().text = $"{fragmentCount} / {requiredCount}";
        }

        private string GetPassiveName(CrewPassiveType type)
        {
            switch (type)
            {
                case CrewPassiveType.FishRarityIncrease:
                    return "희귀도 증가";

                case CrewPassiveType.OfflineRewardEfficiency:
                    return "오프라인 보상 효율";

                case CrewPassiveType.BossFishSpecialization:
                    return "보스 물고기 보너스";

                case CrewPassiveType.MultiplayerContributionIncrease:
                    return "협동 기여도";

                case CrewPassiveType.AutoFishingSpeedIncrease:
                    return "자동 낚시 속도";

                default:
                    return "알 수 없음";
            }
        }
        #endregion

        #region Button Click Event
        public void OnClickCrewInfo(CrewInstanceData crew)
        {
            _selectedCrew = crew;

            ShowCrewInfo(crew);
        }

        public void OnClickEquip(CrewInstanceData crew)
        {
            string result = CrewManager.Instance.EquipCrew(crew);

            RefreshUI();

            _popupPanel.SetActive(true);
            _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = $"{result}";
        }

        public void OnClickUnequip(int index)
        {
            CrewManager.Instance.UnequipCrew(index);
            RefreshUI();
        }

        public void OnClickUnlockButton()
        {
            CrewManager.Instance.UnlockSlot(
                onSuccess: allUnlocked =>
                {
                    Debug.Log(CrewManager.Instance.CrewSlots[3].IsUnlocked);

                    if (allUnlocked)
                    {
                        _popupPanel.SetActive(true);
                        _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = "슬롯 잠금이 해제되었습니다.";
                        RefreshCrewSlots();
                    }
                    else
                    {
                        _popupPanel.SetActive(true);
                        _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = "슬롯 잠금이 해제되었습니다.";
                        RefreshCrewSlots();
                        Debug.Log("슬롯 해금 성공");
                    }
                },
                onFail: error =>
                {
                    _popupPanel.SetActive(true);
                    _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = "골드가 부족합니다.";
                });
        }
        public void OnclickDebugSlotLock()
        {
            CrewManager.Instance.SlotLock();
            RefreshCrewSlots();
        }
        public void PopupPanelActive()
        {
            _popupPanel.SetActive(!_popupPanel.activeSelf);
        }
        public void OnClickBackButton()
        {
            _crewInfoPanel.SetActive(false);
            _selectedCrew = null;
            RefreshUI();
        }
        

        public void OnClickCrewUpgradeButton()
        {
            if (_selectedCrew == null)
                return;

            _crewUpgradeButton.interactable = false;

            CrewGrade beforeGrade = _selectedCrew.Grade;

            CrewManager.Instance.UpgradeCrew(_selectedCrew,
                upgradedCrew =>
                {
                    _selectedCrew = upgradedCrew;
                    PlayUpgradeAnimation(
                        beforeGrade,
                        upgradedCrew.Grade,
                        () =>
                        {
                            _upgradeFlash.gameObject.SetActive(false);

                            RefreshUI();
                            _crewUpgradeButton.interactable = true;
                        });
                },

                error =>
                {
                    _crewUpgradeButton.interactable = true;

                    _popupPanel.SetActive(true);
                    _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = error;
                });
        }
        public void OnClickRerollPassive(int uiIndex)
        {
            List<CrewInstanceData> equipped = CrewManager.Instance.GetEquippedCrews();

            if (_selectedCrew == null)
                return;

            bool result = CrewManager.Instance.RerollSinglePassive(
                _selectedCrew.CrewId,
                uiIndex);

            if (!result)
            {
                _popupPanel.SetActive(true);
                _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = "패시브를 변경할 수 없습니다.";
                return;
            }

            _selectedCrew = CrewManager.Instance.FindCrew(_selectedCrew.CrewId);

            RefreshTotalPassive();
            ShowCrewInfo(_selectedCrew);
        }

        public void OnClickFeedFood()
        {
            if (_isFeeding)
                return;

            if (_selectedCrew == null)
                return;

            if (_selectedCrew.Passives.Count == 0)
                return;

            _isFeeding = true;

            CrewManager.Instance.IncreasePassiveProgress(
                _selectedCrew.CrewId,
                onSuccess: crew =>
                {
                    _selectedCrew = crew;

                    RefreshTotalPassive();
                    ShowCrewInfo(_selectedCrew);

                    _isFeeding = false;
                },
                onFail: error =>
                {
                    _popupPanel.SetActive(true);
                    _popupPanel.GetComponentInChildren<TextMeshProUGUI>().text = $"{error}";
                    _isFeeding = false;
                });
        }

        #endregion

        #region 연출 - 패시브 강화
        private void PlayPassiveSliderAnimation(Slider slider, int startValue, int endValue, int maxValue)
        {
            Sequence sequence = DOTween.Sequence();

            TextMeshProUGUI text = slider.GetComponentInChildren<TextMeshProUGUI>();

            slider.maxValue = maxValue;
            slider.value = startValue;

            int currentValue = startValue;

            sequence.Join(
                slider.DOValue(endValue, 0.5f)
                    .SetEase(Ease.OutCubic));

            sequence.Join(
                DOTween.To(
                    () => currentValue,
                    x =>
                    {
                        currentValue = x;
                        text.text = $"{currentValue} / {maxValue}";
                    },
                    endValue,
                    0.5f)
                .SetEase(Ease.OutCubic));
        }

        #endregion

        #region 연출 - 선원 승급
        private void PlayUpgradeAnimation(CrewGrade beforeGrade, CrewGrade afterGrade, Action onComplete)
        {
            _upgradeSequence?.Kill();

            _upgradeFlash.gameObject.SetActive(true);

            Color glowColor =
                afterGrade == CrewGrade.SR
                ? new Color32(170, 70, 255, 255)      // 보라
                : new Color32(255, 215, 0, 255);      // 금색

            _crewInfoOutline.color = Color.white;
            _crewInfoOutline.transform.localScale = Vector3.one;

            _upgradeFlash.color = glowColor;
            _upgradeFlash.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0);

            _upgradeSequence = DOTween.Sequence();

            //----------------------------------------
            // 1. 점점 빛나기
            //----------------------------------------

            _upgradeSequence.Append(
                _crewInfoOutline.DOColor(glowColor, 0.45f));

            _upgradeSequence.Join(
                _crewInfoOutline.transform
                    .DOScale(1.08f, 0.45f)
                    .SetEase(Ease.OutQuad));

            //----------------------------------------
            // 2. 번쩍
            //----------------------------------------

            _upgradeSequence.Append(
                _upgradeFlash
                    .DOFade(1f, 0.18f)
                    .SetEase(Ease.OutQuad));

            //----------------------------------------
            // 3. 이 순간 Sprite 교체
            //----------------------------------------

            _upgradeSequence.AppendCallback(() =>
            {
                if (afterGrade == CrewGrade.SR)
                {
                    _crewInfoOutline.sprite = _outlineSR;
                    _crewInfoGrade.sprite = _gradeSR;
                    _crewInfoNameSpace.sprite = _nameSpaceSR;
                }
                else
                {
                    _crewInfoOutline.gameObject.SetActive(false);
                    _outlineSSR.gameObject.SetActive(true);

                    _crewInfoGrade.sprite = _gradeSSR;
                    _crewInfoNameSpace.sprite = _nameSpaceSSR;
                }
            });

            //----------------------------------------
            // 4. 빛 사라짐
            //----------------------------------------

            _upgradeSequence.Append(
                _upgradeFlash
                    .DOFade(0f, 0.5f)
                    .SetEase(Ease.OutCubic));

            _upgradeSequence.Join(
                _crewInfoOutline
                    .DOColor(Color.white, 0.5f));

            _upgradeSequence.Join(
                _crewInfoOutline.transform
                    .DOScale(1f, 0.5f));

            //----------------------------------------

            _upgradeSequence.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        #endregion
    }
}