using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using JHS.Fishing;


namespace RMS.UI
{
    // 입질(Bite) 상태일 때만 활성화되는 수동 낚시 트리거 버튼.

    public class BiteInteractButton : MonoBehaviour
    {
        [Header("연동")]
        [Tooltip("AutoFishingController 참조. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private AutoFishingController _controller;

        [Header("UI")]
        [Tooltip("활성화/비활성화할 Button 컴포넌트. 비워두면 이 오브젝트에서 자동 탐색.")]
        [SerializeField] private Button _button;

        [Header("반짝임 효과")]
        [Tooltip("펄스/컬러 대상 Graphic. 비워두면 Button의 targetGraphic 사용.")]
        [SerializeField] private Graphic _pulseTarget;
        [SerializeField] private float _pulseScale = 1.15f;
        [SerializeField] private float _pulseSpeed = 6f;
        [SerializeField] private Color _glowColor = new Color(1f, 0.95f, 0.2f); // 레몬색
        [Range(0f, 1f)]
        [SerializeField] private float _glowIntensity = 0.8f;

        private Coroutine _pulseRoutine;
        private Vector3 _originalScale;
        private Color _originalColor;


        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_pulseTarget == null) _pulseTarget = _button.targetGraphic;

            _originalScale = transform.localScale;
            if (_pulseTarget != null) _originalColor = _pulseTarget.color;

            _button.interactable = false;
            _button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<AutoFishingController>();
            
            if (_controller != null)
                _controller.OnPhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnPhaseChanged -= OnPhaseChanged;

            StopPulse();
        }

        private void OnPhaseChanged(FishingPhase phase)
        {
            bool isBite = (phase == FishingPhase.Bite);

            if (_button != null)
                _button.interactable = isBite;

            if (isBite) StartPulse();
            else StopPulse();
        }

        private void OnClicked()
        {
            _controller?.RequestInteraction();
            if (_button != null)
                _button.interactable = false;

            StopPulse();
        }

        private void StartPulse()
        {
            if (_pulseRoutine != null) return;
            _pulseRoutine = StartCoroutine(PulseLoop());
        }

        private void StopPulse()
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            transform.localScale = _originalScale;
            if (_pulseTarget != null)
                _pulseTarget.color = _originalColor;
        }

        private IEnumerator PulseLoop()
        {
            float scaleAmplitude = (_pulseScale - 1f) * 0.5f;

            while (true)
            {
                // 0~1을 오가는 사인파 (같은 위상으로 스케일/색상 동기화)
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed);

                float scale = 1f + scaleAmplitude * wave * 2f;
                transform.localScale = _originalScale * scale;

                if (_pulseTarget != null)
                    _pulseTarget.color = Color.Lerp(_originalColor, _glowColor, wave * _glowIntensity);

                yield return null;
            }
        }
    }
}

