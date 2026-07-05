namespace JHS.Equipment
{
    // 배 액티브 스킬(야근전등)의 레어도 보정을 RMS 물고기 선정에 주입하기 위한 통로.
    // 발동 중에만 >0인 실시간 값이라, 값이 아니라 provider로 주입해 매 선택마다 읽는다. (ICrewBonus와 같은 패턴)
    public interface IBoatRarityProvider
    {
        float RarityBonus { get; }   // 야근전등 발동 중 레어도 보정 (없으면 0)
    }
}
