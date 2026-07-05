using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class MainStage_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.MAINSTAGE;

        public MainStage_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter MainStage State");
            RuntimeStateEventBus.PublishMainStageStateEntered();
        }

        public override void UpdateState()
        {
            // MainStage 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit MainStage State");
            RuntimeStateEventBus.PublishMainStageStateExited();
        }
    }
}