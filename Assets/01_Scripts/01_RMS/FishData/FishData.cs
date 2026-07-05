using UnityEngine;


namespace RMS.Data
{
    // 물고기 1종의 정의 데이터

    [CreateAssetMenu(fileName = "FishData", menuName = "Scriptable Objects/FishData")]
    public class FishData : ScriptableObject
    {
        // 식별
        [Header("식별")]
        [Tooltip("CSV / JSON 테이블의 fishId와 1:1 대응. 절대 중복 불가.")]
        [SerializeField] private string _fishId;

        [Tooltip("인벤토리, 도감, UI에서 쓰는 표시 이름")]
        [SerializeField] private string _displayName;

        [Tooltip("도감, 가방, UI에 표시할 아이콘")]
        [SerializeField] private Sprite _icon;

        [TextArea(2, 4)]
        [Tooltip("도감 설명 텍스트")]
        [SerializeField] private string _description;


        // 등급 및 서식지
        [Header("등급 / 서식지")]
        [Tooltip("물고기 희귀도 등급")]
        [SerializeField] private FishRarity _rarity;

        [Tooltip("등장 가능한 스테이지 ID 목록")]
        [SerializeField] private string[] _allowedStageIds;


        // 낚시 수치
        [Header("낚시 수치")]
        [Tooltip("자동 낚시에서 이 물고기가 선택될 기본 가중치. 높을수록 자주 낚인다.")]
        [SerializeField] private float _spawnWeight = 1f;

        [Tooltip("수동 낚시 미니게임 게이지 난이도 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _manualDifficulty = 0.3f;


        // 경제 수치
        [Header("경제 수치")]
        [Tooltip("가방에서 직접 판매할 때 골드 단가. UI에 반영.")]
        [SerializeField] private int _sellPriceGold;

        [Tooltip("요리 재료로 쓰일 수 있는지 여부")]
        [SerializeField] private bool _usableAsIngredient = true;


        // 도감
        [Header("도감")]
        [Tooltip("도감 발견 시 1회 지급하는 보상 그룹 ID. 없으면 비워 둔다.")]
        [SerializeField] private string _codexRewardGroupId;


        // 공개 프로퍼티 (읽기 전용)
        public string FishId             => _fishId;
        public string DisplayName        => _displayName;
        public Sprite Icon               => _icon;
        public string Description        => _description;
        public FishRarity Rarity         => _rarity;
        public string[] AllowedStageIds  => _allowedStageIds;
        public float SpawnWeight         => _spawnWeight;
        public float ManualDifficulty    => _manualDifficulty;
        public int SellPriceGold         => _sellPriceGold;
        public bool UsableAsIngredient   => _usableAsIngredient;
        public string CodexRewardGroupId => _codexRewardGroupId;


        // 유효성 검사 (에디터 전용)
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_fishId))
                Debug.LogWarning($"[FishData] {name}: fishId가 비어 있습니다. CSV itemId와 맞춰야 합니다.", this);

            if (_sellPriceGold < 0)
            {
                Debug.LogWarning($"[FishData] {name}: sellPriceGold가 음수입니다.", this);
                _sellPriceGold = 0;
            }
        }
#endif
    }
}
