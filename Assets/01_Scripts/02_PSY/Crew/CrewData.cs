using UnityEngine;

namespace Crew
{
    // 선원의 기본 정보
    [CreateAssetMenu(fileName = "CrewData", menuName = "Scriptable Objects/CrewData")]
    public class CrewData : ScriptableObject
    {
        [Header("Crew Info")]
        public string CrewId;
        public string CrewName;
        public CrewGrade CrewGrade;

        [Header("Crew Images")]
        public Sprite CrewSprite;

        [Header("Crew Prefab")]
        public GameObject CrewPrefab;
    }
}