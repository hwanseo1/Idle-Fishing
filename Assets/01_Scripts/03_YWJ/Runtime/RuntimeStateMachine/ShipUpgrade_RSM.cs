using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class ShipUpgrade_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.SHIPUPGRADE;

        public ShipUpgrade_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter ShipUpgrade State");
            RuntimeStateEventBus.PublishShipUpgradeStateEntered();
        }

        public override void UpdateState()
        {
            // ShipUpgrade 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit ShipUpgrade State");
            RuntimeStateEventBus.PublishShipUpgradeStateExited();
        }
    }
}