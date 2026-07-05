using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class Cooking_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.COOKING;

        public Cooking_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter Cooking State");
            RuntimeStateEventBus.PublishCookingStateEntered();
        }

        public override void UpdateState()
        {
            // Cooking 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit Cooking State");
            RuntimeStateEventBus.PublishCookingStateExited();
        }
    }
}