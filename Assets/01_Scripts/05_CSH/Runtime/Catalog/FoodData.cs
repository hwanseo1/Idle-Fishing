using UnityEngine;

namespace Fisher.Data
{
    /// <summary>
    /// 인벤토리에 들어간 요리 결과물 foodId를 선원 성장 값으로 연결하는 데이터입니다.
    /// RecipeData는 제작 규칙이고, FoodData는 먹이기/판매 선택에 쓰는 결과물 규칙입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Objects/FoodData")]
    public sealed class FoodData : ScriptableObject
    {
        #region Identity

        [Header("식별")]
        [Tooltip("FoodInventory에 저장되는 itemId입니다. recipeId가 아닙니다.")]
        [SerializeField] private string _foodId;

        [Tooltip("TitleData FoodList의 displayNameKo")]
        [SerializeField] private string _displayNameKo;

        [Tooltip("같은 foodId를 가진 CSH ItemData입니다. 아이콘/가방 표시용 참조입니다.")]
        [SerializeField] private ItemData _itemData;

        #endregion

        #region TitleData Contract

        [Header("TitleData FoodList")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _sellable = true;

        [Min(0)]
        [SerializeField] private int _sellGold;

        [SerializeField] private string _category = "Food";
        [SerializeField] private string _sourceType = "Cooking";
        [SerializeField] private string _cookTag = "meal";

        [Min(0)]
        [SerializeField] private int _crewExp;

        #endregion

        #region Properties

        public string FoodId => _foodId;
        public string DisplayNameKo => _displayNameKo;
        public ItemData ItemData => _itemData;
        public bool IsEnabled => _enabled;
        public bool Sellable => _sellable;
        public int SellGold => _sellGold;
        public string Category => _category;
        public string SourceType => _sourceType;
        public string CookTag => _cookTag;
        public int CrewExp => _crewExp;

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_sellGold < 0)
            {
                _sellGold = 0;
            }

            if (_crewExp < 0)
            {
                _crewExp = 0;
            }

            if (string.IsNullOrWhiteSpace(_foodId))
            {
                Debug.LogWarning($"[FoodData] {name}: foodId가 비어 있습니다.", this);
            }

            if (_itemData != null && _itemData.ItemId != _foodId)
            {
                Debug.LogWarning($"[FoodData] {name}: ItemData.ItemId와 foodId가 다릅니다. itemId={_itemData.ItemId}, foodId={_foodId}", this);
            }
        }
#endif
    }
}
