using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class StageChange_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.STAGECHANGE;

        public StageChange_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter StageChange State");
            RuntimeStateEventBus.PublishStageChangeStateEntered();
        }

        public override void UpdateState()
        {
            // StageChange 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit StageChange State");
            RuntimeStateEventBus.PublishStageChangeStateExited();
        }
    }
}