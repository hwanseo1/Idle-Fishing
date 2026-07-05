using UnityEngine;


namespace Runtime.RSM
{
    public class Collection_RSM : Root_RSM
    {
        protected override RuntimeState RSMState => RuntimeState.COLLECTION;

        public Collection_RSM()
        {

        }

        public override void EnterState()
        {
            Debug.Log("Enter Collection State");
            RuntimeStateEventBus.PublishCollectionStateEntered();
        }

        public override void UpdateState()
        {
            // Collection 상태에서의 업데이트 로직을 여기에 작성
        }

        public override void ExitState()
        {
            Debug.Log("Exit Collection State");
            RuntimeStateEventBus.PublishCollectionStateExited();
        }
    }
}