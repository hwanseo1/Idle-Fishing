namespace JHS.Fishing
{
    // 선원 패시브 보정 (실제 구현은 선원 파트)
    public interface ICrewBonus
    {
        float AutoPassiveBonus { get; }     // 자동 낚시 산출 보정
        float AutoSpeedMultiplier { get; }  // 자동 낚시 속도 (클수록 캐스팅 빨라짐)
        float FishRarityBonus { get; }      // 선원 레어도 보정
        float BossFishBonus { get; }        // 보스 물고기 특화 (수동 보스 피해 배율)
    }

    // 선원 시스템 없을 때 쓰는 더미 (보정/속도 1.0)
    public sealed class DummyCrewBonus : ICrewBonus
    {
        public float AutoPassiveBonus => 1f;
        public float AutoSpeedMultiplier => 1f;
        public float FishRarityBonus => 0f;
        public float BossFishBonus => 1f;   // 보정 없음 = 1배
    }
}
