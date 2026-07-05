using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JHS.Equipment;
using JHS.Sound;

namespace JHS.Fishing
{
    // 배 액티브 스킬 "발동 연출" 전담(표현 레이어). BoatActiveSkillController.OnSkillActivated만 구독한다 —
    // 낚시 로직/보상/데이터/버프 계산은 전혀 모른다. (FishCatchFlightView와 동일한 '연출 분리' 사상, TP-10)
    //
    //   진공 흡입기(지속) : 소용돌이 스프라이트 시트가 배 옆에서 회전 재생 + 물고기들이 빨려 들어옴 (duration 루프)
    //   야근 전등(지속)   : 빛무리 스틸이 켜져 맥동 + 반짝이가 피어오름 (duration 유지)
    //   유인 펭귄(즉발)   : 펭귄 시트(날갯짓)가 포물선으로 날아가 풍덩 → 물고기 몇 마리 배로 몰림 (1회)
    //
    // 좌표는 전부 월드 기준. 배가 출렁이면(BoatFloatingMotion) 연출도 같이 흔들려 자연스럽다(매 프레임 BoatPoint 재샘플).
    // 스프라이트 시트는 런타임 슬라이스(cols×rows), 렌더러는 풀로 재사용(GC 없음).
    // 시트/스틸 미배정 시 런타임 원형으로 폴백 — 아트 없이도 타이밍·위치 검증 가능.
    public class BoatSkillFxView : MonoBehaviour
    {
        [Header("연동 (비우면 자동 탐색)")]
        [SerializeField] private BoatActiveSkillController _controller;
        [SerializeField] private Transform _boat;
        [SerializeField] private Transform _seaOrigin;

        [Header("공통")]
        [SerializeField] private string _sortingLayer = "Default";
        [SerializeField] private int _sortingOrder = 60;
        [SerializeField] private Vector2 _anchorOffset = new Vector2(0f, -0.3f);
        [Tooltip("시트 애니 재생 속도(초당 프레임).")]
        [SerializeField] private float _framesPerSecond = 14f;

        [Header("① 진공 흡입기 (시트: 소용돌이 회전)")]
        [SerializeField] private Texture2D _vacuumSheet;
        [SerializeField] private int _vacuumCols = 6;
        [SerializeField] private int _vacuumRows = 3;
        [SerializeField] private Color _vacuumColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private float _vacuumWorldSize = 1.4f;
        [SerializeField] private float _vacuumSuckPerSec = 7f;
        [SerializeField] private float _suckRadius = 1.5f;
        [Tooltip("소용돌이 위치: 바다 기준점(SeaOrigin)에서의 오프셋(월드). 배 중앙이 아니라 물 위에 치게.")]
        [SerializeField] private Vector2 _vacuumOffset = new Vector2(0f, 0f);

        [Header("② 야근 전등 (시트: 성스러운 오로라 강림 · 검정→밝기알파)")]
        [SerializeField] private Texture2D _lampSheet;
        [SerializeField] private int _lampCols = 4;
        [SerializeField] private int _lampRows = 4;
        [SerializeField] private Color _lampColor = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("빛기둥 높이(월드). 하단이 갑판에 닿고 위로 솟는다.")]
        [SerializeField] private float _lampWorldSize = 3.4f;
        [Tooltip("배(갑판) 기준 빛기둥 착지 위치 오프셋(월드).")]
        [SerializeField] private Vector2 _lampOffset = new Vector2(0f, 0.2f);

        [Header("③ 유인 펭귄 (시트: 날갯짓)")]
        [SerializeField] private Texture2D _penguinSheet;
        [SerializeField] private int _penguinCols = 6;
        [SerializeField] private int _penguinRows = 4;
        [SerializeField] private Color _penguinColor = Color.white;
        [SerializeField] private float _penguinWorldSize = 1.6f;
        [SerializeField] private float _penguinFlightTime = 1.0f;
        [SerializeField] private float _penguinArc = 2.4f;
        [SerializeField] private int _penguinLureFish = 4;

        [Header("흡입/유인 물고기 (시트: 헤엄)")]
        [SerializeField] private Texture2D _fishSheet;
        [SerializeField] private int _fishCols = 4;
        [SerializeField] private int _fishRows = 4;
        [SerializeField] private float _fishWorldSize = 0.55f;

        [Header("착수 물보라 (시트 재활용, 예: FishingFX_SplashSheet)")]
        [SerializeField] private Texture2D _splashSheet;
        [SerializeField] private int _splashCols = 4;
        [SerializeField] private int _splashRows = 4;
        [SerializeField] private float _splashWorldSize = 1.4f;
        [SerializeField] private float _splashDuration = 0.35f;

        [Header("디버그")]
        [SerializeField] private bool _logToConsole = false;

        private Transform _poolRoot;
        private readonly Dictionary<string, Stack<SpriteRenderer>> _pools = new Dictionary<string, Stack<SpriteRenderer>>();
        private Sprite _fallbackCircle;
        private Sprite[] _vacuumFrames, _penguinFrames, _fishFrames, _splashFrames, _lampFrames;

        private Vector3 BoatPoint
        {
            get { return (_boat != null ? _boat.position : transform.position) + (Vector3)_anchorOffset; }
        }
        private Vector3 SeaPoint
        {
            get
            {
                return _seaOrigin != null ? _seaOrigin.position
                     : (_boat != null ? _boat.position : transform.position) + Vector3.down * 1.5f;
            }
        }
        // 진공 소용돌이가 치는 지점 — 배가 아니라 바다(SeaOrigin) 위. 흡입 물고기도 이 지점으로 빨려든다.
        private Vector3 VacuumPoint
        {
            get { return SeaPoint + (Vector3)_vacuumOffset; }
        }

        private void OnEnable()
        {
            if (_controller == null) _controller = FindFirstObjectByType<BoatActiveSkillController>();
            if (_boat == null)
            {
                var b = GameObject.Find("Boat");
                if (b != null) _boat = b.transform;
            }
            _vacuumFrames  = Slice(_vacuumSheet, _vacuumCols, _vacuumRows);
            _penguinFrames = Slice(_penguinSheet, _penguinCols, _penguinRows);
            _fishFrames    = Slice(_fishSheet, _fishCols, _fishRows);
            _splashFrames  = Slice(_splashSheet, _splashCols, _splashRows);
            _lampFrames    = Slice(_lampSheet, _lampCols, _lampRows, 0f);   // 하단 피벗: 빛기둥 바닥이 갑판에
            if (_controller != null) _controller.OnSkillActivated += HandleSkillActivated;
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.OnSkillActivated -= HandleSkillActivated;
            StopAllCoroutines();
        }

        private void HandleSkillActivated(BoatSkill skill, float value, float duration)
        {
            if (_logToConsole) Debug.Log("[BoatSkillFx] " + skill + " 연출 (value=" + value + ", dur=" + duration + ")");
            switch (skill)
            {
                case BoatSkill.VacuumSuction:
                    HS_Sound.Play(HS_SoundId.BoatSkillVacuum);
                    StartCoroutine(VacuumFx(Mathf.Max(0.2f, duration))); break;
                case BoatSkill.OvertimeLamp:
                    HS_Sound.Play(HS_SoundId.BoatSkillLamp);
                    StartCoroutine(LampFx(Mathf.Max(0.2f, duration)));   break;
                case BoatSkill.DecoyPenguin:
                    HS_Sound.Play(HS_SoundId.BoatSkillPenguinToss);   // 발동=던지는 순간 토스음
                    StartCoroutine(PenguinFx());                          break;
            }
        }

        // ── ① 진공 흡입기 ────────────────────────────────────────────────────────
        private IEnumerator VacuumFx(float duration)
        {
            SpriteRenderer swirl = Rent("swirl", _sortingOrder);
            float baseScale = ScaleFor(FrameOf(_vacuumFrames, 0), _vacuumWorldSize);
            float spawnAcc = 0f, t = 0f;
            while (t < duration)
            {
                swirl.transform.position = VacuumPoint;
                swirl.sprite = FrameAt(_vacuumFrames, t);
                float fade = FadeInOut(t, duration, 0.2f);
                float pulse = 1f + 0.06f * Mathf.Sin(t * 8f);
                swirl.transform.localScale = Vector3.one * baseScale * pulse;
                Color col = _vacuumColor; col.a *= fade; swirl.color = col;

                spawnAcc += _vacuumSuckPerSec * Time.deltaTime;
                while (spawnAcc >= 1f) { spawnAcc -= 1f; StartCoroutine(SuckOne()); }
                t += Time.deltaTime;
                yield return null;
            }
            Return("swirl", swirl);
        }

        private IEnumerator SuckOne()
        {
            SpriteRenderer sr = Rent("fish", _sortingOrder - 1);
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rad = _suckRadius * Random.Range(0.7f, 1.1f);
            float baseScale = ScaleFor(FrameOf(_fishFrames, 0), _fishWorldSize);
            float dur = Random.Range(0.4f, 0.7f);
            float t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                float r = Mathf.Lerp(rad, 0.05f, u);
                float a = ang + u * 6f;
                sr.transform.position = VacuumPoint + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f, 0f);
                sr.sprite = FrameAt(_fishFrames, t);
                sr.transform.localScale = Vector3.one * baseScale * (1f - 0.8f * u);
                Color col = Color.white; col.a = 1f - u * u; sr.color = col;
                t += Time.deltaTime;
                yield return null;
            }
            Return("fish", sr);
        }

        // ── ② 야근 전등: 성스러운 오로라 빛기둥이 갑판으로 강림(시트 재생, 하단 앵커) ──
        // 시트가 반짝이까지 담고 있어 별도 스파클 스폰 없음. 검정 배경은 밝기알파로 투명 처리됨.
        private IEnumerator LampFx(float duration)
        {
            SpriteRenderer glow = Rent("lampGlow", _sortingOrder + 3);   // 배 위로 얹히게 위쪽 정렬
            float baseScale = ScaleFor(FrameOf(_lampFrames, 0), _lampWorldSize);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                glow.transform.position = BoatPoint + (Vector3)_lampOffset;   // 빛기둥 하단이 갑판에
                glow.sprite = FrameAt(_lampFrames, t);
                float fade = FadeInOut(t, duration, 0.4f);                    // 은은하게 나타나고 사라짐
                float pulse = 1f + 0.04f * Mathf.Sin(t * 3f);                 // 잔잔한 호흡
                glow.transform.localScale = Vector3.one * baseScale * pulse;
                Color col = _lampColor; col.a *= fade; glow.color = col;
                yield return null;
            }
            Return("lampGlow", glow);
        }

        // ── ③ 유인 펭귄 ──────────────────────────────────────────────────────────
        // 짧으면 뭐가 지나갔는지 모르므로 3단계로 또렷하게:
        //  A. 등장 팝인(배 위에 톡 나타나 살짝 떠오름) → B. 느리고 높은 포물선+텀블 → C. 물속으로 쏙(페이드)+물보라
        private IEnumerator PenguinFx()
        {
            SpriteRenderer p = Rent("penguin", _sortingOrder + 2);
            p.color = _penguinColor;
            float baseScale = ScaleFor(FrameOf(_penguinFrames, 0), _penguinWorldSize);
            Vector3 from = BoatPoint;
            Vector3 to = SeaPoint + new Vector3(Random.Range(-1.2f, 1.2f), 0f, 0f);
            float animT = 0f;   // 프레임 애니용 누적(구간 넘어가도 연속 재생)

            // A. 등장 팝인 — "펭귄이다!"를 인지할 시간(0.28초, 오버슈트 팝)
            const float popDur = 0.28f;
            for (float tt = 0f; tt < popDur; tt += Time.deltaTime)
            {
                float u = tt / popDur;
                float s = u < 0.6f ? Mathf.Lerp(0f, 1.15f, u / 0.6f) : Mathf.Lerp(1.15f, 1f, (u - 0.6f) / 0.4f);
                p.transform.position = from + Vector3.up * (0.3f * u);
                p.transform.localScale = Vector3.one * baseScale * s;
                p.sprite = FrameAt(_penguinFrames, animT);
                animT += Time.deltaTime;
                yield return null;
            }

            // B. 포물선 비행 — 느리게 + 높게 + 완만한 텀블(한 바퀴 미만이라 형태가 읽힘)
            Vector3 launch = from + Vector3.up * 0.3f;
            for (float tt = 0f; tt < _penguinFlightTime; tt += Time.deltaTime)
            {
                float u = tt / _penguinFlightTime;
                Vector3 pos = Vector3.Lerp(launch, to, u);
                pos.y += _penguinArc * 4f * u * (1f - u);
                p.transform.position = pos;
                p.transform.rotation = Quaternion.Euler(0f, 0f, -300f * u);
                p.transform.localScale = Vector3.one * baseScale;
                p.sprite = FrameAt(_penguinFrames, animT);
                animT += Time.deltaTime;
                yield return null;
            }
            p.transform.rotation = Quaternion.identity;

            // C. 착수 — 물속으로 쏙 들어가듯 줄며 페이드
            const float diveDur = 0.18f;
            for (float tt = 0f; tt < diveDur; tt += Time.deltaTime)
            {
                float u = tt / diveDur;
                p.transform.position = to;
                p.transform.localScale = Vector3.one * baseScale * Mathf.Lerp(1f, 0.2f, u);
                Color c = _penguinColor; c.a = 1f - u; p.color = c;
                p.sprite = FrameAt(_penguinFrames, animT);
                animT += Time.deltaTime;
                yield return null;
            }
            Return("penguin", p);

            HS_Sound.Play(HS_SoundId.BoatSkillPenguinSplash);   // 착수 순간 물 splash음
            yield return PlaySplash(to);

            for (int i = 0; i < _penguinLureFish; i++)
                StartCoroutine(LureFishToBoat(to + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.2f, 0.2f), 0f),
                                              Random.Range(0f, 0.12f)));
        }

        private IEnumerator LureFishToBoat(Vector3 origin, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            SpriteRenderer sr = Rent("fish", _sortingOrder - 1);
            float baseScale = ScaleFor(FrameOf(_fishFrames, 0), _fishWorldSize * 1.15f);
            float dur = Random.Range(0.5f, 0.75f);
            float arc = Random.Range(0.8f, 1.4f);
            float t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                Vector3 p = Vector3.Lerp(origin, BoatPoint, u);
                p.y += arc * 4f * u * (1f - u);
                sr.transform.position = p;
                sr.sprite = FrameAt(_fishFrames, t);
                sr.transform.localScale = Vector3.one * baseScale * (u > 0.85f ? Mathf.Lerp(1f, 0.6f, (u - 0.85f) / 0.15f) : 1f);
                Color c = Color.white; c.a = u > 0.85f ? Mathf.Lerp(1f, 0f, (u - 0.85f) / 0.15f) : 1f; sr.color = c;
                t += Time.deltaTime;
                yield return null;
            }
            Return("fish", sr);
        }

        private IEnumerator PlaySplash(Vector3 at)
        {
            SpriteRenderer sr = Rent("splash", _sortingOrder - 2);
            sr.transform.position = at;
            sr.color = Color.white;
            if (_splashFrames != null && _splashFrames.Length > 0)
            {
                float baseScale = ScaleFor(_splashFrames[0], _splashWorldSize);
                sr.transform.localScale = Vector3.one * baseScale;
                float t = 0f;
                while (t < _splashDuration)
                {
                    sr.sprite = FrameAt(_splashFrames, t, _splashFrames.Length / Mathf.Max(0.01f, _splashDuration));
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                sr.sprite = FallbackCircle();
                float t = 0f;
                while (t < _splashDuration)
                {
                    float u = t / _splashDuration;
                    sr.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, _splashWorldSize, u);
                    Color c = new Color(0.8f, 0.92f, 1f, 1f - u); sr.color = c;
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            Return("splash", sr);
        }

        // ── 프레임/유틸 ──────────────────────────────────────────────────────────
        private Sprite FrameAt(Sprite[] frames, float t) { return FrameAt(frames, t, _framesPerSecond); }
        private Sprite FrameAt(Sprite[] frames, float t, float fps)
        {
            if (frames == null || frames.Length == 0) return FallbackCircle();
            int i = ((int)(t * fps)) % frames.Length;
            if (i < 0) i += frames.Length;
            return frames[i];
        }
        private Sprite FrameOf(Sprite[] frames, int i)
        {
            return (frames != null && frames.Length > 0) ? frames[Mathf.Clamp(i, 0, frames.Length - 1)] : FallbackCircle();
        }

        private static float FadeInOut(float t, float dur, float edge)
        {
            if (t < edge) return t / edge;
            if (t > dur - edge) return Mathf.Max(0f, (dur - t) / edge);
            return 1f;
        }

        // 스프라이트 월드 높이가 worldH가 되도록 하는 스케일.
        private float ScaleFor(Sprite sp, float worldH)
        {
            return sp == null ? worldH : worldH / Mathf.Max(0.01f, sp.bounds.size.y);
        }

        // 시트 텍스처를 cols×rows로 슬라이스(읽기 순서: 위 행부터 좌→우). 미배정이면 null.
        // pivotY: 0.5=중앙, 0=하단(빛기둥처럼 바닥 기준 앵커).
        private Sprite[] Slice(Texture2D tex, int cols, int rows, float pivotY = 0.5f)
        {
            if (tex == null) return null;
            cols = Mathf.Max(1, cols); rows = Mathf.Max(1, rows);
            float w = tex.width / (float)cols, h = tex.height / (float)rows;
            var frames = new Sprite[cols * rows];
            int idx = 0;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    float x = col * w;
                    float y = tex.height - (row + 1) * h;   // 텍스처 원점 좌하단 → 위 행부터
                    frames[idx++] = Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(0.5f, pivotY), 100f);
                }
            return frames;
        }

        // ── 풀 ───────────────────────────────────────────────────────────────────
        private Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject("BoatSkillFX_Pool");
                    go.transform.SetParent(transform, false);
                    _poolRoot = go.transform;
                }
                return _poolRoot;
            }
        }

        private SpriteRenderer Rent(string key, int order)
        {
            Stack<SpriteRenderer> stack;
            if (!_pools.TryGetValue(key, out stack)) { stack = new Stack<SpriteRenderer>(); _pools[key] = stack; }
            SpriteRenderer sr = stack.Count > 0 ? stack.Pop() : NewRenderer(key, order);
            sr.color = Color.white;
            sr.transform.rotation = Quaternion.identity;
            sr.enabled = true;
            return sr;
        }

        private void Return(string key, SpriteRenderer sr)
        {
            sr.enabled = false;
            Stack<SpriteRenderer> stack;
            if (!_pools.TryGetValue(key, out stack)) { stack = new Stack<SpriteRenderer>(); _pools[key] = stack; }
            stack.Push(sr);
        }

        private SpriteRenderer NewRenderer(string name, int order)
        {
            var go = new GameObject("BoatFX_" + name);
            go.transform.SetParent(PoolRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = _sortingLayer;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        private Sprite FallbackCircle()
        {
            if (_fallbackCircle != null) return _fallbackCircle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _fallbackCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _fallbackCircle;
        }
    }
}
