using System.Collections;
using JHS.Sound;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    [DisallowMultipleComponent]
    public sealed class FisherCurrencyEntryFeedback : MonoBehaviour
    {
        private const float GainScale = 1.08f;
        private const float ScaleInSeconds = 0.08f;
        private const float ScaleOutSeconds = 0.18f;
        private const float PopupRise = 34f;
        private const float SoundCooldownSeconds = 0.08f;
        private static readonly Color GainFlashColor = new Color(1f, 0.84f, 0.24f, 1f);
        private static float _lastSoundTime = -999f;

        private TextMeshProUGUI _amountText;
        private TextMeshProUGUI _gainText;
        private CanvasGroup _gainCanvasGroup;
        private Coroutine _routine;
        private Color _normalColor = Color.white;
        private bool _hasAmount;
        private long _lastAmount;

        public void Bind(TextMeshProUGUI amountText, Color normalColor)
        {
            _amountText = amountText;
            _normalColor = normalColor;
        }

        public void SetAmount(long amount, bool feedbackEnabled)
        {
            if (!feedbackEnabled)
            {
                return;
            }

            if (_hasAmount && amount > _lastAmount)
            {
                PlayGain(amount - _lastAmount);
            }

            _lastAmount = amount;
            _hasAmount = true;
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            transform.localScale = Vector3.one;
            if (_amountText != null)
            {
                _amountText.color = _normalColor;
            }

            if (_gainText != null)
            {
                _gainText.gameObject.SetActive(false);
            }
        }

        private void PlayGain(long delta)
        {
            if (!isActiveAndEnabled || delta <= 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastSoundTime >= SoundCooldownSeconds)
            {
                _lastSoundTime = now;
                HS_Sound.Play(HS_SoundId.CurrencyGain);
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(PlayGainRoutine(delta));
        }

        private IEnumerator PlayGainRoutine(long delta)
        {
            EnsureGainText();

            Vector3 baseScale = Vector3.one;
            Vector3 peakScale = baseScale * GainScale;
            Color baseColor = _amountText == null ? _normalColor : _amountText.color;
            float elapsed = 0f;

            if (_gainText != null)
            {
                _gainText.text = "+" + CompactNumberFormatter.Format(delta);
                _gainText.color = GainFlashColor;
                _gainText.gameObject.SetActive(true);
            }

            if (_gainCanvasGroup != null)
            {
                _gainCanvasGroup.alpha = 1f;
            }

            RectTransform gainRect = _gainText == null ? null : _gainText.rectTransform;
            if (gainRect != null)
            {
                gainRect.anchoredPosition = Vector2.zero;
            }

            while (elapsed < ScaleInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ScaleInSeconds);
                transform.localScale = Vector3.Lerp(baseScale, peakScale, t);
                if (_amountText != null)
                {
                    _amountText.color = Color.Lerp(baseColor, GainFlashColor, t);
                }

                if (gainRect != null)
                {
                    gainRect.anchoredPosition = Vector2.up * (PopupRise * 0.35f * t);
                }

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < ScaleOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ScaleOutSeconds);
                transform.localScale = Vector3.Lerp(peakScale, baseScale, t);
                if (_amountText != null)
                {
                    _amountText.color = Color.Lerp(GainFlashColor, _normalColor, t);
                }

                if (gainRect != null)
                {
                    gainRect.anchoredPosition = Vector2.up * Mathf.Lerp(PopupRise * 0.35f, PopupRise, t);
                }

                if (_gainCanvasGroup != null)
                {
                    _gainCanvasGroup.alpha = 1f - t;
                }

                yield return null;
            }

            transform.localScale = baseScale;
            if (_amountText != null)
            {
                _amountText.color = _normalColor;
            }

            if (_gainText != null)
            {
                _gainText.gameObject.SetActive(false);
            }

            _routine = null;
        }

        private void EnsureGainText()
        {
            if (_gainText != null)
            {
                return;
            }

            GameObject popup = new GameObject("GainText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup), typeof(LayoutElement));
            popup.layer = gameObject.layer;
            popup.transform.SetParent(transform, false);

            LayoutElement layout = popup.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;

            RectTransform rect = popup.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 34f);
            rect.anchoredPosition = Vector2.zero;

            _gainText = popup.GetComponent<TextMeshProUGUI>();
            _gainText.alignment = TextAlignmentOptions.MidlineRight;
            _gainText.enableAutoSizing = true;
            _gainText.fontSizeMin = 12f;
            _gainText.fontSizeMax = 20f;
            _gainText.fontStyle = FontStyles.Bold;
            _gainText.raycastTarget = false;
            _gainText.color = GainFlashColor;
            if (_amountText != null)
            {
                _gainText.font = _amountText.font;
                _gainText.fontSharedMaterial = _amountText.fontSharedMaterial;
            }

            _gainCanvasGroup = popup.GetComponent<CanvasGroup>();
            _gainCanvasGroup.blocksRaycasts = false;
            _gainCanvasGroup.interactable = false;
            popup.SetActive(false);
        }
    }
}
