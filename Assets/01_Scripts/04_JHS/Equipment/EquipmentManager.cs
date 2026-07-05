using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using JHS.Content;

namespace JHS.Equipment
{
    // 장비 능력치/등급을 한 곳에서 관리. 다른 모듈은 Instance.GetStat(stat) 으로 읽어간다.
    public class EquipmentManager : MonoBehaviour, IEquipmentEffects
    {
        public static EquipmentManager Instance { get; private set; }

        [SerializeField] private EquipmentData[] _equipments;  // 4종 SO 등록 (비우면 에디터에서 자동 로드)

        // (type, 새 등급) — UI 갱신용
        public event Action<EquipmentType, int> OnEquipmentUpgraded;
        // (skill, 새 등급) — 배 스킬 강화 UI 갱신용
        public event Action<BoatSkill, int> OnBoatSkillUpgraded;
        // 장착한 배가 바뀔 때 (새 장착 배). 발동 컨트롤러·메인화면 비주얼(BoatView)·UI가 구독.
        public event Action<BoatSkill> OnBoatEquipped;
        // 배가 새로 해금될 때 (해금된 배). UI가 구독해 잠금/장착 상태 갱신.
        public event Action<BoatSkill> OnBoatUnlocked;

        private readonly Dictionary<EquipmentType, EquipmentData> _data = new();
        private readonly Dictionary<EquipmentType, int> _level = new();          // 세이브 대상
        private readonly Dictionary<EquipmentStat, EquipmentType> _statOwner = new();
        private readonly Dictionary<BoatSkill, BoatSkillEntry> _boatSkill = new();
        private readonly List<BoatSkillEntry> _boatSkillsOrdered = new();        // GetBoatSkills 표시 순서 (모든 배 SO 합산)
        private readonly Dictionary<BoatSkill, int> _boatSkillLevel = new();     // 세이브 대상
        private readonly HashSet<BoatSkill> _unlockedBoats = new();              // 해금된 배(런타임 진실). 세이브 대상. SO unlocked는 기본 해금 시드.
        private BoatSkill _equippedBoat;       // 현재 장착한 배 (세이브 대상)
        private bool _hasEquippedBoat;         // 장착 가능한 배가 하나라도 있는가
        private IGoldWallet _gold;
        private IMaterialInventory _materials;
        private IEquipmentServerGateway _server;   // 주입되면 강화/해금/장착이 서버 권위(비동기)로 전환. null이면 로컬(더미) 폴백.
        private IEquipmentCostCatalog _costs;       // 주입되면 표시 비용을 Title Data에서 읽음. null/미로드면 SO값 폴백.
        private bool _inited;

        // ── async 준비 (콘텐츠 async + CCD 소비) ────────────────────────────
        // 이 매니저가 소비하는 JHS 콘텐츠 6종의 공통 ID(= AssetRegistry/CCD 주소). _equipments를 비워도
        // 이 목록으로 ContentService에서 로드한다. (ContentService의 검증 ID 6종과 동일)
        // 이 매니저가 소비하는 JHS 콘텐츠 6종의 공통 ID. CCD 다운로드 게이지(CcdDownloadGate)도
        // 같은 키로 원격 의존성을 미리 받으므로 public 노출(단일 출처).
        public static readonly string[] ContentIds =
        {
            "equip_rod", "equip_float", "equip_chair",
            "boat_vacuum", "boat_lamp", "boat_penguin",
        };
        // ContentService.Assets가 아직 안 붙었을 때 대기하는 독립 타임아웃(초).
        // 게이트 차단/점검 등으로 provider가 끝내 안 와도 EnsureReadyAsync가 무한대기하지 않게 한다.
        private const float ProviderWaitTimeoutSec = 15f;

        public bool IsReady { get; private set; }
        // 조회 딕셔너리에 실제 콘텐츠가 있는가(로드 성공 여부). 서버 스냅샷 적용 전 가드용 —
        // IsReady는 로드 실패(폴백)시에도 true가 되므로, "빈 딕셔너리에 스냅샷을 덮어 진행도 소실"을
        // 막으려면 소비측은 이 값이 true일 때만 스냅샷을 적용해야 한다.
        public bool HasContent => _data != null && _data.Count > 0;
        public event Action OnReady;          // 콘텐츠 로드 완료 시 1회 발행 (이미 Ready면 즉시 호출은 안 함 — 호출측이 IsReady 확인)
        private Task _readyTask;               // single-flight: 6종 중복 로드/핸들 누수 방지

        private void Awake()
        {
            // 중복 가드: 이미 다른 EquipmentManager가 자리잡았으면 늦게 온 이 인스턴스는 스스로 제거.
            // (씬 통합·additive 로드로 매니저가 둘 이상 생겨 Instance가 가로채이는 사고 방지. "처음이 이긴다")
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            EnsureInit();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Awake 순서와 무관하게, 누가 먼저 읽어도 그때 초기화된다.
        private void EnsureInit()
        {
            if (_inited) return;
            _inited = true;

            // 첫 번째 인스턴스만 전역 Instance 차지. 지연초기화(EnsureInit)가 Awake보다 먼저 도는 경우에도
            // 중복이 Instance를 가로채지 못하게 함(중복은 자기 데이터만 init 후 Awake에서 파괴됨).
            if (Instance == null) Instance = this;
            _gold ??= new DummyGoldWallet();
            // 어댑터가 CSH 인벤토리를 찾으면 거기에, 없으면 더미로 자동 폴백.
            // 통일 방향이 CSH가 아니면 SetMaterialInventory()로 다른 어댑터 주입하면 됨.
            _materials ??= new CshMaterialInventoryAdapter();

            // _equipments가 비어 있어도 여기서 자동 로드하지 않는다(과거 #if UNITY_EDITOR 폴백 제거).
            // 콘텐츠는 ContentService(Registry=로컬 AssetRegistry / Addressable=CCD)로만 로드한다 —
            // ContentConsumerBootstrap이 EnsureReadyAsync를 호출해 채운다.
            // (에디터가 CCD 값을 로컬 AssetDatabase 값으로 가려 혼동시키던 문제 제거 → 에디터=빌드 동일 동작)
            if (_equipments == null) _equipments = Array.Empty<EquipmentData>();

            RebuildDictionaries();
        }

        // _equipments로 조회 딕셔너리를 (재)구성한다. 동기 EnsureInit과 async EnsureReadyAsync가 공유.
        // 여러 번 호출해도 안전(clear 후 재구성). 등급/장착은 SO 시드값으로 초기화 —
        // 저장/서버 값은 이후 LoadSaveState/LoadFromServerSnapshot이 덮는다.
        // ⚠️ 그래서 이 재구성은 반드시 세이브/스냅샷 로드보다 '먼저' 끝나야 한다(진행도 소실 방지).
        private void RebuildDictionaries()
        {
            _data.Clear();
            _level.Clear();
            _statOwner.Clear();
            _boatSkill.Clear();
            _boatSkillsOrdered.Clear();
            _boatSkillLevel.Clear();
            _unlockedBoats.Clear();
            _hasEquippedBoat = false;

            foreach (var data in _equipments)
            {
                if (data == null) continue;
                _data[data.type] = data;
                _level[data.type] = 0;   // 초기값 0 — 저장된 등급은 LoadSaveState()로 덮어씀

                if (data.levels == null) continue;
                foreach (var lv in data.levels)
                    if (lv?.stats != null)
                        foreach (var sv in lv.stats)
                            _statOwner[sv.stat] = data.type;
            }

            // 배 스킬 등급 맵 구성 (스킬마다 개별 강화).
            // 배 데이터가 한 SO에 boatSkills 배열로 있든, 배 1척당 SO 1개로 나뉘어 있든
            // type==Boat 인 모든 EquipmentData의 boatSkills를 전부 모은다(SO 분리와 무관하게 동작).
            foreach (var data in _equipments)
            {
                if (data == null || data.type != EquipmentType.Boat || data.boatSkills == null) continue;
                foreach (var s in data.boatSkills)
                {
                    if (s == null || _boatSkill.ContainsKey(s.skill)) continue;   // 중복 SO 방어 (같은 스킬은 한 번만, 처음이 이김)
                    _boatSkill[s.skill] = s;
                    _boatSkillLevel[s.skill] = 0;   // 초기값 0 — 저장된 등급은 LoadSaveState()로 덮어씀
                    _boatSkillsOrdered.Add(s);
                    if (s.unlocked) _unlockedBoats.Add(s.skill);   // 기본 해금(시작부터 열린 배) 시드. 세이브는 LoadSaveState로 합집합.

                    // 기본 장착 = 데이터 순서상 첫 해금 배. (세이브 값 있으면 LoadSaveState가 덮어씀)
                    if (!_hasEquippedBoat && s.unlocked)
                    {
                        _equippedBoat = s.skill;
                        _hasEquippedBoat = true;
                    }
                }
            }

            Debug.Log($"[Equipment] 딕셔너리 구성 — {DebugSummary()}{(_hasEquippedBoat ? $" / 장착:{_equippedBoat}" : " / 장착가능 배 없음")}");
        }

        // 콘텐츠(장비/배 정의 SO 6종)를 ContentService로 비동기 로드해 준비 완료시킨다.
        //  · Registry 모드: 인앱 레지스트리라 사실상 즉시 완료(기존 동작과 동일).
        //  · Addressable 모드: CCD 번들 로드(네트워크) 후 완료.
        // single-flight — 여러 번 호출해도 실제 로드는 1회. 완료 시 IsReady=true + OnReady 1회 발행.
        // ⚠️ 반드시 서버 스냅샷/세이브 로드보다 '먼저' await 되어야 한다(RebuildDictionaries가 등급을 0으로 되돌리므로).
        public Task EnsureReadyAsync(bool forceReload = false)
        {
            EnsureInit();
            // forceReload: 카탈로그가 늦게 갱신된 경우(느린 CCD 패치가 폴백 로드 뒤 완료) 최신 콘텐츠로 다시 로드.
            if (forceReload) { _readyTask = null; IsReady = false; }
            if (IsReady) return Task.CompletedTask;
            return _readyTask ??= LoadContentAndBuildAsync();
        }

        private async Task LoadContentAndBuildAsync()
        {
            try
            {
                var provider = await WaitForProviderAsync();
                if (provider != null)
                {
                    var tasks = new Task<EquipmentData>[ContentIds.Length];
                    for (int i = 0; i < ContentIds.Length; i++)
                        tasks[i] = provider.LoadByIdAsync<EquipmentData>(ContentIds[i]);
                    var loaded = await Task.WhenAll(tasks);   // 메인스레드 유지(ConfigureAwait 미사용)

                    var valid = new List<EquipmentData>(loaded.Length);
                    foreach (var d in loaded)
                        if (d != null) valid.Add(d);

                    if (valid.Count > 0)
                    {
                        _equipments = valid.ToArray();
                        RebuildDictionaries();
                    }
                    else
                    {
                        // CCD 실패/미가용 등 — 기존 _equipments(직접참조 남아있으면)로 폴백. 데이터0 회피.
                        Debug.LogWarning("[Equipment] ContentService 로드 0건 — 기존 _equipments 유지(폴백).");
                    }
                }
                else
                {
                    Debug.LogWarning("[Equipment] ContentService.Assets 준비 안 됨(타임아웃) — 기존 _equipments 유지(폴백).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Equipment] async 콘텐츠 로드 실패 — 기존 _equipments 유지(폴백). {e}");
            }

            IsReady = true;
            OnReady?.Invoke();
            // 이른 소비자(BoatView/발동 컨트롤러)가 로드 완료 전에 구독했을 수 있다 —
            // 장착 배 확정 후 1회 재발행해 최신 배로 동기화(핸들러 idempotent). LoadFromServerSnapshot 말미와 동일 사상.
            if (_hasEquippedBoat) OnBoatEquipped?.Invoke(_equippedBoat);
        }

        // ContentService.Assets가 붙을 때까지 대기(Awake 순서 무관). 실시간 기준 타임아웃 시 null 반환(무한대기 금지).
        private async Task<IAssetProvider> WaitForProviderAsync()
        {
            float deadline = Time.realtimeSinceStartup + ProviderWaitTimeoutSec;
            while (ContentService.Assets == null)
            {
                if (Time.realtimeSinceStartup >= deadline) return null;
                await Task.Delay(16);   // 한 프레임가량 양보(핫스핀 방지). UnitySynchronizationContext라 메인스레드로 복귀.
            }
            return ContentService.Assets;
        }

        // 재화 시스템 준비되면 주입
        public void SetGoldWallet(IGoldWallet wallet) => _gold = wallet ?? _gold;

        // 인벤토리 시스템 준비되면 주입 (어댑터로 연결, 다른 모듈 코드는 건드리지 않음)
        public void SetMaterialInventory(IMaterialInventory inventory) => _materials = inventory ?? _materials;

        // 서버(PlayFab) 연결 시 주입. 주입되면 강화/해금/장착이 서버 권위(Request* 비동기)로 동작한다.
        public void SetServerGateway(IEquipmentServerGateway gateway) => _server = gateway;
        public bool HasServerGateway => _server != null;

        // Title Data 비용표 주입. 주입되면 표시 비용을 Title Data에서 읽고, 없으면 SO값으로 폴백.
        public void SetCostCatalog(IEquipmentCostCatalog catalog) => _costs = catalog;

        // 현재 골드: 서버 연결 시 서버 동기 잔액, 아니면 로컬 지갑. (표시·사전검사 공용)
        private long CurrentGold => _server != null ? _server.Gold : (_gold?.Gold ?? 0);

        public long Gold { get { EnsureInit(); return CurrentGold; } }

        // 표시용 골드 강제 동기화. 서버 연결 시 서버 잔액을 다시 읽어 스토어(PlayFabDataStore)를 최신화한 뒤 콜백.
        // (강화/선박창이 열릴 때 호출 — 로그인 직후 스테일 로컬캐시가 옛 골드로 보이는 문제 방지)
        public void RequestRefreshGold(Action onDone = null)
        {
            EnsureInit();
            if (_server != null) _server.RefreshCurrency(_ => onDone?.Invoke());
            else onDone?.Invoke();   // 로컬 지갑(_gold)은 항상 라이브 — 별도 동기화 불필요
        }

        // 서버 종류 ID(공유 ID). SO에 기입된 id를 그대로 내준다(없으면 null). 서버 콜의 equipmentId/shipId로 사용.
        public string GetEquipmentId(EquipmentType type) { EnsureInit(); return _data.TryGetValue(type, out var d) ? d.id : null; }
        public string GetBoatId(BoatSkill skill) { EnsureInit(); return _boatSkill.TryGetValue(skill, out var s) ? s.id : null; }

#if UNITY_EDITOR
        // 에디터 전용 디버그: 현재 연결된 지갑에 골드를 더한다(Refund 경로 재사용). 빌드엔 미포함.
        public void DebugAddGold(long amount) { EnsureInit(); _gold?.Refund(amount); }
#endif

        public string DebugSummary()
            => $"등록장비[{_data.Count}]: {string.Join(",", _data.Keys)} / Boat스킬 {GetBoatSkills().Count}개";

        // ── 읽기 (다른 모듈 공용) ──────────────────────────────
        public float GetStat(EquipmentStat stat, float fallback = 0f)
        {
            EnsureInit();
            if (!_statOwner.TryGetValue(stat, out var type)) return fallback;

            var data = _data[type];
            int idx = Mathf.Clamp(GetLevel(type), 0, data.MaxLevelIndex);
            if (idx < 0) return fallback;

            foreach (var sv in data.levels[idx].stats)
                if (sv.stat == stat) return sv.value;
            return fallback;
        }

        // 특정 등급의 스탯값 (UI 미리보기용: "0.10 → 0.14"의 다음값 표시)
        public float GetStatAt(EquipmentType type, EquipmentStat stat, int levelIndex, float fallback = 0f)
        {
            EnsureInit();
            if (!_data.TryGetValue(type, out var data) || data.levels == null || data.levels.Length == 0) return fallback;
            int idx = Mathf.Clamp(levelIndex, 0, data.MaxLevelIndex);
            foreach (var sv in data.levels[idx].stats)
                if (sv.stat == stat) return sv.value;
            return fallback;
        }

        // 이 장비가 보유한 스탯 종류 목록 (등급 전체에서 처음 등장 순서대로, 중복 제거).
        // UI가 "이 장비의 효과 행을 몇 개·어떤 스탯으로 그릴지" 결정할 때 사용. (표현/데이터 분리)
        public IReadOnlyList<EquipmentStat> GetStats(EquipmentType type)
        {
            EnsureInit();
            var result = new List<EquipmentStat>();
            if (!_data.TryGetValue(type, out var d) || d.levels == null) return result;
            foreach (var lv in d.levels)
                if (lv?.stats != null)
                    foreach (var sv in lv.stats)
                        if (!result.Contains(sv.stat)) result.Add(sv.stat);
            return result;
        }

        // UI 표시명 (데이터의 displayName, 없으면 enum 이름)
        public string GetDisplayName(EquipmentType type)
        {
            EnsureInit();
            return (_data.TryGetValue(type, out var d) && !string.IsNullOrEmpty(d.displayName)) ? d.displayName : type.ToString();
        }

        public string GetDescription(EquipmentType type) { EnsureInit(); return _data.TryGetValue(type, out var d) ? d.description : ""; }
        public Sprite GetIcon(EquipmentType type) { EnsureInit(); return _data.TryGetValue(type, out var d) ? d.icon : null; }

        public IReadOnlyList<BoatSkillEntry> GetBoatSkills()
        {
            EnsureInit();
            return _boatSkillsOrdered;   // 모든 배 SO에서 합산한 표시 순서
        }

        // 배 스킬 1종의 설정(쿨다운/지속/해금 등). 없으면 null. (발동 컨트롤러가 장착 배 1척만 적재할 때 사용)
        public BoatSkillEntry GetBoatSkillEntry(BoatSkill skill)
        {
            EnsureInit();
            return _boatSkill.TryGetValue(skill, out var s) ? s : null;
        }

        // ── 배 장착 (3척 중 1척, 그 배 스킬만 발동) ─────────────
        public BoatSkill EquippedBoat { get { EnsureInit(); return _equippedBoat; } }
        public bool HasEquippedBoat { get { EnsureInit(); return _hasEquippedBoat; } }

        // 장착 변경. 미존재/미해금이면 false(변경 없음). 같은 배 재장착은 true지만 이벤트 미발행.
        public bool TryEquipBoat(BoatSkill skill)
        {
            EnsureInit();
            if (!_boatSkill.ContainsKey(skill) || !_unlockedBoats.Contains(skill)) return false;   // 미존재/미해금

            if (_hasEquippedBoat && _equippedBoat == skill) return true;   // 이미 장착 — 변경 없음

            _equippedBoat = skill;
            _hasEquippedBoat = true;
            OnBoatEquipped?.Invoke(_equippedBoat);
            return true;
        }

        // ── 배 해금 (골드 구매) ───────────────────────────────
        // 해금 상태의 진실은 런타임 집합(_unlockedBoats). SO의 unlocked는 "기본 해금(시작부터)" 시드일 뿐.
        // unlockCostGold는 id 기준 공유 비용 엔티티 — 추후 Title Data(BoatUnlockCostList)로 승격: 서버 권위검증/카탈로그가 소비.
        public bool IsBoatUnlocked(BoatSkill skill) { EnsureInit(); return _unlockedBoats.Contains(skill); }

        // 해금 비용(골드). 이미 해금했거나 미존재면 -1.
        public int GetBoatUnlockCost(BoatSkill skill)
        {
            EnsureInit();
            if (_unlockedBoats.Contains(skill) || !_boatSkill.TryGetValue(skill, out var s)) return -1;
            // Title Data 우선(권위 비용), 없으면 SO값 폴백.
            if (_costs != null && !string.IsNullOrEmpty(s.id) && _costs.TryGetBoatUnlockGold(s.id, out var g))
                return g;
            return s.unlockCostGold;
        }

        // 지금 해금 가능한가 (미해금 && 골드 충분).
        public bool CanUnlockBoat(BoatSkill skill)
        {
            EnsureInit();
            if (_unlockedBoats.Contains(skill) || !_boatSkill.TryGetValue(skill, out var s)) return false;
            return CurrentGold >= GetBoatUnlockCost(skill);   // 표시 비용(Title Data 우선)과 동일 기준 사전검사. 최종 판정은 서버.
        }

        // 골드를 차감하고 배를 해금. 이미 해금/미존재/골드부족이면 false(아무것도 안 깎임).
        public bool TryUnlockBoat(BoatSkill skill)
        {
            EnsureInit();
            if (_unlockedBoats.Contains(skill) || !_boatSkill.TryGetValue(skill, out var s)) return false;
            if (!TrySpendCost(s.unlockCostGold, Array.Empty<MaterialCost>())) return false;   // 골드만(재료 없음). 강화와 같은 원자적 차감 경로 재사용.

            _unlockedBoats.Add(skill);
            OnBoatUnlocked?.Invoke(skill);
            return true;
        }

        public int GetLevel(EquipmentType type) { EnsureInit(); return _level.TryGetValue(type, out var lv) ? lv : 0; }

        public int GetMaxLevel(EquipmentType type) { EnsureInit(); return _data.TryGetValue(type, out var d) ? d.MaxLevelIndex : -1; }

        public bool IsMaxLevel(EquipmentType type) => GetLevel(type) >= GetMaxLevel(type);

        public int GetUpgradeCost(EquipmentType type)
        {
            if (IsMaxLevel(type)) return -1;
            var d = _data[type];
            // Title Data 우선(권위 비용), 없으면 SO값 폴백.
            if (_costs != null && !string.IsNullOrEmpty(d.id) && _costs.TryGetEquipmentUpgradeGold(d.id, GetLevel(type), out var g))
                return g;
            return d.levels[GetLevel(type)].upgradeCostGold;
        }

        // 다음 등급으로 올리는 데 필요한 재료 목록 (최대 레벨이면 빈 배열)
        public IReadOnlyList<MaterialCost> GetUpgradeMaterials(EquipmentType type)
        {
            EnsureInit();
            if (IsMaxLevel(type) || !_data.ContainsKey(type)) return Array.Empty<MaterialCost>();
            return _data[type].levels[GetLevel(type)].materialCosts ?? Array.Empty<MaterialCost>();
        }

        // 재료 보유 수량 (UI 필요재료 패널의 보유/부족 표시용). 연결된 인벤토리(_materials)에서 읽음.
        // (실제 차감은 서버 권위 — 이건 표시용. 부족 판정도 표시 기준일 뿐 최종 판정은 서버)
        public int GetMaterialCount(string materialId)
        {
            EnsureInit();
            return string.IsNullOrEmpty(materialId) ? 0 : (_materials?.GetCount(materialId) ?? 0);
        }

        // ── 강화 ──────────────────────────────────────────────
        // 골드 + 재료를 원자적으로 차감. 하나라도 부족하면 false (아무것도 안 깎임).
        // 장비 강화와 배 스킬 강화가 공유한다.
        private bool TrySpendCost(int gold, IReadOnlyList<MaterialCost> materials)
        {
            if (_gold.Gold < gold) return false;
            if (!_materials.Has(materials)) return false;

            if (!_gold.TrySpend(gold)) return false;
            if (!_materials.TryConsume(materials))
            {
                _gold.Refund(gold);   // 재료 차감 실패 시 골드 환불
                return false;
            }
            return true;
        }

        public bool TryUpgrade(EquipmentType type)
        {
            EnsureInit();
            if (!_data.ContainsKey(type) || IsMaxLevel(type)) return false;

            var entry = _data[type].levels[GetLevel(type)];
            if (!TrySpendCost(entry.upgradeCostGold, entry.materialCosts)) return false;

            _level[type]++;
            OnEquipmentUpgraded?.Invoke(type, _level[type]);
            return true;
        }

        // ── 배 스킬 강화 (스킬마다 개별, value=확률/계수만 등급별 상승) ──────────
        public int GetBoatSkillLevel(BoatSkill skill) { EnsureInit(); return _boatSkillLevel.TryGetValue(skill, out var lv) ? lv : 0; }

        public int GetBoatSkillMaxLevel(BoatSkill skill) { EnsureInit(); return _boatSkill.TryGetValue(skill, out var s) ? s.MaxLevelIndex : -1; }

        public bool IsBoatSkillMax(BoatSkill skill) => GetBoatSkillLevel(skill) >= GetBoatSkillMaxLevel(skill);

        // 현재 등급의 효과 수치(확률/계수). BoatActiveSkillController가 매 프레임 읽어간다.
        public float GetBoatSkillValue(BoatSkill skill) => GetBoatSkillValueAt(skill, GetBoatSkillLevel(skill));

        // 특정 등급의 효과 수치 (UI 미리보기용: 다음 등급값 표시)
        public float GetBoatSkillValueAt(BoatSkill skill, int levelIndex)
        {
            EnsureInit();
            if (!_boatSkill.TryGetValue(skill, out var s) || s.levels == null || s.levels.Length == 0) return 0f;
            int idx = Mathf.Clamp(levelIndex, 0, s.MaxLevelIndex);
            return s.levels[idx].value;
        }

        // UI 표시명 (데이터의 displayName, 없으면 enum 이름)
        public string GetBoatSkillName(BoatSkill skill)
        {
            EnsureInit();
            return (_boatSkill.TryGetValue(skill, out var s) && !string.IsNullOrEmpty(s.displayName)) ? s.displayName : skill.ToString();
        }

        public string GetBoatSkillDescription(BoatSkill skill) { EnsureInit(); return _boatSkill.TryGetValue(skill, out var s) ? s.description : ""; }
        public Sprite GetBoatSkillIcon(BoatSkill skill) { EnsureInit(); return _boatSkill.TryGetValue(skill, out var s) ? s.icon : null; }

        public int GetBoatSkillUpgradeCost(BoatSkill skill)
        {
            EnsureInit();
            if (IsBoatSkillMax(skill) || !_boatSkill.TryGetValue(skill, out var s)) return -1;
            // Title Data 우선(권위 비용), 없으면 SO값 폴백.
            if (_costs != null && !string.IsNullOrEmpty(s.id) && _costs.TryGetBoatUpgradeGold(s.id, GetBoatSkillLevel(skill), out var g))
                return g;
            return s.levels[GetBoatSkillLevel(skill)].upgradeCostGold;
        }

        public IReadOnlyList<MaterialCost> GetBoatSkillUpgradeMaterials(BoatSkill skill)
        {
            EnsureInit();
            if (IsBoatSkillMax(skill) || !_boatSkill.TryGetValue(skill, out var s)) return Array.Empty<MaterialCost>();
            return s.levels[GetBoatSkillLevel(skill)].materialCosts ?? Array.Empty<MaterialCost>();
        }

        public bool TryUpgradeBoatSkill(BoatSkill skill)
        {
            EnsureInit();
            if (!_boatSkill.TryGetValue(skill, out var s) || IsBoatSkillMax(skill)) return false;

            var lv = s.levels[GetBoatSkillLevel(skill)];
            if (!TrySpendCost(lv.upgradeCostGold, lv.materialCosts)) return false;

            _boatSkillLevel[skill]++;
            OnBoatSkillUpgraded?.Invoke(skill, _boatSkillLevel[skill]);
            return true;
        }

        // ── 서버 권위 요청 (비동기) ────────────────────────────
        // 서버 게이트웨이가 주입되면 강화/해금/장착은 이 Request* 를 쓴다(골드·레벨·해금 판정 = 서버).
        // 성공 시 서버가 돌려준 값으로 로컬 상태를 맞추고 기존 이벤트를 발행(UI는 그대로 구독).
        // onResult(true/false)는 항상 호출됨(UI 로딩 해제용). 서버 미연결이면 로컬 Try*로 즉시 폴백.

        public void RequestUpgrade(EquipmentType type, Action<bool> onResult = null)
        {
            EnsureInit();
            if (!_data.ContainsKey(type) || IsMaxLevel(type)) { onResult?.Invoke(false); return; }
            if (_server == null) { onResult?.Invoke(TryUpgrade(type)); return; }   // 무서버 폴백

            var data = _data[type];
            if (string.IsNullOrEmpty(data.id)) { Debug.LogWarning($"[Equipment] {type} id 미설정 — 서버 강화 불가"); onResult?.Invoke(false); return; }

            _server.UpgradeEquipment(data.id, r =>
            {
                if (r.success)
                {
                    // 서버 1-base → 로컬 0-base. 성공인데 newServerLevel이 비어(≤0)오면(응답 스키마 드리프트) 0으로 리셋하지
                    // 말고 낙관적으로 현재+1 유지(다음 스냅샷이 정정). 골드는 이미 차감됐는데 Lv.1로 보이는 진행도 소실감 방지.
                    int fallbackLevel = (_level.TryGetValue(type, out var curLv) ? curLv : 0) + 1;
                    _level[type] = Mathf.Clamp(r.newServerLevel > 0 ? r.newServerLevel - 1 : fallbackLevel, 0, data.MaxLevelIndex);
                    OnEquipmentUpgraded?.Invoke(type, _level[type]);
                }
                else Debug.LogWarning($"[Equipment] {type} 강화 실패(서버): {r.error}");
                onResult?.Invoke(r.success);
            });
        }

        public void RequestUpgradeBoatSkill(BoatSkill skill, Action<bool> onResult = null)
        {
            EnsureInit();
            if (!_boatSkill.TryGetValue(skill, out var s) || IsBoatSkillMax(skill) || !_unlockedBoats.Contains(skill)) { onResult?.Invoke(false); return; }
            if (_server == null) { onResult?.Invoke(TryUpgradeBoatSkill(skill)); return; }

            if (string.IsNullOrEmpty(s.id)) { Debug.LogWarning($"[Boat] {skill} id 미설정 — 서버 강화 불가"); onResult?.Invoke(false); return; }

            _server.UpgradeShip(s.id, r =>
            {
                if (r.success)
                {
                    // 성공인데 newServerLevel 비어오면(스키마 드리프트) 0 리셋 대신 낙관적 현재+1 유지(다음 스냅샷 정정).
                    int fallbackLevel = (_boatSkillLevel.TryGetValue(skill, out var curLv) ? curLv : 0) + 1;
                    _boatSkillLevel[skill] = Mathf.Clamp(r.newServerLevel > 0 ? r.newServerLevel - 1 : fallbackLevel, 0, s.MaxLevelIndex);
                    OnBoatSkillUpgraded?.Invoke(skill, _boatSkillLevel[skill]);
                }
                else Debug.LogWarning($"[Boat] {skill} 강화 실패(서버): {r.error}");
                onResult?.Invoke(r.success);
            });
        }

        public void RequestUnlockBoat(BoatSkill skill, Action<bool> onResult = null)
        {
            EnsureInit();
            if (_unlockedBoats.Contains(skill) || !_boatSkill.TryGetValue(skill, out var s)) { onResult?.Invoke(false); return; }
            if (_server == null) { onResult?.Invoke(TryUnlockBoat(skill)); return; }

            if (string.IsNullOrEmpty(s.id)) { Debug.LogWarning($"[Boat] {skill} id 미설정 — 서버 해금 불가"); onResult?.Invoke(false); return; }

            _server.UnlockShip(s.id, r =>
            {
                if (r.success)
                {
                    _unlockedBoats.Add(skill);
                    OnBoatUnlocked?.Invoke(skill);
                }
                else Debug.LogWarning($"[Boat] {skill} 해금 실패(서버): {r.error}");
                onResult?.Invoke(r.success);
            });
        }

        public void RequestEquipBoat(BoatSkill skill, Action<bool> onResult = null)
        {
            EnsureInit();
            if (!_boatSkill.TryGetValue(skill, out var s) || !_unlockedBoats.Contains(skill)) { onResult?.Invoke(false); return; }
            if (_hasEquippedBoat && _equippedBoat == skill) { onResult?.Invoke(true); return; }   // 이미 장착 — 변경 없음
            if (_server == null) { onResult?.Invoke(TryEquipBoat(skill)); return; }

            if (string.IsNullOrEmpty(s.id)) { Debug.LogWarning($"[Boat] {skill} id 미설정 — 서버 장착 불가"); onResult?.Invoke(false); return; }

            _server.EquipShip(s.id, r =>
            {
                if (r.success)
                {
                    _equippedBoat = skill;
                    _hasEquippedBoat = true;
                    OnBoatEquipped?.Invoke(skill);
                }
                else Debug.LogWarning($"[Boat] {skill} 장착 실패(서버): {r.error}");
                onResult?.Invoke(r.success);
            });
        }

        // 로그인 시 서버 동기 데이터(PlayFabDataStore)에서 현재 상태를 덮어쓴다(레벨은 서버 1-base).
        // 기본 해금 배 시드는 유지(합집합). 로드 끝에 장착 배 1회 발행(아래 레이스 방어 주석 참조).
        public void LoadFromServerSnapshot(
            IReadOnlyDictionary<string, int> equipmentServerLevels,
            IReadOnlyDictionary<string, ShipSnapshot> shipsServer)
        {
            EnsureInit();

            if (equipmentServerLevels != null)
                foreach (var kv in _data)
                {
                    var d = kv.Value;
                    if (d == null || d.MaxLevelIndex < 0 || string.IsNullOrEmpty(d.id)) continue;   // 배(levels 없음)·id 없음 제외
                    if (equipmentServerLevels.TryGetValue(d.id, out var serverLevel))
                        _level[kv.Key] = Mathf.Clamp(serverLevel - 1, 0, d.MaxLevelIndex);
                }

            if (shipsServer != null)
            {
                BoatSkill? equipped = null;
                foreach (var kv in _boatSkill)
                {
                    var entry = kv.Value;
                    if (entry == null || string.IsNullOrEmpty(entry.id) || !shipsServer.TryGetValue(entry.id, out var snap)) continue;
                    if (entry.MaxLevelIndex >= 0)
                        _boatSkillLevel[kv.Key] = Mathf.Clamp(snap.level - 1, 0, entry.MaxLevelIndex);
                    if (snap.isOpened) _unlockedBoats.Add(kv.Key);
                    if (snap.equipped) equipped = kv.Key;
                }
                // 장착 배 복원: 서버가 장착으로 표시 + 해금된 경우만. 아니면 기존(기본) 장착 유지.
                if (equipped.HasValue && _unlockedBoats.Contains(equipped.Value))
                {
                    _equippedBoat = equipped.Value;
                    _hasEquippedBoat = true;
                }
            }

            Debug.Log($"[Equipment] 서버 스냅샷 로드 완료 — {DebugSummary()}");

            // 레이스 방어: 로드가 구독자(BoatView/발동컨트롤러)보다 늦게 끝나면, 화면이 옛 기본 배(진공)를
            // 그린 채 갱신을 못 받던 문제가 있었다. 장착 배 확정 후 1회 발행해 최신 배로 동기화한다.
            // 핸들러는 idempotent — BoatView=스프라이트 스왑, 발동컨트롤러=스킬 비우고 재적재.
            if (_hasEquippedBoat) OnBoatEquipped?.Invoke(_equippedBoat);
        }

        // ── 세이브 훅 (열어둠) ─────────────────────────────────
        // 저장 위치 결정 전이라도 값을 주고받는 통로만 미리 연다.
        // 저장 담당: 시작 시 LoadSaveState(불러온값), 저장 시점에 GetSaveState() 결과를 직렬화.

        // 현재 등급들을 직렬화 가능한 형태로 내준다.
        public EquipmentSaveState GetSaveState()
        {
            EnsureInit();

            var eq = new List<EquipmentLevelEntry>(_level.Count);
            foreach (var kv in _level)
                // levels 없는 타입(예: Boat — boatSkills로 강화)은 등급 개념이 없어 저장 제외
                if (_data.TryGetValue(kv.Key, out var d) && d.MaxLevelIndex >= 0)
                    eq.Add(new EquipmentLevelEntry { type = kv.Key, level = kv.Value });

            var boat = new List<BoatSkillLevelEntry>(_boatSkillLevel.Count);
            foreach (var kv in _boatSkillLevel)
                boat.Add(new BoatSkillLevelEntry { skill = kv.Key, level = kv.Value });

            return new EquipmentSaveState
            {
                equipmentLevels = eq.ToArray(),
                boatSkillLevels = boat.ToArray(),
                unlockedBoats = new List<BoatSkill>(_unlockedBoats).ToArray(),
                equippedBoat = _hasEquippedBoat ? (int)_equippedBoat : -1,   // 장착 배 없으면 -1
            };
        }

        // 저장된 등급을 복원. 현재 데이터에 없는 키는 무시, 등급은 유효 범위로 클램프(구버전 세이브 방어).
        // UI 갱신 이벤트는 발행하지 않음(로드는 강화가 아님) — 로드 후 화면 갱신은 호출 측에서.
        public void LoadSaveState(EquipmentSaveState state)
        {
            EnsureInit();
            if (state == null) return;

            if (state.equipmentLevels != null)
                foreach (var e in state.equipmentLevels)
                    // levels 없는 타입(MaxLevelIndex<0, 예: Boat)은 건너뜀 — Clamp가 -1을 만드는 것 방지
                    if (_data.TryGetValue(e.type, out var d) && d.MaxLevelIndex >= 0)
                        _level[e.type] = Mathf.Clamp(e.level, 0, d.MaxLevelIndex);

            if (state.boatSkillLevels != null)
                foreach (var e in state.boatSkillLevels)
                    if (_boatSkill.TryGetValue(e.skill, out var s))
                        _boatSkillLevel[e.skill] = Mathf.Clamp(e.level, 0, s.MaxLevelIndex);

            // 해금 배 복원: 저장된 해금 목록을 현재 데이터에 존재하는 것만 합집합(기본 해금 배는 init 시드로 항상 유지 → 구버전 세이브도 안전).
            if (state.unlockedBoats != null)
                foreach (var sk in state.unlockedBoats)
                    if (_boatSkill.ContainsKey(sk)) _unlockedBoats.Add(sk);

            // 장착 배 복원. -1(미지정)이거나 미존재/미해금이면 기본 장착 유지(구버전/손상 세이브 방어).
            // 이벤트 미발행(로드는 강화·조작이 아님) — 로드 후 발동 컨트롤러는 EquippedBoat를 직접 읽어 적재.
            if (state.equippedBoat >= 0
                && _boatSkill.ContainsKey((BoatSkill)state.equippedBoat)
                && _unlockedBoats.Contains((BoatSkill)state.equippedBoat))
            {
                _equippedBoat = (BoatSkill)state.equippedBoat;
                _hasEquippedBoat = true;
            }
        }

#if UNITY_EDITOR
        private static EquipmentData[] LoadAllEquipmentData()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:EquipmentData");
            return Array.ConvertAll(guids, g =>
                UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentData>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(g)));
        }

        [ContextMenu("에디터: 모든 EquipmentData 자동 등록")]
        private void Editor_AutoLoadEquipments()
        {
            _equipments = LoadAllEquipmentData();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[Equipment] EquipmentData {_equipments.Length}개 자동 등록 완료");
        }
#endif
    }
}
