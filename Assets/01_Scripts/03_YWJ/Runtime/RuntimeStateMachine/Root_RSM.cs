using UnityEngine;


namespace Runtime.RSM
{
    public abstract class Root_RSM
    {
        protected abstract RuntimeState RSMState { get; }

        public abstract void EnterState();

        public abstract void UpdateState();

        public abstract void ExitState();
    }
}