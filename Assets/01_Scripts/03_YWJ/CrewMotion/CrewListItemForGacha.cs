using Crew;
using DG.Tweening;
using Reward;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CrewListItemForGacha : MonoBehaviour
{
    [Header("Crew Name")]
    [SerializeField] private TMP_Text _crewName;

    [Header("Crew Image")]
    [SerializeField] private Image _crewImage;
    [SerializeField] private Image _crewGradeImage;
    [SerializeField] private Image _crewOutlineImage;
    [SerializeField] private Image _crewNameSpaceImage;

    [Header("Outline Sprite by Grade")]
    [SerializeField] private Sprite _outlineSprite_R;
    [SerializeField] private Sprite _outlineSprite_SR;
    [SerializeField] private Image _outlineImage_SSR;

    [Header("Grade Sprite")]
    [SerializeField] private Sprite _gradeSprite_R;
    [SerializeField] private Sprite _gradeSprite_SR;
    [SerializeField] private Sprite _gradeSprite_SSR;

    [Header("Name Space Image")]
    [SerializeField] private Sprite _nameSpaceSprite_R;
    [SerializeField] private Sprite _nameSpaceSprite_SR;
    [SerializeField] private Sprite _nameSpaceSprite_SSR;

    [Header("Fragment")]
    [SerializeField] private MatItemListForGacha _fragmentPrefab;
    [SerializeField] private Slider _crewFragmentSlider;

    [Header("New Icon")]
    [SerializeField] private Image _newIcon;

    [Header("Front & Back")]
    [SerializeField] private GameObject _front;
    [SerializeField] private GameObject _back;

    private bool _isDuplicate;

    private Sequence _fragmentSequence;
    private Sequence _fragmentRewardSequence;

    private RectTransform _rect;

    private CrewData _crewData;
    private GachaReward _reward;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    public void Init(CrewData crewData, GachaReward reward)
    {
        _crewData = crewData;
        _reward = reward;
        _crewFragmentSlider.gameObject.SetActive(false);
        _fragmentPrefab.gameObject.SetActive(false);
        _newIcon.gameObject.SetActive(false);

        _crewName.text = crewData.CrewName;
        _crewImage.sprite = crewData.CrewSprite;

        SetImageByGrade(crewData.CrewGrade);

        _isDuplicate = reward.IsDuplicate;

        if (_isDuplicate)
        {
            string fragmentId = $"fragment_{crewData.CrewId}";

            Sprite sprite =
                CrewManager.Instance.Convertor.TryGetFragmentSprite(
                    fragmentId,
                    out Sprite fragmentSprite)
                    ? fragmentSprite
                    : null;

            _fragmentPrefab.Init(
                sprite,
                $"+{CrewManager.Instance.GetDuplicateFragmentReward(crewData.CrewGrade)}");

            if (crewData.CrewGrade != CrewGrade.SSR)
            {
                _crewFragmentSlider.gameObject.SetActive(true);
            }
        }
    }

    private void SetImageByGrade(CrewGrade crewGrade)
    {
        _crewOutlineImage.gameObject.SetActive(crewGrade != CrewGrade.SSR);
        _outlineImage_SSR.gameObject.SetActive(crewGrade == CrewGrade.SSR);

        if (crewGrade == CrewGrade.R)
        {
            _crewOutlineImage.sprite = _outlineSprite_R;
            _crewGradeImage.sprite = _gradeSprite_R;
            _crewNameSpaceImage.sprite = _nameSpaceSprite_R;
        }
        else if (crewGrade == CrewGrade.SR)
        {
            _crewOutlineImage.sprite = _outlineSprite_SR;
            _crewGradeImage.sprite = _gradeSprite_SR;
            _crewNameSpaceImage.sprite = _nameSpaceSprite_SR;
        }
        else if (crewGrade == CrewGrade.SSR)
        {
            _crewGradeImage.sprite = _gradeSprite_SSR;
            _crewNameSpaceImage.sprite = _nameSpaceSprite_SSR;
        }
    }

    #region FregmentSlider Animation
    private void SetCrewFragmentSlider(CrewGrade grade, int previousCount, int currentCount)
    {
        int requiredCount = CrewManager.Instance.GetRequiredFragmentCount(grade);

        switch (grade)
        {
            case CrewGrade.R:
                _crewFragmentSlider.fillRect.GetComponent<Image>().color = new Color32(29, 58, 90, 255);
                break;

            case CrewGrade.SR:
                _crewFragmentSlider.fillRect.GetComponent<Image>().color = new Color32(81, 52, 107, 255);
                break;
        }

        int start = Mathf.Min(previousCount, requiredCount);
        int end = Mathf.Min(currentCount, requiredCount);

        PlayFragmentAnimation(start, end, requiredCount);
    }

    private void PlayFragmentAnimation(int startValue, int endValue, int maxValue)
    {
        _fragmentSequence?.Kill();

        startValue = Mathf.Clamp(startValue, 0, maxValue);
        endValue = Mathf.Clamp(endValue, 0, maxValue);

        TextMeshProUGUI text =
            _crewFragmentSlider.GetComponentInChildren<TextMeshProUGUI>();

        _crewFragmentSlider.maxValue = maxValue;
        _crewFragmentSlider.value = startValue;

        int currentValue = startValue;

        _fragmentSequence = DOTween.Sequence();

        _fragmentSequence.Join(
            _crewFragmentSlider
                .DOValue(endValue, 0.8f)
                .SetEase(Ease.OutCubic));

        _fragmentSequence.Join(
            DOTween.To(
                () => currentValue,
                x =>
                {
                    currentValue = x;
                    text.text = $"{currentValue} / {maxValue}";
                },
                endValue,
                0.8f)
            .SetEase(Ease.OutCubic));
    }
    #endregion

    #region Fragment Reward Animation
    private void PlayFragmentRewardAnimation()
    {
        _fragmentRewardSequence?.Kill();

        CanvasGroup canvasGroup =
            _fragmentPrefab.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup =
                _fragmentPrefab.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;

        _fragmentPrefab.gameObject.SetActive(true);

        _fragmentRewardSequence = DOTween.Sequence();

        _fragmentRewardSequence.Append(
            canvasGroup
                .DOFade(0.3f, 0.45f)
                .SetEase(Ease.InOutSine));

        _fragmentRewardSequence.Append(
            canvasGroup
                .DOFade(1f, 0.45f)
                .SetEase(Ease.InOutSine));

        _fragmentRewardSequence.SetLoops(2);

        _fragmentRewardSequence.OnComplete(() =>
        {
            canvasGroup.alpha = 1f;
        });
    }
    #endregion

    #region Flip Animation
    public void PlayFlip(Action onComplete = null)
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(transform.DOScaleX(0, 0.2f));

        seq.AppendCallback(() =>
        {
            _front.SetActive(true);
            _back.SetActive(false);
        });

        seq.Append(transform.DOScaleX(1, 0.2f));

        seq.OnComplete(() =>
        {
            if (_isDuplicate)
            {
                PlayFragmentRewardAnimation();

                SetCrewFragmentSlider(
                    _crewData.CrewGrade,
                    _reward.PreviousFragment,
                    _reward.CurrentFragment);
            }
            else
            {
                _newIcon.gameObject.SetActive(true);
            }

            onComplete?.Invoke();
        });
    }
    #endregion
}
