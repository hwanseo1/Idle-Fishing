using System;
using System.Threading.Tasks;

namespace JHS.Backend
{
    // 에디터/테스트용 더블. PlayFab 없이 P0 로직·화면을 다 만들고 검증한다.
    // ⚠️ async 지연 + 실패를 일부러 흉내냄 — 그래야 fallback이 진짜 도는지 테스트됨(Mock 충실도).
    //    실제 PlayFabBackendGateway로 갈아끼울 때 "동기였는데 비동기라 놀람" 같은 일 방지.
    public sealed class MockBackendGateway : IBackendGateway
    {
        private readonly RemoteConfig _config;
        private readonly int _delayMs;
        private readonly bool _simulateFailure;

        public MockBackendGateway(RemoteConfig config, int delayMs = 200, bool simulateFailure = false)
        {
            _config = config;
            _delayMs = delayMs;
            _simulateFailure = simulateFailure;
        }

        public async Task<RemoteConfig> FetchAsync()
        {
            if (_delayMs > 0) await Task.Delay(_delayMs);
            if (_simulateFailure) throw new Exception("[Mock] simulated backend failure");
            return _config;
        }
    }
}
