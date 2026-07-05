using UnityEngine;

namespace JHS.UI
{
    // UI(Canvas) 물고기가 좌우로 헤엄쳐 다니게 하는 이동 전담 스크립트.
    //  - anchoredPosition.x 를 좌우로 왕복(끝에서 방향전환) + y 사인 살랑
    //  - 방향에 맞춰 좌우 뒤집기(localScale.x 부호)
    // 몸 "꿈틀"(회전 wiggle)은 Animator 클립이 따로 담당 → 이 스크립트는 회전/스케일.y를 건드리지 않아 충돌 없음.
    //  (Animator=회전.z, 이 스크립트=anchoredPosition + scale.x 부호)
    // WCE 물고기는 그림 1장(프레임 없음)이라 헤엄=이동+꿈틀로 표현. 미니게임 UI창 등에 프리팹으로 배치.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class FishSwim : MonoBehaviour
    {
        [Header("헤엄 속도/범위")]
        [Tooltip("가로 헤엄 속도 (px/초, UI 기준)")]
        [SerializeField] private float _speed = 120f;
        [Tooltip("왕복할 가로 범위 (anchoredPosition.x 의 min/max)")]
        [SerializeField] private Vector2 _xBounds = new Vector2(-400f, 400f);

        [Header("위아래 살랑")]
        [SerializeField] private float _bobAmplitude = 20f;   // px
        [SerializeField] private float _bobPeriod = 2.2f;     // 초

        [Header("방향")]
        [Tooltip("원본 물고기 그림이 '오른쪽'을 보고 있으면 체크. 헤엄 방향에 맞춰 자동 뒤집기.")]
        [SerializeField] private bool _spriteFacesRight = true;
        [Tooltip("시작 방향: 체크=오른쪽으로 출발")]
        [SerializeField] private bool _startMovingRight = true;

        private RectTransform _rt;
        private float _dir;        // +1 오른쪽 / -1 왼쪽
        private float _baseY;
        private float _t;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _baseY = _rt.anchoredPosition.y;
            _dir = _startMovingRight ? 1f : -1f;
            _t = transform.GetSiblingIndex() * 0.37f;   // 여러 마리면 위상 분산(살랑 타이밍 다르게)
        }

        private void OnEnable() => ApplyFacing();

        // 스포너 등에서 한 번에 설정(범위/기준높이/속도/방향). Instantiate 직후 호출.
        public void Configure(Vector2 xBounds, float baseY, float speed, bool spriteFacesRight, bool startMovingRight)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _xBounds = xBounds;
            _baseY = baseY;
            _speed = speed;
            _spriteFacesRight = spriteFacesRight;
            _startMovingRight = startMovingRight;
            _dir = startMovingRight ? 1f : -1f;
            ApplyFacing();
        }

        private void Update()
        {
            var p = _rt.anchoredPosition;

            p.x += _dir * _speed * Time.deltaTime;
            if (p.x >= _xBounds.y) { p.x = _xBounds.y; _dir = -1f; ApplyFacing(); }
            else if (p.x <= _xBounds.x) { p.x = _xBounds.x; _dir = 1f; ApplyFacing(); }

            _t += Time.deltaTime;
            p.y = _baseY + Mathf.Sin(_t * Mathf.PI * 2f / Mathf.Max(0.01f, _bobPeriod)) * _bobAmplitude;

            _rt.anchoredPosition = p;
        }

        // 헤엄 방향에 맞춰 좌우 뒤집기. (Animator는 scale을 안 건드리므로 안전)
        private void ApplyFacing()
        {
            bool faceRight = _dir > 0f;
            float sign = (faceRight == _spriteFacesRight) ? 1f : -1f;
            var s = _rt.localScale;
            s.x = Mathf.Abs(s.x) * sign;
            _rt.localScale = s;
        }
    }
}
