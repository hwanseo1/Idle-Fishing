using System.Threading.Tasks;

namespace JHS.Backend
{
    // 배포/버전 config 읽기 통로. 게임은 이 인터페이스에만 의존한다(boundary-first/DIP).
    // 구현: MockBackendGateway(에디터/테스트 더블) / PlayFabBackendGateway(실연결, GetTitleData).
    // 실패 시 예외를 던진다 → 호출측(BackendService)이 3단 fallback으로 처리.
    public interface IBackendGateway
    {
        Task<RemoteConfig> FetchAsync();
    }
}
