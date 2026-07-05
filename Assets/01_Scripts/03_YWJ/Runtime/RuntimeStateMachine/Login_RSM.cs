using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class Login_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.LOGIN;

        public Login_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter Login State");
            RuntimeStateEventBus.PublishLoginStateEntered();
        }

        public override void UpdateState()
        {
            // Login 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit Login State");
            RuntimeStateEventBus.PublishLoginStateExited();
        }
    }
}