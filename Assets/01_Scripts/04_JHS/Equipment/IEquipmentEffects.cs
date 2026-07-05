namespace JHS.Equipment
{
    // 장비 능력치 읽기용 공용 인터페이스. 다른 모듈은 전부 이걸로만 값을 가져간다.
    // 스탯마다 중립값이 달라서(배율=1, 가산=0) 호출부가 fallback을 넘긴다.
    public interface IEquipmentEffects
    {
        float GetStat(EquipmentStat stat, float fallback = 0f);
    }

    // 장비 시스템 없을 때 쓰는 더미 (항상 fallback 반환)
    public sealed class DummyEquipmentEffects : IEquipmentEffects
    {
        public float GetStat(EquipmentStat stat, float fallback = 0f) => fallback;
    }
}
