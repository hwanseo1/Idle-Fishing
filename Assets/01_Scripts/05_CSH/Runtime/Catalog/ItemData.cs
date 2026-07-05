using UnityEngine;

namespace Fisher.Data
{
    /// <summary>
    /// CSV / JSON 기준으로 공유되는 가방, 보상, 상점 후보 아이템 1종의 정의 데이터입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
    public class ItemData : ScriptableObject
    {
        #region Identity

        [Header("식별")]
        [Tooltip("CSV / JSON 테이블의 itemId와 1:1 대응. 절대 중복 불가.")]
        [SerializeField] private string _itemId;

        [Tooltip("가방, 상점, 보상 UI에서 쓰는 표시 이름")]
        [SerializeField] private string _displayName;

        [Tooltip("가방, 상점, 보상 UI에 표시할 아이콘")]
        [SerializeField] private Sprite _icon;

        [TextArea(2, 4)]
        [Tooltip("아이템 설명 텍스트")]
        [SerializeField] private string _description;

        #endregion

        #region Type

        [Header("분류")]
        [Tooltip("아이템 사용처 분류")]
        [SerializeField] private ItemCategory _category = ItemCategory.Material;

        [Tooltip("아이템 희귀도")]
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;

        [Tooltip("요리/강화/상점/보상 등 획득 출처")]
        [SerializeField] private string _sourceType;

        #endregion

        #region Inventory

        [Header("가방")]
        [Tooltip("스택형 아이템인지 여부. 장비처럼 고유 인스턴스가 필요한 경우 false.")]
        [SerializeField] private bool _stackable = true;

        [Tooltip("스택형 아이템의 최대 보유 수량")]
        [Min(1)]
        [SerializeField] private int _maxStack = 99;

        [Tooltip("가방에서 직접 판매할 때 골드 단가. 판매 불가면 0.")]
        [Min(0)]
        [SerializeField] private int _sellPriceGold;

        #endregion

        #region Authoring State

        [Header("작성 상태")]
        [Tooltip("테스트/자리표시자 에셋이면 켠다. 켜져 있으면 빈 ID 경고를 내지 않는다.")]
        [SerializeField] private bool _sample;

        [Tooltip("런타임에서 사용할 수 있는 아이템인지 여부")]
        [SerializeField] private bool _enabled = true;

        #endregion

        #region Properties

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public ItemCategory Category => _category;
        public ItemRarity Rarity => _rarity;
        public string SourceType => _sourceType;
        public bool Stackable => _stackable;
        public int MaxStack => _maxStack;
        public int SellPriceGold => _sellPriceGold;
        public bool IsSample => _sample;
        public bool IsEnabled => _enabled;

        #endregion

#if UNITY_EDITOR
        #region Validation

        private void OnValidate()
        {
            if (_maxStack < 1)
            {
                _maxStack = 1;
            }

            if (_sellPriceGold < 0)
            {
                _sellPriceGold = 0;
            }

            if (_sample || !_enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_itemId))
            {
                Debug.LogWarning($"[ItemData] {name}: itemId가 비어 있습니다. CSV itemId와 맞춰야 합니다.", this);
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                Debug.LogWarning($"[ItemData] {name}: 표시 이름이 비어 있습니다.", this);
            }

            if (!_stackable && _maxStack != 1)
            {
                Debug.LogWarning($"[ItemData] {name}: 고유 인스턴스 아이템은 maxStack 1을 권장합니다.", this);
            }
        }

        #endregion
#endif
    }

    public enum ItemCategory
    {
        Material = 0,
        Food = 1,
        Ticket = 2,
        UpgradeMaterial = 3,
        Currency = 4,
        Special = 5,
        Fish = 6,
        HighGradeMaterial = 7,
        Box = 8,
        ChoiceTicket = 9,
        Boat = 10
    }

    public enum ItemRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
        Uncommon = 4
    }
}
