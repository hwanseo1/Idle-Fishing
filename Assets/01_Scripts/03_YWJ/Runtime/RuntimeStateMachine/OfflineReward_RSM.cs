using UI;
using UnityEngine;


namespace Runtime.RSM
{
    public class OfflineReward_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.OFFLINEREWARD;

        public OfflineReward_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter OfflineReward State");
            RuntimeStateEventBus.PublishOfflineRewardStateEntered();
        }

        public override void UpdateState()
        {
            // OfflineReward 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit OfflineReward State");
            RuntimeStateEventBus.PublishOfflineRewardStateExited();
        }
    }
}