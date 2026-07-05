using System;


namespace RMS.Multiplay
{
    // 멀티플레이 하루 3회 제한 + 보상 지급 게이트웨이 인터페이스.
    // 실제 검사/차감/지급은 PlayFab CloudScript(원준님 작업)가 담당하며,
    // 콜백 시그니처 차이는 MultiplayLimitGatewayAdapter가 흡수한다.
    public interface IMultiplayLimitGateway
    {
        // 조회만(차감 없음). 매칭 화면 진입 시 호출해 남은 횟수를 표시.
        void RequestStatus(Action<MultiplayLimitResult> onComplete);

        // 가능하면 1회 차감. 정상 종료(시간 만료) 시점에 호출한다.
        void TryConsume(Action<MultiplayLimitResult> onComplete);

        // 디버그용. playCount를 강제로 0으로 리셋한다.
        void DebugReset(Action<MultiplayLimitResult> onComplete);

        // 매치 종료 후 보상 지급 요청. personalContribution(본인 Score), totalContribution(전체 기여도)을 전달한다.
        void GiveReward(int personalContribution, int totalContribution, Action<MultiplayRewardOutcome> onComplete);
    }


    // 게이트웨이 응답 결과. success가 false면 나머지 필드는 무의미(통신 실패 등).
    public struct MultiplayLimitResult
    {
        public bool Success;
        public int PlayCount;
        public int MaxPlayCount;
        public bool CanPlay;
        public string LastResetAtUtc;

        public static MultiplayLimitResult Failure()
        {
            return new MultiplayLimitResult
            {
                Success = false,
                PlayCount = 0,
                MaxPlayCount = 3,
                CanPlay = false,
                LastResetAtUtc = string.Empty,
            };
        }
    }


    // 서버 없이 테스트하던 시기의 더미. 실제 빌드에서는 사용하지 않음(유닛 테스트용으로 보관).
    public class MultiplayLimitGatewayStub : IMultiplayLimitGateway
    {
        private const int MAX_PLAY_COUNT = 3;
        private int _playCount = 0;

        public void RequestStatus(Action<MultiplayLimitResult> onComplete)
        {
            onComplete?.Invoke(BuildResult());
        }

        public void TryConsume(Action<MultiplayLimitResult> onComplete)
        {
            if (_playCount < MAX_PLAY_COUNT)
                _playCount++;

            onComplete?.Invoke(BuildResult());
        }

        public void DebugReset(Action<MultiplayLimitResult> onComplete)
        {
            _playCount = 0;
            onComplete?.Invoke(BuildResult());
        }

        public void GiveReward(int personalContribution, int totalContribution, Action<MultiplayRewardOutcome> onComplete)
        {
            onComplete?.Invoke(new MultiplayRewardOutcome
            {
                Success = true,
                GrantedItems = null,
                GrantedCurrencies = null,
            });
        }

        private MultiplayLimitResult BuildResult()
        {
            return new MultiplayLimitResult
            {
                Success = true,
                PlayCount = _playCount,
                MaxPlayCount = MAX_PLAY_COUNT,
                CanPlay = _playCount < MAX_PLAY_COUNT,
                LastResetAtUtc = string.Empty,
            };
        }
    }
}