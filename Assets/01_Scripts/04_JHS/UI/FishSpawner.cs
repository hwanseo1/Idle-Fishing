using UnityEngine;

namespace JHS.UI
{
    // UI 물고기 "떼" 자동 생성기. 하나만 놓으면 N마리를 명시적 범위 안에 자동 배치·헤엄.
    // 범위는 단일 SwimmingFish와 동일하게 "명시적 값"(_xBounds/_yBounds)으로 지정 → 단일이 딱 맞던 느낌 그대로,
    // 마릿수만 늘린 형태. (RectTransform 크기에 의존하지 않아 어디 놓든 동일하게 동작)
    // 그림은 _sprites 풀에서 무작위 배정 → 물고기마다 이미지/프리팹 수동 추가 불필요.
    //
    // 사용: 미니게임 패널에 빈 UI 오브젝트 만들어 부착(보통 패널 중앙) → _fishPrefab=SwimmingFish,
    //       _sprites=등장 물고기들, _count=마릿수, _xBounds/_yBounds=헤엄 범위(부모 중앙 기준 px).
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class FishSpawner : MonoBehaviour
    {
        [Header("필수")]
        [SerializeField] private GameObject _fishPrefab;     // SwimmingFish 프리팹
        [SerializeField] private Sprite[] _sprites;          // 등장 물고기 그림 풀 (무작위 배정)

        [Header("떼")]
        [SerializeField] private int _count = 6;
        [SerializeField] private Vector2 _speedRange = new Vector2(70f, 150f);
        [SerializeField] private Vector2 _sizeRange = new Vector2(90f, 150f);

        [Header("헤엄 범위 (이 오브젝트 중앙 기준 px — 단일 프리팹과 동일 개념)")]
        [Tooltip("가로 왕복 범위 (min,max)")]
        [SerializeField] private Vector2 _xBounds = new Vector2(-400f, 400f);
        [Tooltip("세로 분포 범위 (min,max) — 중앙 밴드. 넓히면 위아래로 더 퍼짐")]
        [SerializeField] private Vector2 _yBounds = new Vector2(-180f, 180f);

        [Header("그림 방향 / 생성")]
        [SerializeField] private bool _spritesFaceRight = true;
        [SerializeField] private bool _spawnOnStart = true;

        private void Start() { if (_spawnOnStart) Spawn(); }

        public void Spawn()
        {
            if (_fishPrefab == null) { Debug.LogWarning("[FishSpawner] _fishPrefab 미지정"); return; }

            for (int i = 0; i < _count; i++)
            {
                var go = Instantiate(_fishPrefab, transform);
                var frt = (RectTransform)go.transform;

                float y = Random.Range(_yBounds.x, _yBounds.y);
                frt.anchoredPosition = new Vector2(Random.Range(_xBounds.x, _xBounds.y), y);

                float sz = Random.Range(_sizeRange.x, _sizeRange.y);
                frt.sizeDelta = new Vector2(sz, sz);

                if (_sprites != null && _sprites.Length > 0)
                {
                    var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
                    if (img != null) img.sprite = _sprites[Random.Range(0, _sprites.Length)];
                }

                var fs = go.GetComponent<FishSwim>();
                if (fs != null)
                    fs.Configure(_xBounds, y, Random.Range(_speedRange.x, _speedRange.y), _spritesFaceRight, Random.value > 0.5f);
            }
        }
    }
}
