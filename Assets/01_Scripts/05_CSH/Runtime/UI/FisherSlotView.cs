using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// itemId가 아닌 순번 슬롯에 데이터를 바인딩하는 공통 슬롯 View입니다.
    /// 가방, 요리, 상점, 도감이 같은 슬롯 컴포넌트를 쓰되 실제 배치는
    /// <see cref="FisherUiLayoutContract"/>가 화면별로 조정합니다.
    /// </summary>
    public sealed class FisherSlotView : MonoBehaviour
    {
        /// <summary>슬롯 전체 클릭을 받는 버튼입니다. 비어 있는 슬롯에서는 비활성화됩니다.</summary>
        public Button Button;
        /// <summary>선택/비활성/빈 슬롯 상태를 표현하는 슬롯 배경 이미지입니다.</summary>
        public Image BackgroundImage;
        /// <summary>DATA 또는 <see cref="FisherUiArtProfile"/>에서 가져온 itemId 아이콘을 표시합니다.</summary>
        public Image IconImage;
        /// <summary>카테고리, 레시피 위치, 미발견 표시 같은 짧은 상단 배지 텍스트입니다.</summary>
        public TextMeshProUGUI BadgeText;
        /// <summary>상점/요리/도감처럼 이름이 필요한 슬롯에서 표시하는 이름 텍스트입니다.</summary>
        public TextMeshProUGUI NameText;
        /// <summary>수량, 가격, 보유량처럼 슬롯 하단에 짧게 표시하는 숫자 텍스트입니다.</summary>
        public TextMeshProUGUI QuantityText;
        /// <summary>희귀도, 구매 가능 여부, 미발견 상태 같은 보조 정보를 표시합니다.</summary>
        public TextMeshProUGUI MetaText;
        /// <summary>선택된 슬롯 위에 올리는 테두리/하이라이트 오브젝트입니다.</summary>
        public GameObject SelectedFrame;
        /// <summary>잠금 상태를 나타내는 배지입니다. 가방 판매 보호와 미해금 도감 슬롯에 사용합니다.</summary>
        public GameObject LockedBadge;
        /// <summary>우상단 상태 배지입니다. 현재는 신규 또는 보상 가능 상태 중 하나만 표시합니다.</summary>
        public GameObject NewBadge;
        /// <summary>일반 슬롯 배경 스프라이트입니다.</summary>
        public Sprite NormalSprite;
        /// <summary>선택 슬롯 배경 스프라이트입니다.</summary>
        public Sprite SelectedSprite;
        /// <summary>빈 슬롯 배경 스프라이트입니다.</summary>
        public Sprite EmptySprite;
        /// <summary>씬에서 직접 지정한 슬롯 배경 색/스프라이트를 런타임 상태 갱신으로 덮지 않습니다.</summary>
        [NonSerialized] public bool PreserveInspectorChrome;
        [NonSerialized] private bool _hasInspectorChromeSnapshot;
        [NonSerialized] private Color _inspectorNormalColor;
        [NonSerialized] private Sprite _inspectorNormalSprite;
        [NonSerialized] private Image.Type _inspectorImageType;

        /// <summary>
        /// 순번 슬롯에 현재 데이터 한 건을 바인딩합니다.
        /// 이 메서드는 GameObject를 새로 만들지 않고 텍스트, 아이콘, 배지,
        /// 클릭 가능 여부만 갱신해서 Main 씬으로 옮겨도 구조가 흔들리지 않게 합니다.
        /// </summary>
        public void Bind(
            string title,
            string quantity,
            string meta,
            string badge,
            Sprite icon,
            bool selected,
            bool dimmed,
            bool locked,
            bool isNew,
            Action onClick)
        {
            gameObject.SetActive(true);
            SetText(NameText, title);
            SetText(QuantityText, quantity);
            SetText(MetaText, meta);
            SetText(BadgeText, badge);
            SetSprite(icon);
            if (icon == null && NameText != null && !string.IsNullOrWhiteSpace(title))
            {
                NameText.gameObject.SetActive(true);
            }

            if (BackgroundImage != null)
            {
                ApplyChrome(selected, dimmed, false);
            }

            bool showStateBadge = !locked && isNew;
            SetActive(SelectedFrame, selected);
            SetActive(LockedBadge, locked);
            SetActive(NewBadge, showStateBadge);

            if (Button != null)
            {
                Button.interactable = onClick != null;
                Button.onClick.RemoveAllListeners();
                if (onClick != null)
                {
                    Button.onClick.AddListener(() => onClick());
                }
            }
        }

        /// <summary>
        /// 슬롯 구조는 유지하고 표시 데이터와 클릭 리스너만 비웁니다.
        /// 그리드 칸 수를 유지한 채 빈 칸을 보여주기 위한 용도입니다.
        /// </summary>
        public void Clear()
        {
            gameObject.SetActive(true);
            SetText(NameText, string.Empty);
            SetText(QuantityText, string.Empty);
            SetText(MetaText, string.Empty);
            SetText(BadgeText, string.Empty);
            SetSprite(null);
            SetActive(SelectedFrame, false);
            SetActive(LockedBadge, false);
            SetActive(NewBadge, false);

            if (BackgroundImage != null)
            {
                ApplyChrome(false, false, true);
            }

            if (Button != null)
            {
                Button.onClick.RemoveAllListeners();
                Button.interactable = false;
            }
        }

        private void SetSprite(Sprite icon)
        {
            if (IconImage == null)
            {
                return;
            }

            IconImage.sprite = icon;
            IconImage.enabled = icon != null;
            IconImage.color = Color.white;
            IconImage.preserveAspect = true;
        }

        private void ApplyChrome(bool selected, bool dimmed, bool empty)
        {
            if (BackgroundImage == null)
            {
                return;
            }

            if (PreserveInspectorChrome)
            {
                ApplyPreservedChrome(selected, dimmed, empty);
                return;
            }

            Sprite sprite = ResolveStateSprite(selected, empty, null);
            if (!FisherRuntimeUi.ApplyOptionalSprite(BackgroundImage, sprite))
            {
                BackgroundImage.color = StateColor(selected, dimmed, empty);
            }
        }

        private void ApplyPreservedChrome(bool selected, bool dimmed, bool empty)
        {
            EnsureInspectorChromeSnapshot();
            Sprite sprite = ResolveStateSprite(selected, empty, _inspectorNormalSprite);
            if (sprite != null)
            {
                if (sprite == _inspectorNormalSprite)
                {
                    BackgroundImage.sprite = _inspectorNormalSprite;
                    BackgroundImage.type = _inspectorImageType;
                    BackgroundImage.color = Color.white;
                }
                else
                {
                    FisherRuntimeUi.ApplyOptionalSprite(BackgroundImage, sprite);
                }

                return;
            }

            BackgroundImage.color = empty
                ? FisherRuntimeUi.SlotEmptyColor
                : dimmed
                    ? Color.Lerp(_inspectorNormalColor, Color.clear, 0.45f)
                    : selected
                        ? Color.Lerp(_inspectorNormalColor, Color.white, 0.18f)
                        : _inspectorNormalColor;
        }

        private Sprite ResolveStateSprite(bool selected, bool empty, Sprite fallbackSprite)
        {
            if (empty && EmptySprite != null)
            {
                return EmptySprite;
            }

            if (selected && SelectedSprite != null)
            {
                return SelectedSprite;
            }

            return NormalSprite != null ? NormalSprite : fallbackSprite;
        }

        private static Color StateColor(bool selected, bool dimmed, bool empty)
        {
            if (empty)
            {
                return FisherRuntimeUi.SlotEmptyColor;
            }

            if (dimmed)
            {
                return FisherRuntimeUi.SlotDimmedColor;
            }

            return selected ? FisherRuntimeUi.SlotSelectedColor : FisherRuntimeUi.SlotNormalColor;
        }

        private void EnsureInspectorChromeSnapshot()
        {
            if (_hasInspectorChromeSnapshot || BackgroundImage == null)
            {
                return;
            }

            _hasInspectorChromeSnapshot = true;
            _inspectorNormalColor = BackgroundImage.color;
            _inspectorNormalSprite = BackgroundImage.sprite;
            _inspectorImageType = BackgroundImage.type;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
