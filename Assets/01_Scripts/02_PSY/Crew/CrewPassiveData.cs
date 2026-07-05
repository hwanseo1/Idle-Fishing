namespace Crew
{
    [System.Serializable]
    public class CrewPassiveData
    {
        // 선원별 보유할 패시브 정보
        public CrewPassiveType Type;
        public int Level;
        public int LevelProgress = 0;

        public void CalLevelProgress()
        {


        }
    }
}