using System;
using UnityEngine;
using RMS.Data;      // StageData
using RMS.Fishing;   // FishSpawnManager
using Runtime;       // RuntimeState, RuntimeStateController, RuntimeStateEventBus
using JHS.Sound;     // HS_Bgm 파사드

namespace JHS.Fishing
{
    // 스테이지/화면에 맞춰 BGM을 고르는 '번역기'. 실제 재생은 HS_Bgm 파사드에 위임(DIP).
    //  - 타이틀/로그인/오프라인정산(GAMESTART·LOGIN·OFFLINEREWARD): 타이틀 BGM
    //  - 메인 낚시: '맵(mapId)'별 BGM. 같은 맵 서브스테이지(-1~-5)끼린 안 바뀌고, 맵 전환(1→2→3)때만 교체. 맵당 다곡=셔플.
    //  - 보스: 보스 1곡 루프 / 멀티: 멀티 1곡 루프
    // 신호: 화면 구간은 RuntimeStateEventBus, 맵/보스 판별은 FishSpawnManager.OnStageChanged("1-3"/"1-boss")로(원준 무수정 구독).
    // ⚠️ '항상 활성'인 오브젝트에 둘 것.
    public class StageBgmController : MonoBehaviour
    {
        [Serializable]
        public class MapBgm
        {
            public int mapId;            // 1=산들바람, 2=깊은물목, 3=심해균열 …
            public AudioClip[] bgm;      // 그 맵의 BGM(여러 곡이면 랜덤 셔플, 1곡이면 루프)
        }

        [Header("타이틀/로그인 BGM (게임 시작 전, 여러 곡이면 랜덤 셔플)")]
        [SerializeField] private AudioClip[] _titleBgm;

        [Header("맵별 메인 BGM")]
        [SerializeField] private MapBgm[] _mapBgm;

        [Header("보스 BGM (1곡 루프)")]
        [SerializeField] private AudioClip _bossStageBgm;

        [Header("멀티 BGM (1곡 루프)")]
        [SerializeField] private AudioClip _multiStageBgm;

        [Header("스테이지 신호원 (비우면 자동 검색)")]
        [SerializeField] private FishSpawnManager _fishSpawnManager;

        // 같은 대상 재진입 시 곡을 다시 시작하지 않도록 현재 재생 키를 기억("title"/"map:2"/"boss"/"multi").
        private string _currentKey = "";

        private void OnEnable()
        {
            // 게임 시작 전 구간 → 타이틀 BGM
            RuntimeStateEventBus.OnGameStartStateEntered    += HandleTitle;
            RuntimeStateEventBus.OnLoginStateEntered        += HandleTitle;
            RuntimeStateEventBus.OnOfflineRewardStateEntered += HandleTitle;
            // 인게임 진입/전환
            RuntimeStateEventBus.OnMainStageStateEntered    += HandleMainEntered;
            RuntimeStateEventBus.OnBossStageStateEntered    += HandleBoss;
            RuntimeStateEventBus.OnMultiStageStateEntered   += HandleMulti;
            RuntimeStateEventBus.OnMultiStageStateExited    += HandleMultiExit;
            EnsureSubscribed();
        }

        private void OnDisable()
        {
            RuntimeStateEventBus.OnGameStartStateEntered    -= HandleTitle;
            RuntimeStateEventBus.OnLoginStateEntered        -= HandleTitle;
            RuntimeStateEventBus.OnOfflineRewardStateEntered -= HandleTitle;
            RuntimeStateEventBus.OnMainStageStateEntered    -= HandleMainEntered;
            RuntimeStateEventBus.OnBossStageStateEntered    -= HandleBoss;
            RuntimeStateEventBus.OnMultiStageStateEntered   -= HandleMulti;
            RuntimeStateEventBus.OnMultiStageStateExited    -= HandleMultiExit;
            if (_fishSpawnManager != null) _fishSpawnManager.OnStageChanged -= HandleStageChanged;
        }

        // Start에서 구독 보장 + 현재 상태로 초기 동기화(놓친 진입 이벤트 방어).
        private void Start()
        {
            EnsureSubscribed();
            SyncInitialState();
        }

        private void EnsureSubscribed()
        {
            if (_fishSpawnManager == null) _fishSpawnManager = FindFirstObjectByType<FishSpawnManager>();
            if (_fishSpawnManager == null) return;
            _fishSpawnManager.OnStageChanged -= HandleStageChanged;   // 중복 구독 방지
            _fishSpawnManager.OnStageChanged += HandleStageChanged;
        }

        private void SyncInitialState()
        {
            var rsc = FindFirstObjectByType<RuntimeStateController>();
            var st = rsc != null ? rsc.CurrentState : RuntimeState.GAMESTART;
            switch (st)
            {
                case RuntimeState.MAINSTAGE:
                case RuntimeState.STAGECHANGE:  HandleMainEntered(); break;
                case RuntimeState.BOSSSTAGE:    HandleBoss();        break;
                case RuntimeState.MULTISTAGE:
                case RuntimeState.MULTIMATCHING: HandleMulti();      break;
                default:                        HandleTitle();       break;   // 타이틀/로그인/메뉴 등
            }
        }

        // ---- 화면 구간 핸들러 ----
        private void HandleTitle() => Play("title", () => HS_Bgm.PlayShuffle(_titleBgm));

        // 메인스테이지 진입: 현재 스테이지의 맵 BGM으로.
        // ⚠️ 이 오브젝트(BGM_System)는 DontDestroyOnLoad로 씬 전환에도 유지되므로, 멀티 다녀오면 _fishSpawnManager가
        //    파괴된 옛 인스턴스를 가리킬 수 있다 → 매 메인 진입마다 재획득·재구독해 맵/보스 BGM이 계속 동작하게 한다.
        private void HandleMainEntered()
        {
            EnsureSubscribed();
            if (_fishSpawnManager != null && _fishSpawnManager.CurrentStage != null)
                HandleStageChanged(_fishSpawnManager.CurrentStage);
        }

        private void HandleBoss()  => Play("boss",  () => HS_Bgm.PlayLoop(_bossStageBgm));
        private void HandleMulti() => Play("multi", () => HS_Bgm.PlayLoop(_multiStageBgm));

        private void HandleMultiExit()
        {
            _currentKey = "";
            if (_fishSpawnManager != null && _fishSpawnManager.CurrentStage != null)
                HandleStageChanged(_fishSpawnManager.CurrentStage);
        }

        // ---- 맵/보스 전환(스테이지 데이터 기반) ----
        private void HandleStageChanged(StageData stage)
        {
            if (stage == null) return;
            if (!TryParseStage(stage.StageId, out int mapId, out bool isBoss)) return;

            if (isBoss) Play("boss", () => HS_Bgm.PlayLoop(_bossStageBgm));
            else Play("map:" + mapId, () => HS_Bgm.PlayShuffle(GetMapClips(mapId)));
        }

        // 같은 키면 무시(서브스테이지 이동/재진입 시 곡 안 끊김), 바뀌면 재생.
        private void Play(string key, Action play)
        {
            if (_currentKey == key) return;
            _currentKey = key;
            play();
        }

        private AudioClip[] GetMapClips(int mapId)
        {
            if (_mapBgm != null)
                foreach (var m in _mapBgm)
                    if (m != null && m.mapId == mapId) return m.bgm;
            return null;   // 미등록 맵이면 무음(HS_Bgm가 빈 배열 무시)
        }

        // "1-3" -> map 1, "1-boss" -> map 1 + boss. (StageBackgroundController와 동일 규칙)
        private bool TryParseStage(string stageId, out int mapId, out bool isBoss)
        {
            mapId = 0; isBoss = false;
            if (string.IsNullOrEmpty(stageId)) return false;
            var split = stageId.Split('-');
            if (split.Length != 2) return false;
            if (!int.TryParse(split[0], out mapId)) return false;
            isBoss = split[1].Equals("boss", StringComparison.OrdinalIgnoreCase);
            return true;
        }
    }
}
