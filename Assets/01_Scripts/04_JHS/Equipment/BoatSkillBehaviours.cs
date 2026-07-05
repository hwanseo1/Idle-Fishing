using System.Collections.Generic;

namespace JHS.Equipment
{
    // 배 스킬 발동이 컨트롤러로 즉시효과를 내보낼 때 쓰는 통로.
    // (지속 버프가 아닌 "발동 즉시 1회" 효과용)
    public interface IBoatSkillContext
    {
        void RaisePenguin(float value);   // 유인용 로봇 펭귄 — 다음 스테이지 물고기 지급 (예정)
    }

    // 한 프레임의 배 버프 누적값. 채널별 결합 방식이 다름(포획 수=곱, 레어도=합).
    public sealed class BoatBuffState
    {
        public float CatchMultiplier;
        public float RarityBonus;

        public void Reset()
        {
            CatchMultiplier = 1f;   // 곱 항등원
            RarityBonus = 0f;       // 합 항등원
        }
    }

    // 배 스킬 1종의 행동 정의.
    // 새 스킬 추가 = 이 인터페이스 구현체 추가 + BoatSkillRegistry 등록. 컨트롤러 루프는 수정하지 않는다.
    // value(확률/계수)는 등급별로 바뀌므로 cfg가 아니라 매개변수로 받는다(컨트롤러가 현재 등급값을 넘김).
    public interface IBoatSkill
    {
        BoatSkill Type { get; }

        // 쿨타임마다 호출. 지속 버프면 지속시간(초)을, 즉시효과면 0을 반환.
        // 즉시효과(이벤트 발행 등)는 ctx로 수행한다. value = 현재 등급의 효과 수치.
        float Activate(BoatSkillEntry cfg, float value, IBoatSkillContext ctx);

        // 발동 지속 동안 매 프레임 호출. 현재 등급값(value) 기준 기여분을 buff에 누적.
        void Accumulate(float value, BoatBuffState buff);
    }

    // 진공 흡입기 : 발동 중 자동 낚는 수 증가 (곱)
    public sealed class VacuumSuctionSkill : IBoatSkill
    {
        public BoatSkill Type => BoatSkill.VacuumSuction;
        public float Activate(BoatSkillEntry cfg, float value, IBoatSkillContext ctx) => cfg.duration;
        public void Accumulate(float value, BoatBuffState buff) => buff.CatchMultiplier *= 1f + value;
    }

    // K-직장인 야근 전등 : 발동 중 레어도 증가 (합)
    public sealed class OvertimeLampSkill : IBoatSkill
    {
        public BoatSkill Type => BoatSkill.OvertimeLamp;
        public float Activate(BoatSkillEntry cfg, float value, IBoatSkillContext ctx) => cfg.duration;
        public void Accumulate(float value, BoatBuffState buff) => buff.RarityBonus += value;
    }

    // 유인용 로봇 펭귄 : 즉시효과 (다음 스테이지 물고기 지급, 예정). 지속 없음.
    public sealed class DecoyPenguinSkill : IBoatSkill
    {
        public BoatSkill Type => BoatSkill.DecoyPenguin;
        public float Activate(BoatSkillEntry cfg, float value, IBoatSkillContext ctx)
        {
            ctx.RaisePenguin(value);
            return 0f;   // 즉시효과 — 지속(active) 켜지지 않음
        }
        public void Accumulate(float value, BoatBuffState buff) { }   // 지속 기여 없음
    }

    // 스킬 종류 → 행동 매핑. 새 스킬은 Build()에 한 줄만 추가하면 된다.
    // 전략들은 상태가 없어(설정은 cfg로 주입) 싱글톤으로 공유한다.
    public static class BoatSkillRegistry
    {
        private static readonly Dictionary<BoatSkill, IBoatSkill> _map = Build();

        private static Dictionary<BoatSkill, IBoatSkill> Build()
        {
            var map = new Dictionary<BoatSkill, IBoatSkill>();
            void Add(IBoatSkill s) => map[s.Type] = s;

            Add(new VacuumSuctionSkill());
            Add(new OvertimeLampSkill());
            Add(new DecoyPenguinSkill());

            return map;
        }

        public static IBoatSkill Get(BoatSkill skill) => _map.TryGetValue(skill, out var s) ? s : null;
    }
}
