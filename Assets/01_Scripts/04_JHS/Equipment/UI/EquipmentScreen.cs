using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JHS.Equipment.UI
{
    // 장비 화면 — "값만 채워주는" 정적 바인더.
    // 레이아웃/위치/크기/디자인은 전부 에디터에서 손배치하고, 이 스크립트는 표시값 갱신과 클릭 처리만 한다.
    // (동적 행 생성 없음 — 모든 요소는 아래 [SerializeField] 칸에 직접 연결)
    //
    // 구조: 슬롯(낚싯대/찌/의자) 중 1개 선택 → 그 장비의 강화창 하나만 갱신. (SRP — 표현만, 로직은 EquipmentManager)
    public sealed class EquipmentScreen : MonoBehaviour
    {
        [Header("매니저 (비우면 Instance 사용)")]
        [SerializeField] private EquipmentManager _manager;

        [Header("재화 (우상단)")]
        [SerializeField] private TMP_Text _goldText;

        [Header("강화재료 보유 (우상단, 선택)")]
        [SerializeField] private TMP_Text _materialOwnedText;                    // 강화재료 보유 수량 표시
        [SerializeField] private string _ownedMaterialId = "mat_upgrade_common"; // 표시할 재료 ID

        [Header("슬롯 (낚싯대 / 찌 / 의자)")]
        [SerializeField] private SlotView[] _slots;

        [Header("강화창 — 선택된 장비")]
        [SerializeField] private Image _selectedIcon;     // 강화창 왼쪽 아이콘
        [SerializeField] private TMP_Text _nameText;      // 장비 이름
        [SerializeField] private TMP_Text _levelText;     // "Lv.X » Lv.Y" (max면 "Lv.X")
        [SerializeField] private EffectRowView[] _effectRows;  // 효과 행 (낚싯대=3, 찌·의자=2 → 남는 행 자동 숨김)
        [SerializeField] private Button _upgradeButton;   // 강화 버튼
        [SerializeField] private TMP_Text _upgradeCostText; // 차감 골드 (max면 "MAX") — 버튼 위 비용. 비워두면 미사용

        [Header("필요 재료 패널 (강화 비용 표시 — 골드/재료 1종 기준)")]
        [SerializeField] private TMP_Text _reqGoldText;     // 필요 골드 숫자 (단위 '골드'는 정적/스프라이트)
        [SerializeField] private TMP_Text _reqMaterialText; // 필요 재료 수량 숫자 (단위 '개'는 정적/스프라이트)
        [Tooltip("보유량이 필요량보다 부족할 때 칠할 색 (부족한 쪽만). 충분/MAX면 원색 복원")]
        [SerializeField] private Color _reqLackColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("강화 성공/실패 피드백 (선택 — 연출 전담)")]
        [SerializeField] private UpgradeFeedbackBurst _feedback;

        [Header("장비 총효과 (스탯 8개 자동 — 행 순서 = enum 0~7, 라벨/값 코드가 채움)")]
        [SerializeField] private TotalEffectView[] _totalEffects;
        [SerializeField] private GameObject _totalEffectPopup;   // 화면 켜질 때 항상 닫힘 보장 (선택)

        [Header("효과 라벨 매핑 (스탯 → 한글 라벨/표기)")]
        [SerializeField] private StatLabel[] _statLabels;

        [Header("장비별 표시 스탯 (비우면 데이터의 전체 스탯)")]
        [SerializeField] private DisplayedStats[] _displayedStats;

        private readonly Dictionary<EquipmentStat, StatLabel> _labelMap = new();
        private readonly Dictionary<EquipmentType, EquipmentStat[]> _displayMap = new();
        private static readonly EquipmentStat[] _allStats = (EquipmentStat[])Enum.GetValues(typeof(EquipmentStat));
        private EquipmentType _selected;
        private bool _hasSelection;

        // 필요 골드/재료 텍스트의 원색 캐시 (부족→빨강 후 원색 복원용). 첫 갱신 때 1회 캡처.
        private Color _reqGoldBaseColor; private bool _reqGoldBaseCaptured;
        private Color _reqMatBaseColor;  private bool _reqMatBaseCaptured;

        private EquipmentManager Manager => _manager != null ? _manager : (_manager = EquipmentManager.Instance);

        // ── 인스펙터 구조체들 ────────────────────────────────
        [Serializable]
        public sealed class SlotView
        {
            public EquipmentType type;          // 이 슬롯이 가리키는 장비 (Rod/Float/Chair)
            public Button button;               // 슬롯 클릭 버튼
            public TMP_Text levelText;          // 슬롯에 표시할 "Lv.X" (선택)
            public GameObject selectedHighlight; // 선택 시 켜질 테두리 (선택)
            public Sprite icon;                 // 강화창 왼쪽에 띄울 이 장비의 스프라이트 (인스펙터 배정, 선택)
        }

        [Serializable]
        public sealed class EffectRowView
        {
            public GameObject root;     // 행 전체 — 스탯이 없으면 통째로 숨김
            public Image iconImage;     // 라벨 왼쪽 작은 아이콘 (스탯별, 선택 — 없으면 자동 숨김)
            public TMP_Text labelText;  // "직접 낚시 게이지 안정"
            public TMP_Text currentText; // 현재값 "+12%"
            public TMP_Text nextText;   // 다음값 "+14%" (max면 비움)
            public GameObject arrow;    // " » " 화살표 (선택, max면 숨김)
        }

        [Serializable]
        public sealed class TotalEffectView
        {
            public TMP_Text labelText;  // 스탯 한글 라벨 (코드가 자동 채움)
            public TMP_Text valueText;  // 그 수치 표시 칸 (코드가 자동 채움)
        }

        [Serializable]
        public sealed class DisplayedStats
        {
            public EquipmentType type;       // 어떤 장비의
            public EquipmentStat[] stats;    // 어떤 스탯들을, 이 순서로 효과행에 표시할지 (예: 낚싯대 = 다중포획/보스피해/입질성공)
        }

        [Serializable]
        public sealed class StatLabel
        {
            public EquipmentStat stat;
            public string label;        // 한글 라벨
            public Sprite icon;         // 효과 행 왼쪽에 띄울 작은 아이콘 (선택 — 라벨처럼 스탯을 따라감)
            public bool asPercent;      // true면 value*100 + "%"
            public string suffix;       // asPercent가 아닐 때 붙일 단위 (예: "마리"), 없으면 비움
        }

        // ── 생명주기 ─────────────────────────────────────────
        private void Awake()
        {
            _labelMap.Clear();
            if (_statLabels != null)
                foreach (var s in _statLabels)
                    if (s != null) _labelMap[s.stat] = s;

            _displayMap.Clear();
            if (_displayedStats != null)
                foreach (var d in _displayedStats)
                    if (d != null && d.stats != null && d.stats.Length > 0) _displayMap[d.type] = d.stats;

            if (_slots != null)
                foreach (var slot in _slots)
                {
                    if (slot?.button == null) continue;
                    var captured = slot.type;
                    slot.button.onClick.AddListener(() => Select(captured));
                }

            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        private void OnEnable()
        {
            if (Manager != null) Manager.OnEquipmentUpgraded += OnEquipmentUpgraded;

            // 장비 화면이 켜질 때 총효과 팝업은 항상 닫힌 상태로 시작 (편집 중 켜둔 채 저장돼도 안전)
            if (_totalEffectPopup != null) _totalEffectPopup.SetActive(false);

            // 기본 선택: 아직 고른 적 없으면 첫 슬롯
            if (!_hasSelection && _slots != null && _slots.Length > 0)
            {
                _selected = _slots[0].type;
                _hasSelection = true;
            }
            RefreshAll();

            // 로그인 직후엔 서버 골드 캐시(PlayFabDataStore)가 스테일이라 옛 골드가 보일 수 있다.
            // 화면을 열 때 서버 잔액을 한 번 강제 동기화하고, 응답이 오면 골드 라벨만 다시 갱신한다.
            if (Manager != null)
                Manager.RequestRefreshGold(() => { if (this != null) RefreshCurrency(); });
        }

        private void OnDisable()
        {
            if (Manager != null) Manager.OnEquipmentUpgraded -= OnEquipmentUpgraded;
        }

        private void OnEquipmentUpgraded(EquipmentType type, int level) => RefreshAll();

        // ── 선택 / 강화 ──────────────────────────────────────
        public void Select(EquipmentType type)
        {
            _selected = type;
            _hasSelection = true;
            RefreshAll();
        }

        private void OnUpgradeClicked()
        {
            if (Manager == null || !_hasSelection) return;

            // 강화 실패는 확률이 아니라 '재화 부족'이 유일 원인(EquipmentManager.TrySpendCost) →
            // 서버 왕복으로 보유량이 바뀌기 전에, 요청 직전 보유량으로 부족 사유를 캡처한다.
            bool lackGold = Manager.Gold < Manager.GetUpgradeCost(_selected);
            bool lackMat  = !HasEnoughMaterials(_selected);

            // 비관적 UX: 서버 왕복 동안 버튼 잠금(중복클릭 가드) → 콜백에서 RefreshAll로 상태/골드 복구.
            // 성공 시 newLevel·골드 반영, 실패 시 원상태. (서버 미연결이면 Manager가 즉시 로컬 폴백)
            if (_upgradeButton != null) _upgradeButton.interactable = false;
            Manager.RequestUpgrade(_selected, ok =>
            {
                RefreshAll();
                if (_feedback == null) return;
                if (ok) _feedback.PlaySuccess();
                else    _feedback.PlayFail(lackGold, lackMat);   // 부족한 쪽(골드/재료) 흔들기
            });
        }

        // 선택 장비의 강화 재료를 보유량이 모두 충족하는지(클라 사전검사 — 실패 사유 판정용).
        private bool HasEnoughMaterials(EquipmentType type)
        {
            var mats = Manager.GetUpgradeMaterials(type);
            if (mats == null) return true;
            foreach (var m in mats)
                if (Manager.GetMaterialCount(m.materialId) < m.count) return false;
            return true;
        }

        // ── 갱신 ─────────────────────────────────────────────
        private void RefreshAll()
        {
            if (Manager == null) return;
            RefreshCurrency();
            RefreshSlots();
            RefreshDetail();
            RefreshTotals();
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
            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                if (slot.levelText != null) slot.levelText.text = $"Lv.{Manager.GetLevel(slot.type) + 1}";
                if (slot.selectedHighlight != null)
                    slot.selectedHighlight.SetActive(_hasSelection && slot.type == _selected);
            }
        }

        private void RefreshDetail()
        {
            if (!_hasSelection) return;
            var type = _selected;
            bool max = Manager.IsMaxLevel(type);
            int level = Manager.GetLevel(type);

            if (_selectedIcon != null)
            {
                // 선택된 슬롯에 인스펙터로 꽂아둔 스프라이트 우선. 없으면 데이터(SO) 아이콘으로 폴백.
                _selectedIcon.sprite = IconForSlot(type) ?? Manager.GetIcon(type);
                _selectedIcon.enabled = _selectedIcon.sprite != null;
            }
            if (_nameText != null) _nameText.text = Manager.GetDisplayName(type);
            if (_levelText != null)
                _levelText.text = max ? "MAX" : $"Lv.{level + 1} » Lv.{level + 2}";   // 표시는 1-based (데이터 인덱스 0-based)

            RefreshEffectRows(type, level, max);

            if (_upgradeCostText != null)
                _upgradeCostText.text = max ? "MAX" : $"{Manager.GetUpgradeCost(type):N0}";
            if (_upgradeButton != null) _upgradeButton.interactable = !max;

            RefreshRequirements(type, max);
        }

        // 필요재료 패널: 강화 비용(골드 + 재료 1종) 표시. (실제 차감은 서버 권위 — 여긴 표시용)
        private void RefreshRequirements(EquipmentType type, bool max)
        {
            if (_reqGoldText != null)
            {
                if (!_reqGoldBaseCaptured) { _reqGoldBaseColor = _reqGoldText.color; _reqGoldBaseCaptured = true; }
                _reqGoldText.text = max ? "MAX" : $"{Manager.GetUpgradeCost(type):N0}";
                // 보유 골드가 필요 골드보다 적으면 빨강(부족), 아니면 원색. (MAX는 부족 아님)
                bool lackGold = !max && Manager.Gold < Manager.GetUpgradeCost(type);
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
                    var mats = Manager.GetUpgradeMaterials(type);
                    _reqMaterialText.text = (mats == null || mats.Count == 0) ? "-" : $"{mats[0].count:N0}";   // 1종 기준 (여러 종은 추후 행 확장)
                    // 보유 재료가 필요 재료보다 적으면 빨강(부족), 아니면 원색.
                    _reqMaterialText.color = HasEnoughMaterials(type) ? _reqMatBaseColor : _reqLackColor;
                }
            }
        }

        private void RefreshEffectRows(EquipmentType type, int level, bool max)
        {
            if (_effectRows == null) return;
            var stats = StatsToShow(type);

            for (int i = 0; i < _effectRows.Length; i++)
            {
                var row = _effectRows[i];
                if (row == null) continue;

                bool used = i < stats.Count;
                if (row.root != null) row.root.SetActive(used);
                if (!used) continue;

                var stat = stats[i];
                if (row.iconImage != null)
                {
                    var sp = IconOf(stat);
                    row.iconImage.sprite = sp;
                    row.iconImage.enabled = sp != null;   // 스탯 아이콘 미배정이면 숨김
                }
                if (row.labelText != null) row.labelText.text = LabelOf(stat);
                if (row.currentText != null)
                    row.currentText.text = FormatValue(stat, Manager.GetStatAt(type, stat, level));
                if (row.nextText != null)
                    row.nextText.text = max ? "" : FormatValue(stat, Manager.GetStatAt(type, stat, level + 1));
                if (row.arrow != null) row.arrow.SetActive(!max);
            }
        }

        // 스탯 8개를 행 순서대로 자동 채움(행0=enum0 …). 라벨/값 모두 코드가 채우므로 인스펙터 타이핑 불필요.
        private void RefreshTotals()
        {
            if (_totalEffects == null) return;
            for (int i = 0; i < _totalEffects.Length; i++)
            {
                var t = _totalEffects[i];
                if (t == null) continue;

                if (i >= _allStats.Length)   // 행이 스탯(8개)보다 많으면 남는 행은 비움
                {
                    if (t.labelText != null) t.labelText.text = "";
                    if (t.valueText != null) t.valueText.text = "";
                    continue;
                }

                var stat = _allStats[i];
                if (t.labelText != null) t.labelText.text = LabelOf(stat);
                if (t.valueText != null) t.valueText.text = FormatValue(stat, Manager.GetStat(stat));
            }
        }

        // 이 장비에서 효과행에 보여줄 스탯 목록.
        // 인스펙터에서 장비별로 골랐으면 그걸, 안 골랐으면 데이터의 전체 스탯으로 폴백. (큐레이션/안전 둘 다)
        private IReadOnlyList<EquipmentStat> StatsToShow(EquipmentType type)
            => _displayMap.TryGetValue(type, out var s) ? s : Manager.GetStats(type);

        // 해당 장비 슬롯에 인스펙터로 배정한 스프라이트(없으면 null → 폴백/숨김).
        private Sprite IconForSlot(EquipmentType type)
        {
            if (_slots == null) return null;
            foreach (var slot in _slots)
                if (slot != null && slot.type == type) return slot.icon;
            return null;
        }

        // ── 표기 헬퍼 ────────────────────────────────────────
        private string LabelOf(EquipmentStat stat)
            => _labelMap.TryGetValue(stat, out var s) && !string.IsNullOrEmpty(s.label) ? s.label : stat.ToString();

        // 스탯 → 효과 행 아이콘 (인스펙터 매핑, 없으면 null → 행에서 숨김)
        private Sprite IconOf(EquipmentStat stat)
            => _labelMap.TryGetValue(stat, out var s) ? s.icon : null;

        private string FormatValue(EquipmentStat stat, float value)
        {
            if (_labelMap.TryGetValue(stat, out var s))
            {
                if (s.asPercent) return $"{value * 100f:0.#}%";
                return string.IsNullOrEmpty(s.suffix) ? value.ToString("0.##") : value.ToString("0.##") + s.suffix;
            }
            return value.ToString("0.##");
        }
    }
}
