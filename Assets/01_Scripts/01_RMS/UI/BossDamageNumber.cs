using System.Collections;
using TMPro;
using UnityEngine;


namespace RMS.UI
{
    // 데미지 숫자 1개의 상승+페이드 애니메이션. BossDamageNumberSpawner가 생성한다.
    public class BossDamageNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _riseDistance = 80f;
        [SerializeField] private float _duration = 0.6f;

        public void Play(int damage)
        {
            _text.text = damage.ToString();
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            RectTransform rt = (RectTransform)transform;
            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + Vector2.up * _riseDistance;
            Color c = _text.color;
            float t = 0f;

            while (t < _duration)
            {
                t += Time.deltaTime;
                float p = t / _duration;
                rt.anchoredPosition = Vector2.Lerp(start, end, p);
                _text.color = new Color(c.r, c.g, c.b, 1f - p);
                yield return null;
            }

            Destroy(gameObject); // 피격이 잦으면 오브젝트 풀링으로 교체 고려
        }
    }
}