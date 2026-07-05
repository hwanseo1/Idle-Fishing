using UnityEngine;
using DG.Tweening;

namespace JHS.Equipment
{
    // 메인화면 "장착한 배"가 파도에 둥실대는 통짜 둥둥 연출 전담.
    // 자기 Transform만 흔든다 — 어떤 배인지(BoatView)·선원이 얹혔는지 전혀 모름(SRP).
    // 이 오브젝트(배 루트)를 흔들므로, 자식으로 붙는 선원/VFX는 둥둥을 그대로 물려받는다(파도에 한 덩어리로 출렁).
    // 켜질 때 시작·꺼질 때 정지·복원 → 스테이지 게이팅(활성/비활성)에 자연히 맞물려 추가 토글이 필요 없다.
    // 진폭/주기/각도는 인스펙터 노출 — 나중에 선원·부위 애니가 얹혀 과해지면 여기서 줄인다.
    [DisallowMultipleComponent]
    public class BoatFloatingMotion : MonoBehaviour
    {
        [Header("상하 bobbing")]
        [Tooltip("위아래 진폭(월드 유닛). 중앙(시작 위치) 기준 대칭으로 ±이 값만큼.")]
        [SerializeField] private float _bobAmplitude = 0.12f;
        [Tooltip("한 번 오르내리는 데 걸리는 시간(초).")]
        [SerializeField] private float _bobPeriod = 2.0f;

        [Header("좌우 기울기 rocking")]
        [Tooltip("기울기 각도(도). 중앙 기준 ±이 값.")]
        [SerializeField] private float _rockAngle = 2.5f;
        [Tooltip("기울기 한 주기(초). bobPeriod와 다르게 둬야 기계적이지 않은 물결이 된다.")]
        [SerializeField] private float _rockPeriod = 2.6f;

        // 흔들림의 중앙 기준(켜질 때 한 번 잡고, 트윈이 이 주위를 왕복).
        private Vector3 _baseLocalPos;
        private Vector3 _baseLocalEuler;
        private bool _captured;

        private Tween _bobTween;
        private Tween _rockTween;

        private void Awake() => CaptureBase();

        private void OnEnable()
        {
            // 게이팅으로 처음부터 비활성이었다면 Awake가 안 불렸을 수 있어 여기서 보장(최초 1회).
            if (!_captured) CaptureBase();
            StartFloating();
        }

        private void OnDisable() => StopFloating();

        private void CaptureBase()
        {
            _baseLocalPos = transform.localPosition;
            _baseLocalEuler = transform.localEulerAngles;
            _captured = true;
        }

        private void StartFloating()
        {
            StopFloating();

            // 중앙(_base)을 기준으로 대칭 왕복하도록, 시작점을 한쪽 끝에 놓고 반대 끝으로 Yoyo.
            transform.localPosition = _baseLocalPos + Vector3.down * _bobAmplitude;
            transform.localEulerAngles = _baseLocalEuler + new Vector3(0f, 0f, -_rockAngle);

            // SetLink: 오브젝트 파괴 시 트윈 자동 Kill(누수 방지). OnDisable의 Kill과 이중 안전망.
            _bobTween = transform
                .DOLocalMoveY(_baseLocalPos.y + _bobAmplitude, _bobPeriod)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);

            _rockTween = transform
                .DOLocalRotate(_baseLocalEuler + new Vector3(0f, 0f, _rockAngle), _rockPeriod, RotateMode.Fast)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        private void StopFloating()
        {
            _bobTween?.Kill();
            _rockTween?.Kill();
            _bobTween = null;
            _rockTween = null;

            // 흔들다 멈춘 위치를 중앙으로 되돌림 — 다음 켜짐·선원 정합이 어긋나지 않게.
            if (_captured)
            {
                transform.localPosition = _baseLocalPos;
                transform.localEulerAngles = _baseLocalEuler;
            }
        }
    }
}
