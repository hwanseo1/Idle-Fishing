using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class MultiMatching_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.MULTIMATCHING;

        public MultiMatching_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter MultiMatching State");
            RuntimeStateEventBus.PublishMultiMatchingStateEntered();
        }

        public override void UpdateState()
        {
            // MultiMatching 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit MultiMatching State");
            RuntimeStateEventBus.PublishMultiMatchingStateExited();
        }
    }
}