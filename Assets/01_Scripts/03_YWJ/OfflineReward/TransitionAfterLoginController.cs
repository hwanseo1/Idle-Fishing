using UnityEngine;
using OfflineReward;
using System.Threading.Tasks;
using Runtime;



public class TransitionAfterLoginController : MonoBehaviour
{
    [Header("Offline Reward Calculator")]
    [SerializeField] private OfflineRewardCaculator _offlineRewardCaculator;

    [Header("Runtime State Controller")]
    [SerializeField] private RuntimeStateController _runtimeStateController;


    private void Awake()
    {
        _offlineRewardCaculator = FindFirstObjectByType<OfflineRewardCaculator>();
        _runtimeStateController = FindFirstObjectByType<RuntimeStateController>();
    }

    public async Task CheckNextStateAsync()
    {
        // 버전게이트 + 콘텐츠 패치 — 로그인 직후·오프라인 정산 전에 수행 (JHS).
        // 차단/점검이면 정산·전환을 전부 중단(게이트 화면이 막음). 통과면 콘텐츠 패치 완료까지 기다린 뒤 정산.
        var backend = JHS.Backend.BackendService.Instance;
        if (backend != null)
        {
            await backend.Boot();   // 로그인 후라 라이브 config fetch OK
            if (backend.Decision == JHS.Backend.GateDecision.BlockedOutdated
                || backend.Decision == JHS.Backend.GateDecision.Maintenance)
            {
                return;   // VersionGateScreen이 차단/점검 화면 표시. 정산·전환 안 함.
            }

            // 콘텐츠 패치(원격 카탈로그 갱신)를 정산 전에 완료. dataVersion은 방금 받은 config 기준.
            await JHS.Content.ContentCatalogUpdater.SyncIfChangedAsync(backend.Config != null ? backend.Config.dataVersion : null);

            // 패치 후 콘텐츠(장비/배 정의) 로드까지 완료해야 메인 진입 — "패치→로드→메인" 순서 보장.
            // 이게 없으면 부트스트랩이 async로 로드하며 패치/다운로드와 레이스 → 빈 상태로 메인 진입 가능(JHS).
            if (JHS.Equipment.EquipmentManager.Instance != null)
                await JHS.Equipment.EquipmentManager.Instance.EnsureReadyAsync();
        }

        // 콘텐츠 완전 준비 후 "업데이트 완료" 버튼을 눌러야 정산으로 진행(자기완결 오버레이, 상태머신 무관 — JHS).
        // Registry/게이트 미배치면 즉시 통과.
        await JHS.Content.CcdDownloadGate.ConfirmUpdateCompleteAsync();

        await _offlineRewardCaculator.CalculateOfflineRewardAsync();

        if (_offlineRewardCaculator.OfflineRewardCost > 0)
        {
            _runtimeStateController.CurrentState = RuntimeState.OFFLINEREWARD;
        }
        else
        {
            _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
        }
    }
}