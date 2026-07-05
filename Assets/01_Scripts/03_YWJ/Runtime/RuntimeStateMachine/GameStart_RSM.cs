using UnityEngine;
using UI;

namespace Runtime.RSM
{
    public class GameStart_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.GAMESTART;

        public GameStart_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter GameStart State");
            RuntimeStateEventBus.PublishGameStartStateEntered();
        }

        public override void UpdateState()
        {
            // GameStart 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit GameStart State");
            RuntimeStateEventBus.PublishGameStartStateExited();
        }
    }
}