using System.Threading.Tasks;

namespace JHS.PatchTool
{
    // Title Data "키 → 현재 JSON 값"을 읽는 통로(읽기 규격). Phase2 — 푸시 전후 라이브 값 확인용.
    // 쓰기(ITitleDataPublisher)와 분리(ISP): 소비자가 읽기만 필요할 때 쓰기 의존 안 함.
    public interface ITitleDataReader
    {
        // 키의 현재 값을 읽는다. 키가 없으면 Success=true·Value=null. 실패는 결과로 돌려준다(예외 X).
        Task<ReadResult> ReadAsync(string key);
    }

    // 읽기 결과(성공 여부 + 값(키 없으면 null) + 사람이 읽을 메시지).
    public readonly struct ReadResult
    {
        public readonly bool Success;
        public readonly string Value;     // 키 미설정 시 null
        public readonly string Message;

        public ReadResult(bool success, string value, string message)
        {
            Success = success;
            Value = value;
            Message = message;
        }

        public static ReadResult Ok(string value) => new ReadResult(true, value, "OK");
        public static ReadResult Fail(string message) => new ReadResult(false, null, message);
    }
}
