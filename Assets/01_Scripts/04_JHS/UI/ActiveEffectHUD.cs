using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JHS.Equipment;

namespace JHS.Fishing
{
    // 메인화면 "효과 상태" HUD (연출 전담). 두 가지를 보여준다:
    //   ① 선박 액티브 스킬 — BoatActiveSkillController.GetStates()를 매 프레임 폴링해 쿨타임/발동중을 라디얼로.
    //      (배는 한 번에 1척만 장착 → 슬롯은 1벌, 장착 배에 따라 아이콘만 교체)
    //   ② 장비 다중포획   — AutoFishingController.OnMultiCatch를 구독해 "터질 때" 아이콘 번쩍 + xN 팝으로.
    // 로직(컨트롤러)은 이 클래스를 전혀 모른다 — 읽기/구독만 한다(연출 분리, BoatView·FishingView와 동일 사상, TP-10).
    //
    // 아트 없어도 골격으로 동작: 인스펙터에 아무것도 안 꽂으면 _logToConsole로 확인. 아이콘/라벨 꽂으면 자동 반영.
    public class ActiveEffectHUD : MonoBehaviour
    {
        // 배 스킬 → 아이콘 매핑. 장착 배가 바뀌면 그 스킬 아이콘으로 교체한다.
        [Serializable]
        public struct BoatSkillIcon
        {
            public BoatSkill skill;
            public Sprite icon;
        }

        [Header("① 선박 액티브 스킬 (슬롯 1벌 — 장착 배 스킬로 아이콘 교체)")]
        [SerializeField] private BoatActiveSkillController _boatController;  // 비우면 씬에서 자동 탐색
        [SerializeField] private GameObject _boatSkillRoot;    // 슬롯 루트 — 장착 배 없으면 통째로 숨김
        [SerializeField] private Image _boatSkillIcon;         // 스킬 아이콘 (장착 배 스킬로 교체)
        [SerializeField] private Image _boatCooldownFill;      // 쿨다운 오버레이 (Image Type=Filled, Fill Method=Radial360). fillAmount=남은쿨/총쿨
        [SerializeField] private GameObject _boatActiveGlow;   // 발동 중 켜지는 글로우/테두리 (지속스킬=발동 내내, 즉발스킬=아래 팝으로 잠깐)
        [SerializeField] private float _boatInstantPopSeconds = 0.6f;  // 즉발 스킬(펭귄) 발동 시 glow 번쩍 유지 시간
        [SerializeField] private BoatSkillIcon[] _boatSkillIcons;   // 스킬별 아이콘(진공/야근전등/펭귄)

        [Header("② 장비 다중포획 발동 피드백")]
        [SerializeField] private AutoFishingController _autoController;      // 비우면 씬에서 자동 탐색
        [SerializeField] private Image _multiCatchIcon;                     // 다중포획 아이콘 (평소 흐림 → 터지면 진하게)
        [SerializeField] private GameObject _multiCatchGlow;                // 발동 순간 켜지는 글로우
        [SerializeField] private TMP_Text _multiCatchCountLabel;           // "x3" — 발동 동안만 표시
        [SerializeField] private float _multiCatchPopSeconds = 0.6f;        // 번쩍 유지 시간
        [SerializeField] private Color _multiCatchIdleColor = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private Color _multiCatchActiveColor = Color.white;

        [Header("디버그")]
        [SerializeField] private bool _logToConsole = false;

        private float _multiCatchTimer;
        private float _boatInstantPopTimer;   // 즉발 배스킬(펭귄) glow 번쩍 잔여시간
        private BoatSkill _shownSkill;
        private bool _iconSet;

        private void OnEnable()
        {
            if (_boatController == null) _boatController = FindFirstObjectByType<BoatActiveSkillController>();
            if (_autoController == null) _autoController = FindFirstObjectByType<AutoFishingController>();

            if (_autoController != null) _autoController.OnMultiCatch += HandleMultiCatch;
            if (_boatController != null) _boatController.OnDecoyPenguin += HandleDecoyPenguin;

            _multiCatchTimer = 0f;
            _boatInstantPopTimer = 0f;
            SetMultiCatchVisual(0f);   // 시작은 흐림
        }

        private void OnDisable()
        {
            if (_autoController != null) _autoController.OnMultiCatch -= HandleMultiCatch;
            if (_boatController != null) _boatController.OnDecoyPenguin -= HandleDecoyPenguin;
        }

        private void Update()
        {
            UpdateBoatSlot();
            UpdateMultiCatchPop();
        }

        // ── ① 선박 쿨타임 폴링 (장착 배 1척의 스킬) ──────────────────────────
        private void UpdateBoatSlot()
        {
            // GetStates()는 장착된 배 1척의 스킬만 반환(보통 0~1개). 첫 항목을 쓴다.
            bool present = false;
            (BoatSkill skill, float cd, float cooldown, float active) cur = default;
            if (_boatController != null)
                foreach (var s in _boatController.GetStates()) { cur = s; present = true; break; }

            if (_boatSkillRoot != null) _boatSkillRoot.SetActive(present);   // 미장착이면 슬롯 숨김
            if (!present) return;

            // 장착 배가 바뀐 경우에만 아이콘 교체
            if (_boatSkillIcon != null && (!_iconSet || _shownSkill != cur.skill))
            {
                var sp = FindIcon(cur.skill);
                if (sp != null) _boatSkillIcon.sprite = sp;
                _shownSkill = cur.skill; _iconSet = true;
            }

            bool firing = cur.active > 0f;
            // glow: 지속스킬은 발동 내내(firing), 즉발스킬(펭귄)은 이벤트 팝 타이머 동안 — 둘을 OR로 합쳐
            // 매 프레임 폴링이 즉발 팝을 다음 프레임에 꺼버리지 않게 한다.
            if (_boatInstantPopTimer > 0f) _boatInstantPopTimer -= Time.deltaTime;
            bool glowing = firing || _boatInstantPopTimer > 0f;
            // glow/발동 동안엔 쿨다운 오버레이를 "표시만" 비운다(쿨타임 cd는 컨트롤러가 백그라운드로 계속 깎음).
            // → glow가 끝나면 그새 돌아간 쿨다운(cur.cd) 그대로 fill이 이어서 나타난다(풀 리셋 아님).
            if (_boatCooldownFill != null)
                _boatCooldownFill.fillAmount = (glowing || cur.cooldown <= 0f) ? 0f : Mathf.Clamp01(cur.cd / cur.cooldown);
            if (_boatActiveGlow != null) _boatActiveGlow.SetActive(glowing);
        }

        // 즉발 배스킬(유인용 로봇 펭귄) 발동 — 지속이 없어 polling으로는 못 잡으므로 이벤트로 glow를 잠깐 번쩍.
        private void HandleDecoyPenguin(float value)
        {
            if (_logToConsole) Debug.Log($"[ActiveEffectHUD] 펭귄 즉발 ▶ value={value}");
            _boatInstantPopTimer = _boatInstantPopSeconds;
        }

        private Sprite FindIcon(BoatSkill skill)
        {
            if (_boatSkillIcons != null)
                foreach (var e in _boatSkillIcons)
                    if (e.skill == skill) return e.icon;
            return null;
        }

        // ── ② 다중포획 발동 피드백 ───────────────────────────────────────────
        private void HandleMultiCatch(int count)
        {
            if (_logToConsole) Debug.Log($"[ActiveEffectHUD] 다중포획 발동 ▶ x{count}");
            _multiCatchTimer = _multiCatchPopSeconds;
            if (_multiCatchCountLabel != null) _multiCatchCountLabel.text = $"x{count}";
            SetMultiCatchVisual(1f);
        }

        private void UpdateMultiCatchPop()
        {
            if (_multiCatchTimer <= 0f) return;
            _multiCatchTimer -= Time.deltaTime;
            if (_multiCatchTimer <= 0f) SetMultiCatchVisual(0f);   // 복귀(흐림)
        }

        // t: 0 = 평소(흐림), 1 = 발동(진하게+글로우+xN). 보간으로 페이드 여지 남김.
        private void SetMultiCatchVisual(float t)
        {
            if (_multiCatchIcon != null) _multiCatchIcon.color = Color.Lerp(_multiCatchIdleColor, _multiCatchActiveColor, t);
            if (_multiCatchGlow != null) _multiCatchGlow.SetActive(t > 0f);
            if (_multiCatchCountLabel != null) _multiCatchCountLabel.enabled = t > 0f;
        }
    }
}
