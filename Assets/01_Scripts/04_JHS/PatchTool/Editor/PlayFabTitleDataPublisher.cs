using System.Threading.Tasks;
#if ENABLE_PLAYFABADMIN_API
using PlayFab;
using PlayFab.AdminModels;
#endif

namespace JHS.PatchTool
{
    // 실연결 Publisher — PlayFab Admin API(SetTitleData)로 Title Data에 쓴다.
    // ⚠️ 보안: Editor 폴더에 있어 플레이어 빌드에 절대 미포함. Secret Key는 PlayFabSettings에 세팅된 걸 사용
    //    (호출 측[PatchToolWindow]이 푸시 직전 DeveloperSecretKey 주입). 키는 소스/빌드에 절대 커밋 금지.
    // ENABLE_PLAYFABADMIN_API define가 꺼져 있으면 컴파일은 되되(프로젝트 안 깨짐) 실행 시 안내 후 실패(graceful).
    public sealed class PlayFabTitleDataPublisher : ITitleDataPublisher
    {
        public Task<PublishResult> PublishAsync(string key, string json)
        {
#if ENABLE_PLAYFABADMIN_API
            var tcs = new TaskCompletionSource<PublishResult>();
            var request = new SetTitleDataRequest { Key = key, Value = json };
            PlayFabAdminAPI.SetTitleData(request,
                _ => tcs.SetResult(PublishResult.Ok($"TitleData['{key}'] 푸시 성공")),
                err => tcs.SetResult(PublishResult.Fail($"[PlayFab] SetTitleData 실패: {err.Error} - {err.ErrorMessage}")));
            return tcs.Task;
#else
            return Task.FromResult(PublishResult.Fail(
                "ENABLE_PLAYFABADMIN_API define가 꺼져 있음 — Player Settings → Scripting Define Symbols에 추가해야 SetTitleData 사용 가능. (Mock으로는 dry-run 가능)"));
#endif
        }
    }
}
