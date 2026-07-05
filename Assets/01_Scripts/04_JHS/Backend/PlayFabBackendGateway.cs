using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace JHS.Backend
{
    // IBackendGateway의 실연결 구현 — PlayFab Title Data에서 RemoteConfig(JSON 1키)를 받아온다.
    // 콜백 기반 GetTitleData를 TaskCompletionSource로 async 규격(Task<RemoteConfig>)에 맞춘다.
    // 실패(세션없음/네트워크/키없음/파싱) 시 예외 → BackendService가 3단 fallback(캐시→동봉)으로 처리.
    //
    // ⚠️ 경계: 이 게이트웨이는 로그인하지 않는다. 익명로그인(세연 담당)으로 세션이 선 뒤 호출되는 전제.
    //    세션이 없으면 GetTitleData가 인증 오류 → 예외 → fallback(=오프라인처럼 graceful). [collab: no touch others' code]
    public sealed class PlayFabBackendGateway : IBackendGateway
    {
        private readonly string _titleDataKey;

        // titleDataKey = PlayFab 콘솔 Title Data에 둘 키 이름(그 값 = RemoteConfig JSON 한 장).
        public PlayFabBackendGateway(string titleDataKey = "RemoteConfig")
            => _titleDataKey = string.IsNullOrEmpty(titleDataKey) ? "RemoteConfig" : titleDataKey;

        // 콜백이 하나도 안 오는 스톨 대비 타임아웃(초). 초과 시 예외 → BackendService 3단 fallback으로.
        private const float TimeoutSec = 15f;

        public async Task<RemoteConfig> FetchAsync()
        {
            var tcs = new TaskCompletionSource<RemoteConfig>();

            var request = new GetTitleDataRequest { Keys = new List<string> { _titleDataKey } };
            PlayFabClientAPI.GetTitleData(request,
                result =>
                {
                    // 키가 없거나 빈 값 → fallback으로 떨어지게 예외
                    if (result.Data == null ||
                        !result.Data.TryGetValue(_titleDataKey, out var json) ||
                        string.IsNullOrEmpty(json))
                    {
                        tcs.SetException(new Exception($"[PlayFab] Title Data에 '{_titleDataKey}' 키 없음/빈값"));
                        return;
                    }

                    // JSON 1장 → RemoteConfig 파싱 (캐시·동봉 SO와 동일 타입·동일 파싱 경로)
                    try
                    {
                        var config = JsonUtility.FromJson<RemoteConfig>(json);
                        if (config == null)
                            tcs.SetException(new Exception($"[PlayFab] '{_titleDataKey}' JSON 파싱 결과 null"));
                        else
                            tcs.SetResult(config);
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(new Exception($"[PlayFab] RemoteConfig 파싱 실패: {e.Message}"));
                    }
                },
                error => tcs.SetException(
                    new Exception($"[PlayFab] GetTitleData 실패: {error.Error} - {error.ErrorMessage}")));

            // 콜백 성공/실패 어느 쪽도 안 오는 경우(스톨) 방어 — 타임아웃이 이기면 예외로 fallback 유도.
            var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(TimeoutSec)));
            if (done != tcs.Task)
                throw new Exception($"[PlayFab] GetTitleData 타임아웃({TimeoutSec}s) — fallback");
            return await tcs.Task;   // 결과 또는 콜백이 set한 예외를 그대로 전파
        }
    }
}
