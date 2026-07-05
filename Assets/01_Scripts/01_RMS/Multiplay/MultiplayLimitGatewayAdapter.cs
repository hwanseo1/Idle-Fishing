namespace RMS.Multiplay
{
    // 원준님의 실제 MultiplayGateway(PlayFabGateway.Instance.MultiplayLimit)를
    // IMultiplayLimitGateway 인터페이스에 맞게 감싸는 어댑터.
    // (클래스명 MultiplayLimitGateway → MultiplayGateway로 변경됨)
    //
    // ⚠️ GiveMultiplayReward는 서버 검증 없이 클라가 보낸 값을 그대로 지급한다(치팅 방지 미구현, 추후 추가 예정).
    public class MultiplayLimitGatewayAdapter : IMultiplayLimitGateway
    {
        public void RequestStatus(System.Action<MultiplayLimitResult> onComplete)
        {
            var gateway = GetRealGateway();
            if (gateway == null)
            {
                onComplete?.Invoke(MultiplayLimitResult.Failure());
                return;
            }

            gateway.GetMultiplayLimit(
                model => onComplete?.Invoke(ToResult(model)),
                _ => onComplete?.Invoke(MultiplayLimitResult.Failure()));
        }

        public void TryConsume(System.Action<MultiplayLimitResult> onComplete)
        {
            var gateway = GetRealGateway();
            if (gateway == null)
            {
                onComplete?.Invoke(MultiplayLimitResult.Failure());
                return;
            }

            gateway.CheckAndConsumeMultiplayCount(
                response => onComplete?.Invoke(ToResult(response?.data)),
                _ => onComplete?.Invoke(MultiplayLimitResult.Failure()));
        }

        public void DebugReset(System.Action<MultiplayLimitResult> onComplete)
        {
            var gateway = GetRealGateway();
            if (gateway == null)
            {
                onComplete?.Invoke(MultiplayLimitResult.Failure());
                return;
            }

            gateway.DebugResetMultiplayCount(
                model => onComplete?.Invoke(ToResult(model)),
                _ => onComplete?.Invoke(MultiplayLimitResult.Failure()));
        }

        public void GiveReward(int personalContribution, int totalContribution, System.Action<MultiplayRewardOutcome> onComplete)
        {
            var gateway = GetRealGateway();
            if (gateway == null)
            {
                onComplete?.Invoke(MultiplayRewardOutcome.Failure());
                return;
            }

            gateway.GiveMultiplayReward(
                personalContribution,
                totalContribution,
                response => onComplete?.Invoke(ToRewardOutcome(response)),
                _ => onComplete?.Invoke(MultiplayRewardOutcome.Failure()));
        }

        private MultiplayRewardOutcome ToRewardOutcome(MultiplayRewardResponse response)
        {
            if (response == null || !response.success)
                return MultiplayRewardOutcome.Failure();

            return new MultiplayRewardOutcome
            {
                Success = true,
                GrantedItems = response.rewards?.grantedItems,
                GrantedCurrencies = response.rewards?.grantedCurrencies,
            };
        }

        private MultiplayGateway GetRealGateway()
        {
            return PlayFabGateway.Instance != null ? PlayFabGateway.Instance.MultiplayLimit : null;
        }

        // model이 null이면(파싱 실패 등) 통신 실패로 간주
        private MultiplayLimitResult ToResult(MultiplayLimitJSONModel model)
        {
            if (model == null) return MultiplayLimitResult.Failure();

            return new MultiplayLimitResult
            {
                Success = true,
                PlayCount = model.playCount,
                MaxPlayCount = model.maxPlayCount,
                CanPlay = model.canPlay,
                LastResetAtUtc = model.lastResetAtUtc,
            };
        }
    }


    // 보상 지급 결과. MatchResultPanel이 화면에 표시할 최소 정보만 담는다.
    public struct MultiplayRewardOutcome
    {
        public bool Success;
        public MultiplayGrantedItem[] GrantedItems;
        public MultiplayGrantedCurrency[] GrantedCurrencies;

        public static MultiplayRewardOutcome Failure()
        {
            return new MultiplayRewardOutcome
            {
                Success = false,
                GrantedItems = null,
                GrantedCurrencies = null,
            };
        }
    }
}