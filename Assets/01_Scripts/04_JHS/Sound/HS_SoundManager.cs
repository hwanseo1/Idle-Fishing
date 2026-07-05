using System.Collections.Generic;
using UnityEngine;

namespace JHS.Sound
{
    // 중앙 효과음 매니저. 게임 로직은 전혀 모르고 id로만 재생 요청을 받는다(SRP).
    // 전역 접근은 HS_Sound 정적 파사드 경유(매니저 없으면 Dummy 무음).
    // 이름 충돌 방지로 HS_ 프리픽스 — 다른 팀원이 SoundManager를 따로 만들어도 안 부딪힌다.
    [DisallowMultipleComponent]
    public class HS_SoundManager : MonoBehaviour, IHS_SoundService
    {
        [System.Serializable]
        public struct ClipEntry
        {
            public HS_SoundId id;
            public AudioClip clip;                  // 비워두면 그 id는 무음(나중에 끼우기)
            [Range(0f, 1f)] public float volume;    // 0이면 1로 간주(미설정 기본 풀볼륨)
        }

        [Header("효과음 클립 (id → clip). 클립 비우면 그 id는 조용히 무시.")]
        [SerializeField] private ClipEntry[] _clips;

        [Header("동시 재생용 AudioSource 풀 크기(겹쳐 울릴 수 있게)")]
        [SerializeField] private int _poolSize = 6;

        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 1f;

        private readonly Dictionary<HS_SoundId, ClipEntry> _map = new();
        private AudioSource[] _pool;
        private int _next;

        private void Awake()
        {
            BuildMap();
            BuildPool();
        }

        private void OnEnable() => HS_Sound.Register(this);
        private void OnDisable() => HS_Sound.Unregister(this);

        private void BuildMap()
        {
            _map.Clear();
            if (_clips == null) return;
            foreach (var e in _clips)
                if (e.id != HS_SoundId.None) _map[e.id] = e;   // 같은 id 중복이면 마지막 항목 우선
        }

        private void BuildPool()
        {
            int n = Mathf.Max(1, _poolSize);
            _pool = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;   // 2D (UI/연출용)
                _pool[i] = src;
            }
        }

        public void Play(HS_SoundId id)
        {
            if (id == HS_SoundId.None) return;
            if (_pool == null) return;
            if (!_map.TryGetValue(id, out var e) || e.clip == null) return;  // 클립 미등록 = 무음

            float vol = _masterVolume * (e.volume <= 0f ? 1f : e.volume);
            NextSource().PlayOneShot(e.clip, vol);
        }

        // 라운드로빈으로 풀에서 다음 소스를 꺼내 쓴다(연타/동시발생 시 끊김 완화).
        private AudioSource NextSource()
        {
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            return src;
        }
    }
}
