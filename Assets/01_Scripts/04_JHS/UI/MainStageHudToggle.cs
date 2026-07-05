using UnityEngine;
using Runtime;   // RuntimeState, RuntimeStateController, RuntimeStateEventBus

namespace JHS.Fishing
{
    // 메인스테이지(+선택적으로 보스) 진입/이탈에 맞춰 대상 UI를 켜고 끈다.
    // FishingStateActivator와 동일 사상: YWJ RuntimeState 이벤트를 받아 번역만 한다(DIP, YWJ 무수정).
    // ⚠️ 이 컴포넌트는 '항상 활성'인 오브젝트에 둬야 한다 — 대상이 꺼져도 이벤트를 계속 받아 다시 켤 수 있게.
    //    (대상 HUD 자신에 붙이면, 꺼진 동안 이벤트를 못 받아 영영 못 켜진다.)
    public class MainStageHudToggle : MonoBehaviour
    {
        [SerializeField] private GameObject _target;            // 켜고 끌 HUD 루트 (메인스테이지 전용 UI)
        [SerializeField] private bool _includeBossStage = true; // 보스 스테이지에서도 표시할지 (낚시 게이트와 동일하게 기본 true)

        private void OnEnable()
        {
            RuntimeStateEventBus.OnMainStageStateEntered += HandleEntered;
            RuntimeStateEventBus.OnMainStageStateExited  += HandleExited;
            RuntimeStateEventBus.OnBossStageStateEntered += HandleBossEntered;
            RuntimeStateEventBus.OnBossStageStateExited  += HandleBossExited;

            SyncInitialState();   // 구독 전에 이미 발행된 진입 이벤트를 놓친 경우 방어
        }

        private void OnDisable()
        {
            RuntimeStateEventBus.OnMainStageStateEntered -= HandleEntered;
            RuntimeStateEventBus.OnMainStageStateExited  -= HandleExited;
            RuntimeStateEventBus.OnBossStageStateEntered -= HandleBossEntered;
            RuntimeStateEventBus.OnBossStageStateExited  -= HandleBossExited;
        }

        private void HandleEntered() => Show(true);
        private void HandleExited() => Show(false);
        private void HandleBossEntered() { if (_includeBossStage) Show(true); }
        private void HandleBossExited() { if (_includeBossStage) Show(false); }

        private void Show(bool on)
        {
            if (_target != null) _target.SetActive(on);
        }

        // 부팅 시 RuntimeStateController.Start()가 이미 MAINSTAGE로 전환하며 진입 이벤트를 쏜 뒤일 수 있다.
        // 놓쳤어도 현재 상태를 직접 읽어 한 번 동기화한다.
        private void SyncInitialState()
        {
            var rsc = FindFirstObjectByType<RuntimeStateController>();
            if (rsc == null) return;
            bool on = rsc.CurrentState == RuntimeState.MAINSTAGE ||
                      (_includeBossStage && rsc.CurrentState == RuntimeState.BOSSSTAGE);
            Show(on);
        }
    }
}
