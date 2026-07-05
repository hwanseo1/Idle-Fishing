using System.Collections.Generic;
using System.Threading.Tasks;
#if ENABLE_PLAYFABADMIN_API
using PlayFab;
using PlayFab.AdminModels;
#endif

namespace JHS.PatchTool
{
    // 실연결 Reader — PlayFab Admin API(GetTitleData)로 현재 라이브 값을 읽는다(Phase2: 푸시 전후 확인용).
    // 읽기는 비파괴라 dry-run 불필요. Editor 폴더 = 빌드 미포함. Secret Key는 호출 측[PatchToolWindow]이 주입.
    // ENABLE_PLAYFABADMIN_API define가 꺼져 있으면 컴파일은 되되 실행 시 안내 후 실패(graceful).
    public sealed class PlayFabTitleDataReader : ITitleDataReader
    {
        public Task<ReadResult> ReadAsync(string key)
        {
#if ENABLE_PLAYFABADMIN_API
            var tcs = new TaskCompletionSource<ReadResult>();
            var request = new GetTitleDataRequest { Keys = new List<string> { key } };
            PlayFabAdminAPI.GetTitleData(request,
                res =>
                {
                    if (res.Data != null && res.Data.TryGetValue(key, out var val))
                        tcs.SetResult(ReadResult.Ok(val));
                    else
                        tcs.SetResult(ReadResult.Ok(null));   // 키 없음(미설정)
                },
                err => tcs.SetResult(ReadResult.Fail($"[PlayFab] GetTitleData 실패: {err.Error} - {err.ErrorMessage}")));
            return tcs.Task;
#else
            return Task.FromResult(ReadResult.Fail(
                "ENABLE_PLAYFABADMIN_API define가 꺼져 있음 — Player Settings → Scripting Define Symbols에 추가해야 GetTitleData 사용 가능."));
#endif
        }
    }
}
