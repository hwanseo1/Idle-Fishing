using System;

namespace JHS.Equipment
{
    // 장비/배스킬 등급 저장용 DTO. 저장 "위치"(YWJ DataCenter vs CSH PlayerStateSaveData)는 미결이라,
    // EquipmentManager는 이 형태로 값을 내주고(GetSaveState)·받기(LoadSaveState)만 한다.
    // 저장 담당이 정해지면 이 타입을 직렬화해 어디든 저장하면 됨 (JHS 코드 추가 변경 불필요).
    [Serializable]
    public class EquipmentSaveState
    {
        public EquipmentLevelEntry[] equipmentLevels;
        public BoatSkillLevelEntry[] boatSkillLevels;

        // 해금한 배(=BoatSkill 정수값) 목록. 없으면(구버전 세이브) SO 기본 해금만 적용(하위호환).
        public BoatSkill[] unlockedBoats;

        // 장착한 배(=BoatSkill의 정수값). -1 = "미지정" 센티넬.
        // ⚠️ 0은 유효한 enum 값(VacuumSuction)이라 "안 채움"과 구분이 안 됨 → 기본 -1로 두고,
        //    값을 안 넣은 호출자(예: 배 저장 B1 합의 전 EquipmentSaveAdapter)는 -1이 유지돼 LoadSaveState가 건너뜀.
        public int equippedBoat = -1;
    }

    [Serializable]
    public struct EquipmentLevelEntry
    {
        public EquipmentType type;
        public int level;
    }

    [Serializable]
    public struct BoatSkillLevelEntry
    {
        public BoatSkill skill;
        public int level;
    }
}
