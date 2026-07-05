namespace JHS.Equipment
{
    // 장비가 제공하는 능력치 종류. 각 스탯은 장비 1종에만 속한다. (배는 스탯 대신 액티브 스킬)
    // ⚠️ 이 값은 데이터 에셋(.asset의 {stat: N})·세이브·UI 총효과 행순서에 정수로 박혀 있다.
    //    순서 변경/중간 삽입 절대 금지 — 추가는 항상 맨 끝에 새 정수값으로만. (어기면 기존 데이터·세이브가 조용히 엉뚱하게 매핑됨)
    public enum EquipmentStat
    {
        // 낚싯대
        MultiCatchChance   = 0,    // 다중 포획 확률 (0~1)
        GreenGaugeSpeed    = 1,    // (수동) 초록 게이지 차는 속도 배율
        RedGaugeSpeed      = 2,    // (수동) 붉은 게이지 차는 속도 배율
        BossDamage         = 3,    // 보스전 피해량

        // 찌
        MultiCatchCount    = 4,    // 다중 포획 시 마릿수
        RarityWeightBonus = 5,    // 레어도 높은 물고기 출현 가중치 보정값 (StageData 기본 가중치에 합산)

        // 의자
        BiteIntervalReduce = 6,    // 입질 시간 감소율 (0~1, 클수록 자주 뭄)
        OfflineCapBonus    = 7,     // 오프라인 정산 시간 한계 증가
        // (8 = 옛 FailGaugeSpeedReduction. 빨간 게이지가 RedGaugeSpeed(#2)로 통합되며 제거됨. 데이터·코드 사용처 0 확인 후 삭제. 다음 추가는 9부터 — 8 재사용 금지)
    }

    // 배 액티브 스킬 (자동 낚시 중 쿨타임마다 자동 발동)
    // ⚠️ 세이브에 정수로 저장됨 — 순서 변경/중간 삽입 금지, 추가는 맨 끝에만.
    public enum BoatSkill
    {
        VacuumSuction = 0,   // 진공 흡입기 : 자동 낚는 수 증가
        OvertimeLamp  = 1,   // K-직장인 야근 전등 : 일정시간 레어도 증가
        DecoyPenguin  = 2    // 유인용 로봇 펭귄 : 다음 스테이지 물고기 지급 (예정)
    }
}
