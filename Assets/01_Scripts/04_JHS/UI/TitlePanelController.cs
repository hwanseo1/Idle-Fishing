using UnityEngine;
using UnityEngine.UI;
using Runtime;   // RuntimeStateController, RuntimeState

namespace JHS.UI
{
    // 타이틀(GameStart) 화면 — 로그인 전에 뜨는 화면. 게임 시작 / 게임 종료.
    // - 게임 시작: RuntimeState를 LOGIN으로 전환(로그인 화면으로). ※자동로그인/명시 로그인 흐름은 부트(PSY/YWJ) 영역.
    // - 게임 종료: Application.Quit.
    // RuntimeUIController가 GAMESTART 상태일 때 이 패널을 표시한다.
    public class TitlePanelController : MonoBehaviour
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _quitButton;

        private void Awake()
        {
            if (_startButton != null) _startButton.onClick.AddListener(OnStart);
            if (_quitButton != null)  _quitButton.onClick.AddListener(OnQuit);
        }

        private void OnStart()
        {
            var rsc = FindFirstObjectByType<RuntimeStateController>();
            if (rsc != null) rsc.CurrentState = RuntimeState.LOGIN;
            else Debug.LogWarning("[Title] RuntimeStateController 없음 — 로그인 전환 불가");
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
