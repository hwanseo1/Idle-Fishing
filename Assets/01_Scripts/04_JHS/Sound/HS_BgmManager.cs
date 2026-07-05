using System.Collections;
using UnityEngine;

namespace JHS.Sound
{
    // 중앙 BGM 매니저. SFX(HS_SoundManager)와 분리된 별도 책임(SRP):
    // 루프 재생 + 크로스페이드 + 셔플(여러 곡 랜덤 순환)만 담당하고 게임 로직은 모른다.
    // 전역 접근은 HS_Bgm 정적 파사드 경유(매니저 없으면 Dummy 무음).
    // AudioSource 2개(A/B)를 번갈아 쓰며 곡 전환 시 서로 페이드해 끊김 없이 교체한다.
    [DisallowMultipleComponent]
    public class HS_BgmManager : MonoBehaviour, IHS_BgmService
    {
        [Header("BGM 믹스 볼륨(SFX 대비 배경음악 크기). 전역 볼륨은 AudioListener.volume가 따로 관리.")]
        [Range(0f, 1f)]
        [SerializeField] private float _bgmVolume = 0.7f;

        [Header("곡 전환 크로스페이드 시간(초)")]
        [SerializeField] private float _fadeSeconds = 1.2f;

        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _active;        // 지금 소리내는 소스
        private Coroutine _fadeRoutine;

        // 셔플(메인스테이지 3곡 랜덤 순환) 상태
        private AudioClip[] _shuffleClips;
        private bool _isShuffle;
        private int _lastShuffleIndex = -1; // 직전 곡 반복 회피용

        // 씬 전환(멀티↔메인)에도 BGM이 끊기지 않게 이 오브젝트(BGM_System)를 유지한다.
        // 같은 오브젝트의 StageBgmController도 함께 살아남아 멀티/보스 BGM을 계속 구동한다.
        private static HS_BgmManager _instance;

        private void Awake()
        {
            // "처음이 이긴다": 메인 재진입 등으로 새 BGM_System이 생기면 중복이므로 스스로 파괴(기존 유지).
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _a = CreateSource();
            _b = CreateSource();
            _active = _a;
        }

        private void OnEnable()
        {
            if (_instance != this) return;   // 파괴 예정 중복은 파사드에 등록하지 않음
            HS_Bgm.Register(this);
        }

        private void OnDisable()
        {
            if (_instance == this) HS_Bgm.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private AudioSource CreateSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // 2D
            src.loop = false;
            src.volume = 0f;
            return src;
        }

        // 단일곡 루프(보스/멀티 스테이지용). 이미 같은 곡이 루프 중이면 무시.
        public void PlayLoop(AudioClip clip)
        {
            if (clip == null) return;
            _isShuffle = false;
            _shuffleClips = null;
            if (_active != null && _active.isPlaying && _active.clip == clip && _active.loop) return;
            CrossfadeTo(clip, loop: true);
        }

        // 여러 곡 랜덤 순환(메인스테이지 3곡용). 한 곡이 끝나면 다른 곡을 랜덤으로 이어 재생한다.
        // 이미 같은 리스트로 셔플 중이면 무시(메인 재진입 시 곡이 튀지 않게).
        public void PlayShuffle(AudioClip[] clips)
        {
            var valid = Filter(clips);
            if (valid.Length == 0) return;
            if (_isShuffle && SameSet(_shuffleClips, valid)) return;

            _isShuffle = true;
            _shuffleClips = valid;
            _lastShuffleIndex = -1;
            PlayNextShuffle();
        }

        public void Stop()
        {
            _isShuffle = false;
            _shuffleClips = null;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutAll());
        }

        private void Update()
        {
            // 셔플 모드: 현재 곡이 끝나면(페이드 중이 아닐 때) 다음 곡을 랜덤으로.
            if (!_isShuffle || _fadeRoutine != null) return;
            if (_active != null && !_active.isPlaying) PlayNextShuffle();
        }

        private void PlayNextShuffle()
        {
            int idx = PickNextIndex();
            _lastShuffleIndex = idx;
            CrossfadeTo(_shuffleClips[idx], loop: false);
        }

        private int PickNextIndex()
        {
            int n = _shuffleClips.Length;
            if (n == 1) return 0;
            int idx;
            do { idx = Random.Range(0, n); } while (idx == _lastShuffleIndex);
            return idx;
        }

        // 놀고 있는(비활성) 소스에 새 곡을 얹어 페이드 인, 기존 소스는 페이드 아웃 후 정지.
        private void CrossfadeTo(AudioClip clip, bool loop)
        {
            var next = (_active == _a) ? _b : _a;
            next.clip = clip;
            next.loop = loop;
            next.volume = 0f;
            next.Play();

            var prev = _active;
            _active = next;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Crossfade(prev, next));
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float t = 0f;
            float startFrom = (from != null) ? from.volume : 0f;
            float dur = Mathf.Max(0.01f, _fadeSeconds);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                if (from != null) from.volume = Mathf.Lerp(startFrom, 0f, k);
                to.volume = Mathf.Lerp(0f, _bgmVolume, k);
                yield return null;
            }
            to.volume = _bgmVolume;
            if (from != null) { from.Stop(); from.clip = null; from.volume = 0f; }
            _fadeRoutine = null;
        }

        private IEnumerator FadeOutAll()
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, _fadeSeconds);
            float va = _a.volume, vb = _b.volume;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                _a.volume = Mathf.Lerp(va, 0f, k);
                _b.volume = Mathf.Lerp(vb, 0f, k);
                yield return null;
            }
            _a.Stop(); _a.clip = null; _a.volume = 0f;
            _b.Stop(); _b.clip = null; _b.volume = 0f;
            _fadeRoutine = null;
        }

        private static AudioClip[] Filter(AudioClip[] clips)
        {
            if (clips == null) return System.Array.Empty<AudioClip>();
            int count = 0;
            foreach (var c in clips) if (c != null) count++;
            var result = new AudioClip[count];
            int i = 0;
            foreach (var c in clips) if (c != null) result[i++] = c;
            return result;
        }

        private static bool SameSet(AudioClip[] a, AudioClip[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
