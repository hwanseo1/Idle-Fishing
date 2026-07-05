using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace JHS.Content
{
    // dataVersion으로 Addressables 원격 카탈로그 갱신을 게이팅하는 순수 로직(트랙 B).
    // 서버 콘텐츠 버전(트랙 A RemoteConfig.dataVersion)을 인자로 받아, 로컬에 적용된 버전과 다를 때만
    // 카탈로그를 확인·갱신한다. Addressables/PlayerPrefs에만 의존(서버·백엔드 모름 = DIP).
    public static class ContentCatalogUpdater
    {
        private const string Key = "jhs.content.dataVersion.v1";

        // 동기 시도 완료 시 발행(갱신 여부·skip 무관). 콘텐츠 '소비'는 이 뒤에 하면 항상 최신을 본다.
        // = "패치 먼저 → 그다음 로드" 순서 보장용 신호. (검증 셀프테스트가 구독해 첫 실행부터 새 값을 봄)
        public static event System.Action OnSyncCompleted;

        // 이번 부팅에서 dataVersion이 로컬과 달라 콘텐츠 패치/다운로드가 필요했는지(최초 설치 포함).
        // = "업데이트 완료" 게이트를 띄울지 판단하는 신호. 동일 버전 재접속(이미 최신)이면 false → 패널 안 띄움.
        public static bool DidUpdateThisSession { get; private set; }

        // 서버 dataVersion이 로컬과 다르면 원격 카탈로그를 확인·갱신하고 새 버전을 저장한다.
        // 반환: 카탈로그를 실제로 갱신했으면 true.
        public static async Task<bool> SyncIfChangedAsync(string serverDataVersion)
        {
            bool updated = false;
            // Addressables 카탈로그 확인/갱신이 던지면(미초기화·네트워크·카탈로그 오류) 부팅 전환 전체가 중단되고
            // OnSyncCompleted가 발행 안 돼 소비자가 45s 폴백에만 의존하게 된다 → 감싸서 실패해도 진행.
            try { updated = await SyncCoreAsync(serverDataVersion); }
            catch (System.Exception e) { Debug.LogWarning("[ContentUpdate] 동기 실패 — skip하고 진행. " + e.Message); }
            OnSyncCompleted?.Invoke();   // 갱신/skip/실패 어느 경로든 "동기 끝" 알림 → 소비자가 이 뒤에 로드
            return updated;
        }

        private static async Task<bool> SyncCoreAsync(string serverDataVersion)
        {
            if (string.IsNullOrEmpty(serverDataVersion))
            {
                Debug.Log("[ContentUpdate] 서버 dataVersion 없음 — skip");
                DidUpdateThisSession = false;
                return false;
            }

            string localVersion = PlayerPrefs.GetString(Key, null);
            if (serverDataVersion == localVersion)
            {
                Debug.Log($"[ContentUpdate] dataVersion 동일({serverDataVersion}) — 카탈로그 갱신 불필요");
                DidUpdateThisSession = false;
                return false;
            }

            // 버전이 다름 = 최초 설치 or 업데이트 → 콘텐츠 패치/다운로드 발생 → "업데이트 완료" 게이트 대상.
            DidUpdateThisSession = true;

            string localDisplay = string.IsNullOrEmpty(localVersion) ? "(없음)" : localVersion;
            Debug.Log($"[ContentUpdate] dataVersion 변경 감지: 로컬={localDisplay} → 서버={serverDataVersion}. 카탈로그 확인…");

            bool updated = await UpdateCatalogsAsync();

            // 확인 완료 → 새 버전 기록(다음 부팅에서 재확인 안 하도록). 갱신분 없어도 버전은 맞춰둔다.
            PlayerPrefs.SetString(Key, serverDataVersion);
            PlayerPrefs.Save();
            return updated;
        }

        private static async Task<bool> UpdateCatalogsAsync()
        {
            // 원격 카탈로그에 갱신분이 있는지 확인 (수동 release 위해 autoRelease=false)
            var checkHandle = Addressables.CheckForCatalogUpdates(false);
            List<string> catalogs = await checkHandle.Task;
            bool hasUpdates = checkHandle.Status == AsyncOperationStatus.Succeeded && catalogs != null && catalogs.Count > 0;
            Addressables.Release(checkHandle);

            if (!hasUpdates)
            {
                Debug.Log("[ContentUpdate] 갱신할 카탈로그 없음(원격이 로컬과 동일)");
                return false;
            }

            var updateHandle = Addressables.UpdateCatalogs(catalogs, false);
            await updateHandle.Task;
            bool ok = updateHandle.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(updateHandle);

            Debug.Log(ok ? $"[ContentUpdate] 카탈로그 {catalogs.Count}건 갱신 완료" : "[ContentUpdate] 카탈로그 갱신 실패");
            return ok;
        }

        public static void Clear() => PlayerPrefs.DeleteKey(Key);   // 테스트용(첫 실행/버전 초기화 경로 확인)
    }
}
