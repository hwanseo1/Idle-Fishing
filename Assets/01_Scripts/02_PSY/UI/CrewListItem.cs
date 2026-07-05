using Crew;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CrewListItem : MonoBehaviour
{
    [Header("Crew Name")]
    [SerializeField] private TMP_Text _crewName;

    [Header("Crew Image")]
    [SerializeField] private Image _crewImage;
    [SerializeField] private Image _crewGradeImage;
    [SerializeField] private Image _crewOutlineImage;
    [SerializeField] private Image _crewNameSpaceImage;

    [Header("Crew Buttons")]
    [SerializeField] private Button _selectButton;
    [SerializeField] private Button _infoButton;

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

    private CrewInstanceData _crew;

    public Button SelectButton => _selectButton;
    public Button InfoButton => _infoButton;

    public void Init(CrewInstanceData crew, CrewData crewData)
    {
        _crew = crew;

        _crewName.text = $"{crewData.CrewName}";

        _crewImage.sprite = crewData.CrewSprite;

        SetImageByGrade(crew.Grade);
    }

    private void SetImageByGrade(CrewGrade crewGrade)
    {
        _crewOutlineImage.gameObject.SetActive(_crew.Grade != CrewGrade.SSR);
        _outlineImage_SSR.gameObject.SetActive(_crew.Grade == CrewGrade.SSR);

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
}