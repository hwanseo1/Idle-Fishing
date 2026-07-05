using JHS.Fishing;
using RMS.Data;
using RMS.Fishing;
using Runtime;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.UI
{
    // 보스 스테이지 UI 전담 컨트롤러.
    // FishSpawnManager의 이벤트를 구독해 HP바, 타이머, 보스 정보를 갱신한다.
    // BossStage_Panel의 루트 오브젝트에 부착한다.
    // *이 컴포넌트가 붙은 루트 오브젝트는 씬 시작부터 항상 활성 상태로 둔다.*
    public class BossStageUI : MonoBehaviour
    {
        [Header("연동")]
        [Tooltip("씬의 FishSpawnManager. 없으면 자동 탐색.")]
        [SerializeField] private FishSpawnManager _spawnManager;

        [Header("보스 정보")]
        [Tooltip("보스 이미지")]
        [SerializeField] private Image _bossIcon;

        [Tooltip("보스 이름 텍스트")]
        [SerializeField] private TextMeshProUGUI _bossNameText;

        [Header("HP")]
        [Tooltip("HP 슬라이더 value 0~1로 제어)")]
        [SerializeField] private Slider _hpSlider;

        [Tooltip("HP 수치 텍스트")]
        [SerializeField] private TextMeshProUGUI _hpText;

        [Header("타이머")]
        [Tooltip("남은 시간 텍스트")]
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("이 시간(초) 이하면 경고 색상으로 전환")]
        [SerializeField] private float _timerWarningThreshold = 10f;

        [Tooltip("시간 부족 시 텍스트 색상")]
        [SerializeField] private Color _timerWarningColor = Color.red;

        [Header("결과 연출")]
        [Tooltip("클리어 연출 오브젝트 (완료 시 잠깐 표시 후 숨김)")]
        [SerializeField] private GameObject _clearEffect;

        [Tooltip("실패 연출 오브젝트")]
        [SerializeField] private GameObject _failEffect;

        [Tooltip("결과 연출 표시 시간(초)")]
        [SerializeField] private float _resultDisplayDuration = 2f;

        [Header("보스 클리어 보상")]
        [Tooltip("BossStage_Panel 아래의 RewardPanel 오브젝트")]
        [SerializeField] private BossRewardPanel _bossRewardPanel;

        [Tooltip("정산 확인 후 상태 전환을 위해 BossStageActivator와 연동")]
        [SerializeField] private BossStageActivator _bossStageActivator;

        [Header("정산 패널 표시 중 숨길 UI")]
        [Tooltip("입질 버튼, 멀티 버튼 등 정산 패널이 떠 있는 동안 숨길 오브젝트 목록")]
        [SerializeField] private GameObject[] _hideWhileRewarding;

        [Header("자동낚시")]
        [Tooltip("정산 패널 표시 중 자동낚시를 정지시킬 AutoFishingController")]
        [SerializeField] private AutoFishingController _autoFishingController;

        [SerializeField] private RuntimeStateController _runtimeStateController;


        // 런타임 상태
        private float _remainingTime;
        private bool _timerRunning;
        private bool _bossFinished;
        private bool _bossStageInitialized; // 탭 복귀 시 재초기화 방지
        private Color _timerDefaultColor;
        private BossData _clearedBoss; // 정산 패널에 넘길 보스 데이터
        private StageData _clearedStage;


        private void Awake()
        {
            if (_spawnManager == null)
                _spawnManager = FindFirstObjectByType<FishSpawnManager>();

            if (_timerText != null)
                _timerDefaultColor = _timerText.color;

            // HP 변경, 보스 클리어, 타임 아웃 이벤트만 구독.
            // OnStageChanged는 BossStageActivator가 담당.
            if (_spawnManager != null)
            {
                _spawnManager.OnBossHpChanged += HandleHpChanged;
                _spawnManager.OnBossCleared += HandleBossCleared;
                _spawnManager.OnBossTimeLimitExpired += HandleTimeLimitExpired;
            }
            else
            {
                Debug.LogWarning("[BossStageUI] FishSpawnManager를 찾지 못했습니다. Inspector에서 직접 연결해 주세요.");
            }
        }


        private void OnDestroy()
        {
            if (_spawnManager == null) return;
            _spawnManager.OnBossHpChanged -= HandleHpChanged;
            _spawnManager.OnBossCleared -= HandleBossCleared;
            _spawnManager.OnBossTimeLimitExpired -= HandleTimeLimitExpired;
        }


        private void Update()
        {
            if (!_timerRunning || _bossFinished) return;

            _remainingTime -= Time.deltaTime;

            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                _timerRunning = false;
                UpdateTimerUI(0f);
                HandleTimeLimitExpired();
                return;
            }

            UpdateTimerUI(_remainingTime);
        }


        // 외부 진입점 -----
        // BossStageActivator가 패널을 활성화한 뒤 바로 호출한다.
        public void ShowBossStage(BossData boss)
        {
            if (boss == null) return;
            if (_bossStageInitialized) return; // 탭 복귀 등으로 재호출돼도 보스전 리셋 방지

            _bossStageInitialized = true;
            _bossFinished = false;
            _clearEffect?.SetActive(false);
            _failEffect?.SetActive(false);

            // 보스 정보
            if (_bossIcon != null) _bossIcon.sprite = boss.Icon;
            if (_bossNameText != null) _bossNameText.text = boss.DisplayName;

            // HP 초기화
            UpdateHpUI(boss.MaxHp, boss.MaxHp);

            // 타이머
            if (boss.TimeLimitSeconds > 0)
            {
                _remainingTime = boss.TimeLimitSeconds;
                _timerRunning = true;
                if (_timerText != null) _timerText.color = _timerDefaultColor;
                UpdateTimerUI(_remainingTime);
            }
            else
            {
                _timerRunning = false;
                if (_timerText != null) _timerText.text = "∞";
            }
        }

        // 보스 종료 시 타이머 강제 정지. BossStageActivator에서 패널 끄기 전에 호출.
        public void HideBossStage()
        {
            _timerRunning = false;
            _bossFinished = true;
            _bossStageInitialized = false;
            StopAllCoroutines();
        }


        // 이벤트 핸들러 -----
        private void HandleHpChanged(int currentHp, int maxHp)
        {
            UpdateHpUI(currentHp, maxHp);
        }

        private void HandleBossCleared(BossData boss)
        {
            if (_bossFinished) return;
            _bossFinished = true;
            _timerRunning = false;
            _clearedBoss = boss;
            _clearedStage = _spawnManager.CurrentStage;
            StartCoroutine(ShowResultAndNotify(isCleared: true));
        }

        private void HandleTimeLimitExpired()
        {
            if (_bossFinished) return;
            _bossFinished = true;
            _timerRunning = false;
            StartCoroutine(ShowResultAndNotify(isCleared: false));
        }


        // UI 갱신 -----
        private void UpdateHpUI(int currentHp, int maxHp)
        {
            if (_hpSlider != null)
                _hpSlider.value = maxHp > 0 ? (float)currentHp / maxHp : 0f;

            if (_hpText != null)
                _hpText.text = $"{currentHp} / {maxHp}";
        }

        private void UpdateTimerUI(float seconds)
        {
            if (_timerText == null) return;

            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            _timerText.text = $"{m:00}:{s:00}";
            _timerText.color = seconds <= _timerWarningThreshold
                ? _timerWarningColor
                : _timerDefaultColor;
        }

        private IEnumerator ShowResultAndNotify(bool isCleared)
        {
            if (isCleared)
                _clearEffect?.SetActive(true);
            else
                _failEffect?.SetActive(true);

            yield return new WaitForSeconds(_resultDisplayDuration);

            _clearEffect?.SetActive(false);
            _failEffect?.SetActive(false);

            if (isCleared)
            {
                // 자동낚시 정지, 입질/멀티 버튼 숨김
                if (_autoFishingController != null) _autoFishingController.enabled = false;
                SetRewardingObjects(false);

                if (_bossRewardPanel != null)
                {
                    var results = BossSettlementService.SettleClear(
                        _clearedBoss,
                        _clearedStage,
                        _spawnManager.FirstStage);

                    _bossRewardPanel.Show(results, () =>
                    {
                        // 자동낚시 복구, 버튼 복구
                        if (_autoFishingController != null) _autoFishingController.enabled = true;
                        SetRewardingObjects(true);

                        _bossStageActivator?.OnRewardConfirmed();
                        if (_runtimeStateController != null)
                            _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
                    });
                }
                else
                {
                    if (_autoFishingController != null) _autoFishingController.enabled = true;
                    SetRewardingObjects(true);
                    _bossStageActivator?.OnRewardConfirmed();
                    if (_runtimeStateController != null)
                        _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
                }
            }
            else
            {
                // 패배 시도 자동낚시 정지 및 버튼 숨김 (보스 추가 공격 방지)
                if (_autoFishingController != null) _autoFishingController.enabled = false;
                SetRewardingObjects(false);

                _spawnManager?.HandleBossTimeLimitExpired();
                if (_runtimeStateController != null)
                    _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;

                // MAINSTAGE 전환 후 자동낚시 복구
                if (_autoFishingController != null) _autoFishingController.enabled = true;
                SetRewardingObjects(true);
            }
        }

        private void SetRewardingObjects(bool active)
        {
            if (_hideWhileRewarding == null) return;
            foreach (GameObject obj in _hideWhileRewarding)
                if (obj != null) obj.SetActive(active);
        }


        // 에디터 테스트
#if UNITY_EDITOR
        [ContextMenu("Test/보스 UI 표시 (대왕 문어 더미)")]
        private void Editor_ShowDummy()
        {
            if (_bossNameText != null) _bossNameText.text = "대왕 문어";
            UpdateHpUI(300, 300);
            _remainingTime = 120f;
            _timerRunning = true;
            _bossFinished = false;
            _clearEffect?.SetActive(false);
            _failEffect?.SetActive(false);
        }
#endif

    }
}

