using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.Multiplay
{
    // 정산 보상 목록의 아이콘 한 칸. RewardSlot 프리팹 루트에 붙인다.

    public class RewardSlotView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;

        public void Set(Sprite icon, int count)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null; // 아이콘 매핑이 없으면 빈 칸 대신 깨진 이미지가 보이지 않게 숨김
            }

            if (countText != null)
                countText.text = $"x{count} ";
        }
    }
}