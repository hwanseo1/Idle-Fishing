using UnityEngine;
using UnityEngine.EventSystems;

namespace JHS.UI
{
    // 누를 때 살짝 축소 → 떼면 복귀. 클릭 피드백용.
    // Color Tint(곱연산)는 어두운/검정 버튼에선 표가 안 나서, 색이 아니라 "크기"로 눌림을 보여준다.
    // 아무 UI 오브젝트(Button 등)에 붙이면 동작 — 색·스프라이트와 무관. (표현만, 클릭 로직은 Button.onClick 그대로)
    [DisallowMultipleComponent]
    public sealed class PressScaleFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.92f;  // 눌렀을 때 배율
        [SerializeField] private RectTransform _target;  // 스케일 대상 (비우면 자기 자신)

        private Vector3 _origin = Vector3.one;
        private bool _captured;

        private RectTransform T => _target != null ? _target : (RectTransform)transform;

        private void Awake()
        {
            if (!_captured) { _origin = T.localScale; _captured = true; }   // 원본 스케일 1회 캡처
        }

        // 비활성/재활성 시 눌림 상태로 남는 것 방지 (예: 화면 닫힐 때 PointerUp 못 받는 경우)
        private void OnDisable() => Restore();

        public void OnPointerDown(PointerEventData eventData) => T.localScale = _origin * _pressedScale;

        public void OnPointerUp(PointerEventData eventData) => Restore();

        private void Restore() => T.localScale = _origin;
    }
}
