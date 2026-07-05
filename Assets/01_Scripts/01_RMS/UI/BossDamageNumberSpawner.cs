using UnityEngine;


namespace RMS.UI
{
    // 보스 아이콘 위치를 기준으로 데미지 넘버 프리팹을 생성한다.
    public class BossDamageNumberSpawner : MonoBehaviour
    {
        [Tooltip("BossDamageNumber 프리팹")]
        [SerializeField] private BossDamageNumber _prefab;

        [Tooltip("생성 기준점. 보스 아이콘의 RectTransform.")]
        [SerializeField] private RectTransform _spawnAnchor;

        [Tooltip("연속 피격 시 겹치지 않도록 X축 랜덤 오프셋 범위")]
        [SerializeField] private float _randomXRange = 30f;

        public void Spawn(int damage)
        {
            if (_prefab == null || _spawnAnchor == null) return;

            BossDamageNumber popup = Instantiate(_prefab, _spawnAnchor.parent);
            RectTransform rt = (RectTransform)popup.transform;
            rt.anchoredPosition = _spawnAnchor.anchoredPosition
                + new Vector2(Random.Range(-_randomXRange, _randomXRange), 0f);

            popup.Play(damage);
        }
    }
}