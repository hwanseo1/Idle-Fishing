using System.Threading.Tasks;

namespace JHS.PatchTool
{
    // Title Data에 "키 → JSON 문자열"을 쓰는 통로(쓰기 규격). 에디터 전용 도구의 핵심 추상화.
    // 실제 구현(PlayFab Admin)과 dry-run Mock을 갈아끼운다(DIP). 소비자(PatchService)는 이 인터페이스만 의존.
    public interface ITitleDataPublisher
    {
        // 키에 JSON 값을 쓴다. 성공/실패를 결과로 돌려준다(예외로 흘리지 않음).
        Task<PublishResult> PublishAsync(string key, string json);
    }

    // 푸시 결과 한 묶음(성공 여부 + 사람이 읽을 메시지).
    public readonly struct PublishResult
    {
        public readonly bool Success;
        public readonly string Message;

        public PublishResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static PublishResult Ok(string message = "OK") => new PublishResult(true, message);
        public static PublishResult Fail(string message) => new PublishResult(false, message);
    }
}
