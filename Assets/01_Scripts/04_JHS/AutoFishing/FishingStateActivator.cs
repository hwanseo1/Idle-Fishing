using UnityEngine;
using Runtime;   // RuntimeState, RuntimeStateController

namespace JHS.Fishing
{
    // YWJ RuntimeState ↔ 자동낚시 활성화 다리(어댑터).
    // "메인스테이지 진입/이탈" 알림을 받아 AutoFishingController에 켜/꺼 명령으로 번역한다.
    // 컨트롤러는 YWJ를 모르고, YWJ 의존은 이 어댑터에만 가둔다(DIP). YWJ 코드는 읽기만(무수정).
    public class FishingStateActivator : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _controller;  // 비우면 같은 오브젝트→씬에서 자동 탐색

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<AutoFishingController>();
            if (_controller == null) _controller = FindFirstObjectByType<AutoFishingController>();

            RuntimeStateEventBus.OnMainStageStateEntered += HandleEntered;
            RuntimeStateEventBus.OnMainStageStateExited  += HandleExited;
            RuntimeStateEventBus.OnBossStageStateEntered += HandleEntered;
            RuntimeStateEventBus.OnBossStageStateExited += HandleExited;

            SyncInitialState();   // 구독 전에 이미 발행된 진입 이벤트를 놓치는 경우 방어
        }

        private void OnDisable()
        {
            RuntimeStateEventBus.OnMainStageStateEntered -= HandleEntered;
            RuntimeStateEventBus.OnMainStageStateExited  -= HandleExited;
            RuntimeStateEventBus.OnBossStageStateEntered -= HandleEntered;
            RuntimeStateEventBus.OnBossStageStateExited -= HandleExited;
        }

        private void HandleEntered() => _controller?.SetGameplayActive(true);
        private void HandleExited() => _controller?.SetGameplayActive(false);

        // 부팅 시 RuntimeStateController.Start()가 이미 MAINSTAGE로 전환하며 진입 이벤트를 쏜 뒤일 수 있다.
        // 그 이벤트를 놓쳤어도 현재 상태를 직접 읽어 한 번 동기화한다.
        private void SyncInitialState()
        {
            if (_controller == null) return;
            var rsc = FindFirstObjectByType<RuntimeStateController>();
            if (rsc != null)
                _controller.SetGameplayActive(
                    rsc.CurrentState == RuntimeState.MAINSTAGE ||
                    rsc.CurrentState == RuntimeState.BOSSSTAGE);
        }
    }
}
