using UnityEngine;
using UnityEngine.InputSystem;

namespace RMS.Fishing
{
    public class HarpoonShooterController : MonoBehaviour
    {
        [SerializeField] private RectTransform _rotateTarget;
        [SerializeField] private RectTransform _firePoint;
        [SerializeField] private RectTransform _playArea;

        [SerializeField] private float _angleOffset = 180f;

        private Vector2 _fireDirection = Vector2.right;

        private void Update()
        {
            RotateToMouse();
        }

        private void RotateToMouse()
        {
            if (_rotateTarget == null || _firePoint == null || _playArea == null)
                return;

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _playArea,
                mouseScreenPos,
                null,
                out Vector2 mouseLocalPos);

            Vector2 fireLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _playArea,
                RectTransformUtility.WorldToScreenPoint(null, _firePoint.position),
                null,
                out fireLocalPos);

            Vector2 dir = (mouseLocalPos - fireLocalPos).normalized;

            if (dir.sqrMagnitude <= 0.001f)
                return;

            _fireDirection = dir;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            angle = -angle; // 
            angle += _angleOffset;

            _rotateTarget.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public Vector2 GetFireDirection()
        {
            return _fireDirection;
        }

        public Vector3 GetFireWorldPosition()
        {
            return _firePoint.position;
        }
    }
}