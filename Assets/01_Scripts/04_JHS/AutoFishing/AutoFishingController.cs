using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JHS.Equipment;
using RMS.Data;      // FishData
using RMS.Fishing;   // FishSpawnManager (팀 결정: 인터페이스 없이 직접 호출)

namespace JHS.Fishing
{
    // 자동 낚시 루프. 토글 없음 — 항상 자동, 입질 때 상호작용하면 그 1회만 수동 분기.
    // Cast → 입질 → (무입력) AutoResolve / (상호작용) 수동 미니게임 → 다음 Cast
    public class AutoFishingController : MonoBehaviour
    {
        [Header("튜닝값 (임시)")]
        [SerializeField] private float _baseReward = 1.3f;
        [Range(0f, 1f)][SerializeField] private float _autoPenalty = 0.3769231f;  // 방치 패널티
        [Tooltip("수동 낚시 보상 배수(수동이 자동보다 유리하도록). 값은 밸런스 영역.")]
        [SerializeField] private float _manualBonus = 2f;                   // 수동 보너스(수동 보상 배수)
        [SerializeField] private float _baseCastInterval = 2f;
        [SerializeField] private float _biteWaitMin = 1f;                   // 입질까지 대기
        [SerializeField] private float _biteWaitMax = 3f;
        [SerializeField] private float _interactWindow = 1.5f;             // 상호작용 받는 시간

        [Header("에디터 테스트")]
        [SerializeField] private bool _useDummyManualForTest = false;       // 더미 미니게임 끼우기
        [SerializeField] private ManualResult _dummyManualResult = ManualResult.Success;

        [Header("보스 전투")]
        [Tooltip("수동 미니게임 성공 시 보스 피해량. 자동 공격은 BossData.BaseDamagePerHit 사용.")]
        [SerializeField] private int _manualBossDamage = 30;

        [Header("수동낚시 확률적 다중 포획")]
        [Tooltip("수동 낚시 성공(Reeling 성공) 시 같은 fish가 추가로 더 낚일 확률/마릿수. " +
                 "수동낚시 전용 고정값 — 희귀도가 높을수록 발동 확률은 낮아진다.")]
        [SerializeField] private float _commonMultiCatchChance = 0.35f;
        [SerializeField] private float _rareMultiCatchChance = 0.25f;
        [SerializeField] private float _epicMultiCatchChance = 0.15f;
        [SerializeField] private float _legendaryMultiCatchChance = 0.05f;

        [Tooltip("Common/Rare 발동 시 마릿수 범위")]
        [SerializeField] private int _multiCatchCountMin = 2;
        [SerializeField] private int _multiCatchCountMax = 3;

        [Header("보스전 다중 피격")]
        [Tooltip("고정 확률/배율로 독립 변수처럼 둔다. 발동 시 1회 피해량을 이 배율만큼 곱해 한 번에 적용한다.")]
        [SerializeField] private float _bossMultiCatchChance = 0.10f;
        [SerializeField] private int _bossMultiCatchMultiplier = 2;

        // 포획 시 발행 (잡힌 물고기, 기여도, 수동여부). 물고기는 FishSource 미연결 시 null.
        public event Action<FishData, float, bool> OnFishCaught;

        // 장비 다중포획이 "터진" 순간 발행 (마릿수, count>1일 때만). 연출(ActiveEffectHUD)이 구독해
        // 메인화면 다중포획 아이콘을 번쩍/xN 팝으로 보여준다. 컨트롤러는 구독자를 모른다(연출 분리).
        public event Action<int> OnMultiCatch;

        // 루프 단계가 바뀔 때 발행. 연출(FishingView)이 구독해 애니메이션/이펙트로 번역.
        // 컨트롤러는 구독자가 무엇을 하는지 모른다 — 신호만 쏜다(연출 분리).
        public event Action<FishingPhase> OnPhaseChanged;
        private void SetPhase(FishingPhase phase) => OnPhaseChanged?.Invoke(phase);

        // 입질 순간 '확정된 물고기'를 발행(Bite 직전). 연출이 잡히기 전부터 등급별 연출(전설 오라 등)을
        // 깔 수 있게 하는 통로. null이면 보스/미연결. 컨트롤러는 구독자를 모른다(연출 분리).
        public event Action<FishData> OnFishHooked;

        // 보스 스테이지에서 보스를 '타격'할 때마다 발행(타격당 1회). 인자=(수동여부, 다중배율).
        // 구독측(사운드/연출)이 자동/수동/다중을 구분해 처리. 컨트롤러는 구독자를 모른다(연출 분리).
        public event Action<bool, int> OnBossHit;

        [Header("디버그/테스트")]
        [Tooltip("켜면 입질마다 전설 물고기를 강제로 물게 한다(연출·사운드 테스트용). 끄면 정상 확률.")]
        [SerializeField] private bool _forceLegendaryCatch = false;
        [Tooltip("강제할 전설 물고기(선택). 비우면 에디터에서 프로젝트의 전설 물고기를 자동 탐색.")]
        [SerializeField] private FishData _forceLegendaryFish;
        [Tooltip("켜면 입질마다 에픽 물고기를 강제로 물게 한다(전설 토글이 우선). 끄면 정상 확률.")]
        [SerializeField] private bool _forceEpicCatch = false;
        [Tooltip("강제할 에픽 물고기(선택). 비우면 에디터에서 프로젝트의 에픽 물고기를 자동 탐색.")]
        [SerializeField] private FishData _forceEpicFish;

        [Header("연동 (선택)")]
        [SerializeField] private BoatActiveSkillController _boat;  // 있으면 배 액티브 버프 반영
        [SerializeField] private FishSpawnManager _spawnManager;   // 입질 시 물고기 선정 (RMS). 없으면 씬에서 자동 탐색

        // 다른 파트가 채우는 의존성. 주입 안 하면 더미로 동작.
        private IEquipmentEffects _equipOverride;   // SetEquipment로 명시 주입 시 우선
        private DummyEquipmentEffects _dummyEquip;
        private ICrewBonus _crew;
        private IManualFishing _manual;   // null이면 항상 자동만

        // 자동 보상에 적용되는 효과 파이프라인. 새 효과는 여기에 Add (AutoResolve 수정 불필요).
        private readonly List<IRewardModifier> _rewardModifiers = RewardPipeline.CreateDefault();
        private Func<float> _rng = () => UnityEngine.Random.value;   // 테스트 시 교체 가능

        // 우선순위: 명시 주입 > 씬의 EquipmentManager(켜진 시점 무관) > 더미
        private IEquipmentEffects Equip =>
            _equipOverride
            ?? (EquipmentManager.Instance as IEquipmentEffects)
            ?? (_dummyEquip ??= new DummyEquipmentEffects());

        // 활성화 게이트: 메인스테이지에 있을 때만 낚시. 어댑터(FishingStateActivator)가 SetGameplayActive로 갱신.
        // 기본 true — 어댑터 없이(예: JHS 단독 테스트 씬) 켜도 바로 동작. 어댑터가 있으면 실제 상태로 덮어씀.
        [Header("활성화 게이트")]
        [SerializeField] private bool _inMainStage = true;

        private bool _interactRequested;  // 입력 신호 (다음 입질에서 소비)
        private int _catchCount;
        private Coroutine _loop;
        private bool _penguinPrimed;   // 펭귄 발동 시 다음 1마리를 다음 스테이지 물고기로 예약



        private void OnEnable()
        {
            ResolveDependencies();
            // 스테이지 변경 구독
            if (_spawnManager != null)
                _spawnManager.OnStageChanged += OnStageChanged;
            // 펭귄(유인용 로봇 펭귄) 발동 구독 (중복 구독 방지)
            if (_boat != null)
            {
                _boat.OnDecoyPenguin -= OnDecoyPenguin;
                _boat.OnDecoyPenguin += OnDecoyPenguin;
            }
            RefreshLoopState();
        }

        private void OnDisable()
        {
            if (_spawnManager != null)
                _spawnManager.OnStageChanged -= OnStageChanged;
            if (_boat != null)
                _boat.OnDecoyPenguin -= OnDecoyPenguin;
            StopLoop();
        }


        // 장비는 Equip 프로퍼티가 매번 해석하므로 여기선 선원/미니게임/배만.
        private void ResolveDependencies()
        {
            // 테스트용 더미 미니게임(명시 옵션)이 켜져 있으면 그게 우선.
            if (_useDummyManualForTest && _manual == null)
                _manual = new DummyManualFishing(_dummyManualResult);
            // 배가 다른 오브젝트에 있어도 자동 연결 (인스펙터에 직접 넣으면 그게 우선)
            if (_boat == null) _boat = FindFirstObjectByType<BoatActiveSkillController>();
            // 스폰매니저도 동일 패턴 (인스펙터 우선, 없으면 씬에서 탐색). 미발견 시 SelectFishOnBite는 null.
            if (_spawnManager == null) _spawnManager = FindFirstObjectByType<FishSpawnManager>();

            // 선원·미니게임은 남 모듈(PSY/RMS) 구현체 — 인터페이스로 발견해 주입한다(남 코드 무수정, DIP).
            // 명시 주입(SetCrew/SetManualFishing)이나 더미 옵션이 있으면 그게 우선, 없을 때만 씬에서 탐색.
            // (더미는 MonoBehaviour가 아니라 이 스캔에 안 걸림 → 실제 구현체만 발견된다)
            if (_crew == null || _crew is DummyCrewBonus || _manual == null)
            {
                // 미니게임 패널은 평소 비활성(꺼진 UI) 상태라 비활성 오브젝트까지 포함해 탐색해야 잡힌다.
                var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (_crew == null || _crew is DummyCrewBonus)
                    _crew = behaviours.OfType<ICrewBonus>().FirstOrDefault();
                if (_manual == null)
                {
                    var found = behaviours.OfType<IManualFishing>().FirstOrDefault();
                    if (found != null) SetManualFishing(found);
                }
            }
            _crew ??= new DummyCrewBonus();   // 못 찾으면 더미 유지 (단독 테스트씬 대비)

            // 장비 레어도 가중치 보정(찌 RarityWeightBonus)과 선원 레어도 보정을 RMS 물고기 선정에 반영 — 스폰매니저 공개 API로 주입.
            // (JHS가 장비·낚시 루프를 소유하므로 주입도 여기서. RMS 코드는 안 건드림. 미주입 시 스폰매니저는 상한 없음으로 폴백)
            if (_spawnManager != null)
            {
                var eq = _equipOverride ?? (EquipmentManager.Instance as IEquipmentEffects);
                if (eq != null) _spawnManager.SetEquipment(eq);
                if (_crew != null) _spawnManager.SetCrew(_crew);
                if (_boat == null) _boat = FindFirstObjectByType<BoatActiveSkillController>();
                if (_boat != null) _spawnManager.SetBoatRarity(_boat);   // 야근전등 레어도 보정 주입
            }
        }


        // 외부 주입 API
        public void SetEquipment(IEquipmentEffects equip) => _equipOverride = equip ?? _equipOverride;
        public void SetCrew(ICrewBonus crew) => _crew = crew ?? _crew;
        public void SetManualFishing(IManualFishing minigame)
        {
            _manual = minigame;

            if (minigame is ManualFishingMinigame mmg)
            {
                mmg.SetEquipment(Equip);
                mmg.SetCrew(_crew);
            }
        }

        // 보상 효과 추가/난수원 교체 (확장·테스트용)
        public void AddRewardModifier(IRewardModifier modifier) { if (modifier != null) _rewardModifiers.Add(modifier); }
        public void SetRng(Func<float> rng) => _rng = rng ?? _rng;

        // 입질 때 사용자가 누르면 호출
        public void RequestInteraction() => _interactRequested = true;

        // ── 활성화 게이트 ──────────────────────────────────────
        // 외부(상태 어댑터)에서 메인스테이지 진입/이탈을 알려준다. 들어오는 명령은 이 하나뿐.
        public void SetGameplayActive(bool active)
        {
            _inMainStage = active;
            RefreshLoopState();
        }

        // 현재 보스 스테이지인가 (스폰매니저·스테이지 없으면 보스 아님으로 간주).
        private bool IsBossStage =>
            _spawnManager != null && _spawnManager.CurrentStage != null
            && _spawnManager.CurrentStage.IsBossStage;

        // 낚시가 돌아야 하는 조건: 메인스테이지.
        private bool ShouldFish => _inMainStage;

        // 모든 켜고끔은 이 함수 하나를 거친다(조건 재평가). 트리거: OnEnable / 스테이지변경 / 상태어댑터.
        // 보스 진입/재활성/세이브로드 누수(기여도 새던 버그)도 이 단일 조건으로 일괄 방어.
        private void RefreshLoopState()
        {
            bool active = ShouldFish;
            if (active) StartLoop();
            else { StopLoop(); SetPhase(FishingPhase.Idle); }   // 안 잡을 땐 쉬는 포즈로
            _boat?.SetGameplayActive(active && !IsBossStage);   // 배 액티브 스킬은 메인스테이지 && 보스 아님일 때만(보스전엔 효과가 소비되지 않아 연출까지 정지)
        }

        private void StartLoop()
        {
            if (_loop == null) _loop = StartCoroutine(FishingLoop());
        }

        private void StopLoop()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        }

        private IEnumerator FishingLoop()
        {
            while (true)
            {
                // 던지기 + 입질 대기 (의자 등급이 높을수록 자주 뭄)
                SetPhase(FishingPhase.Casting);
                float baseWait = UnityEngine.Random.Range(_biteWaitMin, _biteWaitMax);
                float wait = RewardCalculator.CalcBiteWait(baseWait, Equip.GetStat(EquipmentStat.BiteIntervalReduce, 0f));
                SetPhase(FishingPhase.Waiting);
                yield return new WaitForSeconds(wait);

                // 🎣 입질 — 이 시점에 어떤 물고기인지 확정한다.
                // (미니게임이 그 물고기의 난이도(ManualDifficulty)를 읽어야 하고, 포획 시 보스 해금 기록도 필요)
                FishData fish = IsBossStage ? null : SelectFishOnBite();
                OnFishHooked?.Invoke(fish);   // 연출에 먼저 통지(전설 오라 등) → Bite 단계 핸들러가 등급을 알 수 있게
                SetPhase(FishingPhase.Bite);

                // 상호작용 창
                bool interacted = false;
                float t = 0f;
                while (t < _interactWindow)
                {
                    if (_interactRequested) { interacted = true; break; }
                    t += Time.deltaTime;
                    yield return null;
                }
                _interactRequested = false;

                // 분기 (입질 때 정한 물고기를 그대로 흘려보냄)
                SetPhase(FishingPhase.Reeling);
                if (interacted && _manual != null)
                {
                    ManualResult result = ManualResult.Idle;
                    if (IsBossStage)
                        // 보스 스테이지 - BossData 기반 난이도로 미니게임 실행
                        yield return _manual.RunMiniGame(_spawnManager.CurrentBossData, r => result = r);
                    else
                        // 일반 스테이지 - 입질 때 확정한 물고기 기반으로 미니게임 실행
                        yield return _manual.RunMiniGame(fish, r => result = r);
                    ResolveManual(result, fish);
                }
                else
                {
                    AutoResolve(fish);
                }

                // 다음 캐스팅 간격 (선원 속도 반영)
                yield return new WaitForSeconds(
                    RewardCalculator.CalcCastInterval(_baseCastInterval, _crew.AutoSpeedMultiplier));
            }
        }

        // 입질 시 어떤 물고기인지 확정. 레어도 가중치(장비 RarityWeightBonus·선원 보정) 반영은 SelectFish 내부가 처리.
        // 팀 결정(2026-06-08): 인터페이스 추상화 없이 FishSpawnManager.SelectFish() 직접 호출.
        // 스폰매니저 미연결 시 null(더미)로 안전 동작.
        private FishData SelectFishOnBite()
        {
            // [디버그] 등급 확정 토글: 켜져 있으면 정상 선택을 건너뛰고 해당 등급을 물게 한다(전설 우선).
            if (_forceLegendaryCatch)
            {
                FishData forced = ResolveForced(_forceLegendaryFish, FishRarity.Legendary);
                if (forced != null) return forced;
            }
            if (_forceEpicCatch)
            {
                FishData forced = ResolveForced(_forceEpicFish, FishRarity.Epic);
                if (forced != null) return forced;
            }

            if (_spawnManager == null) return null;

            // 펭귄 예약 시 다음 1마리를 다음 스테이지 물고기로 치환 (다음이 보스/없으면 null → 정상 폴백)
            if (_penguinPrimed)
            {
                FishData next = _spawnManager.SelectFishFromNextStage();
                if (next != null)
                {
                    _penguinPrimed = false;
                    return next;
                }
            }
            return _spawnManager.SelectFish();
        }

        // [디버그] 등급 확정 토글이 쓸 물고기. 지정(explicit) 없으면 에디터에서 그 등급(아이콘 보유) 1종을 자동 탐색.
        private FishData ResolveForced(FishData explicitFish, FishRarity rarity)
        {
            if (explicitFish != null) return explicitFish;
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FishData"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var f = UnityEditor.AssetDatabase.LoadAssetAtPath<FishData>(path);
                if (f != null && f.Rarity == rarity && f.Icon != null) return f;
            }
#endif
            return null;
        }

        // 펭귄(유인용 로봇 펭귄) 발동: 다음 1마리를 다음 스테이지 물고기로 예약. 현재가 보스면 예약 안 함.
        private void OnDecoyPenguin(float value)
        {
            if (IsBossStage) return;
            _penguinPrimed = true;
            Debug.Log("[AutoFishing] 유인용 로봇 펭귄 발동 — 다음 1마리 다음 스테이지 물고기 예약");
        }

        // 자동 보상 — 선원 패시브를 시드로, 등록된 효과(다중 포획·배 액티브 등)를 순서대로 적용.
        // 효과 추가/변경은 _rewardModifiers(RewardPipeline)에서. 이 메서드는 효과 종류를 모른다.
        private void AutoResolve(FishData fish)
        {
            // per-fish 기여도: "값 배율"(원격 goldMultiplier 등)만 적용. 마릿수 배율은 아래 count로 분리한다.
            float reward = RewardCalculator.CalcAutoReward(_baseReward, _autoPenalty, _crew.AutoPassiveBonus);

            var ctx = new RewardContext(Equip, _boat != null ? _boat.CatchMultiplier : 1f, _rng);
            for (int i = 0; i < _rewardModifiers.Count; i++)
                reward = _rewardModifiers[i].Apply(reward, in ctx);

            // 마릿수: 다중포획(장비 확률→마릿수) × 배 진공흡입기(발동 중 배율). 마릿수만큼 GiveFish 호출
            // → 인벤토리 지급(OnFishCaught)과 기여도(RegisterCatch)가 함께 N마리분으로 반영된다.
            // (보스 스테이지는 마릿수 개념 없음 — GiveBossHit이 자체 다중피격 처리하므로 1로 고정)
            int count = IsBossStage ? 1 : RollAutoCatchCount();
            for (int i = 0; i < count; i++)
                GiveFish(fish, reward, isManual: false, suppressLog: count > 1);

            if (count > 1 && fish != null)
            {
                Debug.Log($"<color=#39FF14>[AutoFishing] #{_catchCount} 자동 다중 포획! {fish.DisplayName}({fish.Rarity}) x{count}마리</color>");
                OnMultiCatch?.Invoke(count);   // 메인화면 다중포획 피드백
            }

            SetPhase(FishingPhase.Success);   // 자동은 항상 포획
        }

        /*
            자동 낚시 마릿수 판정 (일반 스테이지). GiveFish 호출 횟수 = 인벤토리·기여도에 함께 반영되는 마릿수.
            - 다중포획: 장비 MultiCatchChance 확률로 발동 → MultiCatchCount(찌, 정수 마릿수)마리.
            - 배 진공흡입기: 발동 중 CatchMultiplier(>1)만큼 마릿수 증가(소수는 반올림). 미발동이면 1.
            반환 1 = 미발동(1마리).
        */
        private int RollAutoCatchCount()
        {
            int count = 1;
            if (_rng() < Equip.GetStat(EquipmentStat.MultiCatchChance, 0f))
                count = Mathf.Max(1, Mathf.RoundToInt(Equip.GetStat(EquipmentStat.MultiCatchCount, 1f)));

            float boatMult = _boat != null ? _boat.CatchMultiplier : 1f;   // 진공 흡입기 발동 중이면 >1
            if (boatMult > 1f)
                count = Mathf.Max(count, Mathf.RoundToInt(count * boatMult));

            return Mathf.Max(1, count);
        }

        private void ResolveManual(ManualResult result, FishData fish)
        {
            switch (result)
            {
                case ManualResult.Idle: AutoResolve(fish); break;                    // 자동 폴백 (Success는 AutoResolve가 발행)
                case ManualResult.Success: ResolveManualSuccess(fish); break;
                case ManualResult.Fail: SetPhase(FishingPhase.Fail); break;          // 보상 0 (놓침)
            }
        }


        /*
            수동 낚시 성공 처리.
            - 일반 스테이지: fish.Rarity 기반 확률적 다중 포획(RollManualMultiCatchCount)을 굴려 마릿수를 정한다.
            - 보스 스테이지: 보스는 별도의 고정 확률/배율(RollBossMultiCatchMultiplier)로
              독립 판정하고, 발동 시 1회 피해량에 배율을 곱해 한 번에 적용한다(물고기 지급 없음).
        */
        private void ResolveManualSuccess(FishData fish)
        {
            if (IsBossStage)
            {
                int multiplier = RollBossMultiCatchMultiplier();
                GiveBossHit(isManual: true, multiCatchCount: multiplier);
            }
            else
            {
                int catchCount = fish != null ? RollManualMultiCatchCount(fish.Rarity) : 1;
                float manualReward = RewardCalculator.CalcManualReward(_baseReward, _manualBonus);  // 수동 보너스 배수 적용

                for (int i = 0; i < catchCount; i++)
                    GiveFish(fish, manualReward, isManual: true, suppressLog: catchCount > 1);

                if (catchCount > 1)
                {
                    Debug.Log($"<color=#39FF14>[AutoFishing] #{_catchCount} 수동낚시 다중 포획! {fish.DisplayName}({fish.Rarity}) x{catchCount}마리 +{manualReward * catchCount:0.##} 기여도</color>");
                    OnMultiCatch?.Invoke(catchCount);   // 메인화면 다중포획 피드백
                }
            }

            SetPhase(FishingPhase.Success);
        }


        /*
            수동낚시 확률적 다중 포획 판정 (일반 스테이지, 싱글/멀티 공통).
            희귀도가 높을수록 발동 확률은 낮아진다(Common 35% > Rare 25% > Epic 15% > Legendary 5%).
            발동 시 마릿수: 모든 희귀도 공통 2~3 랜덤.
            장비 MultiCatchChance/Count(자동낚시 전용 RewardModifiers)와는 별개의 수동낚시 전용 고정값.
            반환값 1 = 다중 포획 미발동(기존과 동일하게 1마리분).
        */
        private int RollManualMultiCatchCount(FishRarity rarity)
        {
            float chance = GetManualMultiCatchChance(rarity);
            if (_rng() >= chance) return 1;

            return UnityEngine.Random.Range(_multiCatchCountMin, _multiCatchCountMax + 1); // Range(int,int)는 max 미포함이라 +1
        }

        private float GetManualMultiCatchChance(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Rare: return _rareMultiCatchChance;
                case FishRarity.Epic: return _epicMultiCatchChance;
                case FishRarity.Legendary: return _legendaryMultiCatchChance;
                default: return _commonMultiCatchChance;
            }
        }


        /*
            보스전 확률적 다중 피격 판정. 
            보스마다 동일한 고정 확률/배율을 쓴다(장비/선원 강화가 이미
            보스 피해량 성장을 담당하므로 다중 포획(피격)은 독립적인 보너스 변수로 둔다).
            반환값 1 = 다중(피격) 포획 미발동(기존과 동일하게 피해량 배율 없음).
        */
        private int RollBossMultiCatchMultiplier()
        {
            return _rng() < _bossMultiCatchChance ? Mathf.Max(1, _bossMultiCatchMultiplier) : 1;
        }


        /*
            보스 피격 전용 경로 (자동/수동 공통 호출). GiveFish의 보스 분기를 분리한 버전.
            multiCatchCount: 수동낚시 확률적 다중 포획(RollBossMultiCatchMultiplier)이 발동했으면
            2 이상 — 피해량 자체를 그만큼 배율로 곱해 RegisterBossHit을 1회만 호출한다.
            보스는 희귀도 개념이 없어 물고기 정보를 따로 받지 않는다(자동 공격이나 다중 포획
            미발동 시엔 1).
        */
        private void GiveBossHit(bool isManual, int multiCatchCount)
        {
            _catchCount++;
            OnBossHit?.Invoke(isManual, multiCatchCount);   // 타격당 1회 신호(자동/수동/다중 구분은 구독측이)
            BossData boss = _spawnManager?.CurrentBossData;
            int equipBossDmg = Mathf.RoundToInt(Equip.GetStat(EquipmentStat.BossDamage, 0f));

            if (isManual)
            {
                int baseDmg = boss != null && boss.ManualDamagePerHit > 0
                    ? boss.ManualDamagePerHit
                    : _manualBossDamage;
                int manualDmg = Mathf.RoundToInt((baseDmg + equipBossDmg) * _crew.BossFishBonus) * Mathf.Max(1, multiCatchCount);
                _spawnManager?.RegisterBossHit(manualDmg, isManual: true);

                if (multiCatchCount > 1)
                    Debug.Log($"<color=#39FF14>[AutoFishing] #{_catchCount} 보스전 다중 피격! x{multiCatchCount}배 보스 피격 -{manualDmg} (기본:{baseDmg} + 장비:{equipBossDmg} × 선원보정:{_crew.BossFishBonus:0.##} × {multiCatchCount}배)</color>");
                else
                    Debug.Log($"[AutoFishing] #{_catchCount} 수동 보스 피격 -{manualDmg} (기본:{baseDmg} + 장비:{equipBossDmg} × 선원보정:{_crew.BossFishBonus:0.##})");
            }
            else
            {
                int baseDmg = boss?.BaseDamagePerHit ?? 0;
                int autoDmg = (baseDmg + equipBossDmg) * Mathf.Max(1, multiCatchCount);
                _spawnManager?.RegisterBossHit(autoDmg, isManual: false);
                Debug.Log($"[AutoFishing] #{_catchCount} 자동 보스 피격 -{autoDmg} (기본:{baseDmg} + 장비:{equipBossDmg})");
            }
        }


        private void GiveFish(FishData fish, float contribution, bool isManual, bool suppressLog = false)
        {
            if (IsBossStage)
            {
                GiveBossHit(isManual, multiCatchCount: 1);  // _catchCount 증가는 GiveBossHit 내부에서 1회만
                return;
            }

            _catchCount++;

            // 일반 스테이지에서만 fish null 체크
            if (fish == null)
            {
                Debug.LogWarning($"[AutoFishing] GiveFish: fish=null (스테이지={_spawnManager?.CurrentStage?.StageId ?? "없음"}). 물고기 미지급.");
                return;
            }

            _spawnManager?.RegisterCatch(fish, contribution);
            OnFishCaught?.Invoke(fish, contribution, isManual);

            if (!suppressLog)
                Debug.Log($"[AutoFishing] #{_catchCount} {(isManual ? "수동" : "자동")} {fish.DisplayName}  +{contribution:0.##} 기여도");
        }

        private void OnStageChanged(StageData stage)
        {
            // 스테이지가 바뀌면 조건 재평가 (보스 진입/이탈 반영).
            RefreshLoopState();
        }

#if UNITY_EDITOR
        [ContextMenu("Test/입질 때 상호작용(RequestInteraction)")]
        private void Editor_RequestInteraction() => RequestInteraction();
#endif
    }
}