using System.Collections.Generic;

namespace Crew
{
    [System.Serializable]
    public class CrewInstanceData
    {
        // 선원 인스턴스 데이터 클래스
        // 유저가 갖고 있을 선원 고유 데이터
        public string CrewId;
        public CrewGrade Grade;
        public int DuplicateCount;
        public List<CrewPassiveData> Passives = new();
    }
}