using UnityEngine;

namespace JHS.Content
{
    // 에셋 로더 접근점 (기존 코드베이스의 싱글톤 패턴 따름 — EquipmentManager.Instance와 동일 스타일).
    // 소비자(차선호 UI 등)는 ContentService.Assets.LoadByIdAsync<EquipmentData>("equip_rod") 식으로
    // ID로 그 실제 데이터를 받는다. (한 ID = 한 데이터, 아이콘 등은 그 데이터 안에)
    //
    // 로더 구현은 인스펙터 _kind로 선택한다(전환용). Registry=인앱 레지스트리(Local), Addressable=Addressables.
    // 소비자 코드는 IAssetProvider만 알아서 어느 쪽이든 무변경(DIP). Addressable의 Local→Remote(CCD) 전환도 소비자 투명.
    public sealed class ContentService : MonoBehaviour
    {
        public static IAssetProvider Assets { get; private set; }

        private enum ProviderKind { Registry, Addressable }

        [SerializeField] private ProviderKind _kind = ProviderKind.Registry;   // 로더 구현 선택
        [SerializeField] private AssetRegistry _registry;   // Registry 모드에서 동봉 레지스트리 할당
        [SerializeField] private bool _selfTest = false;    // 검증용: Start에서 등록 ID들을 직접 로드해 로그(확인 후 OFF/제거)

        // 검증용 ID 목록 — AssetRegistry/Addressable에 등록된 6개와 동일
        private static readonly string[] _selfTestIds =
        {
            "equip_rod", "equip_float", "equip_chair",
            "boat_vacuum", "boat_lamp", "boat_penguin",
        };

        private void Awake()
        {
            // 중복 가드 — "처음이 이긴다" (씬 통합/additive 로드 대비)
            if (Assets != null) { Destroy(gameObject); return; }

            if (_kind == ProviderKind.Registry && _registry == null)
                Debug.LogWarning("[ContentService] AssetRegistry 미할당 — 로드 결과가 전부 null이 됩니다.");

            Assets = _kind == ProviderKind.Addressable
                ? new AddressableAssetProvider()
                : new RegistryAssetProvider(_registry);

            DontDestroyOnLoad(gameObject);
        }

        // 검증용: 콘텐츠 패치(ContentCatalogUpdater.SyncIfChangedAsync) '완료 후'에 로드한다.
        // 씬로드 즉시 로드하면 갱신 전 옛값을 읽어 "2번 켜야 반영"되던 문제 → OnSyncCompleted 뒤에 1회 로드해
        // 첫 실행부터 최신값을 본다(= 운영의 "소비는 패치 뒤에" 순서). 패치가 안 도는 환경이면 셀프테스트도 안 돔(검증용이라 무해).
        private bool _selfTestDone;

        private void Start()
        {
            if (!_selfTest) return;
            ContentCatalogUpdater.OnSyncCompleted += RunSelfTestOnce;
        }

        private void OnDestroy() => ContentCatalogUpdater.OnSyncCompleted -= RunSelfTestOnce;

        private async void RunSelfTestOnce()
        {
            if (_selfTestDone) return;
            _selfTestDone = true;
            ContentCatalogUpdater.OnSyncCompleted -= RunSelfTestOnce;

            foreach (var id in _selfTestIds)
            {
                var asset = await Assets.LoadByIdAsync<Object>(id);
                string detail = asset != null ? asset.GetType().Name + " : " + asset.name : "NULL";
                // 배 SO면 첫 스킬 첫 레벨 value도 찍어 CCD 반영 확인. Addressables 로드값이라 CCD 내용물 그대로.
                if (asset is JHS.Equipment.EquipmentData ed && ed.boatSkills != null && ed.boatSkills.Length > 0
                    && ed.boatSkills[0].levels != null && ed.boatSkills[0].levels.Length > 0)
                    detail += $"  value={ed.boatSkills[0].levels[0].value}";
                Debug.Log($"[ContentService.SelfTest] {id} → {detail}");
            }
        }
    }
}
