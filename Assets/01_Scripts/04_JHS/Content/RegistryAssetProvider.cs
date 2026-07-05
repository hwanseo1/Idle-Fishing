using System.Threading.Tasks;
using UnityEngine;

namespace JHS.Content
{
    // IAssetProvider의 첫 구현: 인앱 동봉 AssetRegistry를 동기 조회해 "이미 완료된" Task로 반환.
    // Addressables 도입 전 차선호 UI를 즉시 언블록하는 용도.
    // 동봉이라 핸들 해제가 불필요 → Release는 no-op. (Addressables 구현 때 ref-count 버전으로 교체)
    public sealed class RegistryAssetProvider : IAssetProvider
    {
        private readonly AssetRegistry _registry;

        public RegistryAssetProvider(AssetRegistry registry) => _registry = registry;

        public Task<T> LoadByIdAsync<T>(string id) where T : Object
            => Task.FromResult(_registry != null ? _registry.Get<T>(id) : null);

        public void Release(Object asset) { /* 인앱 동봉 레지스트리는 해제 불필요 */ }
    }
}
