using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Crew
{
    [CreateAssetMenu(fileName = "CrewDatabase", menuName = "Scriptable Objects/CrewDatabase")]
    public class CrewDatabase : ScriptableObject
    {
        public List<CrewData> Crews = new();

        public List<CrewData> GetCrewsByGrade(CrewGrade grade)
        {
            return Crews.Where(crew => crew.CrewGrade == grade).ToList();
        }

        public CrewData GetCrewData(string crewId)
        {
            return Crews.FirstOrDefault(crew => crew.CrewId == crewId);
        }
    }
}