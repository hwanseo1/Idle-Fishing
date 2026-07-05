using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JHS.Backend
{
    // 버전게이트 판정(BackendService.Decision)을 "에디터에 손배치한 패널"로 표시하는 정적 바인더.
    // 레이아웃은 에디터에서, 이 스크립트는 패널 켜고/끄기 + 텍스트 채움 + 종료버튼 처리만 한다. (SRP)
    //
    // ⚠️ 이 컴포넌트는 "항상 활성인 오브젝트"(예: Canvas나 매니저)에 붙여야 한다.
    //    꺼지는 패널 위에 붙이면 Awake/Start가 안 돌아 화면이 안 뜬다. 패널들은 SerializeField로 참조만.
    public sealed class VersionGateScreen : MonoBehaviour
    {
        [Header("차단 패널 (빌드 버전 낮음 — 강제 업데이트)")]
        [SerializeField] private GameObject _blockPanel;
        [SerializeField] private TMP_Text _blockTitle;
        [SerializeField] private TMP_Text _blockBody;
        [SerializeField] private Button _blockQuitButton;

        [Header("점검 패널 (Maintenance)")]
        [SerializeField] private GameObject _maintenancePanel;
        [SerializeField] private TMP_Text _maintenanceTitle;
        [SerializeField] private TMP_Text _maintenanceBody;
        [SerializeField] private Button _maintenanceQuitButton;

        [Header("제목 문구 (본문은 현재/필요버전·점검메시지로 코드가 채움)")]
        [SerializeField] private string _blockTitleText = "업데이트 필요";
        [SerializeField] private string _maintenanceTitleText = "점검 중";

        private BackendService _svc;

        private void Awake()
        {
            // 종료 버튼 연결 (둘 다 게임 종료)
            if (_blockQuitButton != null) _blockQuitButton.onClick.AddListener(QuitGame);
            if (_maintenanceQuitButton != null) _maintenanceQuitButton.onClick.AddListener(QuitGame);

            // 시작은 둘 다 꺼둠 (판정 후 필요한 것만 켬)
            if (_blockPanel != null) _blockPanel.SetActive(false);
            if (_maintenancePanel != null) _maintenancePanel.SetActive(false);
        }

        private void Start()
        {
            // 모든 Awake 끝난 뒤(Start) Instance 확보 — 스크립트 실행순서 무관.
            _svc = BackendService.Instance;
            if (_svc == null)
            {
                Debug.LogWarning("[VersionGateScreen] BackendService 없음 — 화면 비활성.");
                return;
            }
            _svc.OnReady += Render;
            if (_svc.IsReady) Render();   // 부팅이 이미 끝났으면 즉시 렌더
        }

        private void OnDestroy()
        {
            if (_svc != null) _svc.OnReady -= Render;
        }

        // 판정 결과에 따라 해당 패널만 켜고 텍스트를 채운다. (Ok/권장 = 둘 다 꺼짐 → 게임 진행)
        private void Render()
        {
            string client = Application.version;
            var cfg = _svc.Config;
            var decision = _svc.Decision;

            bool block = decision == GateDecision.BlockedOutdated;
            bool maint = decision == GateDecision.Maintenance;

            if (_blockPanel != null) _blockPanel.SetActive(block);
            if (_maintenancePanel != null) _maintenancePanel.SetActive(maint);

            if (block)
            {
                if (_blockTitle != null) _blockTitle.text = _blockTitleText;
                if (_blockBody != null)
                    _blockBody.text =
                        $"이 버전은 더 이상 지원되지 않습니다.\n새로운 빌드를 다운로드해 주세요.\n(필요 {cfg?.minVersion} / 현재 {client})";
            }
            else if (maint)
            {
                if (_maintenanceTitle != null) _maintenanceTitle.text = _maintenanceTitleText;
                if (_maintenanceBody != null)
                    _maintenanceBody.text =
                        (cfg != null && !string.IsNullOrEmpty(cfg.maintenanceMessage))
                            ? cfg.maintenanceMessage
                            : "서버 점검 중입니다. 잠시 후 다시 시도해 주세요.";
            }
            // Ok / RecommendUpdate → 위 SetActive(false)로 둘 다 꺼짐
        }

        // 게임 종료. 에디터에선 Application.Quit이 안 먹어서 플레이 중지로 대체.
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        // Mock 값 바꾼 뒤 Play 중 다시 그려보고 싶을 때(에디터 전용).
        [ContextMenu("다시 렌더")]
        private void Editor_Rerender() { if (_svc != null) Render(); }
#endif
    }
}
