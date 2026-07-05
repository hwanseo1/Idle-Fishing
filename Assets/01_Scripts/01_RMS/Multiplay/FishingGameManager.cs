using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace RMS.Multiplay
{
    // 낚시 경쟁 미니게임(멀티) GameManager.
    // 플레이어 스폰, 3인 매치 타이머, 전체/개인 기여도 집계, 정산 및 메인씬 복귀를 담당한다.
    public class FishingGameManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region 상수
        private const int MAX_PLAYERS = 3;
        private const float END_DELAY = 3f; // 종료 후 메인씬 복귀까지 딜레이
        #endregion


        #region Inspector
        [Header("Prefabs")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("TotalContributionSync가 붙은 NetworkObject 프리팹. Host가 매치 시작 시 1개 스폰한다.")]
        [SerializeField] private NetworkObject totalContributionSyncPrefab;

        [Header("Spawn Points")]
        [Tooltip("플레이어 수(3)만큼 배치하세요.")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Match Settings")]
        [SerializeField] private float matchDuration = 180f; // 3분
        [SerializeField] private int totalContributionGoal = 300;  // 전체 기여도 목표치(임시)

        [Header("UI")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;    // "ex.대기 중 N/3", "게임 시작!" 등
        [SerializeField] private TMP_Text totalContributionText;    // "ex.전체 기여도 N/1000"
        [SerializeField] private TMP_Text myStageIdText;    // "ex.내 진행도: 2-3"
        [SerializeField] private GameObject gameUI;

        [Header("플레이어 점수판 (화면 고정 UI)")]
        [Tooltip("PlayerIndex 1, 2, 3 순서로 등록. 각 슬롯은 P-라벨과 점수 텍스트를 담는다.")]
        [SerializeField] private PlayerScoreSlot[] playerScoreSlots;

        [Header("정산 결과 팝업")]
        [Tooltip("매치 종료 시 표시할 정산 팝업. 비워두면 기존 방식(자동 대기 후 메인씬 복귀)으로 폴백한다.")]
        [SerializeField] private MatchResultPanel resultPanel;
        #endregion


        // 화면 고정 점수판 슬롯 한 칸
        [System.Serializable]
        public class PlayerScoreSlot
        {
            public TMP_Text labelText;   // "P1" 등
            public TMP_Text scoreText;   // 점수
        }


        #region 런타임 상태
        private NetworkRunner _networkRunner;
        private bool _isMatchRunning = false;
        private bool _isMatchOver = false;
        private int _playerCount = 0;
        private float _matchTimer = 0f;

        // 전체 기여도 (네트워크 동기화 — TotalContributionSync 위임)
        public int TotalContribution => _totalContributionSync != null ? _totalContributionSync.TotalContribution : 0;
        private TotalContributionSync _totalContributionSync;
        #endregion


        #region 하루 3회 제한 - 게이트웨이
        // 정상 종료(시간 만료)일 때만 TryConsume 호출. PlayFab 계정 상태라 Host/Client 구분 없이 각자 호출한다.
        private readonly IMultiplayLimitGateway _limitGateway = new MultiplayLimitGatewayAdapter();
        #endregion


        #region 로컬 상태
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
        private Coroutine _timerCoroutine;
        #endregion


        #region Fusion 생명주기
        private void Start()
        {
            if (gameUI != null) gameUI.SetActive(false);
            StartCoroutine(WaitForRunner());
        }

        private void Update()
        {
            UpdateStatusUI();
        }

        private void OnDestroy()
        {
            if (_networkRunner != null)
                _networkRunner.RemoveCallbacks(this);
        }

        // Runner가 생성될 때까지 대기 후 콜백 등록
        private IEnumerator WaitForRunner()
        {
            while (true)
            {
                _networkRunner = FindFirstObjectByType<NetworkRunner>();
                if (_networkRunner != null && _networkRunner.IsRunning)
                {
                    _networkRunner.AddCallbacks(this);
                    Debug.Log("[FishingGameManager] Runner 연결 완료.");

                    // Host만 TotalContributionSync 스폰 (StateAuthority를 가져야 하므로 Host 전용)
                    if (_networkRunner.IsServer)
                    {
                        SpawnPlayer(_networkRunner, _networkRunner.LocalPlayer);
                        SpawnTotalContributionSync(_networkRunner);
                    }

                    // 모든 클라이언트(Host 포함)는 sync 인스턴스가 생길 때까지 별도로 대기
                    // (클라는 Host가 스폰한 NetworkObject가 복제되어 도착할 때까지 기다려야 함)
                    StartCoroutine(WaitForTotalContributionSync());

                    yield break;
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        // sync 오브젝트가 (Host의 스폰 → 복제 또는 자기 스폰으로) 씬에 나타날 때까지 대기.
        private IEnumerator WaitForTotalContributionSync()
        {
            while (_totalContributionSync == null)
            {
                _totalContributionSync = FindFirstObjectByType<TotalContributionSync>();
                if (_totalContributionSync != null)
                {
                    Debug.Log("[FishingGameManager] TotalContributionSync 연결 완료.");
                    RefreshTotalContributionUI();
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        // Host 전용: 매치 전체에서 공유할 TotalContributionSync 1개를 스폰한다.
        private void SpawnTotalContributionSync(NetworkRunner runner)
        {
            if (totalContributionSyncPrefab == null)
            {
                Debug.LogWarning("[FishingGameManager] totalContributionSyncPrefab이 비어 있습니다. 전체 기여도가 동기화되지 않습니다.");
                return;
            }

            var obj = runner.Spawn(totalContributionSyncPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
        }
        #endregion


        #region 매치 타이머
        private IEnumerator MatchTimerRoutine()
        {
            _matchTimer = matchDuration;

            while (_matchTimer > 0f && !_isMatchOver)
            {
                _matchTimer -= Time.deltaTime;
                UpdateTimerUI();
                yield return null;
            }

            if (!_isMatchOver)
            {
                _matchTimer = 0f;
                UpdateTimerUI();
                EndMatch("시간 종료!", notifyClients: true, isEarlyExit: false);
            }
        }
        #endregion


        #region 전체 기여도
        public void AddTotalContribution(int amount)
        {
            // 클라이언트에서 호출된 경우 무시 (Host만 갱신 권한)
            if (_networkRunner != null && !_networkRunner.IsServer) return;
            if (_totalContributionSync == null)
            {
                Debug.LogWarning("[FishingGameManager] TotalContributionSync가 아직 없습니다. 기여도 추가를 건너뜁니다.");
                return;
            }

            _totalContributionSync.AddContribution(amount);
            // UI 갱신과 목표 체크는 Render()에서 변경을 감지한 OnTotalContributionSynced가 처리한다.
        }

        // TotalContributionSync.Render()에서 값이 바뀔 때마다 호출됨 (모든 클라이언트, 매치 시작 전후 동일하게 동작).
        public void OnTotalContributionSynced(int total)
        {
            RefreshTotalContributionUI();
            CheckContributionGoal();
        }

        private void CheckContributionGoal()
        {
            if (TotalContribution >= totalContributionGoal && !_isMatchOver)
            {
                Debug.Log("<color=#ffb700>[FishingGameManager] 전체 기여도 목표 달성!</color>");
                // TODO: 전체 보상 달성 연출 (UI 이펙트 등)
            }
        }

        private void RefreshTotalContributionUI()
        {
            if (totalContributionText != null)
                totalContributionText.text = $"TOTAL SCORE {TotalContribution} / {totalContributionGoal}";
        }
        #endregion


        #region 플레이어 스폰 / 디스폰
        private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            int index = _spawnedPlayers.Count;
            Vector3 pos = (spawnPoints != null && index < spawnPoints.Length)
                ? spawnPoints[index].position
                : new Vector3(index * 3f, 0f, 0f);

            int playerIndex = index + 1; // 1, 2, 3

            NetworkObject obj = runner.Spawn(
                playerPrefab, pos, Quaternion.identity, player,
                (r, o) =>
                {
                    var pc = o.GetComponent<FishingPlayerController>();
                    if (pc != null) pc.PlayerIndex = playerIndex;
                }
            );

            _spawnedPlayers[player] = obj;
            _playerCount = _spawnedPlayers.Count;

            Debug.Log($"[FishingGameManager] Player {player.PlayerId} 스폰 완료. 현재 {_playerCount}/{MAX_PLAYERS}");
        }

        private void DespawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!_spawnedPlayers.TryGetValue(player, out var obj)) return;

            var pc = obj.GetComponent<FishingPlayerController>();
            if (pc != null && _totalContributionSync != null)
                _totalContributionSync.MarkPlayerLeft(pc.PlayerIndex);

            runner.Despawn(obj);
            _spawnedPlayers.Remove(player);
            _playerCount = _spawnedPlayers.Count;

            // 게임 중 퇴장 시 종료 처리. Host가 EndMatch를 실행하고 모든 클라에 신호를 전파한다.
            if (_isMatchRunning && !_isMatchOver)
                EndMatch($"Player {player.PlayerId} 퇴장으로 종료", notifyClients: true, isEarlyExit: true);
        }
        #endregion


        #region 플레이어 점수판
        // FishingPlayerController.Render()에서 Score 변경 시 호출. RPC 불필요(각자 로컬 동기화 값을 읽는 것뿐).
        public void UpdatePlayerScoreUI(int playerIndex, int score)
        {
            int slotIndex = playerIndex - 1;
            if (playerScoreSlots == null || slotIndex < 0 || slotIndex >= playerScoreSlots.Length)
                return;

            var slot = playerScoreSlots[slotIndex];
            if (slot == null) return;

            if (slot.labelText != null)
                slot.labelText.text = $"P{playerIndex}";
            if (slot.scoreText != null)
                slot.scoreText.text = $"{score}";
        }

        // FishingPlayerController.Spawned()에서 본인 클라이언트만 호출. RPC 불필요(본인 화면에만 표시).
        public void UpdateMyStageIdUI(string maxStageId)
        {
            if (myStageIdText != null)
                myStageIdText.text = $"My Section {maxStageId}";
        }

        // 매치 시작 시 라벨만 먼저 표시(점수는 0부터). 슬롯이 비활성 상태로 안 보이게 두는 대신
        // 바로 P1/P2/P3와 0점을 보여줘 누가 참가했는지 미리 알 수 있게 한다.
        private void InitPlayerScoreSlots()
        {
            if (playerScoreSlots == null) return;
            for (int i = 0; i < playerScoreSlots.Length; i++)
                UpdatePlayerScoreUI(i + 1, 0);
        }
        #endregion


        #region 매치 흐름
        // FishingLobby의 카운트다운 완료 후 외부에서 호출
        public void OnLobbyCountdownFinished()
        {
            if (!_isMatchRunning)
                StartMatch();
        }


        // 모든 클라이언트에서 GameUI 켜기 (FishingLobby에서 카운트다운 완료 시 호출)
        public void ShowGameUI()
        {
            if (gameUI != null) gameUI.SetActive(true);
        }


        private void StartMatch()
        {
            _isMatchRunning = true;
            _isMatchOver = false;

            // Host만 리셋 권한 보유 (StateAuthority). 클라는 Render()로 전파받은 값을 그대로 따른다.
            if (_networkRunner != null && _networkRunner.IsServer)
            {
                if (_totalContributionSync != null)
                {
                    _totalContributionSync.ResetContribution();
                    _totalContributionSync.ResetMatchEndFlag(); // 다음 매치에서도 Render() 변경 감지가 동작하도록 리셋
                    _totalContributionSync.ResetLeftPlayers();  // 이전 매치 퇴장 기록이 다음 매치 정산에 섞이지 않도록 리셋
                }

                // 매치 시작 시점에 모든 플레이어의 Score를 0으로 정리한다.
                // (물고기 _pendingCatches 정산은 폐기됨 — 5번 항목 참고. ResetScore만 수행.)
                foreach (var pc in FindObjectsByType<FishingPlayerController>(FindObjectsSortMode.None))
                {
                    pc.ResetScore();
                }
            }

            Debug.Log("<color=#00FFAA>[FishingGameManager] ★ 매치 시작!</color>");

            // [JHS 추가/민서 통지] 멀티 BGM 트리거 — JHS StageBgmController가 이 이벤트를 구독해 멀티 배경음으로 전환.
            // RSM의 MULTISTAGE 상태가 실제로 전환되지 않아(orphan) 이벤트가 안 떠서, 매치 시작 지점에서 직접 발행한다.
            RuntimeStateEventBus.PublishMultiStageStateEntered();

            if (statusText != null) statusText.text = "낚시 시작!";
            RefreshTotalContributionUI();
            InitPlayerScoreSlots();

            // 타이머 시작
            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerCoroutine = StartCoroutine(MatchTimerRoutine());
        }


        // notifyClients=true(호스트 직접 호출 경로)면 TotalContributionSync로 모든 클라에 종료를 전파한다.
        // isEarlyExit: 시간 만료 정상 종료가 아니라 누군가 도중 퇴장해 끝난 조기 종료인지 여부.
        private void EndMatch(string reason, bool notifyClients, bool isEarlyExit)
        {
            // 중복 실행 방지 (Host의 DespawnPlayer/타이머 종료와 클라의 OnMatchEndedSynced가 거의 동시에 들어올 수 있음)
            if (_isMatchOver) return;

            _isMatchOver = true;
            _isMatchRunning = false;

            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }

            Debug.Log($"[FishingGameManager] 매치 종료: {reason} (isEarlyExit={isEarlyExit})");

            if (statusText != null) statusText.text = $"종료: {reason}";

            // 정산 UI 표시 이후로는 자동낚시가 더 반영되지 않도록 즉시 구독 해제
            foreach (var pc in FindObjectsByType<FishingPlayerController>(FindObjectsSortMode.None))
            {
                if (pc != null) pc.StopCollectingMatchResults();
            }

            // 정상 종료일 때만 하루 3회 제한 횟수를 실제로 차감 (조기 종료/매칭 취소는 차감 안 함)
            if (!isEarlyExit)
            {
                _limitGateway.TryConsume(result =>
                {
                    if (!result.Success)
                        Debug.LogWarning("[FishingGameManager] 멀티플레이 횟수 차감 요청 실패(통신 오류).");
                });
            }

            // Host만 모든 클라에 종료 신호 전파 (NotifyMatchEndedToAll 내부에도 HasStateAuthority 체크가 있어 이중 방어됨)
            if (notifyClients && _networkRunner != null && _networkRunner.IsServer && _totalContributionSync != null)
                _totalContributionSync.NotifyMatchEndedToAll(isEarlyExit);

            ShowMatchResult(isEarlyExit);
        }


        // TotalContributionSync.Render()가 IsMatchEnded 변경을 감지했을 때 호출됨 (Host 포함 모든 클라).
        public void OnMatchEndedSynced(bool isEarlyExit)
        {
            EndMatch("호스트 매치 종료 신호 수신", notifyClients: false, isEarlyExit: isEarlyExit);
        }


        // 정산 팝업 표시. resultPanel이 없으면 기존 방식(자동 대기 후 메인씬 복귀)으로 폴백.
        // 정상 종료일 때만 보상을 요청하며, 서버 응답을 기다리지 않고 팝업은 먼저 띄운다.
        private void ShowMatchResult(bool isEarlyExit)
        {
            if (resultPanel == null)
            {
                StartCoroutine(ReturnToMainRoutine());
                return;
            }

            var entries = BuildRankEntries();
            var data = new MatchResultPanel.MatchResultData
            {
                IsEarlyExit = isEarlyExit,
                Entries = entries,
                TotalContribution = TotalContribution,
                TotalContributionGoal = totalContributionGoal,
                GoalReached = TotalContribution >= totalContributionGoal,
            };

            resultPanel.Show(data, OnResultConfirmed);

            if (!isEarlyExit)
                RequestMyReward(entries);
        }


        // 본인(HasInputAuthority) 몫의 보상을 요청한다. entries에서 본인 Score를 찾아 personalContribution으로 사용.
        // 보상은 원준님의 GiveMultiplayReward(personalContribution, totalContribution 점수 구간 기반 테이블) 방식으로 통일.
        // 매치 중 낚은 물고기 자체를 별도로 지급하는 경로는 사용하지 않음.
        private void RequestMyReward(List<MatchResultPanel.RankEntry> entries)
        {
            int myScore = 0;
            foreach (var entry in entries)
            {
                if (entry.IsLocalPlayer)
                {
                    myScore = entry.Score;
                    break;
                }
            }

            _limitGateway.GiveReward(myScore, TotalContribution, outcome =>
            {
                if (resultPanel == null) return;

                if (!outcome.Success)
                {
                    Debug.LogWarning("[FishingGameManager] 멀티플레이 보상 지급 요청 실패(통신 오류).");
                    resultPanel.ShowRewardFailure();
                    return;
                }

                resultPanel.ApplyRewardOutcome(outcome.GrantedItems, outcome.GrantedCurrencies);
            });
        }


        // 퇴장하지 않은(=TotalContributionSync.HasPlayerLeft == false) 컨트롤러만 모아 점수 내림차순으로 순위를 만든다.
        // (정책: 중도 퇴장자는 순위 목록에서 완전히 제외)
        private bool IsTotalContributionSyncUsable()
        {
            return _totalContributionSync != null
                && _totalContributionSync.Object != null
                && _totalContributionSync.Object.IsValid;
        }

        private List<MatchResultPanel.RankEntry> BuildRankEntries()
        {
            var list = new List<MatchResultPanel.RankEntry>();
            bool canCheckLeftPlayers = IsTotalContributionSyncUsable();

            foreach (var pc in FindObjectsByType<FishingPlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null || pc.Object == null || !pc.Object.IsValid)
                    continue;

                if (canCheckLeftPlayers && _totalContributionSync.HasPlayerLeft(pc.PlayerIndex))
                    continue;

                list.Add(new MatchResultPanel.RankEntry
                {
                    PlayerIndex = pc.PlayerIndex,
                    Score = pc.Score,
                    IsLocalPlayer = pc.HasInputAuthority,
                });
            }

            list.Sort((a, b) => b.Score.CompareTo(a.Score)); // 점수 내림차순
            return list;
        }


        // 정산 팝업의 확인 버튼이 호출. 여기서부터 실제 씬 복귀 절차를 시작한다.
        private void OnResultConfirmed()
        {
            StartCoroutine(ReturnToMainRoutine());
        }


        // 메인씬 복귀(정산 팝업의 확인 버튼, 또는 팝업 미연결 시 자동 호출).
        private IEnumerator ReturnToMainRoutine()
        {
            // 팝업이 있던 경로라면 이미 사용자가 확인을 눌렀으니 추가 대기 없이 바로 진행하고,
            // 팝업이 없는 폴백 경로에서만 기존처럼 잠깐 대기해 종료 텍스트를 보여줄 시간을 준다.
            if (resultPanel == null)
                yield return new WaitForSeconds(END_DELAY);

            if (_networkRunner != null)
            {
                _networkRunner.Shutdown();
                yield return new WaitForSeconds(0.3f);
            }

            bool canLoad = false;
            yield return FisherInventorySceneTransitionGuard.FlushAndRefreshBeforeSceneLoad(
                this,
                "LoadScene(0):MatchReturn",
                result => canLoad = result);

            if (!canLoad)
            {
                if (statusText != null) statusText.text = "인벤토리 동기화 실패";
                if (resultPanel != null) resultPanel.ShowInventorySyncFailure();
                yield break;
            }

            SceneManager.LoadScene(0);
        }
        #endregion


        #region UI 헬퍼
        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            if (_isMatchRunning && !_isMatchOver)
            {
                int mins = Mathf.FloorToInt(_matchTimer / 60f);
                int secs = Mathf.FloorToInt(_matchTimer % 60f);
                timerText.text = $"{mins:00}:{secs:00}";
            }
            else if (!_isMatchRunning && !_isMatchOver)
            {
                timerText.text = "--:--";
            }
        }

        private void UpdateStatusUI()
        {
            if (statusText == null || _isMatchRunning || _isMatchOver) return;
            statusText.text = $"대기 중 {_playerCount}/{MAX_PLAYERS}";
        }
        #endregion


        #region INetworkRunnerCallbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer) SpawnPlayer(runner, player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer) DespawnPlayer(runner, player);
        }


        // Host 연결이 끊겼을 때 매치를 강제 종료한다.
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (_isMatchRunning && !_isMatchOver)
                EndMatch($"연결 종료: {reason}", notifyClients: false, isEarlyExit: true);
        }


        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (_isMatchRunning && !_isMatchOver)
                EndMatch($"호스트 연결 끊김: {reason}", notifyClients: false, isEarlyExit: true);
        }


        // ── 미사용 콜백 ──
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        #endregion
    }
}