using UnityEngine;


namespace Runtime.RSM
{
    public class BossStage_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.BOSSSTAGE;

        public BossStage_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter BossStage State");
            RuntimeStateEventBus.PublishBossStageStateEntered();
        }

        public override void UpdateState()
        {
            // BossStage 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit BossStage State");
            RuntimeStateEventBus.PublishBossStageStateExited();
        }
    }
}