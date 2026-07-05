using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace JHS.EditorTools
{
    // JHS CCD 배포 툴 — Addressables "Build & Release"(빌드 + CCD 업로드 + 릴리스)를 메뉴 한 번으로.
    // Unity 공개 API AddressableAssetSettings.BuildAndReleasePlayerContent()/UpdateAndReleasePlayerContent() 래핑.
    //  · New Build & Release      : 전체 새 빌드 후 릴리스(처음/대규모 변경).
    //  · Update a Previous Build  : 이전 빌드 대비 변경분만 콘텐츠 업데이트 릴리스(hot-update 정석).
    // ⚠️ 라이브 CCD 버킷(production/latest)에 실제 업로드 → 실행 전 대상(환경/LoadPath) 확인 다이얼로그.
    //    에디터의 Unity Cloud 로그인 세션으로 인증한다. (CI 헤드리스 자동화는 별도 서비스계정 키 필요)
    public static class CcdReleaseTool
    {
        [MenuItem("JHS/CCD 배포/New Build & Release (전체 새 빌드)", false, 100)]
        public static void BuildAndRelease() => Run(false);

        [MenuItem("JHS/CCD 배포/Update a Previous Build & Release (콘텐츠 업데이트)", false, 101)]
        public static void UpdateAndRelease() => Run(true);

        private static bool _running;

        // 엣지 전파 폴링용 HTTP 클라이언트(302 자동 추적 → 서명된 CDN 엣지 URL까지 도달).
        private static readonly HttpClient _http = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = true })
        { Timeout = TimeSpan.FromSeconds(15) };

        private const int EdgePollTimeoutSec = 180;   // 전파 대기 최대 시간
        private const int EdgePollIntervalSec = 3;    // 폴링 간격

        private static async void Run(bool isUpdate)
        {
            if (_running) { EditorUtility.DisplayDialog("CCD 배포", "이미 배포가 진행 중입니다.", "확인"); return; }

            var s = AddressableAssetSettingsDefaultObject.Settings;
            if (s == null) { EditorUtility.DisplayDialog("CCD 배포", "Addressables 설정이 없습니다.", "확인"); return; }

            string pid = s.activeProfileId;
            string env = s.profileSettings.GetValueByName(pid, "EnvironmentName");
            string remoteLoad = s.profileSettings.GetValueByName(pid, "Remote.LoadPath");
            string kind = isUpdate ? "Update a Previous Build & Release (콘텐츠 업데이트)"
                                   : "New Build & Release (전체 새 빌드)";

            bool ok = EditorUtility.DisplayDialog(
                "⚠️ 라이브 CCD 배포",
                "방식: " + kind + "\n환경: " + env + "\nLoadPath: " + remoteLoad +
                "\n\n실제 CCD 버킷에 업로드/릴리스합니다. 계속할까요?",
                "배포", "취소");
            if (!ok) return;

            _running = true;
            DateTime releaseStartUtc = DateTime.UtcNow;   // 이번 릴리스 산출물만 폴링하기 위한 기준 시각
            try
            {
                Debug.Log("[CcdReleaseTool] " + kind + " 시작…");
                var result = isUpdate
                    ? await AddressableAssetSettings.UpdateAndReleasePlayerContent()
                    : await AddressableAssetSettings.BuildAndReleasePlayerContent();

                if (result != null && string.IsNullOrEmpty(result.Error))
                {
                    Debug.Log("[CcdReleaseTool] 릴리스 완료 ✅ (" + result.Duration.ToString("0.0") + "s) — 엣지 전파 확인 시작…");
                    // 릴리스 직후 CDN 엣지 전파를 확인(200 될 때까지 폴링). 여기까지 통과해야 dataVersion을 올려도 최초접속 실패가 없음.
                    await WaitForEdgePropagationAsync(s, releaseStartUtc);
                }
                else
                {
                    string err = result != null ? result.Error : "(결과 null)";
                    Debug.LogError("[CcdReleaseTool] 실패: " + err);
                    EditorUtility.DisplayDialog("CCD 배포 실패", err, "확인");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CcdReleaseTool] 예외: " + e);
                EditorUtility.DisplayDialog("CCD 배포 예외", e.Message, "확인");
            }
            finally
            {
                _running = false;
                EditorUtility.ClearProgressBar();
            }
        }

        // 방금 릴리스된 원격 파일(번들+카탈로그)이 "앱이 실제 요청하는 다운로드 URL"에서 200이 될 때까지 폴링한다.
        //  · Remote.LoadPath({...}/entry_by_path/content/?path=) + 파일명 = 앱이 요청하는 URL과 동일.
        //  · HttpClient가 client-api의 302를 자동으로 따라가 서명된 CDN 엣지까지 도달 → 최종 상태가 곧 엣지 준비 상태.
        //  · 번들 파일명은 콘텐츠 해시라 빌드마다 바뀜 → 200 = 새 바이트가 엣지에 실제 전파됨을 의미.
        private static async Task WaitForEdgePropagationAsync(AddressableAssetSettings s, DateTime sinceUtc)
        {
            string pid = s.activeProfileId;
            string loadBase = s.profileSettings.GetValueByName(pid, "Remote.LoadPath");        // ...content/?path=
            string buildPathRaw = s.profileSettings.GetValueByName(pid, "Remote.BuildPath");   // 로컬 빌드 출력 폴더

            if (string.IsNullOrEmpty(loadBase) || string.IsNullOrEmpty(buildPathRaw))
            {
                ShowDoneDialog(false, "Remote.LoadPath/BuildPath 미설정 — 전파 확인을 생략했습니다.");
                return;
            }

            // BuildPath는 프로젝트 루트 기준 상대 경로(예: CCDBuildData/.../latest).
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildAbs = Path.IsPathRooted(buildPathRaw)
                ? buildPathRaw
                : Path.GetFullPath(Path.Combine(projectRoot, buildPathRaw));

            if (!Directory.Exists(buildAbs))
            {
                ShowDoneDialog(false, "빌드 출력 폴더를 찾지 못해 전파 확인을 생략했습니다.\n" + buildAbs);
                return;
            }

            // 이번 릴리스에서 새로 쓰인 원격 파일만 대상(스테일 파일 제외).
            var files = Directory.GetFiles(buildAbs, "*", SearchOption.TopDirectoryOnly)
                .Where(p =>
                {
                    string ext = Path.GetExtension(p).ToLowerInvariant();
                    string name = Path.GetFileName(p).ToLowerInvariant();
                    bool interesting = ext == ".bundle"
                        || (name.StartsWith("catalog_") && (ext == ".json" || ext == ".hash"));
                    return interesting && File.GetLastWriteTimeUtc(p) >= sinceUtc.AddSeconds(-5);
                })
                .Select(Path.GetFileName)
                .ToList();

            if (files.Count == 0)
            {
                ShowDoneDialog(true, "변경된 원격 파일이 없어 전파 확인이 불필요합니다.");
                return;
            }

            Debug.Log("[CcdReleaseTool] 엣지 전파 폴링 시작 — 대상 " + files.Count + "개.");

            var pending = new HashSet<string>(files);
            double deadline = EditorApplication.timeSinceStartup + EdgePollTimeoutSec;
            bool canceled = false;

            while (pending.Count > 0 && EditorApplication.timeSinceStartup < deadline)
            {
                int doneCount = files.Count - pending.Count;
                double remaining = deadline - EditorApplication.timeSinceStartup;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "CCD 엣지 전파 확인",
                        "다운로드 URL 200 대기 " + doneCount + "/" + files.Count +
                        "  (남은 " + Mathf.CeilToInt((float)remaining) + "s)",
                        files.Count == 0 ? 1f : (float)doneCount / files.Count))
                {
                    canceled = true;
                    break;
                }

                var justReady = new List<string>();
                foreach (var f in pending)
                {
                    if (await IsUrlReadyAsync(loadBase + Uri.EscapeDataString(f)))
                        justReady.Add(f);
                }
                foreach (var f in justReady) pending.Remove(f);

                if (pending.Count == 0) break;
                await Task.Delay(EdgePollIntervalSec * 1000);
            }

            if (canceled)
            {
                Debug.LogWarning("[CcdReleaseTool] 엣지 폴링 사용자 취소 — 미확인 " + pending.Count + "개.");
                ShowDoneDialog(false, "전파 확인을 취소했습니다. dataVersion을 올리기 전 잠시 후 재확인을 권장합니다.\n미확인: " + pending.Count + "/" + files.Count);
            }
            else if (pending.Count == 0)
            {
                Debug.Log("[CcdReleaseTool] 엣지 전파 완료 ✅ — 전 파일 200.");
                ShowDoneDialog(true, "엣지 전파 확인 완료 (" + files.Count + "개).\n이제 Title Data의 dataVersion을 올려도 최초 접속 실패가 없습니다.");
            }
            else
            {
                Debug.LogWarning("[CcdReleaseTool] 엣지 전파 타임아웃 — 미전파 " + pending.Count + "/" + files.Count + ".");
                ShowDoneDialog(false, "일부 파일이 아직 엣지에 전파되지 않았습니다 (" + pending.Count + "/" + files.Count + ").\n잠시 후 재확인한 뒤 dataVersion을 올리세요.");
            }
        }

        // 앱이 쓰는 URL로 GET(302 자동 추적) → 최종 응답이 200/206이면 엣지 준비 완료.
        private static async Task<bool> IsUrlReadyAsync(string url)
        {
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    // 앞 1바이트만 요청해 대역폭 절약(서버가 무시하면 200 전체, 지원하면 206).
                    req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                    using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
                    {
                        int code = (int)resp.StatusCode;
                        return code == 200 || code == 206;
                    }
                }
            }
            catch (Exception e)
            {
                // 일시적 네트워크 오류 → 다음 폴링에서 재시도.
                Debug.Log("[CcdReleaseTool] 폴링 재시도 " + url + " : " + e.Message);
                return false;
            }
        }

        private static void ShowDoneDialog(bool ok, string msg)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(ok ? "CCD 배포 완료 ✅" : "CCD 배포 — 확인 필요 ⚠️", msg, "확인");
        }
    }
}
