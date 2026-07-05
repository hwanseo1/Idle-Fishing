using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JHS.Sound;   // 배 해금/장착 효과음 (유저 액션 지점에서만 재생)

namespace JHS.Equipment.UI
{
    // 배 화면 — 장비 화면(EquipmentScreen)과 같은 구조의 정적 바인더(총효과 없음).
    // 배는 "스탯 여러 개"가 아니라 스킬당 값 1개(확률/계수)라 효과 표시가 1줄이다.
    // 레이아웃/위치는 전부 에디터 손배치, 이 스크립트는 표시값 갱신과 클릭만. (SRP)
    //
    // 슬롯(배 스킬) 중 1개 선택 → 그 스킬의 강화창 하나만 갱신. 잠긴 스킬 슬롯은 자동 숨김.
    public sealed class BoatScreen : MonoBehaviour
    {
        [Header("매니저 (비우면 Instance 사용)")]
        [SerializeField] private EquipmentManager _manager;

        [Header("재화 (우상단, 선택)")]
        [SerializeField] private TMP_Text _goldText;

        [Header("강화재료 보유 (우상단, 선택)")]
        [SerializeField] private TMP_Text _materialOwnedText;                    // 강화재료 보유 수량 표시
        [SerializeField] private string _ownedMaterialId = "mat_upgrade_common"; // 표시할 재료 ID

        [Header("슬롯 (배 스킬들)")]
        [SerializeField] private SlotView[] _slots;

        [Header("강화창 — 선택된 스킬")]
        [SerializeField] private Image _selectedIcon;     // 왼쪽 프리뷰 / 강화창 아이콘
        [SerializeField] private TMP_Text _nameText;      // 스킬 이름
        [SerializeField] private TMP_Text _descText;      // 설명 (선택)
        [SerializeField] private TMP_Text _levelText;     // "Lv.X » Lv.Y" (max면 "Lv.X")
        [SerializeField] private Button _upgradeButton;   // 강화 버튼
        [SerializeField] private TMP_Text _upgradeCostText; // 차감 골드 (max면 "MAX") — 버튼 위 비용. 비워두면 미사용

        [Header("필요 재료 패널 (강화 비용 표시 — 골드/재료 1종 기준)")]
        [SerializeField] private TMP_Text _reqGoldText;     // 필요 골드 숫자 (단위 '골드'는 정적/스프라이트)
        [SerializeField] private TMP_Text _reqMaterialText; // 필요 재료 수량 숫자 (단위 '개'는 정적/스프라이트)
        [Tooltip("보유량이 필요량보다 부족할 때 칠할 색 (부족한 쪽만). 충분/MAX면 원색 복원")]
        [SerializeField] private Color _reqLackColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("강화 성공/실패 피드백 (선택 — 연출 전담)")]
        [SerializeField] private UpgradeFeedbackBurst _feedback;

        [Header("장착 (배 3척 중 1척)")]
        [SerializeField] private Button _equipButton;       // 선택한 배 장착 버튼 (선택)
        [SerializeField] private TMP_Text _equipButtonText; // "장착" / "장착 중" (선택)
        [SerializeField] private string _equipLabel = "장착";
        [SerializeField] private string _equippedLabel = "장착 중";

        [Header("구매/해금 (장착 버튼 옆, 잠기면 비활성)")]
        [SerializeField] private Button _unlockButton;       // 배 구매(골드 해금) 버튼 — 장착 버튼 옆에 배치
        [SerializeField] private TMP_Text _unlockCostText;   // 구매 골드 비용 (선택)
        [SerializeField] private GameObject _unlockCostIcon; // 구매 비용 골드 아이콘 (해금되면 숨김, 선택)

        [Header("효과 표시 (값 1줄)")]
        [SerializeField] private string _effectLabel = "계수";   // 효과 이름 (예: 계수/확률)
        [SerializeField] private bool _effectAsPercent;          // true면 값×100 + "%"
        [SerializeField] private string _effectSuffix;           // asPercent 아닐 때 붙일 단위
        [SerializeField] private Image _effectIcon;              // 효과 라벨 왼쪽 작은 아이콘 (선택 스킬 따라감, 없으면 숨김)
        [SerializeField] private TMP_Text _effectLabelText;      // "계수" (선택, _effectLabel로 채움)
        [SerializeField] private TMP_Text _effectCurrentText;    // 현재값
        [SerializeField] private TMP_Text _effectNextText;       // 다음값 (max면 비움)
        [SerializeField] private GameObject _effectArrow;        // " » " (선택, max면 숨김)

        private BoatSkill _selected;
        private bool _hasSelection;

        // 필요 골드/재료 텍스트의 원색 캐시 (부족→빨강 후 원색 복원용). 첫 갱신 때 1회 캡처.
        private Color _reqGoldBaseColor; private bool _reqGoldBaseCaptured;
        private Color _reqMatBaseColor;  private bool _reqMatBaseCaptured;

        private EquipmentManager Manager => _manager != null ? _manager : (_manager = EquipmentManager.Instance);

        [Serializable]
        public sealed class SlotView
        {
            public BoatSkill skill;             // 이 슬롯이 가리키는 배 스킬
            public Button button;               // 슬롯 클릭 버튼
            public TMP_Text levelText;          // "Lv.X" (선택)
            public GameObject selectedHighlight; // 선택(보고 있는) 시 켜질 테두리 (선택)
            public GameObject equippedBadge;     // 현재 장착된 배에 켜질 표식 (선택, selected와 별개)
            public GameObject lockedBadge;       // 미해금 배에 켜질 자물쇠 표식 (선택)
            public Sprite icon;                 // 강화창 왼쪽에 띄울 이 배 스킬의 스프라이트 (인스펙터 배정, 선택)
            public Sprite effectIcon;           // 효과 라벨 왼쪽에 띄울 작은 아이콘 (선택)
        }

        // ── 생명주기 ─────────────────────────────────────────
        private void Awake()
        {
            if (_slots != null)
                foreach (var slot in _slots)
                {
                    if (slot?.button == null) continue;
                    var captured = slot.skill;
                    slot.button.onClick.AddListener(() => Select(captured));
                }

            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);

            if (_equipButton != null)
                _equipButton.onClick.AddListener(OnEquipClicked);

            if (_unlockButton != null)
                _unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        private void OnEnable()
        {
            if (Manager != null)
            {
                Manager.OnBoatSkillUpgraded += OnBoatSkillUpgraded;
                Manager.OnBoatEquipped += OnBoatEquipped;
                Manager.OnBoatUnlocked += OnBoatUnlocked;
            }

            // 기본 선택: 장착된 배 → 첫 해금된 배 → 첫 슬롯 (기본 해금 배가 강화창에 바로 뜨도록)
            if (!_hasSelection && _slots != null && _slots.Length > 0)
            {
                _selected = DefaultSkill();
                _hasSelection = true;
            }

            // 매니저 준비 전(씬 로드 타이밍)이어도 선택된 배 아이콘은 인스펙터 슬롯에서 즉시 표시
            ApplySelectedIcon();

            RefreshAll();

            // 로그인 직후 서버 골드 캐시(PlayFabDataStore)가 스테일이면 옛 골드가 보일 수 있어,
            // 화면 열 때 서버 잔액을 강제 동기화한 뒤 골드 라벨을 다시 갱신한다.
            if (Manager != null)
                Manager.RequestRefreshGold(() => { if (this != null) RefreshCurrency(); });
        }

        private void OnDisable()
        {
            if (Manager != null)
            {
                Manager.OnBoatSkillUpgraded -= OnBoatSkillUpgraded;
                Manager.OnBoatEquipped -= OnBoatEquipped;
                Manager.OnBoatUnlocked -= OnBoatUnlocked;
            }
        }

        private void OnBoatSkillUpgraded(BoatSkill skill, int level) => RefreshAll();
        private void OnBoatEquipped(BoatSkill skill) => RefreshAll();
        private void OnBoatUnlocked(BoatSkill skill) => RefreshAll();

        // ── 선택 / 강화 ──────────────────────────────────────
        public void Select(BoatSkill skill)
        {
            _selected = skill;
            _hasSelection = true;
            RefreshAll();
        }

        // 비관적 UX: 서버 왕복 동안 해당 버튼 잠금(중복클릭 가드) → 콜백에서 RefreshAll로 상태/골드 복구.
        // (서버 미연결이면 Manager가 즉시 로컬 폴백)
        private void OnUpgradeClicked()
        {
            if (Manager == null || !_hasSelection) return;

            // 배 스킬 강화도 실패=재화 부족이 유일 원인 → 요청 직전 보유량으로 부족 사유를 캡처(부족한 쪽만 흔들기).
            bool lackGold = Manager.Gold < Manager.GetBoatSkillUpgradeCost(_selected);
            bool lackMat  = !HasEnoughMaterials(_selected);

            if (_upgradeButton != null) _upgradeButton.interactable = false;
            Manager.RequestUpgradeBoatSkill(_selected, ok =>
            {
                RefreshAll();
                if (_feedback == null) return;
                if (ok) _feedback.PlaySuccess();
                else    _feedback.PlayFail(lackGold, lackMat);   // 부족한 쪽(골드/재료) 흔들기
            });
        }

        // 선택 배 스킬의 강화 재료를 보유량이 모두 충족하는지(클라 사전검사 — 실패 사유 판정용).
        private bool HasEnoughMaterials(BoatSkill skill)
        {
            var mats = Manager.GetBoatSkillUpgradeMaterials(skill);
            if (mats == null) return true;
            foreach (var m in mats)
                if (Manager.GetMaterialCount(m.materialId) < m.count) return false;
            return true;
        }

        private void OnEquipClicked()
        {
            if (Manager == null || !_hasSelection) return;
            if (_equipButton != null) _equipButton.interactable = false;
            // 유저가 직접 장착한 경우에만 사운드(성공 시). 로그인 로드-동기화 발행과 구분된다.
            Manager.RequestEquipBoat(_selected, ok => { RefreshAll(); if (ok) HS_Sound.Play(HS_SoundId.BoatEquip); });
        }

        private void OnUnlockClicked()
        {
            if (Manager == null || !_hasSelection) return;
            if (_unlockButton != null) _unlockButton.interactable = false;
            Manager.RequestUnlockBoat(_selected, ok => { RefreshAll(); if (ok) HS_Sound.Play(HS_SoundId.BoatUnlock); });
        }

        // ── 갱신 ─────────────────────────────────────────────
        private void RefreshAll()
        {
            if (Manager == null) return;
            RefreshCurrency();
            RefreshSlots();
            RefreshDetail();
        }

        private void RefreshCurrency()
        {
            if (_goldText != null) _goldText.text = $"{Manager.Gold:N0}";
            if (_materialOwnedText != null)
                _materialOwnedText.text = $"{Manager.GetMaterialCount(_ownedMaterialId):N0}";
        }

        private void RefreshSlots()
        {
            if (_slots == null) return;
            bool hasEquip = Manager.HasEquippedBoat;
            BoatSkill equipped = Manager.EquippedBoat;
            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                if (slot.levelText != null) slot.levelText.text = $"Lv.{Manager.GetBoatSkillLevel(slot.skill) + 1}";
                if (slot.selectedHighlight != null)
                    slot.selectedHighlight.SetActive(_hasSelection && slot.skill == _selected);
                if (slot.equippedBadge != null)
                    slot.equippedBadge.SetActive(hasEquip && slot.skill == equipped);
                if (slot.lockedBadge != null)
                    slot.lockedBadge.SetActive(!Manager.IsBoatUnlocked(slot.skill));
            }
        }

        private void RefreshDetail()
        {
            if (!_hasSelection) return;
            var skill = _selected;
            bool max = Manager.IsBoatSkillMax(skill);
            int level = Manager.GetBoatSkillLevel(skill);

            ApplySelectedIcon();
            if (_nameText != null) _nameText.text = Manager.GetBoatSkillName(skill);
            if (_descText != null) _descText.text = Manager.GetBoatSkillDescription(skill);
            if (_levelText != null)
                _levelText.text = max ? "MAX" : $"Lv.{level + 1} » Lv.{level + 2}";   // 표시는 1-based

            if (_effectIcon != null)
            {
                var sp = EffectIconForSlot(skill);
                _effectIcon.sprite = sp;
                _effectIcon.enabled = sp != null;   // 효과 아이콘 미배정이면 숨김
            }
            if (_effectLabelText != null) _effectLabelText.text = _effectLabel;
            if (_effectCurrentText != null)
                _effectCurrentText.text = FormatEffect(skill, Manager.GetBoatSkillValueAt(skill, level));
            if (_effectNextText != null)
                _effectNextText.text = max ? "" : FormatEffect(skill, Manager.GetBoatSkillValueAt(skill, level + 1));
            if (_effectArrow != null) _effectArrow.SetActive(!max);

            bool unlocked = Manager.IsBoatUnlocked(skill);

            if (_upgradeCostText != null)
                _upgradeCostText.text = max ? "MAX" : $"{Manager.GetBoatSkillUpgradeCost(skill):N0}";
            if (_upgradeButton != null) _upgradeButton.interactable = unlocked && !max;   // 잠긴 배는 강화 불가

            RefreshRequirements(skill, max);

            // 장착 버튼: 잠겨있으면 불가, 이미 장착한 배면 비활성("장착 중"), 아니면 활성("장착")
            bool isEquipped = Manager.HasEquippedBoat && Manager.EquippedBoat == skill;
            if (_equipButton != null) _equipButton.interactable = unlocked && !isEquipped;
            if (_equipButtonText != null) _equipButtonText.text = isEquipped ? _equippedLabel : _equipLabel;

            // 구매(해금) 버튼: 장착 버튼과 항상 나란히 노출. 잠긴 배만(골드 충분 시) 누를 수 있고, 해금되면 잠김.
            // → 기본 배(해금됨): 장착 활성·구매 잠김 / 잠긴 배: 장착 잠김·구매 활성 / 구매 완료: 구매 잠김·장착 활성
            if (_unlockButton != null)
                _unlockButton.interactable = !unlocked && Manager.CanUnlockBoat(skill);   // 잠김 + 골드 충분일 때만 활성
            if (_unlockCostText != null)
                _unlockCostText.text = unlocked ? "" : $"{Manager.GetBoatUnlockCost(skill):N0}";
            if (_unlockCostIcon != null) _unlockCostIcon.SetActive(!unlocked);   // 해금/완료된 배는 골드 아이콘 숨김
        }

        // 필요재료 패널: 강화 비용(골드 + 재료 1종) 표시. (실제 차감은 서버 권위 — 여긴 표시용)
        private void RefreshRequirements(BoatSkill skill, bool max)
        {
            if (_reqGoldText != null)
            {
                if (!_reqGoldBaseCaptured) { _reqGoldBaseColor = _reqGoldText.color; _reqGoldBaseCaptured = true; }
                _reqGoldText.text = max ? "MAX" : $"{Manager.GetBoatSkillUpgradeCost(skill):N0}";
                // 보유 골드가 필요 골드보다 적으면 빨강(부족), 아니면 원색. (MAX는 부족 아님)
                bool lackGold = !max && Manager.Gold < Manager.GetBoatSkillUpgradeCost(skill);
                _reqGoldText.color = lackGold ? _reqLackColor : _reqGoldBaseColor;
            }

            if (_reqMaterialText != null)
            {
                if (!_reqMatBaseCaptured) { _reqMatBaseColor = _reqMaterialText.color; _reqMatBaseCaptured = true; }
                if (max)
                {
                    _reqMaterialText.text = "MAX";
                    _reqMaterialText.color = _reqMatBaseColor;
                }
                else
                {
                    var mats = Manager.GetBoatSkillUpgradeMaterials(skill);
                    _reqMaterialText.text = (mats == null || mats.Count == 0) ? "-" : $"{mats[0].count:N0}";   // 1종 기준 (여러 종은 추후 행 확장)
                    // 보유 재료가 필요 재료보다 적으면 빨강(부족), 아니면 원색.
                    _reqMaterialText.color = HasEnoughMaterials(skill) ? _reqMatBaseColor : _reqLackColor;
                }
            }
        }

        // 기본 선택 대상: 현재 장착된 배 → 첫 번째 해금된 배 → (매니저 미준비면) 첫 슬롯.
        private BoatSkill DefaultSkill()
        {
            if (Manager != null && _slots != null)
            {
                if (Manager.HasEquippedBoat) return Manager.EquippedBoat;
                foreach (var slot in _slots)
                    if (slot != null && Manager.IsBoatUnlocked(slot.skill)) return slot.skill;
            }
            return _slots[0].skill;
        }

        // 강화창 왼쪽 아이콘 적용. 인스펙터 슬롯 스프라이트 우선, 없으면 데이터(SO) 폴백.
        // (Manager 없이도 인스펙터 슬롯만으로 그려져, 씬 로드 직후 흰색으로 남지 않음)
        private void ApplySelectedIcon()
        {
            if (_selectedIcon == null || !_hasSelection) return;
            _selectedIcon.sprite = IconForSlot(_selected) ?? (Manager != null ? Manager.GetBoatSkillIcon(_selected) : null);
            _selectedIcon.enabled = _selectedIcon.sprite != null;
        }

        // 해당 배 스킬 슬롯에 인스펙터로 배정한 스프라이트(없으면 null → 폴백/숨김).
        private Sprite IconForSlot(BoatSkill skill)
        {
            if (_slots == null) return null;
            foreach (var slot in _slots)
                if (slot != null && slot.skill == skill) return slot.icon;
            return null;
        }

        // 해당 배 스킬 슬롯의 효과 아이콘(없으면 null → 숨김).
        private Sprite EffectIconForSlot(BoatSkill skill)
        {
            if (_slots == null) return null;
            foreach (var slot in _slots)
                if (slot != null && slot.skill == skill) return slot.effectIcon;
            return null;
        }

        // ── 표기 헬퍼 ────────────────────────────────────────
        // 스킬마다 효과값 성격이 달라 단위를 스킬별로 결정한다(전역 _effectSuffix 하나론 불가).
        //  · 진공 흡입기 = 포획 수 증가율(CatchMultiplier *= 1+value) → %
        //  · 야근 전등   = 레어도 가중치(RarityBonus += value, 합산되는 추상값) → 단위 없음
        //  · 펭귄        = 발동형(value 미사용, 항상 다음 1마리 예약) → 단위 없음
        // 그 외/미래 스킬은 인스펙터 설정(_effectAsPercent/_effectSuffix)으로 폴백.
        private string FormatEffect(BoatSkill skill, float value)
        {
            switch (skill)
            {
                case BoatSkill.VacuumSuction: return $"{value * 100f:0.#}%";
                case BoatSkill.OvertimeLamp:  return value.ToString("0.##");
                case BoatSkill.DecoyPenguin:  return value.ToString("0.##");
            }
            if (_effectAsPercent) return $"{value * 100f:0.#}%";
            return string.IsNullOrEmpty(_effectSuffix) ? value.ToString("0.##") : value.ToString("0.##") + _effectSuffix;
        }
    }
}
