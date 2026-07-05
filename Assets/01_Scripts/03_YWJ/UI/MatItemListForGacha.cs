using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MatItemListForGacha : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image _materialImage;
    [SerializeField] private TextMeshProUGUI _materialName;


    public void Init(Sprite materialSprite, string materialName)
    {
        _materialImage.sprite = materialSprite;
        _materialName.text = materialName;
    }
}
