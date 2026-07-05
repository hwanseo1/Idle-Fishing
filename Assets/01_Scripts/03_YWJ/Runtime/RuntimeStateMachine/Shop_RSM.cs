using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class Shop_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.SHOP;

        public Shop_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter Shop State");
            RuntimeStateEventBus.PublishShopStateEntered();
        }

        public override void UpdateState()
        {
            // Shop 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit Shop State");
            RuntimeStateEventBus.PublishShopStateExited();
        }
    }
}