using JHS.Equipment;
using JHS.Fishing;
using RMS.Data;
using RMS.UI;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace RMS.Fishing
{
    // 현재 스테이지의 FishData 목록에서 가중치 + 희귀도 기반으로 물고기를 선택하고,
    // 포획 기록을 관리하며 스테이지 클리어 조건을 체크한다.
    // 보스 스테이지에서는 HP 추적 및 피해 처리를 담당한다.
    public class FishSpawnManager : MonoBehaviour
    {
        [Header("스테이지 설정")]
        [Tooltip("게임 시작 시 첫 번째 스테이지")]
        [SerializeField] private StageData _currentStage;

        // 스테이지 클리어시 진행 여부를 판단하는 bool - 0625 YWJ 추가
        [Header("스테이지 진행 여부")]
        [SerializeField] private bool _canMoveToNextStage = true;

        [Tooltip("스테이지 체인의 시작점. 보스 클리어 보상 산정에 사용.")]
        [SerializeField] private StageData _firstStage;
        [SerializeField] private BossRewardPanel _bossRewardPanel;


        // 장비 레어도 가중치 보정
        private IEquipmentEffects _equipment;
        private DummyEquipmentEffects _dummyEquip;
        private IEquipmentEffects Equipment =>
            _equipment ?? (_dummyEquip ??= new DummyEquipmentEffects());

        // 선원 레어도 보정 (FishRarityBonus). 주입 안 하면 0으로 폴백.
        private ICrewBonus _crew;

        // 배 액티브 스킬(야근전등) 레어도 보정. 주입 안 하면 0으로 폴백.
        private IBoatRarityProvider _boatRarity;

        // 현재 스테이지에서 포획한 물고기 종류 기록 (fishId 기준)
        private readonly HashSet<string> _caughtFishIds = new HashSet<string>();

        // 현재 스테이지 누적 기여도
        private float _totalContribution = 0f;

        // 스테이지 클리어 여부 (중복 발행 방지)
        private bool _stageCleared = false;

        // 이전 스테이지 참조 (보스 실패 시 복귀용)
        private StageData _previousStage;

        // 보스 스테이지 런타임 상태
        private int _currentBossHp = 0;
        private bool _bossCleared = false;

        // 멀티플레이 전용 누적 물고기 풀.
        private FishEntry[] _multiplayFishPool;

        // 멀티플레이에서 레어도 가중치 기준으로 쓸 스테이지.
        private StageData _multiplayRarityWeightStage;





        // 외부 공개 -----
        public StageData CurrentStage => _currentStage;
        public StageData FirstStage => _firstStage;
        public float TotalContribution => _totalContribution;

        // 0625 YWJ 추가
        public bool CanMoveToNextStage
        {
            get { return _canMoveToNextStage; }
            set
            {
                _canMoveToNextStage = value;
            }
        }

        // 보스 공개 프로퍼티
        public BossData CurrentBossData =>
            _currentStage != null && _currentStage.IsBossStage ? _currentStage.BossData : null;
        public int CurrentBossHp => _currentBossHp;
        public int CurrentBossMaxHp => CurrentBossData != null ? CurrentBossData.MaxHp : 0;

        public event Action<StageData> OnStageCleared;
        public event Action<StageData> OnStageChanged;

        // (currentHp, maxHp) - UI가 구독해서 HP바 갱신
        public event Action<int, int> OnBossHpChanged;

        // (damage, isManual) - 피격 연출(데미지 넘버/플래시/공격 이펙트)용. HP 갱신과는 별개.
        // isManual=true면 수동 낚시 성공으로 인한 피격.
        public event Action<int, bool> OnBossDamaged;

        // 보스 처치 완료 - UI가 구독해서 클리어 연출 재생
        public event Action<BossData> OnBossCleared;

        // 보스 제한 시간 초과 - UI가 구독해서 실패 연출 재생
        public event Action OnBossTimeLimitExpired;


        // 생명 주기
        private void Start()
        {
            OnStageCleared += _ => MoveToNextStage();
        }


        // 장비 주입 -----
        // 장비 시스템이 준비되면 외부에서 주입. 없으면 레어도 가중치 보정 미적용.
        public void SetEquipment(IEquipmentEffects equip) => _equipment = equip ?? _equipment;

        // 선원 주입 -----
        // 선원 시스템이 준비되면 외부에서 주입. 없으면 선원 레어도 보정 미적용.
        public void SetCrew(ICrewBonus crew) => _crew = crew ?? _crew;

        // 배 주입 -----
        // 배 액티브 스킬(야근전등) 레어도 보정 주입. 없으면 미적용. (JHS BoatActiveSkillController가 IBoatRarityProvider 구현)
        public void SetBoatRarity(IBoatRarityProvider boat) => _boatRarity = boat ?? _boatRarity;


        // 멀티플레이 풀 주입 -----
        public void SetMultiplayFishPool(FishEntry[] pool, StageData rarityWeightStage)
        {
            _multiplayFishPool = pool;
            _multiplayRarityWeightStage = rarityWeightStage;
        }


        // 물고기 선택 -----
        // 현재 스테이지의 FishEntry 목록에서
        // 최종 가중치 = FishEntry.spawnWeight x (스테이지 레어도 가중치 + 장비 보정)
        // 공식으로 물고기 1종을 선택해 반환.
        public FishData SelectFish()
        {
            // 멀티플레이 풀이 주입된 경우, 본인의 maxStageId가 보스 스테이지를 가리켜도
            // 정상적으로 동작해야 하므로 _currentStage.IsBossStage 가드를 건너뛴다.
            bool isMultiplay = _multiplayFishPool != null;
            if (!isMultiplay && (_currentStage == null || _currentStage.IsBossStage))
                return null;

            // 멀티플레이 풀이 주입되어 있으면 그것을, 없으면 싱글 모드처럼 현재 스테이지 풀을 사용
            FishEntry[] entries = isMultiplay ? _multiplayFishPool : _currentStage.FishEntries;
            if (entries == null || entries.Length == 0)
                return null;

            // 레어도 가중치 기준 스테이지: 멀티에서는 주입된 전용 스테이지, 싱글에서는 현재 스테이지
            StageData rarityWeightStage = isMultiplay ? _multiplayRarityWeightStage : _currentStage;
            if (rarityWeightStage == null)
                return null;

            // 장비 보정값 + 선원 보정값 (모든 레어도 가중치에 동일하게 합산)
            float equipBonus = Equipment.GetStat(EquipmentStat.RarityWeightBonus, 0f);
            float crewBonus = _crew != null ? _crew.FishRarityBonus : 0f;
            float boatBonus = _boatRarity != null ? _boatRarity.RarityBonus : 0f;
            float rarityBonus = equipBonus + crewBonus + boatBonus;

            // 전체 가중치 합산
            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].fishData == null) continue;
                float rarityW = Mathf.Max(0.01f, rarityWeightStage.GetRarityWeight(entries[i].fishData.Rarity) + rarityBonus);
                totalWeight += entries[i].spawnWeight * rarityW;
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning("[FishSpawnManager] 선택 가능한 물고기가 없습니다. 희귀도 상한을 확인하세요.");
                return null;
            }

            // 가중치 기반 룰렛 휠 선택
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].fishData == null) continue;
                float rarityW = Mathf.Max(0.01f, rarityWeightStage.GetRarityWeight(
                    entries[i].fishData.Rarity) + rarityBonus);

                cumulative += entries[i].spawnWeight * rarityW;
                if (roll <= cumulative)
                    return entries[i].fishData;
            }

            // 부동소수점 오차 방어: 마지막 유효 항목 반환
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                if (entries[i].fishData == null) continue;
                float rarityW = Mathf.Max(0.01f, rarityWeightStage.GetRarityWeight(
                    entries[i].fishData.Rarity) + rarityBonus);
                if (rarityW > 0f) return entries[i].fishData;
            }

            return null;
        }

        // 펭귄(유인용 로봇 펭귄): 다음 스테이지 물고기 1종 선택. 다음 스테이지가 없거나 보스면 null.
        // SelectFish와 동일한 가중치 규칙(스테이지 레어도 + 장비/선원/배 보정)을 다음 스테이지에 적용. (JHS AutoFishingController가 펭귄 발동 시 호출)
        public FishData SelectFishFromNextStage()
        {
            StageData stage = _currentStage != null ? _currentStage.NextStage : null;
            if (stage == null || stage.IsBossStage)
                return null;

            FishEntry[] entries = stage.FishEntries;
            if (entries == null || entries.Length == 0)
                return null;

            float equipBonus = Equipment.GetStat(EquipmentStat.RarityWeightBonus, 0f);
            float crewBonus = _crew != null ? _crew.FishRarityBonus : 0f;
            float boatBonus = _boatRarity != null ? _boatRarity.RarityBonus : 0f;
            float rarityBonus = equipBonus + crewBonus + boatBonus;

            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].fishData == null) continue;
                float rarityW = Mathf.Max(0.01f, stage.GetRarityWeight(entries[i].fishData.Rarity) + rarityBonus);
                totalWeight += entries[i].spawnWeight * rarityW;
            }
            if (totalWeight <= 0f)
                return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].fishData == null) continue;
                float rarityW = Mathf.Max(0.01f, stage.GetRarityWeight(entries[i].fishData.Rarity) + rarityBonus);
                cumulative += entries[i].spawnWeight * rarityW;
                if (roll <= cumulative) return entries[i].fishData;
            }

            // 부동소수점 오차 방어: 마지막 유효 항목 반환
            for (int i = entries.Length - 1; i >= 0; i--)
                if (entries[i].fishData != null) return entries[i].fishData;
            return null;
        }


        // 포획 기록 -----
        // 물고기 포획 시 호출. contribution은 AutoFishingController에서 계산한 기여도.
        public void RegisterCatch(FishData fish, float contribution = 1f)
        {
            if (fish == null || _currentStage == null) return;
            if (_multiplayFishPool == null && _currentStage.IsBossStage) return;

            _caughtFishIds.Add(fish.FishId);
            _totalContribution += contribution;
            CheckStageCleared();
        }


        // 현재 스테이지 포획 현황 반환 (잡은 종류 수, 전체 종류 수)
        public (int caught, int total) GetCatchProgress()
        {
            if (_currentStage == null || _currentStage.FishEntries == null)
                return (0, 0);

            int total = 0;
            int caught = 0;
            foreach (FishEntry entry in _currentStage.FishEntries)
            {
                if (entry.fishData == null) continue;
                total++;
                if (_caughtFishIds.Contains(entry.fishData.FishId))
                    caught++;
            }

            return (caught, total);
        }


        // 현재 스테이지 전종 포획 여부 반환
        public bool IsAllFishCaught()
        {
            if (_currentStage == null || _currentStage.FishEntries == null)
                return false;

            foreach (FishEntry entry in _currentStage.FishEntries)
            {
                if (entry.fishData == null) continue;
                if (!_caughtFishIds.Contains(entry.fishData.FishId))
                    return false;
            }

            return true;
        }


        // 보스 스테이지 -----
        // 보스 스테이지 진입 시 호출. HP를 MaxHp로 초기화하고 OnBossHpChanged 발행.
        // AutoFishingController.RefreshLoopState()에서 IsBossStage 감지 시 호출.
        public void StartBossStage()
        {
            if (_currentStage == null || !_currentStage.IsBossStage)
            {
                Debug.LogWarning("[FishSpawnManager] StartBossStage: 현재 스테이지가 보스 스테이지가 아닙니다.");
                return;
            }

            BossData boss = _currentStage.BossData;
            if (boss == null)
            {
                Debug.LogWarning($"[FishSpawnManager] StartBossStage: {_currentStage.StageId}에 BossData가 없습니다.");
                return;
            }

            _currentBossHp = boss.MaxHp;
            _bossCleared = false;

            _canMoveToNextStage = true;

            Debug.Log($"[FishSpawnManager] 보스 전투 시작: {boss.DisplayName} HP {_currentBossHp}/{boss.MaxHp}");
            OnBossHpChanged?.Invoke(_currentBossHp, boss.MaxHp);
        }

        // 보스에게 피해 적용. 낚시 성공 1회당 BaseDamagePerHit 기준으로 호출.
        // damage가 0 이하면 BossData.BaseDamagePerHit을 기본값으로 사용.
        // isManual: 수동 낚시 성공으로 인한 피격이면 true (공격 이펙트 연출 분기용).
        public void RegisterBossHit(int damage = 0, bool isManual = false)
        {
            if (_currentStage == null || !_currentStage.IsBossStage || _bossCleared)
                return;

            BossData boss = _currentStage.BossData;
            if (boss == null) return;

            // damage 미지정 시 BossData 기본 피해량 사용
            int actualDamage = damage > 0 ? damage : boss.BaseDamagePerHit;
            _currentBossHp = Mathf.Max(0, _currentBossHp - actualDamage);

            Debug.Log($"[FishSpawnManager] 보스 피격: {boss.DisplayName} -{actualDamage} → HP {_currentBossHp}/{boss.MaxHp}");

            OnBossDamaged?.Invoke(actualDamage, isManual); // 연출용 (데미지 넘버/플래시/공격 이펙트)
            OnBossHpChanged?.Invoke(_currentBossHp, boss.MaxHp); // HP UI용

            if (_currentBossHp <= 0)
                HandleBossDefeated(boss);
        }

        // 보스 처치 처리. 중복 발행 방지 후 OnBossCleared 이벤트 발행 -> MoveToNextStage.
        private void HandleBossDefeated(BossData boss)
        {
            if (_bossCleared) return;
            _bossCleared = true;

            Debug.Log($"<color=#FFD700>[FishSpawnManager] 보스 처치: {boss.DisplayName} → 보상 그룹: {boss.ClearRewardGroupId}</color>");
            OnBossCleared?.Invoke(boss);
            MoveToNextStage();
        }


        // 보스 제한 시간 초과 처리. BossStageUI 타이머가 만료되면 호출.
        // 보상 없이 이전 스테이지로 복귀한다.
        public void HandleBossTimeLimitExpired()
        {
            if (_bossCleared) return;   // 이미 클리어된 경우 무시
            _bossCleared = true;        // 중복 호출 방지

            Debug.Log($"[FishSpawnManager] 보스전 제한 시간 초과 → 이전 스테이지로 복귀");
            OnBossTimeLimitExpired?.Invoke();
            MoveToPreviousStage();
        }

        
        // 이전 스테이지로 복귀. 보스 실패 시 호출.
        public void MoveToPreviousStage()
        {
            if (_previousStage == null)
            {
                Debug.LogWarning("[FishSpawnManager] 이전 스테이지가 없습니다. 현재 스테이지 유지.");
                return;
            }

            _currentStage = _previousStage;
            _previousStage = null;
            _caughtFishIds.Clear();
            _totalContribution = 0f;
            _stageCleared = false;
            _currentBossHp = 0;
            _bossCleared = false;

            _canMoveToNextStage = false; // 0625 YWJ 추가: 이전 스테이지로 복귀 시 다음 스테이지 이동 불가로 초기화

            Debug.Log($"<color=#FF6B6B>[FishSpawnManager] 이전 스테이지 복귀 → {_currentStage.StageId}</color>");
            OnStageChanged?.Invoke(_currentStage);
        }


        // 스테이지 전환 -----
        public void MoveToNextStage()
        {
            if (_currentStage == null || _currentStage.NextStage == null)
            {
                Debug.LogWarning("[FishSpawnManager] 다음 스테이지가 없습니다.");
                return;
            }

            if (!_canMoveToNextStage)
            {
                Debug.LogWarning("[FishSpawnManager] _canMoveToNextStage=false → 다음 스테이지로 이동하지 않음 (현재 스테이지 유지).");
                return;
            }

            _previousStage = _currentStage;   // 이전 스테이지 저장 (보스 실패 복귀용)
            _currentStage = _currentStage.NextStage;

            _caughtFishIds.Clear();
            _totalContribution = 0f;
            _stageCleared = false;
            _currentBossHp = 0;
            _bossCleared = false;

            Debug.Log($"<color=#00FF00>[FishSpawnManager] 스테이지 전환 → {_currentStage.StageId}</color>");
            OnStageChanged?.Invoke(_currentStage);

            // 다음 스테이지가 보스 스테이지면 즉시 보스 전투 초기화
            if (_currentStage.IsBossStage)
                StartBossStage();
        }


        // 0619 YWJ 추가
        // 특정 스테이지로 이동 (게임 시작 시 초기 스테이지 설정용)
        public void MoveToStage(StageData stage, float initialContribution = 0f)
        {
            if (stage == null)
            {
                Debug.LogWarning("[FishSpawnManager] 이동할 스테이지가 없습니다.");
                return;
            }

            // 같은 스테이지에 이미 진행 중인 상태(보스 HP, 포획 기록, 기여도)가 있으면
            // 중복 호출로 보고 재초기화를 건너뛴다. (탭 전환 시 MoveToStage 재호출 방어)
            bool alreadyOnThisStage = _currentStage != null && _currentStage.StageId == stage.StageId;
            bool hasInProgressState = _totalContribution > 0f || _currentBossHp > 0 || _caughtFishIds.Count > 0;
            if (alreadyOnThisStage && hasInProgressState)
            {
                Debug.Log($"[FishSpawnManager] MoveToStage: 이미 {stage.StageId}에 진행 중인 상태가 있어 재초기화를 건너뜁니다.");
                return;
            }

            _previousStage = _currentStage;   // 이전 스테이지 저장 (보스 실패 복귀용)
            _currentStage = stage;
            _caughtFishIds.Clear();
            _totalContribution = initialContribution;
            _stageCleared = false;
            _currentBossHp = 0;
            _bossCleared = false;

            Debug.Log($"<color=#00FF00>[FishSpawnManager] 스테이지 이동 → {_currentStage.StageId}</color>");
            OnStageChanged?.Invoke(_currentStage);

            // 이동한 스테이지가 보스 스테이지면 즉시 보스 전투 초기화
            if (_currentStage.IsBossStage)
                StartBossStage();
        }

        // 내부 유틸
        private void CheckStageCleared()
        {
            if (_multiplayFishPool != null) return;

            if (_stageCleared) return;
            if (_totalContribution < _currentStage.ClearContributionThreshold) return;

            _stageCleared = true;
            Debug.Log($"[FishSpawnManager] {_currentStage.StageId} 클리어 (누적 기여도: {_totalContribution:0.##} / {_currentStage.ClearContributionThreshold})");
            OnStageCleared?.Invoke(_currentStage);
        }


        // 에디터 테스트
#if UNITY_EDITOR
        [ContextMenu("Test/포획 현황 출력")]
        private void Editor_PrintProgress()
        {
            var (caught, total) = GetCatchProgress();
            Debug.Log($"[FishSpawnManager] 포획 현황: {caught} / {total}  누적 기여도: {_totalContribution:0.##} / {(_currentStage != null ? _currentStage.ClearContributionThreshold.ToString("0.##") : "?")}  클리어: {_stageCleared}");
        }

        [ContextMenu("Test/물고기 선택 테스트 (10회)")]
        private void Editor_TestSelect()
        {
            for (int i = 0; i < 10; i++)
            {
                RMS.Data.FishData fish = SelectFish();
                Debug.Log($"[FishSpawnManager] 선택: {(fish != null ? $"{fish.DisplayName} ({fish.Rarity})" : "null")}");
            }
        }

        [ContextMenu("Test/보스 피격 테스트")]
        private void Editor_TestBossHit()
        {
            RegisterBossHit();
        }
#endif
    }
}
