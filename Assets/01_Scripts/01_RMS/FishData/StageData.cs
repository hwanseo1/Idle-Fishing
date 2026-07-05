using UnityEngine;


namespace RMS.Data
{
    // 스테이지 1종의 정의 데이터.
    // 등장 물고기 목록, 보스 해금 조건, 다음 스테이지 연결을 담는다.
    // 수치 밸런싱은 이 SO의 값을 수정하는 것으로 관리한다.

    [CreateAssetMenu(fileName = "StageData", menuName = "Scriptable Objects/StageData")]
    public class StageData : ScriptableObject
    {
        // 식별
        [Header("식별")]
        [Tooltip("스테이지 고유 ID. ex) 1-1, 1-2, 1-boss")]
        [SerializeField] private string _stageId;

        [Tooltip("UI에 표시할 스테이지 이름. ex) 산들바람 해안 1-1")]
        [SerializeField] private string _displayName;


        // 스테이지 구분
        [Header("스테이지 구분")]
        [Tooltip("보스 스테이지 여부. true면 fishEntries 대신 bossData를 사용한다.")]
        [SerializeField] private bool _isBossStage;

        [Tooltip("보스 스테이지일 때 등장하는 보스 데이터")]
        [SerializeField] private BossData _bossData;


        // 등장 물고기 목록 (일반 스테이지 전용)
        [Header("등장 물고기 (일반 스테이지)")]
        [Tooltip("이 스테이지에서 등장하는 물고기 항목 목록.")]
        [SerializeField] private FishEntry[] _fishEntries;

        // 레어도 출현 가중치 (일반 스테이지 전용)
        [Header("레어도 출현 가중치 (일반 스테이지)")]
        [Tooltip("희귀도별 기본 출현 가중치. FishEntry.spawnWeight에 곱해져 최종 확률을 결정한다.\n" +
            "목록에 없는 레어도는 가중치 0으로 처리되어 출현 불가.\n" +
            "장비 RarityWeightBonus는 각 레어도 가중치에 합산된다.")]
        [SerializeField] private RarityWeightEntry[] _rarityWeights;

        // 클리어 조건
        [Header("클리어 조건")]
        [Tooltip("스테이지 클리어에 필요한 기여도 임계값. 낚시로 누적된 기여도가 이 값 이상이면 클리어.")]
        [Min(0f)]
        [SerializeField] private float _clearContributionThreshold = 100f;

        // 진행 연결
        [Header("진행 연결")]
        [Tooltip("이 스테이지를 클리어하면 해금되는 다음 스테이지")]
        [SerializeField] private StageData _nextStage;


        // 공개 프로퍼티 (읽기 전용)
        public string StageId          => _stageId;
        public string DisplayName      => _displayName;
        public bool IsBossStage        => _isBossStage;
        public BossData BossData       => _bossData;
        public FishEntry[] FishEntries => _fishEntries;
        public RarityWeightEntry[] RarityWeights => _rarityWeights;
        public float ClearContributionThreshold => _clearContributionThreshold;
        public StageData NextStage     => _nextStage;

        // 이 스테이지에 등장하는 물고기 종류 수
        public int FishKindCount => _fishEntries == null ? 0 : _fishEntries.Length;

        // 특정 레어도의 기본 출현 가중치 조회. 테이블에 없으면 0 반환.
        public float GetRarityWeight(FishRarity rarity)
        {
            if (_rarityWeights == null) return 0f;
            foreach (var entry in _rarityWeights)
                if (entry.rarity == rarity) return entry.weight;
            return 0f;
        }


        #region 멀티 플레이 전용
        // firstStage부터 NextStage 체인을 따라가며, stageId가 maxStageId 이하인
        // 일반 스테이지의 FishEntries만 모두 모아 반환한다.
        // (region.stage 형식 비교. 예: maxStageId="2-3"이면 1지역 전체 + 2지역 1~3만 포함, 2-4/2-5는 제외.
        //  maxStageId가 보스 스테이지 ID(예: "2-boss")면 그 지역의 마지막 일반 스테이지까지로 자동 치환된다.)
        // 싱글 모드 로직(SelectFish, CheckStageCleared 등)에는 전혀 영향을 주지 않는 읽기 전용 순수 함수.
        public static FishEntry[] GetFishEntriesUpToStage(StageData firstStage, string maxStageId)
        {
            var merged = new System.Collections.Generic.List<FishEntry>();

            foreach (StageData stage in EnumerateReachableStages(firstStage, maxStageId))
            {
                if (stage._fishEntries != null)
                    merged.AddRange(stage._fishEntries);
            }

            return merged.ToArray();
        }


        // firstStage부터 maxStageId까지 도달 가능한 일반 스테이지 중 가장 마지막(가중치 기준으로 쓸) 스테이지를 반환한다.
        // (FishSpawnManager.SetMultiplayFishPool의 rarityWeightStage 인자로 사용)
        public static StageData GetRarityWeightStage(StageData firstStage, string maxStageId)
        {
            StageData last = null;
            foreach (StageData stage in EnumerateReachableStages(firstStage, maxStageId))
                last = stage;

            return last;
        }


        // 내부 헬퍼 -----
        // firstStage부터 NextStage 체인을 따라가며, maxStageId까지 도달 가능한 "일반 스테이지"를 순서대로 열거한다.
        // 보스 스테이지는 건너뛴다. maxStageId 자체가 보스 스테이지를 가리키는 경우
        // (예: "2-boss"), 체인에서 실제로 그 ID를 가진 보스 스테이지를 찾아내
        // "그 보스 직전의 일반 스테이지까지"로 자동 치환해서 비교한다.
        // (지역당 스테이지 개수를 하드코딩하지 않기 위해, 숫자 파싱이 아니라 체인 순회로 직접 판별한다)
        private static System.Collections.Generic.IEnumerable<StageData> EnumerateReachableStages(StageData firstStage, string maxStageId)
        {
            string effectiveMaxStageId = ResolveEffectiveMaxStageId(firstStage, maxStageId);
            (int region, int stage) max = ParseStageId(effectiveMaxStageId);

            StageData cur = firstStage;
            while (cur != null)
            {
                if (cur._isBossStage)
                {
                    cur = cur._nextStage;
                    continue;
                }

                (int region, int stage) curPos = ParseStageId(cur._stageId);

                bool withinReach =
                    curPos.region < max.region ||
                    (curPos.region == max.region && curPos.stage <= max.stage);

                if (!withinReach)
                    yield break;

                yield return cur;
                cur = cur._nextStage;
            }
        }

        // maxStageId가 체인 안에서 실제로 보스 스테이지를 가리키는지 확인하고,
        // 그렇다면 그 보스 바로 직전의 일반 스테이지 ID로 치환해서 반환한다.
        // 보스가 아니거나 체인에서 못 찾으면 원래 maxStageId를 그대로 반환한다.
        private static string ResolveEffectiveMaxStageId(StageData firstStage, string maxStageId)
        {
            StageData cur = firstStage;
            StageData lastNonBossStage = null;

            while (cur != null)
            {
                if (cur._stageId == maxStageId)
                {
                    // maxStageId와 정확히 일치하는 스테이지를 찾음
                    return cur._isBossStage
                        ? (lastNonBossStage != null ? lastNonBossStage._stageId : maxStageId)
                        : maxStageId;
                }

                if (!cur._isBossStage)
                    lastNonBossStage = cur;

                cur = cur._nextStage;
            }

            // 체인에서 못 찾은 경우(데이터 누락 등) 원래 값을 그대로 사용
            return maxStageId;
        }

        // "region-stage" 형식 문자열을 (region, stage)로 파싱. 형식이 잘못되면 (1, 1) 반환.
        private static (int region, int stage) ParseStageId(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return (1, 1);

            int dash = stageId.IndexOf('-');
            if (dash < 0) return (1, 1);

            int.TryParse(stageId.Substring(0, dash), out int region);
            int.TryParse(stageId.Substring(dash + 1), out int stage);

            return (region < 1 ? 1 : region, stage < 1 ? 1 : stage);
        }
        #endregion


        // 유효성 검사 (에디터 전용)
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_stageId))
                Debug.LogWarning($"[StageData] {name}: stageId가 비어 있습니다.", this);

            if (_isBossStage)
            {
                if (_bossData == null)
                    Debug.LogWarning($"[StageData] {name}: 보스 스테이지인데 bossData가 없습니다.", this);
            }
            else
            {
                if (_fishEntries == null || _fishEntries.Length == 0)
                    Debug.LogWarning($"[StageData] {name}: 등장 물고기가 없습니다.", this);

                if (_fishEntries != null && _fishEntries.Length > 0)
                {
                    float totalWeight = 0f;
                    for (int i = 0; i < _fishEntries.Length; i++)
                    {
                        if (_fishEntries[i].fishData == null)
                        {
                            Debug.LogWarning($"[StageData] {name}: fishEntries[{i}]에 FishData가 없습니다.", this);
                            continue;
                        }
                        if (_fishEntries[i].spawnWeight <= 0f)
                            Debug.LogWarning($"[StageData] {name}: fishEntries[{i}] ({_fishEntries[i].fishData.name})의 spawnWeight가 0 이하입니다.", this);

                        totalWeight += _fishEntries[i].spawnWeight;
                    }

                    if (totalWeight <= 0f)
                        Debug.LogWarning($"[StageData] {name}: 전체 spawnWeight 합계가 0입니다. 스폰이 동작하지 않습니다.", this);
                }

                // 레어도 가중치 테이블 검사
                if (_rarityWeights == null || _rarityWeights.Length == 0)
                    Debug.LogWarning($"[StageData] {name}: rarityWeights가 비어 있습니다. 모든 물고기 출현 가중치가 0이 됩니다.", this);
            }
        }
#endif
    }

    // 스테이지에 등장하는 물고기 1종의 항목.
    // FishData 참조와 이 스테이지에서의 등장 가중치를 함께 들고있다.
    [System.Serializable]
    public class FishEntry
    {
        [Tooltip("등장할 물고기 데이터")]
        public FishData fishData;

        [Tooltip("이 스테이지에서의 등장 가중치. 높을수록 자주 등장.")]
        [Min(0f)]
        public float spawnWeight = 1f; 
    }

    // 레어도별 기본 출현 가중치 항목.
    // StageData.rarityWeights 배열의 원소로 사용한다.
    [System.Serializable]
    public class RarityWeightEntry
    {
        [Tooltip("희귀도 등급")]
        public FishRarity rarity;

        [Tooltip("이 스테이지에서의 기본 출현 가중치. 0이면 장비 보정 없이는 출현 불가.")]
        [Min(0f)]
        public float weight;
    }
}