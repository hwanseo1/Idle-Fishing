using Crew;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CrewSlotUI : MonoBehaviour
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

    public void SetLocked()
    {
        gameObject.SetActive(false);
        _crewImage.color = Color.gray7;
        _crewName.text = "잠겨있음";
    }

    public void SetEmpty()
    {
        gameObject.SetActive(false);
        _crewName.text = "비어있음";
        _crewImage.sprite = null;
        _crewGradeImage.sprite = null;
        _crewOutlineImage.sprite = null;
        _crewNameSpaceImage.sprite = null;
    }

    public void SetCrew(CrewInstanceData crewData)
    {
        gameObject.SetActive(true);

        CrewData data = CrewManager.Instance.CrewDataBase.GetCrewData(crewData.CrewId);

        _crewName.text = $"{data.CrewName}";

        _crewImage.sprite = data.CrewSprite;

        SetImageByGrade(crewData.Grade);
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

    public void Refresh(CrewManager.CrewSlot slot)
    {
        if (!slot.IsUnlocked)
        {
            SetLocked();
            return;
        }

        if (slot.EquippedCrew == null || slot.EquippedCrew.CrewId == null || slot.EquippedCrew.CrewId == "")
        {
            SetEmpty();
            return;
        }

        SetCrew(slot.EquippedCrew);
    }
}