using RMS.Data;
using UnityEngine;

namespace Fisher.Data
{
    /// <summary>
    /// 물고기 2종을 조합해 요리 아이템을 만드는 레시피 정의 데이터입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeData", menuName = "Scriptable Objects/RecipeData")]
    public class RecipeData : ScriptableObject
    {
        #region Identity

        [Header("식별")]
        [Tooltip("CSV / JSON 테이블의 recipeId와 1:1 대응. 절대 중복 불가.")]
        [SerializeField] private string _recipeId;

        [Tooltip("요리 UI에 표시할 레시피 이름")]
        [SerializeField] private string _displayName;

        [Tooltip("요리 결과 또는 레시피 아이콘")]
        [SerializeField] private Sprite _icon;

        [TextArea(2, 4)]
        [Tooltip("레시피 설명 텍스트")]
        [SerializeField] private string _description;

        #endregion

        #region Ingredients

        [Header("재료")]
        [Tooltip("서로 다른 물고기 2종을 권장합니다.")]
        [SerializeField] private RecipeFishIngredient[] _fishIngredients = new RecipeFishIngredient[2];

        #endregion

        #region Result

        [Header("결과")]
        [Tooltip("완료 시 지급할 요리 아이템")]
        [SerializeField] private ItemData _outputItem;

        [Tooltip("완료 1회당 지급 수량")]
        [Min(1)]
        [SerializeField] private int _outputCount = 1;

        [Tooltip("요리 1회 제작 시간(초)")]
        [Min(0)]
        [SerializeField] private int _durationSeconds = 60;

        [Tooltip("요리 1회 완료 시 지급할 선원 경험치")]
        [Min(0)]
        [SerializeField] private int _crewExp;

        [Tooltip("같은 요리를 한 번에 대기열에 넣을 수 있는 최대 수량")]
        [Range(1, 99)]
        [SerializeField] private int _maxQueueCount = 99;

        #endregion

        #region Authoring State

        [Header("작성 상태")]
        [Tooltip("테스트/자리표시자 에셋이면 켠다. 켜져 있으면 빈 ID 경고를 내지 않는다.")]
        [SerializeField] private bool _sample;

        [Tooltip("런타임에서 사용할 수 있는 레시피인지 여부")]
        [SerializeField] private bool _enabled = true;

        #endregion

        #region Properties

        public string RecipeId => _recipeId;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public RecipeFishIngredient[] FishIngredients => _fishIngredients;
        public ItemData OutputItem => _outputItem;
        public int OutputCount => _outputCount;
        public int DurationSeconds => _durationSeconds;
        public int CrewExp => _crewExp;
        public int MaxQueueCount => _maxQueueCount;
        public bool IsSample => _sample;
        public bool IsEnabled => _enabled;

        #endregion

#if UNITY_EDITOR
        #region Validation

        private void OnValidate()
        {
            if (_outputCount < 1)
            {
                _outputCount = 1;
            }

            if (_durationSeconds < 0)
            {
                _durationSeconds = 0;
            }

            if (_crewExp < 0)
            {
                _crewExp = 0;
            }

            if (_maxQueueCount < 1)
            {
                _maxQueueCount = 1;
            }
            else if (_maxQueueCount > 99)
            {
                _maxQueueCount = 99;
            }

            if (_sample || !_enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_recipeId))
            {
                Debug.LogWarning($"[RecipeData] {name}: recipeId가 비어 있습니다. CSV recipeId와 맞춰야 합니다.", this);
            }

            if (_fishIngredients == null || _fishIngredients.Length != 2)
            {
                Debug.LogWarning($"[RecipeData] {name}: 요리 레시피는 물고기 2종 재료를 권장합니다.", this);
                return;
            }

            FishData firstFish = _fishIngredients[0] == null ? null : _fishIngredients[0].FishData;
            FishData secondFish = _fishIngredients[1] == null ? null : _fishIngredients[1].FishData;
            if (firstFish == null || secondFish == null)
            {
                Debug.LogWarning($"[RecipeData] {name}: 물고기 재료가 비어 있습니다.", this);
            }
            else if (firstFish.FishId == secondFish.FishId)
            {
                Debug.LogWarning($"[RecipeData] {name}: 같은 물고기 2종 조합은 현재 계약에서 사용하지 않습니다.", this);
            }

            for (int i = 0; i < _fishIngredients.Length; i++)
            {
                if (_fishIngredients[i] != null && _fishIngredients[i].Count <= 0)
                {
                    Debug.LogWarning($"[RecipeData] {name}: fishIngredients[{i}] 수량은 1 이상이어야 합니다.", this);
                }
            }

            if (_outputItem == null)
            {
                Debug.LogWarning($"[RecipeData] {name}: 결과 아이템이 비어 있습니다.", this);
            }
        }

        #endregion
#endif
    }

    [System.Serializable]
    public sealed class RecipeFishIngredient
    {
        [Tooltip("재료로 사용할 RMS FishData")]
        [SerializeField] private FishData _fishData;

        [Tooltip("필요 수량")]
        [Min(1)]
        [SerializeField] private int _count = 1;

        public FishData FishData => _fishData;
        public int Count => _count;
    }
}
