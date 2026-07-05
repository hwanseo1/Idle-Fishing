using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.UI
{
    // 보스 아이콘 위에 겹쳐진 Image에 슬래시 스프라이트를 재생한다.
    // 수동 낚시 성공으로 인한 피격일 때만 호출된다 (BossHitEffectController에서 분기).
    public class BossAttackEffect : MonoBehaviour
    {
        [SerializeField] private Image _image;

        [Tooltip("업로드한 슬래시 이미지들. 매번 랜덤으로 뽑아 변화를 준다.")]
        [SerializeField] private Sprite[] _slashSprites;

        [SerializeField] private float _duration = 0.25f;
        [SerializeField] private Vector3 _startScale = Vector3.one * 0.6f;
        [SerializeField] private Vector3 _endScale = Vector3.one * 1.2f;

        public void Play()
        {
            if (_image == null || _slashSprites == null || _slashSprites.Length == 0) return;

            gameObject.SetActive(true);
            _image.sprite = _slashSprites[Random.Range(0, _slashSprites.Length)];
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)); // 매번 다른 각도로 변화

            StopAllCoroutines();
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            Color c = _image.color;
            transform.localScale = _startScale;
            float t = 0f;

            while (t < _duration)
            {
                t += Time.deltaTime;
                float p = t / _duration;
                transform.localScale = Vector3.Lerp(_startScale, _endScale, p);
                _image.color = new Color(c.r, c.g, c.b, 1f - p);
                yield return null;
            }

            gameObject.SetActive(false);
            _image.color = c; // 알파 복구 (재사용 대비)
        }
    }
}