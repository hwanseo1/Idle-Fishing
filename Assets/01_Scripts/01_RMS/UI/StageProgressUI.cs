using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RMS.Data;
using RMS.Fishing;


namespace RMS.UI
{
    // 스테이지 진행 상황(기여도)을 프로그레스바 + 텍스트로 표시.
    // 클리어 시 연출 패널을 잠깐 띄운다.

    public class StageProgressUI : MonoBehaviour
    {
        [Header("연동")]
        [Tooltip("FishSpawnManager 참조. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private FishSpawnManager _spawnManager;

        [Header("프로그레스바")]
        [Tooltip("기여도 진행률을 표시할 Slider (0~1 범위)")]
        [SerializeField] private Slider _progressBar;

        [Tooltip("'현재기여도 / 임계값' 형식으로 표시할 테스트")]
        [SerializeField] private TextMeshProUGUI _contributionText;

        [Tooltip("현재 스테이지 이름을 표시할 텍스트")]
        [SerializeField] private TextMeshProUGUI _stageNameText;

        [Header("클리어 연출")]
        [Tooltip("클리어 시 표시할 패널. 평소엔 꺼둔다.")]
        [SerializeField] private GameObject _clearPanel;

        [Tooltip("클리어 패널 안의 텍스트 (선택)")]
        [SerializeField] private TextMeshProUGUI _clearText;

        [Tooltip("클리어 패널이 표시되는 시간(초)")]
        [SerializeField] private float _clearDisplayDuration = 2f;

        
        private void OnEnable()
        {
            if (_spawnManager == null)
                _spawnManager = FindFirstObjectByType<FishSpawnManager>();

            if (_spawnManager != null)
            {
                _spawnManager.OnStageCleared += OnStageCleared;
                _spawnManager.OnStageChanged += OnStageChanged;
            }

            if (_clearPanel != null)
                _clearPanel.SetActive(false);

            Refresh();
        }

        private void OnDisable()
        {
            if (_spawnManager != null)
            {
                _spawnManager.OnStageCleared -= OnStageCleared;
                _spawnManager.OnStageChanged -= OnStageChanged;
            }
        }

        // 매 프레임 기여도 갱신 (방치형이라 실시간 반영)
        private void Update()
        {
            Refresh();
        }

        // 프로그레스바 + 텍스트 갱신
        private void Refresh()
        {
            if (_spawnManager == null || _spawnManager.CurrentStage == null)
                return;

            float current = _spawnManager.TotalContribution;
            float threshold = _spawnManager.CurrentStage.ClearContributionThreshold;

            // 프로그레스바 (0~1)
            if (_progressBar != null)
                _progressBar.value = threshold > 0f ? Mathf.Clamp01(current / threshold) : 0f;

            // 기여도 텍스트
            if (_contributionText != null)
                _contributionText.text = $"{current:0.#} / {threshold:0.#}";

            // 스테이지 이름 텍스트
            if (_stageNameText != null)
                _stageNameText.text = _spawnManager.CurrentStage.DisplayName;
        }

        // 스테이지 클리어 시 연출
        private void OnStageCleared(StageData stage)
        {
            if (_clearPanel == null) return;
            if (!gameObject.activeInHierarchy) return;
            StartCoroutine(ShowClearPanel(stage));

        }

        // 스테이지 전환 시 프로그레스바 초기화
        private void OnStageChanged(StageData stage)
        {
            if (_progressBar != null)
                _progressBar.value = 0f;

            if (_contributionText != null)
                _contributionText.text = "0 / " + (stage != null ? stage.ClearContributionThreshold.ToString("0.#") : "?");

            if (_stageNameText != null && stage != null)
                _stageNameText.text = stage.DisplayName;
        }

        private IEnumerator ShowClearPanel(StageData stage)
        {
            if (_clearPanel == null) yield break;

            if (_clearText != null)
                _clearText.text = $"{(stage != null ? stage.DisplayName : "스테이지")} 클리어!";

            _clearPanel.SetActive(true);
            yield return new WaitForSeconds(_clearDisplayDuration);
            _clearPanel.SetActive(false);
        }
    }
}

