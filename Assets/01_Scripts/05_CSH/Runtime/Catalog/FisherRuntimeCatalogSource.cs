using Fisher.Data;
using RMS.Data;
using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 런타임에서 CSV 대신 읽는 Fisher 카탈로그 원천 묶음입니다.
    /// CSV는 에디터 생성/밸런스 검증에만 사용하고, 게임 실행은 이 SO와 JSON TextAsset을 기준으로 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "FisherRuntimeCatalogSource", menuName = "Fisher/Runtime Catalog Source")]
    public sealed class FisherRuntimeCatalogSource : ScriptableObject
    {
        [Header("Unity Data")]
        [SerializeField] private ItemData[] _itemDataAssets;
        [SerializeField] private RecipeData[] _recipeDataAssets;
        [SerializeField] private FishData[] _fishDataAssets;

        [Header("Local TitleData JSON")]
        [SerializeField] private TextAsset[] _runtimeJsonAssets;

        public ItemData[] ItemDataAssets => _itemDataAssets ?? System.Array.Empty<ItemData>();
        public RecipeData[] RecipeDataAssets => _recipeDataAssets ?? System.Array.Empty<RecipeData>();
        public FishData[] FishDataAssets => _fishDataAssets ?? System.Array.Empty<FishData>();
        public TextAsset[] RuntimeJsonAssets => _runtimeJsonAssets ?? System.Array.Empty<TextAsset>();
    }
}
