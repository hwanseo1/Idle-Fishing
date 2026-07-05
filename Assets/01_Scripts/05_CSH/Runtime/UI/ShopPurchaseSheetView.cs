using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 상점 상품 클릭 후 구매 전 확인 정보를 표시하는 Shop 전용 Sheet입니다.
    /// 레이아웃과 폰트는 씬을 source of truth로 두고, 런타임은 데이터와 버튼 상태만 바인딩합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopPurchaseSheetView : MonoBehaviour
    {
        public Image IconImage;
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI DescriptionText;
        public TextMeshProUGUI RewardCountText;
        public TextMeshProUGUI PriceText;
        public TextMeshProUGUI StatusText;
        public FisherButtonView PurchaseButton;
        public FisherButtonView CancelButton;

        public void Show(
            Sprite icon,
            string title,
            string description,
            string rewardCount,
            string price,
            string status,
            bool canPurchase,
            bool requestBusy,
            Action onPurchase,
            Action onCancel)
        {
            if (this == null)
            {
                return;
            }

            gameObject.SetActive(true);
            BindIcon(icon);
            SetText(TitleText, title);
            SetText(DescriptionText, description);
            SetText(RewardCountText, rewardCount);
            SetText(PriceText, price);
            SetText(StatusText, status);

            if (PurchaseButton != null)
            {
                PurchaseButton.gameObject.SetActive(true);
                PurchaseButton.Bind(requestBusy ? "요청 중" : "구매", false, canPurchase && !requestBusy, onPurchase);
            }

            if (CancelButton != null)
            {
                CancelButton.gameObject.SetActive(true);
                CancelButton.Bind("취소", false, true, onCancel);
            }
        }

        public void Hide()
        {
            if (this == null)
            {
                return;
            }

            if (PurchaseButton != null)
            {
                PurchaseButton.Bind("구매", false, false, null);
            }

            if (CancelButton != null)
            {
                CancelButton.Bind("취소", false, true, null);
            }

            gameObject.SetActive(false);
        }

        private void BindIcon(Sprite icon)
        {
            if (IconImage == null)
            {
                return;
            }

            IconImage.sprite = icon;
            IconImage.enabled = icon != null;
            IconImage.preserveAspect = true;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.text = value ?? string.Empty;
            FisherRuntimeUi.RefreshWrappedTextPanel(text);
        }
    }
}
