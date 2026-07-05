using System.Threading.Tasks;
using UnityEngine;

namespace JHS.PatchTool
{
    // dry-run / 검증용 Publisher — 실제로 안 쓰고 로그만 남긴다. Secret Key·네트워크 불필요.
    // 파이프라인(소스 로드 → 검증 → Publish)을 키 없이 끝까지 돌려보거나, 푸시 전 미리보기로 쓴다.
    public sealed class MockTitleDataPublisher : ITitleDataPublisher
    {
        public Task<PublishResult> PublishAsync(string key, string json)
        {
            Debug.Log($"[PatchTool/Mock] (dry-run) SET TitleData['{key}'] = {Preview(json)}");
            return Task.FromResult(PublishResult.Ok("dry-run: 실제 푸시 안 함"));
        }

        // 긴 JSON은 앞부분만 잘라 미리보기(콘솔 오염 방지).
        private static string Preview(string json)
        {
            if (string.IsNullOrEmpty(json)) return "(빈 값)";
            return json.Length <= 200 ? json : json.Substring(0, 200) + $"… (총 {json.Length}자)";
        }
    }
}
