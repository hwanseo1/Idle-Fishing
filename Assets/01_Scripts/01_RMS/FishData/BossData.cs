using UnityEngine;


namespace RMS.Data
{
    // 보스 물고기 1종의 정의 데이터.
    // 스테이지 보스(개인)와 협동 보스전 모두 이 SO를 기반으로 하되,
    // <see cref = "BossType"/>으로 두 케이스를 구분한다.

    [CreateAssetMenu(fileName = "BossData", menuName = "Scriptable Objects/BossData")]
    public class BossData : ScriptableObject
    {
        // 식별
        [Header("식별")]
        [Tooltip("CSV / JSON 테이블의 bossFishId 또는 coopExplorationId와 1:1 대응.")]
        [SerializeField] private string _bossId;

        [Tooltip("화면에 표시할 보스 이름")]
        [SerializeField] private string _displayName;

        [Tooltip("보스 초상화 또는 아이콘")]
        [SerializeField] private Sprite _icon;

        [TextArea(2, 4)]
        [Tooltip("도감 설명 텍스트")]
        [SerializeField] private string _description;


        // 보스 구분
        [Header("보스 구분")]
        [Tooltip("StageBoss: 1인 진행 관문 / CoopBoss: 개인+전체 기여도 정산 대상")]
        [SerializeField] private BossType _bossType;

        [Tooltip("이 보스가 잠금을 해제하는 다음 스테이지 ID (StageBoss 전용)")]
        [SerializeField] private string _unlockStageId;


        // 미니게임 난이도
        [Header("미니게임 난이도")]
        [Tooltip("보스 미니게임 경계선 개수")]
        [SerializeField] private int _manualBarrierCount = 3;

        [Tooltip("보스 미니게임 실패 게이지 난이도 (0~1)")]
        [SerializeField] private float _manualDifficulty = 0.5f;

        [Tooltip("수동 미니게임 성공 시 기본 피해량. AutoFishingController._manualBossDamage 대신 이 값을 우선 사용.")]
        [SerializeField] private int _manualDamagePerHit = 30;

        public int ManualBarrierCount => _manualBarrierCount;
        public float ManualDifficulty => _manualDifficulty;
        public int ManualDamagePerHit => _manualDamagePerHit;


        // 전투 수치
        [Header("전투 수치")]
        [Tooltip("보스 최대 HP")]
        [SerializeField] private int _maxHp;

        [Tooltip("제한 시간(초). 0으로 설정하면 제한 없음.")]
        [SerializeField] private int _timeLimitSeconds;

        [Tooltip("플레이어 1인 턴당 기본 피해랑 (장비·선원 보정 전 기준)")]
        [SerializeField] private int _baseDamagePerHit;


        // 보상 연결
        [Header("보상 연결")]
        [Tooltip("보스 처치 시 지급하는 공통 보상 그룹 ID")]
        [SerializeField] private string _clearRewardGroupId;

        [Tooltip("협동 보스: 개인 기여도 달성 시 추가 보상 그룹 ID")]
        [SerializeField] private string _individualRewardGroupId;

        [Tooltip("협동 보스: 전체 기여도 달성 시 추가 보상 그룹 ID")]
        [SerializeField] private string _totalRewardGroupId;

        [Tooltip("이 보스를 처음 잡았을 때 도감 발견으로 처리할 FishData의 fishId (없으면 빈 문자열)")]
        [SerializeField] private string _linkedFishId;


        // 공개 프로퍼티 (읽기 전용)
        public string BossId                  => _bossId;
        public string DisplayName             => _displayName;
        public Sprite Icon                    => _icon;
        public string Description             => _description;
        public BossType BossType              => _bossType;
        public string UnlockStageId           => _unlockStageId;
        public int MaxHp                      => _maxHp;
        public int TimeLimitSeconds           => _timeLimitSeconds;
        public int BaseDamagePerHit           => _baseDamagePerHit;
        public string ClearRewardGroupId      => _clearRewardGroupId;
        public string IndividualRewardGroupId => _individualRewardGroupId;
        public string TotalRewardGroupId      => _totalRewardGroupId;
        public string LinkedFishId            => _linkedFishId;


        // 협동 보스 여부 확인
        public bool IsCoop => _bossType == BossType.CoopBoss;


        // 유효성 검사 (에디터 전용)
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_bossId))
                Debug.LogWarning($"[BossFishData] {name}: bossId가 비어 있습니다. CSV와 맞춰야 합니다.", this);

            if (_maxHp <= 0)
            {
                Debug.LogWarning($"[BossFishData] {name}: maxHp는 1 이상이어야 합니다.", this);
            }

            if (_bossType == BossType.CoopBoss)
            {
                if (string.IsNullOrWhiteSpace(_individualRewardGroupId))
                    Debug.LogWarning($"[BossFishData] {name}: 협동 보스는 individualRewardGroupId를 설정해야 합니다.", this);
                if (string.IsNullOrWhiteSpace(_totalRewardGroupId))
                    Debug.LogWarning($"[BossFishData] {name}: 협동 보스는 totalRewardGroupId를 설정해야 합니다.", this);
            }
        }
#endif
    }


    // ~ 보스 물고기 종류 구분 ~
    public enum BossType
    {
        /// <summary>1인 진행 관문 보스 (다음 스테이지 해금)</summary>
        StageBoss = 0,

        /// <summary>협동 보스전 (개인/전체 기여도 정산)</summary>
        CoopBoss = 1
    }
}