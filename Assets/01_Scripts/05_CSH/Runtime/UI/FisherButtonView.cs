using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 패널 탭과 하단 액션 버튼이 공유하는 정적 버튼 View입니다.
    /// 버튼 GameObject는 씬에 고정해 두고, 어댑터는 라벨/선택 상태/클릭 리스너만 바꿉니다.
    /// </summary>
    public sealed class FisherButtonView : MonoBehaviour
    {
        /// <summary>실제 클릭 이벤트와 interactable 상태를 관리하는 Unity Button입니다.</summary>
        public Button Button;
        /// <summary>일반/선택/비활성 스프라이트와 색을 적용하는 버튼 배경입니다.</summary>
        public Image BackgroundImage;
        /// <summary>탭명 또는 액션명을 표시하는 버튼 라벨입니다.</summary>
        public TextMeshProUGUI LabelText;
        /// <summary>기본 상태에서 사용할 버튼 스프라이트입니다.</summary>
        public Sprite NormalSprite;
        /// <summary>선택된 탭 또는 강조 액션에 사용할 버튼 스프라이트입니다.</summary>
        public Sprite SelectedSprite;
        /// <summary>비활성 액션에 사용할 버튼 스프라이트입니다.</summary>
        public Sprite DisabledSprite;
        /// <summary>씬에서 직접 지정한 버튼 배경 색/스프라이트를 런타임 상태 갱신으로 덮지 않습니다.</summary>
        [NonSerialized] public bool PreserveInspectorChrome;
        [NonSerialized] private bool _hasInspectorChromeSnapshot;
        [NonSerialized] private Color _inspectorNormalColor;
        [NonSerialized] private Sprite _inspectorNormalSprite;
        [NonSerialized] private Image.Type _inspectorImageType;

        /// <summary>
        /// 버튼 표시 상태와 클릭 동작을 한 번에 갱신합니다.
        /// 이전 리스너를 제거한 뒤 새 리스너만 연결하므로 Refresh 반복 시 중복 클릭이 누적되지 않습니다.
        /// </summary>
        public void Bind(string label, bool selected, bool interactable, Action onClick)
        {
            if (LabelText != null)
            {
                LabelText.text = label ?? string.Empty;
            }

            if (BackgroundImage != null && PreserveInspectorChrome)
            {
                ApplyPreservedChromeState(selected, interactable);
            }
            else if (BackgroundImage != null)
            {
                Sprite sprite = !interactable && DisabledSprite != null
                    ? DisabledSprite
                    : selected && SelectedSprite != null
                        ? SelectedSprite
                        : NormalSprite;
                if (!FisherRuntimeUi.ApplyOptionalSprite(BackgroundImage, sprite))
                {
                    BackgroundImage.color = StateColor(selected, interactable);
                }
            }

            if (Button == null)
            {
                return;
            }

            Button.interactable = interactable;
            Button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                Button.onClick.AddListener(() => onClick());
            }
        }

        private void ApplyPreservedChromeState(bool selected, bool interactable)
        {
            EnsureInspectorChromeSnapshot();
            if (BackgroundImage == null)
            {
                return;
            }

            Sprite sprite = !interactable && DisabledSprite != null
                ? DisabledSprite
                : selected && SelectedSprite != null
                    ? SelectedSprite
                    : NormalSprite != null
                        ? NormalSprite
                        : _inspectorNormalSprite;
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

            if (!interactable)
            {
                BackgroundImage.color = Color.Lerp(_inspectorNormalColor, Color.black, 0.35f);
                return;
            }

            BackgroundImage.color = selected
                ? Color.Lerp(_inspectorNormalColor, Color.white, 0.18f)
                : _inspectorNormalColor;
        }

        private static Color StateColor(bool selected, bool interactable)
        {
            return selected
                ? FisherRuntimeUi.ButtonSelectedColor
                : interactable
                    ? FisherRuntimeUi.ButtonColor
                    : FisherRuntimeUi.ButtonDisabledColor;
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
    }
}
