using UnityEngine;
using UnityEngine.UI;

namespace JHS.UI
{
    // 메인화면 설정창 — 마스터 볼륨(전역) / 게임 종료.
    // - 볼륨: AudioListener.volume(전역 마스터, BGM+SFX) + PlayerPrefs 저장.
    // - 종료: Application.Quit (에디터는 isPlaying=false).
    // ※ 로그아웃은 협업(로그아웃 시 YWJ 오토세이브 코루틴/잔여 서버호출 정리) 미완으로 제외(2026-06-30).
    // UI 표현/제어 전담. 버튼·슬라이더는 인스펙터 배선.
    public class SettingPanelController : MonoBehaviour
    {
        [Header("패널 / 열기·닫기")]
        [SerializeField] private GameObject _panel;      // 설정창 루트 (열고 닫음)
        [SerializeField] private Button _openButton;     // MainStage의 SettingButton
        [SerializeField] private Button _closeButton;

        [Header("마스터 볼륨")]
        [SerializeField] private Slider _volumeSlider;

        [Header("종료")]
        [SerializeField] private Button _quitButton;

        private const string VolumeKey = "MasterVolume";

        private void Awake()
        {
            // 저장된 마스터 볼륨 복원(없으면 1).
            float v = PlayerPrefs.GetFloat(VolumeKey, 1f);
            AudioListener.volume = v;

            if (_volumeSlider != null)
            {
                _volumeSlider.minValue = 0f;
                _volumeSlider.maxValue = 1f;
                _volumeSlider.SetValueWithoutNotify(v);
                _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
            if (_openButton != null)   _openButton.onClick.AddListener(Open);
            if (_closeButton != null)  _closeButton.onClick.AddListener(Close);
            if (_quitButton != null)   _quitButton.onClick.AddListener(OnQuit);

            if (_panel != null) _panel.SetActive(false);   // 시작은 닫힘
        }

        public void Open()  { if (_panel != null) _panel.SetActive(true); }
        public void Close() { if (_panel != null) _panel.SetActive(false); }

        private void OnVolumeChanged(float v)
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(VolumeKey, v);
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
