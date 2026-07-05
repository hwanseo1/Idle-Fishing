namespace JHS.Equipment
{
    // 장비 4종 (낚싯대/배/찌/의자)
    // ⚠️ 데이터 에셋(.asset의 type: N)·세이브에 정수로 저장됨 — 순서 변경/중간 삽입 금지, 추가는 맨 끝에만.
    public enum EquipmentType
    {
        Rod   = 0,
        Boat  = 1,
        Float = 2,
        Chair = 3
    }
}
