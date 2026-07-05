using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// Fisher 런타임 UI에 적용할 패널, 버튼, 슬롯, 텍스트, 아이템 아이콘 스킨 계약입니다.
    /// 팀장은 이 ScriptableObject 하나에 공통 스프라이트와 색/폰트, itemId별 아이콘을 연결하면 됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "FisherUiArtProfile", menuName = "Fisher/UI Art Profile")]
    public sealed class FisherUiArtProfile : ScriptableObject
    {
        public const string DefaultResourcePath = "05_CSH/UI/FisherUiArtProfile";
        public const string FallbackResourcePath = "FisherUiArtProfile";

        #region Inspector Sprites

        [Header("Panel Sprites")]
        [SerializeField] private Sprite _panelBackground;
        [SerializeField] private Sprite _sectionBackground;
        [SerializeField] private Sprite _detailBackground;

        [Header("Button Sprites")]
        [SerializeField] private Sprite _buttonNormal;
        [SerializeField] private Sprite _buttonSelected;
        [SerializeField] private Sprite _buttonDisabled;

        [Header("Slot Sprites")]
        [SerializeField] private Sprite _slotNormal;
        [SerializeField] private Sprite _slotSelected;
        [SerializeField] private Sprite _slotEmpty;
        [SerializeField] private Sprite _iconFrame;

        [Header("Item Icon Bindings")]
        [SerializeField] private List<FisherItemIconBinding> _itemIcons = new List<FisherItemIconBinding>();

        #endregion

        #region Inspector Style

        [Header("Text")]
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private Material _fontMaterial;
        [SerializeField] private Color _textPrimary = Color.white;
        [SerializeField] private Color _textMuted = new Color(0.78f, 0.86f, 0.9f, 1f);

        [Header("Fallback Colors")]
        [SerializeField] private Color _panelColor = new Color(0.08f, 0.1f, 0.12f, 0.96f);
        [SerializeField] private Color _sectionColor = new Color(0.06f, 0.08f, 0.09f, 0.5f);
        [SerializeField] private Color _detailColor = new Color(0.12f, 0.18f, 0.22f, 0.92f);
        [SerializeField] private Color _inputColor = new Color(0.08f, 0.13f, 0.16f, 1f);
        [SerializeField] private Color _iconFrameColor = new Color(0.16f, 0.23f, 0.27f, 1f);

        [Header("Button Colors")]
        [SerializeField] private Color _buttonNormalColor = new Color(0.2f, 0.32f, 0.42f, 1f);
        [SerializeField] private Color _buttonSelectedColor = new Color(0.36f, 0.52f, 0.64f, 1f);
        [SerializeField] private Color _buttonDisabledColor = new Color(0.28f, 0.28f, 0.28f, 1f);

        [Header("Slot Colors")]
        [SerializeField] private Color _slotNormalColor = new Color(0.12f, 0.18f, 0.2f, 0.95f);
        [SerializeField] private Color _slotSelectedColor = new Color(0.36f, 0.52f, 0.64f, 0.9f);
        [SerializeField] private Color _slotEmptyColor = new Color(0.12f, 0.18f, 0.2f, 0.88f);
        [SerializeField] private Color _slotDimmedColor = new Color(0.08f, 0.1f, 0.11f, 0.35f);
        [SerializeField] private Color _selectedFrameColor = new Color(0.55f, 0.78f, 1f, 0.55f);

        [Header("Badge Colors")]
        [SerializeField] private Color _newBadgeColor = new Color(0.82f, 0.16f, 0.1f, 1f);
        [SerializeField] private Color _lockedBadgeColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color _outlineColor = new Color(0f, 0f, 0f, 0.95f);

        #endregion

        #region Sprite Accessors

        public Sprite PanelBackground => _panelBackground;
        public Sprite SectionBackground => _sectionBackground;
        public Sprite DetailBackground => _detailBackground;
        public Sprite ButtonNormal => _buttonNormal;
        public Sprite ButtonSelected => _buttonSelected;
        public Sprite ButtonDisabled => _buttonDisabled;
        public Sprite SlotNormal => _slotNormal;
        public Sprite SlotSelected => _slotSelected;
        public Sprite SlotEmpty => _slotEmpty;
        public Sprite IconFrame => _iconFrame;

        #endregion

        #region Style Accessors

        public TMP_FontAsset FontAsset => _fontAsset;
        public Material FontMaterial => _fontMaterial;
        public Color TextPrimary => _textPrimary;
        public Color TextMuted => _textMuted;
        public Color PanelColor => _panelColor;
        public Color SectionColor => _sectionColor;
        public Color DetailColor => _detailColor;
        public Color InputColor => _inputColor;
        public Color IconFrameColor => _iconFrameColor;
        public Color ButtonNormalColor => _buttonNormalColor;
        public Color ButtonSelectedColor => _buttonSelectedColor;
        public Color ButtonDisabledColor => _buttonDisabledColor;
        public Color SlotNormalColor => _slotNormalColor;
        public Color SlotSelectedColor => _slotSelectedColor;
        public Color SlotEmptyColor => _slotEmptyColor;
        public Color SlotDimmedColor => _slotDimmedColor;
        public Color SelectedFrameColor => _selectedFrameColor;
        public Color NewBadgeColor => _newBadgeColor;
        public Color LockedBadgeColor => _lockedBadgeColor;
        public Color OutlineColor => _outlineColor;

        #endregion

        #region Resource Lookup

        public static FisherUiArtProfile LoadFromResources(string resourcePath = null)
        {
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                FisherUiArtProfile explicitProfile = Resources.Load<FisherUiArtProfile>(resourcePath);
                if (explicitProfile != null)
                {
                    return explicitProfile;
                }
            }

            FisherUiArtProfile profile = Resources.Load<FisherUiArtProfile>(DefaultResourcePath);
            return profile != null ? profile : Resources.Load<FisherUiArtProfile>(FallbackResourcePath);
        }

        #endregion

        #region Icon Lookup

        /// <summary>
        /// itemId와 정확히 일치하는 UI 아이콘 스프라이트를 찾습니다.
        /// 이 값이 없으면 런타임은 RMS FishData, CSH ItemData, CSH RecipeData의 Icon 필드를 우선 조회합니다.
        /// </summary>
        public Sprite FindItemIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            for (int i = 0; i < _itemIcons.Count; i++)
            {
                FisherItemIconBinding binding = _itemIcons[i];
                if (binding != null && binding.Matches(itemId))
                {
                    return binding.Sprite;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// 특정 itemId와 스프라이트를 1:1로 연결하는 아트 바인딩입니다.
    /// </summary>
    [Serializable]
    public sealed class FisherItemIconBinding
    {
        [SerializeField] private string _itemId;
        [SerializeField] private Sprite _sprite;

        /// <summary>Inspector에 연결된 실제 아이콘 스프라이트입니다.</summary>
        public Sprite Sprite => _sprite;

        /// <summary>
        /// 전달된 itemId가 이 바인딩의 itemId와 정확히 같은지 확인합니다.
        /// </summary>
        public bool Matches(string itemId)
        {
            return !string.IsNullOrEmpty(_itemId) &&
                   string.Equals(_itemId, itemId, StringComparison.Ordinal);
        }
    }

}
