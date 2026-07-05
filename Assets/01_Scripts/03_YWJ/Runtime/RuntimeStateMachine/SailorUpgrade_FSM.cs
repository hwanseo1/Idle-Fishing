using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class SailorUpgrade_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.SAILORUPGRADE;

        public SailorUpgrade_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter SailorUpgrade State");
            RuntimeStateEventBus.PublishSailorUpgradeStateEntered();
        }

        public override void UpdateState()
        {
            // SailorUpgrade 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit SailorUpgrade State");
            RuntimeStateEventBus.PublishSailorUpgradeStateExited();
        }
    }
}