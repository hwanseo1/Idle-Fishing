using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.UI
{
    // 보스 아이콘(Image) 색상을 짧게 바꿔 피격 느낌을 준다. 자동/수동 공통으로 항상 재생.
    public class BossHitFlash : MonoBehaviour
    {
        [SerializeField] private Image _bossIcon;
        [Tooltip("빨강 or 흰색")]
        [SerializeField] private Color _flashColor = Color.red;
        [SerializeField] private float _flashDuration = 0.12f;

        private Color _originalColor;
        private Coroutine _routine;

        private void Awake()
        {
            if (_bossIcon != null)
                _originalColor = _bossIcon.color;
        }

        public void Flash()
        {
            if (_bossIcon == null) return;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _bossIcon.color = _originalColor;
            }
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _bossIcon.color = _flashColor;
            yield return new WaitForSeconds(_flashDuration);
            _bossIcon.color = _originalColor;
            _routine = null;
        }
    }
}