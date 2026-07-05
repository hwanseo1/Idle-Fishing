using UnityEngine;

namespace JHS.Equipment
{
    // 장비 1종의 등급별 능력치 테이블 (ScriptableObject — 기획/다른 파트도 에셋으로 참조)
    // 장비당 효과가 여러 개라 등급마다 스탯 목록을 둔다. 배는 levels 대신 boatSkills 사용.
    [CreateAssetMenu(menuName = "Data/Equipment", fileName = "EquipmentData")]
    public class EquipmentData : ScriptableObject
    {
        public EquipmentType type;
        public string id;                  // PlayFab↔Unity 공통 "종류 ID" (예: equip_rod). 중앙 동기화(YWJ) 수집용.
        public string displayName;
        [TextArea] public string description;  // UI 설명 줄
        public Sprite icon;                    // UI 아이콘 (선택)
        public LevelEntry[] levels;        // index = 현재 등급 (낚싯대/찌/의자)
        public BoatSkillEntry[] boatSkills; // 배 전용

        public int MaxLevelIndex => (levels == null || levels.Length == 0) ? -1 : levels.Length - 1;
    }

    [System.Serializable]
    public class LevelEntry
    {
        public StatValue[] stats;          // 이 등급에서 제공하는 능력치들
        public int upgradeCostGold;        // 다음 등급으로 올리는 골드 비용
        public MaterialCost[] materialCosts; // 다음 등급으로 올리는 데 필요한 재료 (없으면 골드만)
    }

    [System.Serializable]
    public class StatValue
    {
        public EquipmentStat stat;
        public float value;
    }

    [System.Serializable]
    public class BoatSkillEntry
    {
        public BoatSkill skill;
        public string id;                // PlayFab↔Unity 공통 "종류 ID" (예: boat_vacuum). 중앙 동기화(YWJ) 수집용.
        public string displayName;       // UI 표시명 (예: 진공흡입기)
        [TextArea] public string description; // UI 설명 줄 (예: 발동 중 낚는 수 증가)
        public Sprite icon;              // UI 아이콘 (선택)
        public bool unlocked;            // 기본 해금 여부(시작부터 열림). 잠그려면 false로 두고 unlockCostGold 설정.
        public int unlockCostGold;       // 잠긴 배를 해금하는 골드 비용. (id 기준 공유 비용 엔티티 → 추후 Title Data BoatUnlockCostList로 승격: 서버 권위검증/카탈로그가 소비)
        public float cooldown;           // 발동 간격(초) — 등급 무관
        public float duration;           // 지속시간(초) — 등급 무관
        public BoatSkillLevel[] levels;  // 등급별 효과 수치(확률/계수) + 강화 비용. 스킬마다 개별 강화.

        public int MaxLevelIndex => (levels == null || levels.Length == 0) ? -1 : levels.Length - 1;
    }

    [System.Serializable]
    public class BoatSkillLevel
    {
        public float value;                  // 이 등급의 효과 수치 (확률/계수)
        public int upgradeCostGold;          // 다음 등급으로 올리는 골드 비용
        public MaterialCost[] materialCosts; // 다음 등급으로 올리는 데 필요한 재료 (없으면 골드만)
    }
}
