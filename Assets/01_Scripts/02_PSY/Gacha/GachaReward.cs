namespace Reward
{
    public enum RewardType
    {
        Materials,
        Crew,
        CrewFragment
    }

    [System.Serializable]
    public class GachaReward
    {
        public RewardType RewardType;

        public string RewardId;

        public int Amount;

        public bool IsDuplicate;

        public int PreviousFragment;

        public int CurrentFragment;
    }
}