using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class Inventory_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.INVENTORY;

        public Inventory_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter Inventory State");
            RuntimeStateEventBus.PublishInventoryStateEntered();
        }

        public override void UpdateState()
        {
            // Inventory 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit Inventory State");
            RuntimeStateEventBus.PublishInventoryStateExited();
        }
    }
}