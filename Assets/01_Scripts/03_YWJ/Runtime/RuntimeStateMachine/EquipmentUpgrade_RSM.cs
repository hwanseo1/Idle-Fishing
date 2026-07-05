using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class EquipmentUpgrade_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.EQUIPMENTUPGRADE;

        public EquipmentUpgrade_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter EquipmentUpgrade State");
            RuntimeStateEventBus.PublishEquipmentUpgradeStateEntered();
        }

        public override void UpdateState()
        {
            // EquipmentUpgrade 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit EquipmentUpgrade State");
            RuntimeStateEventBus.PublishEquipmentUpgradeStateExited();
        }
    }
}