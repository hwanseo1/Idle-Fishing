using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;

namespace JHS.Equipment
{
    // 장비/배 상태를 "서버 권위"로 연결하는 부트스트랩 어댑터.
    //  1) PlayFab 로그인 완료되면 EquipmentManager에 서버 게이트웨이(PlayFabEquipmentServerGateway) 주입
    //     → 이후 강화/해금/장착은 서버 CloudScript로 처리(골드·레벨·해금 판정 = 서버).
    //  2) 서버에서 장비/배 최신 상태를 "직접 다시 받아온 뒤" 현재 상태 로드.
    //
    // ⚠️ 중요: PlayFabDataStore는 Awake에서 디스크 로컬 캐시(Player_Playfab.json)를 먼저 로드한다.
    //   그 캐시의 장비/배 레벨은 로그인 당시 값(강화분 미반영)이라, 캐시를 바로 읽으면 레벨이 초기화돼 보인다.
    //   → 그래서 store를 그냥 읽지 않고, RefreshEquipmentData/RefreshShipData로 서버 최신값을 store에 받아온 뒤 로드한다.
    //
    // ⚠️ 트리거: 로그인 완료 시점은 가변(자동/게스트/사용자 클릭)이라 Start 1회 타임아웃으로 기다리면
    //   로그인이 그 창보다 늦게 끝날 때 어댑터가 먼저 포기 → _server 미주입 → 장착불가 + 재화가 폴백지갑값으로 표시되는
    //   레이스 버그가 난다. 그래서 "로그인 상태 이탈"(Login_RSM.ExitState → RuntimeStateEventBus.OnLoginStateExited)
    //   이벤트를 구독해 로그인이 "언제 끝나든" 그 시점에 주입/로드한다. 빠른 자동로그인으로 이미 끝나 있던 경우는
    //   OnEnable 즉시 검사로 커버. 둘 다 _initialized/_initCo 가드로 1회만 성공 실행.
    //
    // ※ 과거: 이 어댑터는 레벨을 YWJ DataChangeListener(로컬 세이브)에서 읽고 썼다. 이제 진실은 서버(UserReadOnlyData).
    // ※ 서버 미연결(에디터 단독 등)이면 주입/로드를 조용히 스킵 → EquipmentManager 기본값 + 로컬 더미로 단독 동작.
    //   협업 규칙: YWJ 코드 무수정, RuntimeStateEventBus/PlayFabGateway/PlayFabDataStore 공개 API만 사용.
    public class EquipmentSaveAdapter : MonoBehaviour
    {
        [Tooltip("게이트웨이/서버 응답을 기다리는 최대 시간(초). 초과 시 가진 값으로 진행.")]
        [SerializeField] private float _readyTimeoutSec = 15f;

        private const string PlayerId = "player1";   // PlayFabDataStore 기본 플레이어 키

        private bool _initialized;     // 한 번 성공하면 재실행 방지(이벤트 다중 발화·중복 주입 방지)
        private Coroutine _initCo;     // 진행 중 코루틴 가드(동시 실행 방지)

        private void OnEnable()
        {
            RuntimeStateEventBus.OnLoginStateExited += HandleLoginDone;
            // 멀티 씬 복귀 등으로 MainScene이 리로드되면 어댑터는 새 인스턴스(_initialized=false)인데,
            // 재로그인이 없어 OnLoginStateExited가 다시 오지 않는다. 메인 진입 신호로 초기화를 재시도한다.
            RuntimeStateEventBus.OnMainStageStateEntered += HandleLoginDone;

            // 이미 로그인된 채로 재활성화된 경우(빠른 자동로그인 / 씬 리로드 복귀) 즉시 시도.
            // ⚠️ 과거엔 PlayFabGateway.Instance까지 '즉시' 준비돼야만 걸었는데, 씬 리로드 타이밍에 게이트웨이가
            //    늦게 뜨면 이 창을 놓치고(이벤트도 재발화 안 함) 어댑터가 영영 미초기화 → 서버 미연결(구매/장착 불가)
            //    + 스냅샷 미로드(레벨·해금·장착배 기본값)로 이어졌다. (멀티 복귀 버그의 실제 원인)
            //  → 로그인만 돼있으면 TryInit을 걸고, 게이트웨이 준비 대기는 InitWhenReady 내부 루프에 맡긴다.
            if (PlayFabClientAPI.IsClientLoggedIn())
                TryInit();
        }

        private void OnDisable()
        {
            RuntimeStateEventBus.OnLoginStateExited -= HandleLoginDone;
            RuntimeStateEventBus.OnMainStageStateEntered -= HandleLoginDone;
            // 코루틴이 중간에 멈추면(비활성화 등) _initCo가 남아 다음 초기화가 영구 차단된다 → 리셋(재활성 시 재시도 가능).
            // _initialized는 건드리지 않으므로 이미 성공한 경우 재실행되지 않는다.
            if (_initCo != null) { StopCoroutine(_initCo); _initCo = null; }
        }

        // 로그인 상태 이탈 = 로그인 성공 후 다음 상태로 전이하는 시점(세션 발급 보장).
        private void HandleLoginDone() => TryInit();

        private void TryInit()
        {
            if (_initialized || _initCo != null) return;   // 이미 성공했거나 진행 중이면 무시
            if (!isActiveAndEnabled) return;               // 코루틴 시작 조건
            _initCo = StartCoroutine(InitWhenReady());
        }

        private IEnumerator InitWhenReady()
        {
            // 1) 게이트웨이/세션 준비 대기. 이벤트 경로면 이미 충족돼 즉시 통과. (만약을 위한 보호 타임아웃)
            float t = 0f;
            while ((PlayFabGateway.Instance == null || !PlayFabClientAPI.IsClientLoggedIn()) && t < _readyTimeoutSec)
            {
                t += Time.deltaTime;
                yield return null;
            }

            var mgr = EquipmentManager.Instance;
            if (mgr == null) { _initCo = null; yield break; }

            if (PlayFabGateway.Instance == null || !PlayFabClientAPI.IsClientLoggedIn())
            {
                Debug.LogWarning("[EquipmentSaveAdapter] PlayFab 미로그인 — 서버 강화 비활성(로컬 기본값/더미 유지). 로그인 완료 이벤트 대기 중.");
                _initCo = null;   // 미성공 → 다음 이벤트(로그인 완료) 오면 재시도 가능
                yield break;
            }

            mgr.SetServerGateway(new PlayFabEquipmentServerGateway());

            // 1.5) Title Data 비용표 로드·주입(표시 비용을 SO → Title Data로). 비동기, 실패해도 SO 폴백이라 무해.
            var costCatalog = new PlayFabEquipmentCostCatalog();
            mgr.SetCostCatalog(costCatalog);
            costCatalog.Load();

            // 2) 서버에서 장비/배 최신 상태를 직접 받아온다(로컬 캐시 선load로 인한 초기화 방지).
            //    RefreshEquipmentData/RefreshShipData는 GetPlayer*Data를 호출해 PlayFabDataStore를 갱신한다.
            var pf = PlayFabGateway.Instance;
            bool eqDone = false, shipDone = false;

            if (pf.Equipment != null) pf.Equipment.RefreshEquipmentData(() => eqDone = true, _ => eqDone = true);
            else eqDone = true;

            if (pf.Ship != null) pf.Ship.RefreshShipData(() => shipDone = true, _ => shipDone = true);
            else shipDone = true;

            t = 0f;
            while ((!eqDone || !shipDone) && t < _readyTimeoutSec)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // 3) EquipmentManager 콘텐츠(장비/배 정의 SO) 로드 완료 보장.
            //    콘텐츠(async+CCD) 소비 전환 대응 — 정의가 아직 안 실렸는데 서버 스냅샷(레벨/해금)을
            //    '빈 딕셔너리'에 적용하면 진행도가 소실된다. 그래서 준비를 먼저 기다린다.
            var readyTask = mgr.EnsureReadyAsync();
            float rt = 0f;
            while (!readyTask.IsCompleted && rt < _readyTimeoutSec)
            {
                rt += Time.deltaTime;
                yield return null;
            }

            // 콘텐츠가 없으면(로드 미완/CCD 실패) 스냅샷 적용을 '스킵'한다 — 진행도 소실 방지.
            // _initialized를 세우지 않아, 다음 로그인 이벤트(또는 재활성)에 다시 시도한다.
            if (!mgr.HasContent)
            {
                Debug.LogWarning("[EquipmentSaveAdapter] EquipmentManager 콘텐츠 미준비(로드 미완/실패) — 서버 스냅샷 적용 스킵(진행도 소실 방지). 다음 로그인 이벤트에 재시도.");
                _initCo = null;
                yield break;
            }

            // 4) (store가 서버 최신 + 콘텐츠 준비됨) → 매니저에 로드
            LoadFromStore(mgr);

            _initialized = true;
            _initCo = null;
            Debug.Log("[EquipmentSaveAdapter] 서버 권위 연결 완료(로그인 완료 이벤트 기반).");
        }

        // PlayFabDataStore(서버 최신 반영분) → EquipmentManager 스냅샷 로드(서버 1-base 그대로 전달, 매니저가 0-base 변환).
        private static void LoadFromStore(EquipmentManager mgr)
        {
            var info = PlayFabDataStore.Instance != null ? PlayFabDataStore.Instance.GetPlayerInfo(PlayerId) : null;
            if (info == null)
            {
                Debug.LogWarning("[EquipmentSaveAdapter] PlayFabDataStore 없음 — 기본값 유지.");
                return;
            }

            var equipLevels = new Dictionary<string, int>();
            if (info.equipment?.equipments != null)
                foreach (var kv in info.equipment.equipments)
                    if (kv.Value != null) equipLevels[kv.Key] = kv.Value.level;

            var ships = new Dictionary<string, ShipSnapshot>();
            if (info.ship?.ships != null)
                foreach (var kv in info.ship.ships)
                    if (kv.Value != null)
                        ships[kv.Key] = new ShipSnapshot
                        {
                            level = kv.Value.level,
                            isOpened = kv.Value.isOpened,
                            equipped = kv.Value.equipped
                        };

            mgr.LoadFromServerSnapshot(equipLevels, ships);
        }
    }
}
