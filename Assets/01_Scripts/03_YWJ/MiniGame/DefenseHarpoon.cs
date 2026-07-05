using UnityEngine;

namespace RMS.Fishing
{
    public class DefenseHarpoon : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private float _lifeTime = 1.5f;

        private Vector2 _direction;
        private float _speed;
        private float _timer;

        public RectTransform Rect => _rectTransform;

        public void Init(Vector2 direction, float speed)
        {
            _direction = direction.normalized;
            _speed = speed;
            _timer = 0f;
        }

        private void Awake()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            _rectTransform.anchoredPosition += _direction * _speed * Time.deltaTime;

            _timer += Time.deltaTime;
            if (_timer >= _lifeTime)
                Destroy(gameObject);
        }
    }
}