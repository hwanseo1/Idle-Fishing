using UnityEngine;   // InspectorName (드롭다운 한글 라벨)

namespace JHS.Sound
{
    // 효과음 식별자. 게임 로직은 이 id로만 "무슨 소리 울려"를 요청하고,
    // 실제 어떤 AudioClip인지는 HS_SoundManager 인스펙터에서 매핑한다(연출/데이터 분리).
    // ※ 세이브에 안 쓰이는 표현용 enum → 순서/값 자유롭게 늘려도 됨.
    // ※ [InspectorName]은 Clips 드롭다운에 한글로만 보이게 할 뿐, 저장은 정수값이라 배선/세이브 영향 없음.
    public enum HS_SoundId
    {
        None = 0,

        // ── 1순위: 낚시 루프 (FishingPhase) ─────────────
        [InspectorName("던지기")]        Cast,
        [InspectorName("입질")]          Bite,
        [InspectorName("당기기")]        Reeling,
        [InspectorName("포획 성공")]      FishingSuccess,
        [InspectorName("놓침(실패)")]     FishingFail,

        // ── 1순위: 포획 결과 (레어도별) ────────────────
        [InspectorName("잡힘-일반")]      CatchCommon,
        [InspectorName("잡힘-희귀")]      CatchRare,
        [InspectorName("잡힘-에픽")]      CatchEpic,
        [InspectorName("잡힘-전설")]      CatchLegendary,
        [InspectorName("다중포획")]       MultiCatch,

        // ── 3순위: 강화 / 장비 ────────────────────────
        [InspectorName("강화 성공")]      UpgradeSuccess,
        [InspectorName("강화 실패")]      UpgradeFail,
        [InspectorName("배 해금")]        BoatUnlock,
        [InspectorName("배 장착")]        BoatEquip,

        // ── 선박 액티브 스킬 발동음 ────────────────────
        [InspectorName("배스킬-진공")]     BoatSkillVacuum,
        [InspectorName("배스킬-전등")]     BoatSkillLamp,
        [InspectorName("배스킬-펭귄")]     BoatSkillPenguin,
        [InspectorName("배스킬-펭귄토스")]  BoatSkillPenguinToss,
        [InspectorName("배스킬-펭귄착수")]  BoatSkillPenguinSplash,

        // ── 보스전 (타격 종류별) ────────────────────────
        [InspectorName("보스타격-자동")]    BossHitAuto,
        [InspectorName("보스타격-수동")]    BossHitManual,
        [InspectorName("보스타격-다중")]    BossHitMulti,

        // ── UI / 보상 피드백 ───────────────────────────
        [InspectorName("재화 획득")]       CurrencyGain,
    }
}
