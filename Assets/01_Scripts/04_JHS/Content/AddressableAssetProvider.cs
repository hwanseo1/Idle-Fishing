using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace JHS.Content
{
    // IAssetProvider의 Addressables 구현(P1). 주소(address) = 공통 종류 ID(equip_rod 등) — 레지스트리와 같은 키.
    // 지금은 Local(앱 내장)로 동작하고, 나중에 같은 코드로 Remote(CCD) 카탈로그만 갈아끼우면 됨 — 소비자 무변경(DIP).
    // 레지스트리 구현(no-op)과 달리 로드 핸들을 보관해 Release에서 실제 해제한다(누수 방지).
    public sealed class AddressableAssetProvider : IAssetProvider
    {
        // 반환한 에셋 → 그 로드 핸들. Release 때 되돌려 해제한다.
        // (단순화: 같은 에셋을 여러 번 로드하면 마지막 핸들만 추적 — Local 단계엔 충분, Remote 다중로드 도입 시 ref-count로 확장)
        private readonly Dictionary<UnityEngine.Object, AsyncOperationHandle> _handles = new();

        // ID(주소)로 비동기 로드. 못 찾으면 인터페이스 계약대로 null(예외를 흘리지 않음).
        public async Task<T> LoadByIdAsync<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id)) return null;

            // 주소 등록 여부 선조회 — 미등록 키에 LoadAssetAsync를 바로 부르면 InvalidKeyException(빨간 에러)이 남으므로,
            // 위치를 먼저 확인해 없으면 조용히 null 반환("못 찾으면 null" 계약을 깔끔히 충족).
            var locHandle = Addressables.LoadResourceLocationsAsync(id, typeof(T));
            IList<IResourceLocation> locations = await locHandle.Task;
            bool found = locHandle.Status == AsyncOperationStatus.Succeeded && locations != null && locations.Count > 0;
            Addressables.Release(locHandle);

            if (!found) return null;

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(id);
            try
            {
                T asset = await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded && asset != null)
                {
                    _handles[asset] = handle;   // Release용 보관
                    return asset;
                }
            }
            catch (Exception)
            {
                // 타입 불일치 등 — null로 흡수(레지스트리 구현과 동일 계약)
            }

            // 성공 반환에 닿지 못한 핸들은 즉시 해제(누수 방지)
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }

        // 로드한 에셋 핸들 해제(ref-count 감소). 보관 안 된 에셋은 무시.
        public void Release(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (_handles.TryGetValue(asset, out var handle))
            {
                _handles.Remove(asset);
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
    }
}
