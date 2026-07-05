using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace JHS.Content
{
    // CCD(Addressables Remote) 다운로드 진행 게이지. 콘텐츠 로드 '전에' 원격 번들을 미리 받아
    // 진행률을 패널(Slider + 텍스트)로 보여준다.
    //  · Registry(로컬)이거나 이미 캐시/로컬(사이즈 0)이면 아무것도 안 하고 즉시 통과.
    //  · 로드 자체는 Addressables가 필요 시 자동 다운로드하므로, 이 게이트는 'UX(진행 표시)'용 best-effort.
    //    (예외가 나도 삼키고 통과 → 이어지는 EnsureReadyAsync의 로드가 자동 다운로드로 폴백)
    // .Result/.Wait 금지 — Addressables Task는 메인스레드 블로킹 시 데드락(폴링 await만 사용).
    [DisallowMultipleComponent]
    public class CcdDownloadGate : MonoBehaviour
    {
        [Tooltip("표시/숨김할 패널 루트(비우면 이 컴포넌트가 붙은 오브젝트).")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TMP_Text _bodyText;
        [Tooltip("진행률 UI 갱신 주기(ms).")]
        [SerializeField] private int _pollMs = 100;

        [Header("업데이트 완료 게이트")]
        [Tooltip("콘텐츠 완전 준비 후 눌러 다음(오프라인 정산)으로 진행하는 버튼. 초기 비활성/숨김.")]
        [SerializeField] private Button _confirmButton;
        [Tooltip("버튼과 안내를 묶은 루트(선택). 비우면 버튼만 토글.")]
        [SerializeField] private GameObject _confirmRoot;

        private GameObject Panel => _panelRoot != null ? _panelRoot : gameObject;

        // 주어진 키들의 원격 의존성을 (필요하면) 다운로드하며 게이지를 표시한다.
        // Registry/캐시/사이즈0이면 즉시 통과. 완료(또는 예외) 시 항상 패널을 숨긴다.
        public async Task EnsureDownloadedAsync(IList<string> keys)
        {
            if (keys == null || keys.Count == 0) return;
            // Addressable 모드가 아니면(Registry) 원격 다운로드 개념이 없다.
            if (!(ContentService.Assets is AddressableAssetProvider)) return;

            var keyList = new List<object>(keys.Count);
            foreach (var k in keys) keyList.Add(k);

            try
            {
                var sizeHandle = Addressables.GetDownloadSizeAsync(keyList);
                long size = await sizeHandle.Task;
                if (sizeHandle.IsValid()) Addressables.Release(sizeHandle);
                if (size <= 0) return;   // 이미 캐시/로컬 — 게이지 불필요

                SetPanel(true);
                ShowProgressBar(true);   // 다운로드 중엔 게이지 표시
                SetConfirm(false);       // 완료 버튼은 숨김
                UpdateUi(0f, 0, size);

                var dl = Addressables.DownloadDependenciesAsync(keyList, Addressables.MergeMode.Union, false);
                while (!dl.IsDone)
                {
                    var st = dl.GetDownloadStatus();
                    UpdateUi(st.Percent, st.DownloadedBytes, st.TotalBytes > 0 ? st.TotalBytes : size);
                    await Task.Delay(_pollMs);
                }
                UpdateUi(1f, size, size);
                if (dl.IsValid()) Addressables.Release(dl);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CcdDownloadGate] 다운로드 예외 — 로드로 진행(Addressables 자동 다운로드 폴백). " + e.Message);
            }
            finally
            {
                SetPanel(false);
            }
        }

        // 콘텐츠가 완전히 준비된 뒤 "업데이트 완료" 버튼을 눌러야 다음(오프라인 정산)으로 진행한다.
        // 준비 안 됐으면(로드 실패/엣지 전파 지연) 백오프 재시도하며 "업데이트 적용 중…"을 표시(해결책2).
        // 새 RSM 상태를 만들지 않는 자기완결 오버레이 — 상태머신과 무관하다.
        //  · 원격(Addressable) 모드가 아니면(Registry/baseline) 업데이트 개념이 없어 즉시 통과.
        public async Task WaitForUpdateCompleteConfirmAsync()
        {
            if (!(ContentService.Assets is AddressableAssetProvider)) return;

            // 이번 부팅에 실제 업데이트(dataVersion 변경/최초 다운로드)가 없었으면 패널을 띄우지 않는다.
            // 이미 최신 콘텐츠를 받은 동일 버전 재접속에선 조용히 통과 — 매 부팅 패널이 뜨던 문제 수정.
            if (!ContentCatalogUpdater.DidUpdateThisSession)
            {
                Debug.Log("[CcdDownloadGate] 업데이트 없음 — 완료 게이트 통과(패널 미표시)");
                return;
            }
            Debug.Log("[CcdDownloadGate] 업데이트 감지 — 완료 버튼 대기");

            SetPanel(true);
            ShowProgressBar(false);   // 게이지 숨김
            SetConfirm(false);        // 버튼 숨김(준비될 때까지)

            // 콘텐츠 준비 대기 + 실패 시 백오프 재시도(해결책2 — 엣지 전파 지연으로 첫 로드가 404여도 수 초 뒤 성공).
            // ⚠️ 무한 대기 금지: 최대 횟수 초과하면 부팅을 막지 않고 통과(best-effort, 로드는 이후 자동 다운로드 폴백).
            var mgr = JHS.Equipment.EquipmentManager.Instance;
            int attempt = 0;
            const int maxAttempts = 6;   // 백오프(2~8s) 합쳐 ~35s 후 포기 → 부팅 영구정지 방지
            while (mgr != null && !mgr.HasContent && attempt < maxAttempts)
            {
                attempt++;
                if (_bodyText != null) _bodyText.text = "업데이트 적용 중… (재시도 " + attempt + ")";
                try { await mgr.EnsureReadyAsync(forceReload: true); }
                catch (System.Exception e) { Debug.LogWarning("[CcdDownloadGate] 재로드 예외 — 재시도. " + e.Message); }
                if (mgr.HasContent) break;
                await Task.Delay(Mathf.Min(2000 + attempt * 1000, 8000));   // 2s→…→최대 8s 백오프
            }
            if (mgr != null && !mgr.HasContent)
                Debug.LogWarning("[CcdDownloadGate] 콘텐츠 준비 실패(재시도 소진) — 게이트 통과(로드는 자동 다운로드 폴백에 맡김).");

            // 준비 완료 → 버튼 활성, 클릭 대기.
            if (_bodyText != null) _bodyText.text = "업데이트가 완료되었습니다.";
            SetConfirm(true);

            // ⚠️ 버튼이 미배선(null)이면 누를 방법이 없어 부팅이 영구 정지하므로 자동 통과.
            if (_confirmButton != null)
            {
                var tcs = new TaskCompletionSource<bool>();
                UnityEngine.Events.UnityAction handler = () => tcs.TrySetResult(true);
                _confirmButton.onClick.AddListener(handler);
                try { await tcs.Task; }
                finally { _confirmButton.onClick.RemoveListener(handler); }
            }
            else Debug.LogWarning("[CcdDownloadGate] _confirmButton 미배선 — 확인 없이 자동 통과(부팅 정지 방지).");

            SetPanel(false);
        }

        // 원준 부팅 컨트롤러에서 참조 없이 한 줄로 호출하기 위한 정적 진입점.
        // 게이트가 없으면(Registry/미배치) 조용히 통과한다.
        public static async Task ConfirmUpdateCompleteAsync()
        {
            var gate = FindFirstObjectByType<CcdDownloadGate>(FindObjectsInactive.Include);
            if (gate == null) return;
            await gate.WaitForUpdateCompleteConfirmAsync();
        }

        private void SetPanel(bool on) { if (Panel != null) Panel.SetActive(on); }

        private void ShowProgressBar(bool on) { if (_progressBar != null) _progressBar.gameObject.SetActive(on); }

        private void SetConfirm(bool on)
        {
            if (_confirmRoot != null) _confirmRoot.SetActive(on);
            else if (_confirmButton != null) _confirmButton.gameObject.SetActive(on);
            if (_confirmButton != null) _confirmButton.interactable = on;
        }

        private void UpdateUi(float percent, long done, long total)
        {
            float p = Mathf.Clamp01(percent);
            if (_progressBar != null)
            {
                _progressBar.minValue = 0f;
                _progressBar.maxValue = 1f;
                _progressBar.value = p;
            }
            if (_bodyText != null)
                _bodyText.text = string.Format("{0:0.0} / {1:0.0} MB ({2}%)", ToMB(done), ToMB(total), Mathf.RoundToInt(p * 100f));
        }

        private static float ToMB(long bytes) => bytes / (1024f * 1024f);
    }
}
