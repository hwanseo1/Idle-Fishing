using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class MultiStage_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.MULTISTAGE;

        public MultiStage_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter MultiStage State");
            RuntimeStateEventBus.PublishMultiStageStateEntered();
        }

        public override void UpdateState()
        {
            // MultiStage 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit MultiStage State");
            RuntimeStateEventBus.PublishMultiStageStateExited();
        }
    }
}