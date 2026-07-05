using System;
using System.Threading.Tasks;
using UnityEngine;

namespace JHS.Backend
{
    // 배포/버전 트랙 접근점 (ContentService와 동일 싱글톤 스타일).
    // 부팅 시 config를 받아 버전게이트를 판정하고 결과를 노출한다.
    // 지금은 MockBackendGateway를 물림 — 나중에 PlayFabBackendGateway로 갈아끼우면 됨(BuildGateway 한 곳).
    //
    // ✅ 구현됨: 3단 fallback(라이브→PlayerPrefs 캐시→동봉 기본 SO, Boot() 참조) + 버전게이트 판정 4종.
    // ⚠️ 미완(다음 단계): ① 차단/점검/권장 UI 화면(YWJ RSM 배치) ② PlayFabBackendGateway(GetTitleData) 실연결.
    public sealed class BackendService : MonoBehaviour
    {
        public static BackendService Instance { get; private set; }

        public RemoteConfig Config { get; private set; }
        public GateDecision Decision { get; private set; } = GateDecision.Ok;
        public bool IsReady { get; private set; }
        public event Action OnReady;   // 판정 완료 시 (버전게이트 화면·게임 부팅이 구독)

        private IBackendGateway _gateway;

        [Header("백엔드 선택")]
        [SerializeField] private bool _usePlayFab = false;                  // OFF=Mock, ON=PlayFab 실연결(GetTitleData)
        [SerializeField] private string _titleDataKey = "RemoteConfig";     // PlayFab Title Data 키(값=RemoteConfig JSON)

        [Header("Mock (테스트용 — 시나리오 흉내)")]
        [SerializeField] private string _mockMinVersion = "1.0.0";
        [SerializeField] private string _mockRecommendedVersion = "1.0.0";
        [SerializeField] private bool _mockMaintenance = false;
        [SerializeField] private int _mockDelayMs = 200;
        [SerializeField] private bool _mockSimulateFailure = false;
        [SerializeField] private string _mockGoldMultiplier = "1.0";   // 데모: 보상배수 라이브 튜닝값(재컴파일 없이 인스펙터로)
        [SerializeField] private string _mockDataVersion = "0";        // 데모: 콘텐츠 버전 — 값 바꾸면 카탈로그 갱신 시뮬레이션

        [Header("Fallback")]
        [SerializeField] private DefaultRemoteConfig _defaultConfig;   // 동봉 기본 config (3단 fallback ①)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _gateway = BuildGateway();
        }

        // 부팅 게이트는 로그인 후 TransitionAfterLoginController.CheckNextStateAsync에서 Boot()를 호출한다.
        // 여기서 자동 Boot하면 로그인 전(세션 없음)에 폴백 판정으로 화면이 떠버려서 자동 실행하지 않는다.
        // (Boot()는 public이라 그 시점에 호출됨. 에디터 단독 테스트는 ContextMenu의 Editor_RebootAfterLogin 사용)

        // 부팅 흐름: config fetch → (실패 시 fallback) → 버전게이트 판정.
        public async Task Boot()
        {
            string clientVersion = Application.version;   // = PlayerSettings.bundleVersion (A-②)
            string source;

            try
            {
                Config = await _gateway.FetchAsync();   // ③ 라이브
                ConfigCache.Save(Config);                // 성공 → 캐시 갱신
                source = "live";
            }
            catch (Exception e)
            {
                // 라이브 실패 → ② 마지막 성공 캐시 → ① 동봉 기본값 순으로 떨어진다(graceful).
                Debug.LogWarning($"[Backend] 라이브 config 실패 → fallback: {e.Message}");
                Config = ConfigCache.Load();
                if (Config != null)
                {
                    source = "cache";
                }
                else
                {
                    Config = _defaultConfig != null ? _defaultConfig.Value : null;
                    source = Config != null ? "bundled-default" : "none";
                }
            }

            Decision = VersionGate.Evaluate(clientVersion, Config);
            IsReady = true;

            Debug.Log($"[Backend] source={source}, clientVersion={clientVersion}, decision={Decision}" +
                      (Config != null ? $" (maintenance={Config.maintenance}, min={Config.minVersion}, rec={Config.recommendedVersion})"
                                      : " (config 전무 → 통과)"));

            OnReady?.Invoke();
        }

        // 게이트웨이 조립 — _usePlayFab 토글로 실연결/Mock 선택(재컴파일 없이 인스펙터에서 전환).
        //  · ON  = PlayFabBackendGateway(GetTitleData) 실연결. 세연 익명로그인 세션 전제, 없으면 fallback.
        //  · OFF = MockBackendGateway. 인스펙터 값으로 차단/점검/실패 시나리오 흉내(오프라인 개발용).
        private IBackendGateway BuildGateway()
        {
            if (_usePlayFab)
                return new PlayFabBackendGateway(_titleDataKey);

            var cfg = new RemoteConfig
            {
                minVersion = _mockMinVersion,
                recommendedVersion = _mockRecommendedVersion,
                maintenance = _mockMaintenance,
                maintenanceMessage = "점검 중입니다. 잠시 후 다시 시도해 주세요.",
                dataVersion = _mockDataVersion,
                balance = new[] { new BalanceEntry { key = "goldMultiplier", value = _mockGoldMultiplier } }  // 데모용 1값
            };
            return new MockBackendGateway(cfg, _mockDelayMs, _mockSimulateFailure);
        }

#if UNITY_EDITOR
        // 에디터 테스트용 — 저장된 fallback 캐시(PlayerPrefs)를 비운다.
        // 캐시에 옛 config(예: 점검 ON)가 남아 fallback 시 그게 뜨는 걸 초기화할 때 사용.
        [ContextMenu("캐시 비우기 (ConfigCache.Clear)")]
        private void Editor_ClearCache()
        {
            ConfigCache.Clear();
            Debug.Log("[Backend] fallback 캐시 비움 — 다음 라이브 실패 시 동봉 기본값(DefaultRemoteConfig)으로 떨어짐.");
        }

        // 에디터 검증 전용 — 로그인 완료 후 컴포넌트 우클릭으로 호출하면 세션이 선 상태로 Boot 재실행 → 라이브 읽기 확인.
        // (부트 순서 정식 통합 = "로그인 후 자동 Boot"는 YWJ RSM 배치 + 3자 합의 후. 이건 수동 검증용.)
        [ContextMenu("검증: 로그인 후 Boot 재실행")]
        private async void Editor_RebootAfterLogin() => await Boot();
#endif
    }
}
