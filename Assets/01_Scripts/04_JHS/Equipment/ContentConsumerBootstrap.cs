using System.Collections;
using UnityEngine;
using JHS.Content;

namespace JHS.Equipment
{
    // 콘텐츠 소비 부트스트랩(JHS 단독). _equipments를 비운 뒤, EquipmentManager가 정의 데이터를
    // ContentService(Registry=로컬 / Addressable=CCD)에서 로드하도록 EnsureReadyAsync()를 적절한 시점에 호출한다.
    //
    // 순서 보장: 콘텐츠 카탈로그 패치(ContentCatalogUpdater.SyncIfChangedAsync)가 끝난 뒤(OnSyncCompleted)
    //   로드해야 CCD 최신값을 첫 실행부터 본다("패치 먼저 → 로드"). 원준 파일을 건드리지 않는 JHS 단독 호출부.
    // 폴백: 점검/게이트 차단으로 OnSyncCompleted가 안 오면(로그인 컨트롤러 early return) 이벤트만 기다리면
    //   영영 로드 못 하므로, 독립 타임아웃 후 그냥 로드한다. "이벤트만 의존 금지".
    // single-flight(EquipmentManager._readyTask)라 이벤트+폴백+어댑터가 겹쳐도 실제 로드는 1회.
    [DisallowMultipleComponent]
    public class ContentConsumerBootstrap : MonoBehaviour
    {
        [Tooltip("OnSyncCompleted가 안 와도 이 시간(초) 후엔 콘텐츠를 로드한다(게이트 차단 등 폴백). CCD 카탈로그 패치(네트워크)가 이 시간보다 오래 걸리면 옛값을 먼저 로드하므로 넉넉히 둔다.")]
        [SerializeField] private float _fallbackAfterSec = 45f;

        private bool _kicked;

        private void OnEnable()  => ContentCatalogUpdater.OnSyncCompleted += HandleSyncCompleted;
        private void OnDisable() => ContentCatalogUpdater.OnSyncCompleted -= HandleSyncCompleted;

        private void Start()
        {
            // Registry(로컬 AssetRegistry)는 카탈로그 패치 개념이 없어 "패치 먼저" 대기가 불필요 → 즉시 로드(개발 스냅).
            // Addressable(CCD)만 패치→로드 순서가 중요하므로 OnSyncCompleted 대기 + 폴백.
            bool remote = ContentService.Assets is AddressableAssetProvider;
            if (!remote) { Kick("Registry 즉시"); return; }
            StartCoroutine(FallbackKick());
        }

        // 패치(카탈로그 갱신) 완료 시점:
        //  · 아직 로드 전이면 → 지금 로드(정상 "패치 먼저 → 로드").
        //  · 폴백이 먼저 로드한 뒤(느린 CCD 패치가 8s 폴백보다 오래 걸림) 패치가 완료됐으면
        //    → 갱신된 카탈로그로 '재로드'해서 옛값이 남는 것을 방지. (라이브 검증서 발견한 순서버그 대응)
        // 패치(카탈로그 갱신) 완료 → 로드. 폴백을 넉넉히(45s) 둬서 정상적으로 패치가 먼저 끝나
        // 여기서 '한 번' 최신 카탈로그로 로드되게 한다(실행 중 재로드/번들충돌 회피 = 라이브 검증서 배운 것).
        private void HandleSyncCompleted() => Kick("패치 완료");

        // 폴백: 패치 신호가 안 와도 타임아웃 후 로드(게이트 차단/무패치 환경 방어).
        private IEnumerator FallbackKick()
        {
            float t = 0f;
            while (!_kicked && t < _fallbackAfterSec)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_kicked) Kick("폴백 타임아웃");
        }

        private void Kick(string reason)
        {
            if (_kicked) return;
            _kicked = true;
            LoadContent(reason, forceReload: false);
        }

        private async void LoadContent(string reason, bool forceReload)
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[ContentConsumerBootstrap] EquipmentManager.Instance 없음 — 로드 스킵(" + reason + ")");
                return;
            }

            // 로드 '전에' CCD 원격 번들 다운로드 게이지(있으면). Registry/캐시면 게이트가 즉시 통과.
            // best-effort UX라 예외/부재해도 로드로 진행(EnsureReadyAsync가 자동 다운로드 폴백).
            var gate = FindFirstObjectByType<CcdDownloadGate>(FindObjectsInactive.Include);
            if (gate != null)
            {
                try { await gate.EnsureDownloadedAsync(EquipmentManager.ContentIds); }
                catch (System.Exception e) { Debug.LogWarning("[ContentConsumerBootstrap] 다운로드 게이지 예외 — 로드 진행. " + e.Message); }
            }

            Debug.Log("[ContentConsumerBootstrap] EnsureReadyAsync 호출(" + reason + ")");
            _ = mgr.EnsureReadyAsync(forceReload);   // fire-and-forget, single-flight (내부 try/catch로 예외 안전)
        }
    }
}
