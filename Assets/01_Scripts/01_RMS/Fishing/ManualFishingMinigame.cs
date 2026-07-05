using JHS.Equipment;
using JHS.Fishing;
using RMS.Data;
using RMS.UI;
using Runtime;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace RMS.Fishing
{
    public class ManualFishingMinigame : MonoBehaviour, IManualFishing
    {
        // 튜닝값
        [Header("게이지 속도")]
        [Tooltip("클릭 유지 시 녹색 게이지 상승 속도. (1.0 = 2.5초에 가득")]
        [SerializeField] private float _fillSpeed = 0.35f;

        [Tooltip("클릭을 뗐을 때 녹색 게이지 하강 속도")]
        [SerializeField] private float _drainSpeed = 0.3f;

        [Header("실패 게이지 기본 속도")]
        [Tooltip("difficulty=0 일 때 실패 게이지 속도 (1/failSpeed = 최대 허용 시간)")]
        [SerializeField] private float _failSpeedBase = 0.04f;
        [SerializeField] private float _failSpeedDifficultyScale = 0.06f;

        [Tooltip("게임 시작 후 빨간 게이지가 차오르기 시작하는 딜레이 (초)")]
        [SerializeField] private float _failStartDelay = 2f;
        [Header("경계 돌파")]
        [Tooltip("Common 기준 경계 돌파에 필요한 클릭 수 (희귀도마다 +1)")]
        [SerializeField] private int _baseBarrierClicks = 3;


        // UI 참조
        [Header("UI")]
        [Tooltip("미니게임 루트 패널 (활성/비활성 제어)")]
        [SerializeField] private GameObject _panel;

        [Tooltip("미니게임 표시 중 숨길 Button. 미니게임 시작 시 꺼지고, 끝나면 다시 켜짐.")]
        [SerializeField] private GameObject _multiButton;
        [SerializeField] private GameObject _biteInteractButton;

        [SerializeField] private RuntimeStateController _runtimeStateController;
        [SerializeField] private BossStageActivator _bossStageActivator;

        [Tooltip("성공 게이지 Image (fillAmount 제어, Fill 타입)")]
        [SerializeField] private Image _fillGauge;

        [Tooltip("실패 게이지 Image (fillAmount 제어, Fill 타입)")]
        [SerializeField] private Image _failGauge;

        [Tooltip("경계선 오브젝트 배열 (희귀도에 따라 활성화)")]
        [SerializeField] private RectTransform[] _barrierMarkers;

        [Tooltip("현재 경계 돌파 요구 클릭 수 표시 TMP")]
        [SerializeField] private TextMeshProUGUI _barrierClickText;
        [SerializeField] private RectTransform _barrierClickContainer;

        [Tooltip("결과 메시지 TMP (없으면 생략)")]
        [SerializeField] private TextMeshProUGUI _resultText;

        // 0628 추가, 연출 보강용 로직 추가
        [SerializeField] private MiniGameFishController _miniGameFishController;


        // 런타임 상태
        private bool _isRunning;
        private bool _pressing;     // 이번 프레임 눌림 여부 (Update -> 루프로 전달)
        private int _clickCount;    // 현재 경계 돌파 누적 클릭
        private bool _atBarrier;
        private int _currentClicksPerBarrier;

        private IEquipmentEffects _equipment;
        private ICrewBonus _crew;
        private DummyEquipmentEffects _dummyEquip;

        private IEquipmentEffects Equipment =>
            _equipment ?? (_dummyEquip ??= new DummyEquipmentEffects());

        public void SetEquipment(IEquipmentEffects equip) => _equipment = equip ?? _equipment;
        public void SetCrew(ICrewBonus crew) => _crew = crew ?? _crew;


        // IManualFishing 구현 -----
        // 일반 낚시용 — FishData 기반으로 난이도/경계선 개수 결정
        public IEnumerator RunMiniGame(FishData fish, Action<ManualResult> onDone)
        {
            float difficulty = fish != null ? fish.ManualDifficulty : 0.3f;
            FishRarity rarity = fish != null ? fish.Rarity : FishRarity.Common;
            float failSpeed = GetActualFailSpeed(difficulty);
            int barrierCount = CalcBarrierCount(rarity);
            int clicksPerBarrier = _baseBarrierClicks + (int)rarity;
            yield return RunMiniGameCore(failSpeed, barrierCount, clicksPerBarrier, onDone);
        }

        // 보스용 오버로드 — BossData.ManualBarrierCount / ManualDifficulty 기반으로 결정
        public IEnumerator RunMiniGame(BossData boss, Action<ManualResult> onDone)
        {
            float difficulty = boss != null ? boss.ManualDifficulty : 0.5f;
            int barrierCount = boss != null ? boss.ManualBarrierCount : 2;
            float failSpeed = GetActualFailSpeed(difficulty);
            // 보스 경계 클릭 수: 경계 개수에 비례해 약간 더 요구
            int clicksPerBarrier = _baseBarrierClicks + barrierCount;
            yield return RunMiniGameCore(failSpeed, barrierCount, clicksPerBarrier, onDone);
        }

        // 공통 미니게임 루프 — 두 오버로드가 파라미터만 다르게 넘기고 이걸 실행
        private IEnumerator RunMiniGameCore(float failSpeed, int barrierCount, int clicksPerBarrier, Action<ManualResult> onDone)
        {
            // 경계선 위치 계산 (게이지를 barrierCount +1 등분)
            float[] barrierThresholds = CalcBarrierThresholds(barrierCount);

            // 초기화 및 패널 표시
            ShowPanel(barrierCount, barrierThresholds);

            float fillValue = 0f;
            float failValue = 0f;
            float elapsed = 0f;
            int currentBarrier = 0;     // 다음에 넘어야 할 경계 인덱스

            _atBarrier = false;
            _clickCount = 0;
            _isRunning = true;
            _pressing = false;
            _currentClicksPerBarrier = clicksPerBarrier;

            ManualResult result = ManualResult.Fail;


            // 미니게임 루프 -----
            while (_isRunning)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // 실패 게이지는 미니게임 시작 후 _failStartDelay 이후부터 상승
                if (elapsed > _failStartDelay)
                    failValue += failSpeed * dt;

                // 실패 판정 (가득 차거나 초록을 따라잡으면 실패)
                if (elapsed > _failStartDelay && (failValue >= 1f || failValue >= fillValue))
                {
                    result = ManualResult.Fail;
                    break;
                }

                if (!_atBarrier)
                {
                    // 경계에 걸리지 않은 구간 - 누르면 오름, 떼면 내려감
                    if (_pressing)
                        fillValue += GetActualFillSpeed() * dt;
                    else
                        fillValue -= _drainSpeed * dt;
                    fillValue = Mathf.Clamp01(fillValue);

                    // 경계 도달 체크
                    if (currentBarrier < barrierThresholds.Length
                        && fillValue >= barrierThresholds[currentBarrier])
                    {
                        fillValue = barrierThresholds[currentBarrier];
                        _atBarrier = true;
                        _clickCount = 0;

                        // 카운트 컨테이너 스케일 리셋
                        if (_barrierClickContainer != null)
                            _barrierClickContainer.localScale = Vector3.one;

                        UpdateBarrierClickUI(clicksPerBarrier);

                        // 카운트 컨테이너 X를 경계선 마커 X와 동일하게
                        if (_barrierClickContainer != null && currentBarrier < _barrierMarkers.Length)
                        {
                            _barrierClickContainer.anchoredPosition = new Vector2(
                                _barrierMarkers[currentBarrier].anchoredPosition.x,
                                _barrierClickContainer.anchoredPosition.y   // Y는 고정
                            );
                        }

                        // 0628 추가, 경계에 걸리면 물고기의 몸부림 증가
                        _miniGameFishController?.SetStruggling(true);
                    }
                }
                else
                {
                    // 경계에서는 drain 없이 위치 고정
                    fillValue = barrierThresholds[currentBarrier];

                    // 경계 돌파 대기 중 - 클릭 수가 모이면 통과
                    if (_clickCount >= clicksPerBarrier)
                    {
                        // 돌파한 경계선 마커 숨기기
                        if (_barrierMarkers != null && currentBarrier < _barrierMarkers.Length)
                            StartCoroutine(BreakEffect(_barrierMarkers[currentBarrier]));

                        // 카운트 컨테이너도 같이 터지게
                        if (_barrierClickContainer != null)
                            StartCoroutine(BreakEffect(_barrierClickContainer));

                        _atBarrier = false;
                        currentBarrier++;
                        _clickCount = 0;
                        UpdateBarrierClickUI(0);

                        // 0628 추가, 경계 돌파 후 몸부림 해제
                        _miniGameFishController?.SetStruggling(false);  // 경계 돌파 후 몸부림 해제
                    }
                }

                // 성공 판정
                if (fillValue >= 1f)
                {
                    result = ManualResult.Success;
                    break;
                }

                UpdateGaugeUI(fillValue, failValue);
                yield return null;
            }

            // 결과 처리 -----
            _isRunning = false;
            _atBarrier = false;
            _pressing = false;

            ShowResult(result);
            yield return new WaitForSeconds(0.6f);  // 결과 잠깐 표시
            _miniGameFishController?.ResetPosition(); // 0628추가, 미니게임 종료 후 물고기 위치 초기화
            HidePanel();

            onDone?.Invoke(result);
        }


        // 입력 수신
        public void OnPressDown()
        {
            if (!_isRunning) return;
            _pressing = true;

            if (_atBarrier)
            {
                _clickCount++;
                UpdateBarrierClickUI(_currentClicksPerBarrier);  // ← 실시간 갱신
                StartCoroutine(BounceEffect(_barrierClickContainer));
            }
        }

        public void OnPressUp()
        {
            _pressing = false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            HidePanel();
            _isRunning = false;
            _atBarrier = false;
            _pressing = false;
        }

        private void Update()
        {
            if (!_isRunning) return;

            if (Mouse.current.leftButton.wasPressedThisFrame) OnPressDown();
            if (Mouse.current.leftButton.wasReleasedThisFrame) OnPressUp();
        }


        // 파라미터 계산 -----
        // [실패 게이지 속도: 기본값 + difficulty 가중]
        private float CalcFailSpeed(float difficulty)
        {
            return _failSpeedBase + (difficulty * _failSpeedDifficultyScale);
        }

        // [경계선 개수: Common=1, Rare=2, Epic=3, Legendary=4]
        private int CalcBarrierCount(FishRarity rarity)
        {
            return (int)rarity + 1;
        }

        // [경계선 위치를 게이지를 (barrierCount+1) 등분해서 반환]
        private float[] CalcBarrierThresholds(int barrierCount)
        {
            float[] thresholds = new float[barrierCount];
            float step = 1f / (barrierCount + 1);
            for (int i = 0; i < barrierCount; i++)
                thresholds[i] = step * (i + 1);
            return thresholds;
        }

        // 충전 속도
        private float GetActualFillSpeed()
        {
            float multiplier = Equipment.GetStat(EquipmentStat.GreenGaugeSpeed, 1f);
            return _fillSpeed * multiplier;
        }

        // 실패 게이지 속도
        private float GetActualFailSpeed(float difficulty)
        {
            float baseSpeed = CalcFailSpeed(difficulty);
            // 낚싯대 RedGaugeSpeed(배율, 1.0=영향없음 / 0.8=빨간게이지 80% 속도). JHS 장비 데이터 참조.
            return baseSpeed * Equipment.GetStat(EquipmentStat.RedGaugeSpeed, 1f);
        }


        // UI 제어 -----
        private void ShowPanel(int barrierCount, float[] thresholds)
        {
            if (_panel != null) _panel.SetActive(true);
            if (_multiButton != null) _multiButton.SetActive(false);
            if (_biteInteractButton != null) _biteInteractButton.SetActive(false);

            if (_fillGauge != null) _fillGauge.fillAmount = 0f;
            if (_failGauge != null) _failGauge.fillAmount = 0f;
            if (_resultText != null) _resultText.gameObject.SetActive(false);

            // 카운트 컨테이너 스케일 초기화
            if (_barrierClickContainer != null)
                _barrierClickContainer.localScale = Vector3.one;

            // 경계선 마커 스케일도 초기화 
            if (_barrierMarkers != null)
            {
                for (int i = 0; i < _barrierMarkers.Length; i++)
                {
                    if (_barrierMarkers[i] != null)
                        _barrierMarkers[i].localScale = Vector3.one;
                }
            }

            // 경계선 마커 활성화 및 위치 설정
            if (_barrierMarkers != null)
            {
                for (int i = 0; i < _barrierMarkers.Length; i++)
                {
                    if (_barrierMarkers[i] == null) continue;

                    bool active = i < barrierCount;
                    _barrierMarkers[i].gameObject.SetActive(active);

                    if (active)
                    {
                        // 게이지 왼쪽 끝 월드 좌표 기준으로 경계선 Y 계산 (Vertical Fill)
                        Vector3[] corners = new Vector3[4];
                        _fillGauge.rectTransform.GetWorldCorners(corners);
                        // corners[0] = 좌하단, corners[1] = 좌상단
                        float gaugeWorldBottom = corners[0].y;
                        float gaugeWorldTop = corners[1].y;
                        float gaugeWorldHeight = gaugeWorldTop - gaugeWorldBottom;

                        // 경계선 월드 Y 위치
                        float targetWorldY = gaugeWorldBottom + thresholds[i] * gaugeWorldHeight;

                        // 월드 좌표를 부모 로컬 좌표로 변환
                        RectTransform parentRect = _barrierMarkers[i].parent as RectTransform;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            parentRect,
                            RectTransformUtility.WorldToScreenPoint(
                                null, new Vector3(corners[0].x, targetWorldY, 0)),
                                null,
                                out Vector2 localPoint
                        );

                        _barrierMarkers[i].anchoredPosition = new Vector2(
                            _barrierMarkers[i].anchoredPosition.x,
                            localPoint.y
                        );
                    }
                }
            }

            UpdateBarrierClickUI(0);
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_biteInteractButton != null) _biteInteractButton.SetActive(true);

            // 보스 스테이지 중엔 멀티 버튼을 복구하지 않는다.
            bool isBossStage = _bossStageActivator != null && _bossStageActivator.IsBossStageActive;
            if (_multiButton != null) _multiButton.SetActive(!isBossStage);

            // 이중 안전장치: BossStageActivator가 보스 스테이지 중이면 강제로 다시 숨김
            _bossStageActivator?.ReapplyBossHide();
        }

        private void UpdateGaugeUI(float fill, float fail)
        {
            if (_fillGauge != null) _fillGauge.fillAmount = fill;
            if (_failGauge != null) _failGauge.fillAmount = fail;
        }

        private void UpdateBarrierClickUI(int required)
        {
            if (_barrierClickContainer == null) return;
            _barrierClickContainer.gameObject.SetActive(required > 0);
            if (_barrierClickText != null && required > 0)
            {
                int remaining = required - _clickCount;
                _barrierClickText.text = $"{remaining}";
            }
        }

        private void ShowResult(ManualResult result)
        {
            if (_resultText == null) return;
            _resultText.gameObject.SetActive(true);
            _resultText.text = result == ManualResult.Success ? "성공!" : "실패...";
        }

        private IEnumerator BreakEffect(RectTransform target)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;

            // 커지는 단계
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float scale = 1f + (t / 0.15f) * 0.2f;
                target.localScale = originalScale * scale;
                yield return null;
            }

            // 사라지는 단계
            t = 0f;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                float scale = 1.5f - (t / 0.1f) * 1.5f;
                target.localScale = originalScale * scale;
                yield return null;
            }

            target.gameObject.SetActive(false);
            target.localScale = originalScale;  // 스케일 복구
        }

        private IEnumerator BounceEffect(RectTransform target)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;

            // 커지는 단계
            float t = 0f;
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                float scale = 1f + (t / 0.08f) * 0.3f;  // 1.0 → 1.3
                target.localScale = originalScale * scale;
                yield return null;
            }

            // 돌아오는 단계
            t = 0f;
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                float scale = 1.3f - (t / 0.08f) * 0.3f;  // 1.3 → 1.0
                target.localScale = originalScale * scale;
                yield return null;
            }

            target.localScale = originalScale;
        }
    }
}
