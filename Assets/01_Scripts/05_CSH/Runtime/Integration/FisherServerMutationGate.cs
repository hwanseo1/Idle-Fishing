using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 서버 mutation 후 낡은 PlayerData/PlayFabDataStore snapshot이 성공 응답을 되덮지 않게 하는 공통 정책입니다.
    /// </summary>
    internal static class FisherServerMutationPolicy
    {
        /// <summary>
        /// 성공 mutation 응답 직후 오래된 PlayerData snapshot pull이 권위 응답을 덮지 않게 막는 시간입니다.
        /// </summary>
        public const float SnapshotPullSuppressSeconds = 6f;
    }

    /// <summary>
    /// 서버 요청 중복 클릭, stale callback, timeout 복구를 패널 어댑터에서 같은 방식으로 처리합니다.
    /// </summary>
    internal sealed class FisherServerRequestGate
    {
        private int _token;
        private float _startedAt;
        private string _requestName = string.Empty;

        /// <summary>
        /// 현재 패널이 서버 mutation 응답을 기다리는 중인지 나타냅니다.
        /// </summary>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// stale callback을 구분하기 위한 현재 요청 번호입니다.
        /// </summary>
        public int Token => _token;

        /// <summary>
        /// 현재 진행 중인 요청이 지정한 요청명과 같은지 확인합니다.
        /// </summary>
        public bool IsBusyFor(string requestName)
        {
            return IsBusy && string.Equals(_requestName, requestName ?? string.Empty, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 서버 요청을 시작하고, 이미 진행 중이면 false를 반환해 중복 클릭을 막습니다.
        /// </summary>
        public bool TryBegin(string requestName)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            _token++;
            _startedAt = Time.unscaledTime;
            _requestName = requestName ?? string.Empty;
            return true;
        }

        /// <summary>
        /// token이 현재 요청과 맞을 때만 요청을 완료 처리합니다.
        /// </summary>
        public bool TryComplete(int token)
        {
            if (!IsCurrent(token))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        /// <summary>
        /// 실패 callback이 현재 요청에 해당할 때만 잠금을 해제합니다.
        /// </summary>
        public bool TryAbort(int token)
        {
            return TryComplete(token);
        }

        /// <summary>
        /// 응답이 timeout을 넘긴 현재 요청이면 잠금을 해제하고 표시용 요청명을 반환합니다.
        /// </summary>
        public bool TryRecoverTimeout(float timeoutSeconds, string fallbackName, out string requestName)
        {
            requestName = string.Empty;
            if (!IsBusy)
            {
                return false;
            }

            float timeout = Mathf.Max(1f, timeoutSeconds);
            if (Time.unscaledTime - _startedAt < timeout)
            {
                return false;
            }

            requestName = DisplayName(fallbackName);
            Invalidate();
            return true;
        }

        /// <summary>
        /// UI 상태 텍스트에 사용할 현재 요청 메시지를 반환합니다.
        /// </summary>
        public string CurrentMessage(string busyPrefix)
        {
            if (!IsBusy)
            {
                return string.Empty;
            }

            string prefix = string.IsNullOrWhiteSpace(busyPrefix) ? "서버 요청 중" : busyPrefix;
            return string.IsNullOrWhiteSpace(_requestName) ? prefix : prefix + ": " + _requestName;
        }

        /// <summary>
        /// 패널 비활성화나 외부 상태 초기화 시 현재 요청을 무효화합니다.
        /// </summary>
        public void Invalidate()
        {
            IsBusy = false;
            _token++;
            _startedAt = 0f;
            _requestName = string.Empty;
        }

        private bool IsCurrent(int token)
        {
            return IsBusy && token == _token;
        }

        private string DisplayName(string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(_requestName))
            {
                return _requestName;
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? "ServerRequest" : fallbackName;
        }
    }
}
