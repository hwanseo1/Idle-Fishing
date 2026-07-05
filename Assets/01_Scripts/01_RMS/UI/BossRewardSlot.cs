using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.UI
{
    // 보상 목록 한 줄. BossRewardPanel이 동적으로 생성한다.
    public class BossRewardSlot : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _amountText;
        [Tooltip("레전더리 물고기일 때 활성화할 강조 오브젝트 (테두리 이펙트 등). 없으면 무시.")]
        [SerializeField] private GameObject _legendaryBadge;

        public void Set(Sprite icon, string fishId, int amount, bool isLegendary)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_amountText != null)
                _amountText.text = $"x{amount}";

            if (_legendaryBadge != null)
                _legendaryBadge.SetActive(isLegendary);
        }
    }
}