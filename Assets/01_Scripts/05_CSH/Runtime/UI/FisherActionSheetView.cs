using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 판매 수량, 요리 수량, 취소 확인 같은 바텀시트/팝업 View입니다.
    /// 가방 판매와 요리 취소/수량 조정처럼 실수 방지가 필요한 조작을
    /// 메인 그리드와 분리해서 표시합니다.
    /// </summary>
    public sealed class FisherActionSheetView : MonoBehaviour
    {
        /// <summary>팝업의 목적을 표시하는 제목 텍스트입니다.</summary>
        public TextMeshProUGUI TitleText;
        /// <summary>판매 단가, 보유량, 취소 안내 같은 설명 텍스트입니다.</summary>
        public TextMeshProUGUI BodyText;
        /// <summary>수량을 직접 입력하는 TMP 입력 필드입니다. 확인 팝업에서는 숨깁니다.</summary>
        public TMP_InputField NumberInput;
        /// <summary>수량을 1 감소시키는 버튼입니다.</summary>
        public FisherButtonView DecreaseButton;
        /// <summary>수량을 1 증가시키는 버튼입니다.</summary>
        public FisherButtonView IncreaseButton;
        /// <summary>가능한 최대 수량으로 맞추는 버튼입니다.</summary>
        public FisherButtonView MaxButton;
        /// <summary>판매, 취소 확정 같은 위험 조작을 실행하는 버튼입니다.</summary>
        public FisherButtonView ConfirmButton;
        /// <summary>팝업을 닫거나 위험 조작을 취소하는 버튼입니다.</summary>
        public FisherButtonView CancelButton;

        /// <summary>
        /// 수량 입력형 바텀시트를 표시합니다.
        /// 직접 입력값은 maxValue 자리수와 실제 상한으로 다시 제한해 overflow 입력을 막습니다.
        /// </summary>
        public void ShowQuantity(
            string title,
            string body,
            int value,
            int maxValue,
            Action<int> onChanged,
            string confirmLabel,
            Action onConfirm,
            Action onCancel)
        {
            ShowSheet();
            SetText(TitleText, title);
            SetText(BodyText, body);
            ApplyReadableTextColor();
            int safeMax = Mathf.Max(1, maxValue);
            int safeValue = Mathf.Clamp(value, 1, safeMax);

            if (NumberInput != null)
            {
                NumberInput.gameObject.SetActive(true);
                NumberInput.characterLimit = safeMax.ToString().Length;
                NumberInput.onValueChanged.RemoveAllListeners();
                NumberInput.onEndEdit.RemoveAllListeners();
                NumberInput.SetTextWithoutNotify(safeValue.ToString());
                NumberInput.onValueChanged.AddListener(raw =>
                {
                    if (int.TryParse(raw, out int parsed) && parsed > safeMax)
                    {
                        NumberInput.SetTextWithoutNotify(safeMax.ToString());
                    }
                });
                NumberInput.onEndEdit.AddListener(raw =>
                {
                    int parsed = int.TryParse(raw, out int result) ? result : 1;
                    onChanged?.Invoke(Mathf.Clamp(parsed, 1, safeMax));
                });
            }

            DecreaseButton?.gameObject.SetActive(true);
            IncreaseButton?.gameObject.SetActive(true);
            MaxButton?.gameObject.SetActive(true);
            ConfirmButton?.gameObject.SetActive(true);
            CancelButton?.gameObject.SetActive(true);
            DecreaseButton?.Bind("-1", false, true, () => onChanged?.Invoke(Mathf.Max(1, safeValue - 1)));
            IncreaseButton?.Bind("+1", false, true, () => onChanged?.Invoke(Mathf.Min(safeMax, safeValue + 1)));
            MaxButton?.Bind("최대", false, true, () => onChanged?.Invoke(safeMax));
            ConfirmButton?.Bind(confirmLabel, false, true, onConfirm);
            CancelButton?.Bind("닫기", false, true, onCancel);
        }

        /// <summary>
        /// 취소/전체 판매처럼 확인이 필요한 단일 선택 팝업을 표시합니다.
        /// 수량 입력 UI는 숨기고 확인/취소 버튼만 남깁니다.
        /// </summary>
        public void ShowConfirm(string title, string body, string confirmLabel, bool canConfirm, Action onConfirm, Action onCancel)
        {
            ShowSheet();
            SetText(TitleText, title);
            SetText(BodyText, body);
            ApplyReadableTextColor();

            if (NumberInput != null)
            {
                NumberInput.gameObject.SetActive(false);
            }

            DecreaseButton?.gameObject.SetActive(false);
            IncreaseButton?.gameObject.SetActive(false);
            MaxButton?.gameObject.SetActive(false);
            ConfirmButton?.gameObject.SetActive(true);
            CancelButton?.gameObject.SetActive(true);
            ConfirmButton?.Bind(confirmLabel, false, canConfirm, onConfirm);
            CancelButton?.Bind("취소", false, true, onCancel);
        }

        private void ShowSheet()
        {
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private void ApplyReadableTextColor()
        {
            Image background = GetComponent<Image>();
            bool brightPlainBackground = background != null &&
                                         background.sprite == null &&
                                         background.color.a > 0.1f &&
                                         Luminance(background.color) > 0.55f;
            Color textColor = brightPlainBackground ? FisherRuntimeUi.ParchmentTextColor : FisherRuntimeUi.TextColor;
            SetColor(TitleText, textColor);
            SetColor(BodyText, textColor);
            if (NumberInput != null && NumberInput.textComponent != null)
            {
                NumberInput.textComponent.color = FisherRuntimeUi.TextColor;
            }
        }

        private static void SetColor(TextMeshProUGUI text, Color color)
        {
            if (text != null)
            {
                text.color = color;
            }
        }

        private static float Luminance(Color color)
        {
            return color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        }
    }
}
