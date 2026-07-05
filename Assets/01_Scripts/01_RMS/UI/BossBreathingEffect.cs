using UnityEngine;

namespace RMS.UI
{
    // 보스 아이콘(Image)의 RectTransform 스케일을 사인파로 pulse시켜
    // "숨쉬는" 느낌을 준다. 셰이더/Material은 전혀 건드리지 않는다.
    public class BossBreathingEffect : MonoBehaviour
    {
        [SerializeField] private float _amplitude = 0.04f;
        [SerializeField] private float _speed = 2f;
        [Tooltip("X축 진폭 비율 (1이면 XY 동일, 낮을수록 세로로만 부푸는 느낌)")]
        [SerializeField] private float _asymmetry = 0.5f;
        [SerializeField] private float _phaseRandomRange = 10f;

        private RectTransform _rt;
        private Vector3 _baseScale;
        private float _phaseOffset;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
        }

        private void OnEnable()
        {
            _phaseOffset = Random.Range(0f, _phaseRandomRange);
        }

        private void Update()
        {
            float t = (Time.time + _phaseOffset) * _speed;
            float wave = Mathf.Sin(t);

            float scaleY = 1f + wave * _amplitude;
            float scaleX = 1f + wave * _amplitude * _asymmetry;

            _rt.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, _baseScale.z);
        }

        private void OnDisable()
        {
            if (_rt != null) _rt.localScale = _baseScale;
        }
    }
}