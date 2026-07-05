using UnityEngine;

namespace JHS.Fishing
{
    // 보상/시간 계산 순수 함수 (나중에 서버 이관 위해 한 곳에 모음)
    public static class RewardCalculator
    {
        // 자동 보상 = 기본 × 방치패널티 × 선원보정 (다중 포획·배 버프는 호출부에서 곱함)
        public static float CalcAutoReward(float baseReward, float autoPenalty, float crewAutoBonus)
        {
            return baseReward * autoPenalty * crewAutoBonus;
        }

        // 수동 보상 = 기본 × 수동보너스 (수동이 자동보다 유리하도록 보너스 배수. 값은 밸런스 영역)
        public static float CalcManualReward(float baseReward, float manualBonus)
        {
            return baseReward * manualBonus;
        }

        // 입질 대기 = 기본대기 × (1 - 의자 감소율). 의자 등급이 오를수록 자주 뭄.
        public static float CalcBiteWait(float baseWait, float biteReduce)
        {
            return baseWait * (1f - Mathf.Clamp01(biteReduce));
        }

        // 캐스팅 간격 = 기본간격 / 선원속도 (하한 적용)
        public static float CalcCastInterval(float baseInterval, float crewSpeedMultiplier, float minInterval = 0.1f)
        {
            float mul = Mathf.Max(crewSpeedMultiplier, 0.01f);
            return Mathf.Max(baseInterval / mul, minInterval);
        }
    }
}
