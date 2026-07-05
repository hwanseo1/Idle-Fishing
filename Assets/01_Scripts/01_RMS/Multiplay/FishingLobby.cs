using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


namespace RMS.Multiplay
{
    /*
        낚시 게임 자동 매칭 로비.
    
        흐름:
        1. 플레이어가 매칭 버튼 클릭
        2. 현재 스테이지 진행도 기준으로 레벨 구간(Tier) 계산
        3. 같은 Tier의 방이 있으면 자동 입장(Client), 없으면 자동 방 생성(Host)
        4. 3명 모이면 3초 카운트다운 -> LobbyUI 끄고 FishingGameManager에 시작 신호


        레벨 Tier 기준 (maxStageId의 region):
        Tier 0 : region 1 (1지역, 예: 1-1~1-boss)
        Tier 1 : region 2 (2지역, 예: 2-1~2-boss)
        Tier 2 : region 3 (3지역, 예: 3-1~3-boss)
        ...region 번호 자체가 Tier가 된다.
    */

    public class FishingLobby : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region 상수
        private const int MAX_PLAYERS = 3;
        private const float COUNTDOWN_SECONDS = 3f;

        // SessionProperty 키
        private const string SESSION_KEY_TIER = "levelTier";
        #endregion


        #region Inspector
        [Header("Fusion")]
        [SerializeField] private NetworkRunner runnerPrefab;

        [Header("Panels")]
        [SerializeField] private GameObject lobbyUI;        // LobbyUI 루트 (게임 시작 시 비활성화)
        [SerializeField] private GameObject mainPanel;      // 매칭 시작 화면
        [SerializeField] private GameObject matchingPanel;  // 매칭 중 화면
        [SerializeField] private GameObject roomPanel;      // 방 입장 후 대기 화면

        [Header("Main Panel UI")]
        [SerializeField] private TMP_Text mainStatusText;
        [SerializeField] private Button matchButton;
        [SerializeField] private Button mainExitButton;

        [Header("Matching Panel UI")]
        [SerializeField] private TMP_Text matchingStatusText;
        [SerializeField] private Button cancelButton;

        [Header("Room Panel UI")]
        [SerializeField] private TMP_Text playerListText;
        [SerializeField] private TMP_Text roomStatusText;
        [SerializeField] private Button exitButton;

        [Header("Player Info")]
        [Tooltip("region 추출 실패 시 폴백 값으로 쓰인다.")]
        [SerializeField] private int currentStage = 1;

        [Header("하루 3회 제한")]
        [Tooltip("남은 횟수 표시. 예: '오늘 남은 횟수 2/3'")]
        [SerializeField] private TMP_Text playLimitText;
        [Tooltip("3회를 모두 사용했을 때만 활성화되는 안내 패널/텍스트. mainPanel 하위에 두는 걸 권장.")]
        [SerializeField] private GameObject playLimitExceededNotice;

        [Header("하루 3회 제한 리셋 - 디버그용")]
        [Tooltip("⚠️ TODO: 출시 전 제거 또는 비활성화 필요. 지금은 클라이언트 빌드 테스트 편의를 위해 빌드에도 노출된 상태.")]
        [SerializeField] private Button debugResetLimitButton;
        #endregion


        #region 하루 3회 제한 - 게이트웨이
        private readonly IMultiplayLimitGateway _limitGateway = new MultiplayLimitGatewayAdapter();
        #endregion


        #region 로컬 상태
        private NetworkRunner _runner;
        private bool _isCountingDown = false;
        #endregion


        #region Unity 생명주기
        private void Awake()
        {
            ShowMainPanel();
            RefreshPlayLimitStatus();
        }

        private void OnEnable()
        {
            matchButton.onClick.AddListener(OnClickMatch);
            cancelButton.onClick.AddListener(OnClickCancel);
            exitButton.onClick.AddListener(OnClickExit);

            if (mainExitButton != null)
                mainExitButton.onClick.AddListener(OnClickExit);

            if (debugResetLimitButton != null)
                debugResetLimitButton.onClick.AddListener(OnClickDebugResetLimit);
        }

        private void OnDisable()
        {
            matchButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();

            if (mainExitButton != null)
                mainExitButton.onClick.RemoveListener(OnClickExit);

            if (debugResetLimitButton != null)
                debugResetLimitButton.onClick.RemoveListener(OnClickDebugResetLimit);
        }
        #endregion


        #region 매칭 시작
        // 매칭 버튼 클릭 시 가장 먼저 하루 3회 제한을 검사한다.
        // 통과(canPlay)해야만 기존 매칭 로직(StartMatchmaking)으로 진입한다.
        // ⚠️ 실제 차감은 매치가 "정상 종료" (시간 만료, isEarlyExit=false)로 끝나는 시점에 FishingGameManager가 수행한다.
        private void OnClickMatch()
        {
            matchButton.interactable = false;

            //if (mainStatusText != null) mainStatusText.text = "확인 중...";
            if (playLimitExceededNotice != null) playLimitExceededNotice.SetActive(false);

            _limitGateway.RequestStatus(result =>
            {
                if (!result.Success)
                {
                    // 통신 실패. 제한 검사를 통과시키지 않고 매칭도 막는다(서버 확인 없이는 진행하지 않음).
                    ShowMainPanel("횟수 확인에 실패했습니다. 다시 시도해주세요.");
                    return;
                }

                RefreshPlayLimitText(result);

                if (!result.CanPlay)
                {
                    if (playLimitExceededNotice != null) playLimitExceededNotice.SetActive(true);
                    matchButton.interactable = true;
                    //if (mainStatusText != null) mainStatusText.text = "매칭을 시작하세요.";
                    return;
                }

                StartMatchmaking();
            });
        }

        private async void StartMatchmaking()
        {
            ShowMatchingPanel();

            // 기존 Runner 정리
            if (_runner != null)
            {
                await _runner.Shutdown();
                if (_runner != null) Destroy(_runner.gameObject);
            }

            _runner = Instantiate(runnerPrefab);
            _runner.AddCallbacks(this);
            _runner.ProvideInput = true;

            int tier = GetLevelTier(ResolveCurrentRegion());

            // AutoHostOrClient:
            // 같은 Tier 방이 있으면 → 자동으로 Client로 입장
            // 없으면             → 자동으로 Host로 방 생성
            var args = new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = null,
                PlayerCount = MAX_PLAYERS,
                SessionProperties = new Dictionary<string, SessionProperty>
            {
                { SESSION_KEY_TIER, tier }
            },
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
            };

            //if (matchingStatusText)
                //matchingStatusText.text = $"매칭 중... (Tier {tier})";

            var result = await _runner.StartGame(args);
            if (this == null) return;

            if (result.Ok)
            {
                DontDestroyOnLoad(_runner.gameObject);
                ShowRoomPanel();
            }
            else
            {
                ShowMainPanel($"매칭 실패: {result.ShutdownReason}");
                if (_runner != null) Destroy(_runner.gameObject);
                _runner = null;
                matchButton.interactable = true;
            }
        }
        #endregion


        #region  플레이어 입장/퇴장 → 자동 시작 체크
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            RefreshPlayerList();
            CheckAutoStart();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            _isCountingDown = false;
            RefreshPlayerList();
        }

        private void CheckAutoStart()
        {
            if (_isCountingDown || _runner == null) return;

            int count = 0;
            foreach (var _ in _runner.ActivePlayers) count++;

            if (count >= MAX_PLAYERS)
                StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            _isCountingDown = true;
            exitButton.interactable = false;

            for (int i = (int)COUNTDOWN_SECONDS; i > 0; i--)
            {
                if (roomStatusText) roomStatusText.text = $"게임 시작! {i}초...";
                yield return new WaitForSeconds(1f);
            }

            // LobbyUI 끄기
            if (lobbyUI != null)
                lobbyUI.SetActive(false);

            // FishingGameManager에 GameUI 활성화 + 매치 시작 신호
            // 모든 클라이언트에서 각자 실행되므로 RPC 불필요
            var gameManager = FindFirstObjectByType<FishingGameManager>();
            if (gameManager != null)
            {
                gameManager.ShowGameUI();
                gameManager.OnLobbyCountdownFinished();
            }

            // 스폰된 모든 PlayerController의 scoreUI 활성화
            foreach (var pc in FindObjectsByType<FishingPlayerController>(FindObjectsSortMode.None))
                pc.ShowScoreUI();
        }
        #endregion


        #region 레벨 Tier 계산
        /*
            Tier 0: 1지역 (maxStageId의 region이 1)
            Tier 1: 2지역 (region이 2)
            Tier 2: 3지역 (region이 3)
            ...
        */
        private int GetLevelTier(int region)
        {
            return Mathf.Max(0, region - 1);
        }

        // PlayFabDataStore의 maxStageId("region-stage" 형식, 예: "2-3")에서 region 숫자만 추출한다.
        // 못 읽어오면 인스펙터의 currentStage(더 이상 정상 경로로는 안 쓰는 폴백 값)로 대체.
        private int ResolveCurrentRegion()
        {
            string maxStageId = PlayFabDataStore.Instance != null
                ? PlayFabDataStore.Instance.GetPlayerInfo()?.stage?.maxStageId
                : null;

            if (string.IsNullOrEmpty(maxStageId))
            {
                Debug.LogWarning("[FishingLobby] maxStageId를 가져오지 못해 currentStage(폴백값)를 사용합니다.");
                return currentStage;
            }

            int dash = maxStageId.IndexOf('-');
            string regionPart = dash >= 0 ? maxStageId.Substring(0, dash) : maxStageId;

            if (!int.TryParse(regionPart, out int region) || region < 1)
            {
                Debug.LogWarning($"[FishingLobby] maxStageId={maxStageId}에서 region 파싱 실패, currentStage(폴백값)를 사용합니다.");
                return currentStage;
            }

            return region;
        }
        #endregion


        #region 취소 / 나가기
        private void OnClickCancel()
        {
            StartCoroutine(CancelRoutine());
        }

        private IEnumerator CancelRoutine()
        {
            cancelButton.interactable = false;
            //if (matchingStatusText) matchingStatusText.text = "취소 중...";

            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                yield return new WaitUntil(() => shutdown.IsCompleted);
                if (_runner != null) Destroy(_runner.gameObject);
            }

            _runner = null;
            yield return new WaitForSeconds(0.3f);
            ShowMainPanel();
            matchButton.interactable = true;
        }

        private void OnClickExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            exitButton.interactable = false;
            if (mainExitButton != null) mainExitButton.interactable = false;

            //if (roomStatusText) roomStatusText.text = "나가는 중...";

            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                yield return new WaitUntil(() => shutdown.IsCompleted);
                if (_runner != null) Destroy(_runner.gameObject);
            }

            _runner = null;
            yield return new WaitForSeconds(0.3f);

            bool canLoad = false;
            yield return FisherInventorySceneTransitionGuard.FlushAndRefreshBeforeSceneLoad(
                this,
                "LoadScene(0):LobbyExit",
                result => canLoad = result);

            if (!canLoad)
            {
                if (roomStatusText) roomStatusText.text = "인벤토리 동기화 실패";
                exitButton.interactable = true;
                if (mainExitButton != null) mainExitButton.interactable = true;
                yield break;
            }

            SceneManager.LoadScene(0);  // 메인씬으로 복귀
        }
        #endregion


        #region UI 헬퍼
        private void ShowMainPanel(string msg = "")
        {
            mainPanel?.SetActive(true);
            matchingPanel?.SetActive(false);
            roomPanel?.SetActive(false);
            if (mainStatusText) mainStatusText.text = msg;
            matchButton.interactable = true;
        }

        private void ShowMatchingPanel()
        {
            mainPanel?.SetActive(false);
            matchingPanel?.SetActive(true);
            roomPanel?.SetActive(false);
            cancelButton.interactable = true;
        }

        private void ShowRoomPanel()
        {
            mainPanel?.SetActive(false);
            matchingPanel?.SetActive(false);
            roomPanel?.SetActive(true);
            exitButton.interactable = true;
            _isCountingDown = false;
            RefreshPlayerList();
        }

        private void RefreshPlayerList()
        {
            if (_runner == null || !_runner.IsRunning) return;

            var sb = new StringBuilder();
            int count = 0;

            foreach (var p in _runner.ActivePlayers)
            {
                count++;
                bool isMe = p == _runner.LocalPlayer;
                sb.AppendLine($"\nPlayer {p.PlayerId}{(isMe ? " (나)" : "")}");
            }

            if (playerListText) playerListText.text = sb.ToString();
            if (roomStatusText && !_isCountingDown)
                roomStatusText.text = $"대기 중 {count}/{MAX_PLAYERS}";
        }
        #endregion


        #region 하루 3회 제한
        // 로비 화면 진입 시(Awake) 호출. 차감 없이 현재 상태만 조회해 남은 횟수를 표시한다.
        private void RefreshPlayLimitStatus()
        {
            _limitGateway.RequestStatus(result =>
            {
                if (!result.Success)
                {
                    if (playLimitText != null) playLimitText.text = "남은 횟수 확인 실패";
                    return;
                }

                RefreshPlayLimitText(result);

                if (playLimitExceededNotice != null)
                    playLimitExceededNotice.SetActive(!result.CanPlay);
            });
        }

        private void RefreshPlayLimitText(MultiplayLimitResult result)
        {
            if (playLimitText == null) return;

            int remaining = Mathf.Max(0, result.MaxPlayCount - result.PlayCount);
            playLimitText.text = $"{remaining}/{result.MaxPlayCount}";
        }

        // ⚠️ TODO: 출시 전 제거 또는 비활성화 필요. 디버그 빌드 테스트 편의를 위해 지금은 빌드에도 노출.
        private void OnClickDebugResetLimit()
        {
            _limitGateway.DebugReset(result =>
            {
                if (!result.Success) return;

                RefreshPlayLimitText(result);
                if (playLimitExceededNotice != null)
                    playLimitExceededNotice.SetActive(!result.CanPlay);
            });
        }
        #endregion


        #region INetworkRunnerCallbacks
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (this == null || gameObject == null) return;
            if (roomPanel != null && roomPanel.activeSelf)
            {
                //ShowMainPanel($"연결 끊김: {reason}");
                if (_runner != null) { Destroy(_runner.gameObject); _runner = null; }
            }
        }

        // ── 미사용 콜백 (인터페이스 구현 필수, 지금은 쓸 내용 없음) ──
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress addr, NetConnectFailedReason reason) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> list) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr msg) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        #endregion
    }
}
