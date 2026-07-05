using System;
using System.Collections.Generic;
using JHS.Equipment;

namespace JHS.Fishing
{
    // 보상 효과 1개가 참조하는 입력들. (효과가 컨트롤러 내부를 모르고도 계산 가능)
    public readonly struct RewardContext
    {
        public readonly IEquipmentEffects Equip;       // 장비 스탯 읽기
        public readonly float BoatCatchMultiplier;     // 배 진공 흡입기 발동 배율 (없으면 1)
        public readonly Func<float> Rng;               // 0~1 난수 (테스트 시 주입 가능)

        public RewardContext(IEquipmentEffects equip, float boatCatchMultiplier, Func<float> rng)
        {
            Equip = equip;
            BoatCatchMultiplier = boatCatchMultiplier;
            Rng = rng;
        }
    }

    // 자동 보상에 곱해지는 효과 1종.
    // 새 효과 추가 = 이 인터페이스 구현체 추가 + 파이프라인 등록. AutoResolve()는 수정하지 않는다.
    public interface IRewardModifier
    {
        float Apply(float reward, in RewardContext ctx);
    }

    // ⚠️ 다중포획·배 진공흡입기 모디파이어 제거(2026-06-25): 이건 "기여도 배율"이 아니라 "마릿수"다.
    //    보상값을 곱하면 기여도만 N배 되고 인벤토리는 1마리만 들어가는 버그(GiveFish 1회 호출)가 났다.
    //    → AutoFishingController.RollAutoCatchCount()에서 실제 GiveFish 호출 횟수(=인벤토리·기여도 함께 N배)로 반영한다.
    //    이 보상 파이프라인은 "per-fish 값 배율"(예: 원격 goldMultiplier)만 담당한다.

    // 원격 설정값(예: goldMultiplier)을 보상에 곱하는 효과 — "재빌드 없이 라이브 튜닝" 데모.
    // 배수는 Func로 주입받아 Apply마다 최신값을 읽는다. (이 클래스는 순수 — Backend를 직접 모름.
    //  값 공급자를 바깥에서 주입 = DIP. 0 이하/공급자 없음은 ×1로 무효 처리해 보상이 죽지 않게 방어.)
    public sealed class RemoteValueRewardModifier : IRewardModifier
    {
        private readonly Func<float> _multiplier;
        public RemoteValueRewardModifier(Func<float> multiplier) => _multiplier = multiplier;

        public float Apply(float reward, in RewardContext ctx)
        {
            float m = _multiplier != null ? _multiplier() : 1f;
            return reward * (m > 0f ? m : 1f);
        }
    }

    // 기본 효과 순서. 새 효과는 여기 한 줄만 추가하면 된다.
    public static class RewardPipeline
    {
        // "값 배율"(per-fish 기여도) 효과만. 마릿수(다중포획·배 진공흡입기)는 RollAutoCatchCount에서 처리.
        // 원격 goldMultiplier 등은 런타임에 AddRewardModifier로 추가됨(RemoteConfigRewardBinder).
        public static List<IRewardModifier> CreateDefault() => new List<IRewardModifier>();
    }
}
