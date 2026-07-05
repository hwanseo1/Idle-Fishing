using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using JHS.Sound;   // HS_Sound 효과음 파사드

namespace JHS.Equipment.UI
{
    // 강화 성공/실패 피드백 연출 전담 presenter (파티클 + 레벨 강조).
    // 매니저/스크린 로직은 전혀 모른다 — Play(success) 만 받아 연출한다. (SRP)
    //  - 성공: ①성공 파티클 ②[A] 레벨 텍스트 펀치+금빛 플래시 ③[B] "+1" 팝업 떠오름
    //  - 실패: ①실패 파티클 + 골드텍스트 흔들기 ②[A] 레벨 텍스트 펀치+빨강 플래시 ③[B] "실패!" 팝업 떠오름
    // A/B는 성공·실패 공통 토글(_levelPunchEnabled / _levelPopupEnabled)로 즉시 끌 수 있다(상대 연출 유지).
    //
    // ※ uGUI(Overlay) 파티클은 각 ParticleSystem 오브젝트의 UIParticle(com.coffee.ui-particle)가 렌더한다.
    //   이 스크립트는 패키지 비의존 — 엔진 ParticleSystem만 제어(Play → UIParticle가 렌더).
    [DisallowMultipleComponent]
    public class UpgradeFeedbackBurst : MonoBehaviour
    {
        [Header("파티클 (각 오브젝트에 UIParticle 동반)")]
        [Tooltip("idle 루프로 항상 켜두고, 발동 때 Emit으로 버스트를 쌓는다(연타해도 끊김 없음).")]
        [SerializeField] private ParticleSystem _successFx;
        [SerializeField] private ParticleSystem _failFx;
        [SerializeField] private int _successBurst = 110;   // 성공 한 번에 뿜을 파티클 수
        [SerializeField] private int _failBurst = 70;       // 실패 한 번에 뿜을 파티클 수

        [Header("실패 시 흔들 대상 (보유 재화 텍스트의 RectTransform — 부족한 쪽만 흔듦)")]
        [Tooltip("골드 부족 시 흔들 — 보통 우상단 보유 골드 텍스트")]
        [SerializeField] private RectTransform _goldShakeTarget;
        [Tooltip("재료 부족 시 흔들 — 보통 우상단 보유 재료 텍스트")]
        [SerializeField] private RectTransform _materialShakeTarget;
        [SerializeField] private float _shakeStrength = 18f;
        [SerializeField] private float _shakeDuration = 0.4f;

        [Header("[A] 레벨 텍스트 펀치 + 색 플래시 (성공=금빛 / 실패=빨강)")]
        [Tooltip("끄면 A 연출만 비활성(성공·실패 둘 다). B는 유지.")]
        [SerializeField] private bool _levelPunchEnabled = true;
        [SerializeField] private TMP_Text _levelText;                 // 강화창 'Lv.X » Lv.Y'
        [SerializeField] private float _levelPunchScale = 1.3f;
        [SerializeField] private Color _levelFlashColor = new Color(1f, 0.85f, 0.2f);    // 성공 금빛
        [SerializeField] private Color _levelFailFlashColor = new Color(0.9f, 0.2f, 0.2f); // 실패 빨강

        [Header("[B] 레벨 팝업 (성공='+1' / 실패='실패!', 위로 떠오르며 사라짐)")]
        [Tooltip("끄면 B 연출만 비활성(성공·실패 둘 다). A는 유지.")]
        [SerializeField] private bool _levelPopupEnabled = true;
        [SerializeField] private RectTransform _levelUpPopup;         // 성공 '+1' (평소 비활성, CanvasGroup 권장)
        [SerializeField] private RectTransform _levelFailPopup;       // 실패 '실패!' (평소 비활성, CanvasGroup 권장)
        [SerializeField] private float _popupRise = 60f;
        [SerializeField] private float _popupDuration = 0.7f;

        private readonly Dictionary<RectTransform, Vector2> _shakeBase = new Dictionary<RectTransform, Vector2>();
        private Color _levelBaseColor; private bool _levelBaseCaptured;
        private readonly Dictionary<RectTransform, Vector2> _popupBase = new Dictionary<RectTransform, Vector2>();

        // 레거시/사유 미상 진입점 — 성공/실패만 알고 부족 사유를 모를 때.
        public void Play(bool success)
        {
            if (success) PlaySuccess();
            else PlayFail();
        }

        public void PlaySuccess()
        {
            // 강화 성공/실패 효과음 (장비·배 공통 깔때기). 클립 미할당이면 무음.
            HS_Sound.Play(HS_SoundId.UpgradeSuccess);
            Burst(_successFx, _successBurst);
            if (_levelPunchEnabled) LevelPunch(_levelFlashColor);   // [A] 금빛
            if (_levelPopupEnabled) LevelPopup(_levelUpPopup);      // [B] "+1"
        }

        // 사유 미상 실패 — 양쪽(골드·재료) 다 흔들어 최소 피드백 보장.
        public void PlayFail() => PlayFail(false, false);

        // 부족 사유별 실패 연출 — 부족한 쪽만 흔든다. 둘 다 false(사유 미상)면 양쪽 다.
        public void PlayFail(bool lackGold, bool lackMaterial)
        {
            HS_Sound.Play(HS_SoundId.UpgradeFail);
            Burst(_failFx, _failBurst);

            bool noReason = !lackGold && !lackMaterial;
            if (lackGold || noReason) Shake(_goldShakeTarget);
            if (lackMaterial || noReason) Shake(_materialShakeTarget);

            if (_levelPunchEnabled) LevelPunch(_levelFailFlashColor); // [A] 빨강
            if (_levelPopupEnabled) LevelPopup(_levelFailPopup);      // [B] "실패!"
        }

        // 연타 안전: 직전 파티클을 지우지 않고 Emit으로 버스트를 위에 쌓는다.
        // 시스템은 idle 루프(rate 0·burst 0)로 항상 재생 중 → Emit한 파티클을 UIParticle가 곧바로 렌더.
        private void Burst(ParticleSystem fx, int count)
        {
            if (fx == null) return;
            if (!fx.isPlaying) fx.Play();   // idle 루프 보장(꺼져 있으면 켬)
            fx.Emit(count);
        }

        private void Shake(RectTransform target)
        {
            if (target == null) return;
            if (!_shakeBase.ContainsKey(target)) _shakeBase[target] = target.anchoredPosition;
            var basePos = _shakeBase[target];

            target.DOKill();
            target.anchoredPosition = basePos;
            target
                .DOShakeAnchorPos(_shakeDuration, new Vector2(_shakeStrength, 0f), 14, 90, false, true)
                .SetLink(target.gameObject)
                .OnComplete(() => { if (target != null) target.anchoredPosition = basePos; });
        }

        // [A] 레벨 숫자 펀치(커졌다 작아짐) + 색 플래시(원색→flash→원색).
        private void LevelPunch(Color flash)
        {
            if (_levelText == null) return;
            var rt = _levelText.rectTransform;
            if (!_levelBaseCaptured) { _levelBaseColor = _levelText.color; _levelBaseCaptured = true; }

            rt.DOKill();
            DOTween.Kill(_levelText);
            rt.localScale = Vector3.one;
            _levelText.color = _levelBaseColor;

            DOTween.Sequence().SetLink(_levelText.gameObject)
                .Append(rt.DOScale(_levelPunchScale, 0.12f).SetEase(Ease.OutBack))
                .Append(rt.DOScale(1f, 0.18f).SetEase(Ease.InOutSine));

            DOTween.To(() => _levelText.color, c => _levelText.color = c, flash, 0.1f)
                .SetTarget(_levelText).SetLink(_levelText.gameObject)
                .OnComplete(() =>
                {
                    if (_levelText == null) return;
                    DOTween.To(() => _levelText.color, c => _levelText.color = c, _levelBaseColor, 0.35f)
                        .SetTarget(_levelText).SetLink(_levelText.gameObject);
                });
        }

        // [B] 팝업이 현재 위치에서 위로 떠오르며 페이드아웃 후 비활성. (성공/실패 팝업 공용)
        private void LevelPopup(RectTransform popup)
        {
            if (popup == null) return;
            if (!_popupBase.ContainsKey(popup)) _popupBase[popup] = popup.anchoredPosition;
            var basePos = _popupBase[popup];

            popup.DOKill();
            popup.gameObject.SetActive(true);
            popup.anchoredPosition = basePos;
            SetPopupAlpha(popup, 1f);

            var seq = DOTween.Sequence().SetLink(popup.gameObject);
            seq.Append(popup.DOAnchorPosY(basePos.y + _popupRise, _popupDuration).SetEase(Ease.OutCubic));
            seq.Join(FadePopup(popup, 0f, _popupDuration));
            seq.OnComplete(() =>
            {
                if (popup == null) return;
                popup.gameObject.SetActive(false);
                popup.anchoredPosition = basePos;
            });
        }

        private Tween FadePopup(RectTransform popup, float to, float dur)
        {
            var cg = popup.GetComponent<CanvasGroup>();
            if (cg != null) return cg.DOFade(to, dur).SetEase(Ease.InSine);
            var gr = popup.GetComponent<UnityEngine.UI.Graphic>();
            if (gr != null)
                return DOTween.To(() => gr.color.a, a => { var c = gr.color; c.a = a; gr.color = c; }, to, dur)
                    .SetTarget(popup).SetEase(Ease.InSine);
            return DOTween.To(() => 0f, _ => { }, to, dur);
        }

        private static void SetPopupAlpha(RectTransform popup, float a)
        {
            var cg = popup.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = a; return; }
            var gr = popup.GetComponent<UnityEngine.UI.Graphic>();
            if (gr != null) { var c = gr.color; c.a = a; gr.color = c; }
        }
    }
}
