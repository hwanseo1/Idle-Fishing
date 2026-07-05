using UnityEngine;

namespace RMS.Fishing
{
    public class DefensePredator : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;

        [Header("Visual")]
        [SerializeField] private RectTransform _visual;
        [SerializeField] private bool _spriteFacesRight = false;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _swimClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private float _swimSoundInterval = 1.5f;
        [SerializeField] private Vector2 _swimPitchRange = new Vector2(0.9f, 1.1f);

        [Header("Retreat")]
        [SerializeField] private float _retreatSpeed = 700f;
        [SerializeField] private float _retreatTime = 0.35f;

        private RectTransform _target;
        private float _moveSpeed;
        private bool _isRetreat;
        private float _retreatTimer;
        private Vector2 _retreatDirection;
        private float _swimSoundTimer;

        public RectTransform Rect => _rectTransform;

        public void Init(RectTransform target, float moveSpeed)
        {
            _target = target;
            _moveSpeed = moveSpeed;
            _isRetreat = false;
            _retreatTimer = 0f;
            _swimSoundTimer = Random.Range(0f, _swimSoundInterval);
        }

        private void Awake()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            if (_visual == null)
                _visual = _rectTransform;

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (_target == null)
                return;

            UpdateSwimSound();

            if (_isRetreat)
            {
                _rectTransform.anchoredPosition +=
                    _retreatDirection * _retreatSpeed * Time.deltaTime;

                UpdateFacing(_retreatDirection);

                _retreatTimer -= Time.deltaTime;

                if (_retreatTimer <= 0f)
                    _isRetreat = false;

                return;
            }

            Vector2 dir =
                (_target.anchoredPosition - _rectTransform.anchoredPosition).normalized;

            _rectTransform.anchoredPosition +=
                dir * _moveSpeed * Time.deltaTime;

            UpdateFacing(dir);
        }

        public void Retreat()
        {
            if (_target == null)
                return;

            PlayHitSound();

            _retreatDirection =
                (_rectTransform.anchoredPosition - _target.anchoredPosition).normalized;

            _retreatTimer = _retreatTime;
            _isRetreat = true;
        }

        private void UpdateSwimSound()
        {
            if (_isRetreat)
                return;

            if (_audioSource == null || _swimClip == null)
                return;

            _swimSoundTimer -= Time.deltaTime;

            if (_swimSoundTimer > 0f)
                return;

            _audioSource.pitch = Random.Range(_swimPitchRange.x, _swimPitchRange.y);
            _audioSource.PlayOneShot(_swimClip);

            _swimSoundTimer = _swimSoundInterval;
        }

        private void PlayHitSound()
        {
            if (_audioSource == null || _hitClip == null)
                return;

            _audioSource.pitch = 1f;
            _audioSource.PlayOneShot(_hitClip);
        }

        private void UpdateFacing(Vector2 dir)
        {
            if (_visual == null)
                return;

            if (Mathf.Abs(dir.x) < 0.01f)
                return;

            float xScale = Mathf.Abs(_visual.localScale.x);
            bool movingRight = dir.x > 0f;

            if (_spriteFacesRight)
                _visual.localScale = new Vector3(movingRight ? xScale : -xScale, _visual.localScale.y, _visual.localScale.z);
            else
                _visual.localScale = new Vector3(movingRight ? -xScale : xScale, _visual.localScale.y, _visual.localScale.z);
        }
    }
}