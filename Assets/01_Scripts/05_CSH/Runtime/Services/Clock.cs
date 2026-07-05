using System;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 요리 완료 시간 계산을 테스트 가능한 방식으로 공급하는 시계 인터페이스입니다.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// 서비스가 기준으로 삼는 UTC 현재 시각입니다.
        /// </summary>
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// 실제 UTC 현재 시각을 사용하는 런타임 시계입니다.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        /// <summary>
        /// 시스템 UTC 현재 시각입니다.
        /// </summary>
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// self-test와 런타임 미리보기에서 시간을 수동으로 넘기기 위한 시계입니다.
    /// </summary>
    public sealed class ManualClock : IClock
    {
        #region Initialization

        /// <summary>
        /// 지정한 시각을 UTC로 보정해 수동 시계를 생성합니다.
        /// </summary>
        public ManualClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        /// <summary>
        /// 현재 시계가 반환하는 UTC 시각입니다.
        /// </summary>
        public DateTime UtcNow { get; private set; }

        #endregion

        #region Control

        /// <summary>
        /// 현재 시각을 지정한 UTC 시각으로 교체합니다.
        /// </summary>
        public void SetUtcNow(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        /// <summary>
        /// 현재 시각을 지정 초만큼 앞으로 이동합니다.
        /// </summary>
        public void AdvanceSeconds(int seconds)
        {
            UtcNow = UtcNow.AddSeconds(seconds);
        }

        #endregion
    }
}
