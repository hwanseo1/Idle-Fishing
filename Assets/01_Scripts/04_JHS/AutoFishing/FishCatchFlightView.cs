using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RMS.Data;   // FishData

namespace JHS.Fishing
{
    // 잡힘 "연출" 전담(표현 레이어). AutoFishingController의 신호만 구독한다 — 낚시 로직/보상/데이터는 전혀 모른다.
    // (FishingView·BiteIndicator와 같은 "연출 분리" 사상. 차이: 그쪽은 Animator/UI, 이쪽은 월드 스프라이트)
    //
    //  Waiting/Bite → 바다 표면 한 지점에 입질 예고: 물결 링이 작게→크게 퍼지며 페이드(루프)
    //  OnFishCaught → 그 지점에서 물보라(스프라이트 시트)가 팡 터지고, 잡힌 물고기 아이콘이 솟아 배까지 포물선 비행
    //
    //  좌표는 전부 월드 기준. 출발점(SeaOrigin)·도착점(Boat)을 Transform 참조로 빼둔 이유:
    //   - 배가 출렁이면(BoatFloatingMotion) 도착점도 같이 흔들려 자동으로 자연스러움
    //   - 나중에 바다 출렁임: SeaOrigin을 파도 오브젝트 자식으로 옮기면 끝
    //   - 나중에 낚싯대 단계: 도착점을 배 → 선원 RodTip으로 교체 + LineRenderer 추가, 입질 물결 자리에 찌 복귀
    //  스프라이트는 풀로 재사용(연속 포획에도 GC 없음). 아트 미배정 시 런타임 원형으로 폴백.
    public class FishCatchFlightView : MonoBehaviour
    {
        [Header("연동 (비우면 씬에서 자동 탐색)")]
        [SerializeField] private AutoFishingController _controller;

        [Header("좌표 (월드)")]
        [Tooltip("물고기가 솟아오르고 물결이 이는 바다 표면 지점. 비우면 이 오브젝트 위치를 사용.")]
        [SerializeField] private Transform _seaOrigin;
        [Tooltip("물고기가 날아가 닿는 도착점(배). 비우면 'Boat' 이름으로 자동 탐색.")]
        [SerializeField] private Transform _boatTarget;
        [Tooltip("매 포획마다 출발점에 더하는 랜덤 폭(x=좌우, y=상하). 같은 자리만 반복되지 않게.")]
        [SerializeField] private Vector2 _originJitter = new Vector2(1.6f, 0.25f);

        [Header("다중/튀어오름 (겹침 방지 + 수동 추가 연출)")]
        [Tooltip("여러 마리 동시 발동 시 출발 시차(초). 마리마다 0~이 값 랜덤 지연 → 줄줄이 날아오름(겹침 방지).")]
        [SerializeField] private float _launchStagger = 0.15f;
        [Tooltip("여러 마리 출발점 좌우 흩뿌림 폭. 겹치지 않게 벌린다.")]
        [SerializeField] private float _multiSpread = 0.6f;
        [Tooltip("수동 낚시 성공 시 추가로 튀어올라 배로 날아가는 장식 물고기 수(보상 무관).")]
        [SerializeField] private int _manualExtraFish = 5;
        [Tooltip("수동 추가 물고기의 스프라이트 풀(마리마다 랜덤). 비우면 잡은 물고기 아이콘 사용.")]
        [SerializeField] private Sprite[] _decoFishSprites;

        [Header("비행 튜닝")]
        [SerializeField] private float _flightDuration = 0.75f;
        [Tooltip("포물선 정점 높이(월드 단위).")]
        [SerializeField] private float _arcHeight = 2f;
        [Tooltip("수면 아래에서 솟아오르는 깊이.")]
        [SerializeField] private float _riseDepth = 0.7f;
        [Tooltip("비행 중 총 회전량(도).")]
        [SerializeField] private float _spinDegrees = 540f;
        [Tooltip("날아가는 물고기의 목표 높이(월드 단위). 아이콘 픽셀크기가 32/64로 달라도 이 높이로 정규화.")]
        [SerializeField] private float _fishTargetHeight = 1.1f;
        [SerializeField] private string _sortingLayer = "Default";
        [SerializeField] private int _sortingOrder = 50;

        [Header("입질 물결 (Waiting/Bite)")]
        [SerializeField] private bool _showBiteCue = true;
        [Tooltip("물결 링 스프라이트(미배정 시 런타임 원형 폴백).")]
        [SerializeField] private Sprite _rippleSprite;
        [SerializeField] private Color _rippleColor = new Color(0.6f, 0.9f, 1f, 0.9f);
        [Tooltip("새 물결 링을 내보내는 간격(초). 작을수록 촘촘.")]
        [SerializeField] private float _rippleInterval = 0.55f;
        [Tooltip("물결 링 1개가 퍼졌다 사라지는 시간.")]
        [SerializeField] private float _rippleLife = 1.1f;
        [Tooltip("물결 링 시작/끝 스케일(월드). 작게→크게 퍼짐.")]
        [SerializeField] private float _rippleStartScale = 0.05f;
        [SerializeField] private float _rippleEndScale = 0.28f;

        [Header("물보라 (OnFishCaught)")]
        [SerializeField] private bool _showSplash = true;
        [Tooltip("물보라 스프라이트 시트 텍스처(미배정 시 원형 폴백). 격자는 아래 cols×rows.")]
        [SerializeField] private Texture2D _splashSheet;
        [SerializeField] private int _splashCols = 4;
        [SerializeField] private int _splashRows = 4;
        [SerializeField] private float _splashDuration = 0.35f;
        [SerializeField] private float _splashScale = 0.7f;
        [SerializeField] private Color _splashTint = Color.white;

        [Header("전설/에픽 연출 (Legendary=금색 · Epic=보라색)")]
        [Tooltip("전설·에픽 포획 시 추가 연출(오라+반짝이+배너) on/off. 크기·반짝이수는 등급 공유.")]
        [SerializeField] private bool _legendaryFlair = true;
        [Tooltip("날아가는 물고기 뒤에 깔리는 금빛 오라 색.")]
        [SerializeField] private Color _legendaryGlowColor = new Color(1f, 0.82f, 0.3f, 0.85f);
        [Tooltip("오라 크기 = 물고기 크기 × 이 값.")]
        [SerializeField] private float _legendaryGlowScale = 2.4f;
        [Tooltip("입질~당기기 동안 물 위에 깔리는 노란 오라 크기(월드 단위).")]
        [SerializeField] private float _legendaryBiteGlowScale = 1.3f;
        [Tooltip("솟아오를 때 터지는 반짝이 개수.")]
        [SerializeField] private int _legendarySparkles = 12;
        [SerializeField] private Color _legendarySparkleColor = new Color(1f, 0.95f, 0.6f, 1f);
        [Tooltip("선택: '전설!' 배너 오브젝트(평소 비활성). 비우면 배너 생략(오라/반짝이만).")]
        [SerializeField] private GameObject _legendaryBanner;
        [Tooltip("배너 표시 시간(초). 팝업 후 이 시간 뒤 사라짐. (전설·에픽 공유)")]
        [SerializeField] private float _legendaryBannerTime = 1.3f;

        [Header("에픽 색/배너 (Epic 등급 — 크기·반짝이수는 전설과 공유)")]
        [Tooltip("에픽 오라 색(보라).")]
        [SerializeField] private Color _epicGlowColor = new Color(0.72f, 0.36f, 1f, 0.85f);
        [Tooltip("에픽 반짝이 색(연보라).")]
        [SerializeField] private Color _epicSparkleColor = new Color(0.85f, 0.62f, 1f, 1f);
        [Tooltip("선택: '에픽!' 배너 오브젝트(평소 비활성).")]
        [SerializeField] private GameObject _epicBanner;

        // 이번 사이클의 솟음 지점(Waiting에서 확정 → 물결/물고기가 같은 자리에서 나오게 인과 일치).
        private Vector3 _currentOrigin;
        private bool _originPicked;
        private int _lastManualBurstFrame = -1;   // 수동 추가 튀어오름 중복 방지(다중포획=같은 프레임 N회 호출)

        private Coroutine _rippleRoutine;
        private Sprite[] _splashFrames;          // 시트에서 런타임 슬라이스
        private Sprite _fallbackCircle;          // 아트 미배정 시 폴백 원형

        private readonly Stack<SpriteRenderer> _fishPool = new Stack<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _splashPool = new Stack<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _ringPool = new Stack<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _glowPool = new Stack<SpriteRenderer>();      // 전설 오라
        private readonly Stack<SpriteRenderer> _sparklePool = new Stack<SpriteRenderer>();   // 전설 반짝이
        private Transform _poolRoot;
        private Coroutine _bannerRoutine;
        private FlairTier _hookedTier;                 // 이번 입질의 연출 등급(OnFishHooked로 갱신)
        private Coroutine _biteGlowRoutine;            // 입질~당기기 노란 오라 루프
        private SpriteRenderer _biteGlow;              // 그 오라 렌더러(빌려와 들고 있음)

        private void OnEnable()
        {
            if (_controller == null) _controller = FindFirstObjectByType<AutoFishingController>();
            if (_boatTarget == null)
            {
                var boat = GameObject.Find("Boat");
                if (boat != null) _boatTarget = boat.transform;
            }
            BuildSplashFrames();
            if (_controller != null)
            {
                _controller.OnPhaseChanged += HandlePhase;
                _controller.OnFishCaught   += HandleFishCaught;
                _controller.OnFishHooked   += HandleHooked;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnPhaseChanged -= HandlePhase;
                _controller.OnFishCaught   -= HandleFishCaught;
                _controller.OnFishHooked   -= HandleHooked;
            }
            StopRipples();
            StopBiteGlow();
        }

        // 연출 등급: 전설/에픽만 추가 연출 대상. 색·배너만 등급별로 다르고 크기/반짝이수는 공유.
        private enum FlairTier { None, Epic, Legendary }

        private FlairTier TierOf(FishData fish)
        {
            if (!_legendaryFlair || fish == null) return FlairTier.None;
            if (fish.Rarity == FishRarity.Legendary) return FlairTier.Legendary;
            if (fish.Rarity == FishRarity.Epic) return FlairTier.Epic;
            return FlairTier.None;
        }
        private Color GlowColorFor(FlairTier t) => t == FlairTier.Epic ? _epicGlowColor : _legendaryGlowColor;
        private Color SparkleColorFor(FlairTier t) => t == FlairTier.Epic ? _epicSparkleColor : _legendarySparkleColor;
        private GameObject BannerFor(FlairTier t) => t == FlairTier.Epic ? _epicBanner : _legendaryBanner;

        // 입질 순간 등급 통지 — 전설/에픽이면 입질~당기기 오라를 예약(실제 시작은 Bite 단계에서).
        private void HandleHooked(FishData fish)
        {
            _hookedTier = TierOf(fish);
        }

        private Vector3 SeaPoint => _seaOrigin != null ? _seaOrigin.position : transform.position;
        private Vector3 BoatPoint => _boatTarget != null ? _boatTarget.position : transform.position;

        // ── 단계 신호 → 입질 물결 + 전설 오라 ───────────────────────────────────
        private void HandlePhase(FishingPhase phase)
        {
            switch (phase)
            {
                case FishingPhase.Waiting:
                    // 이번 사이클의 솟음 지점을 확정하고 물결을 깐다(물고기도 같은 자리에서 솟음).
                    _currentOrigin = SeaPoint + new Vector3(
                        Random.Range(-_originJitter.x, _originJitter.x),
                        Random.Range(-_originJitter.y, _originJitter.y), 0f);
                    _originPicked = true;
                    if (_showBiteCue) StartRipples();
                    break;
                case FishingPhase.Bite:
                    if (_showBiteCue) StartCoroutine(OneRipple(_currentOrigin));   // 입질 — 즉발 물결 한 번 더
                    // 전설/에픽: 입질 지점에 오라를 깔기 시작(당기기 동안 유지, 포획/실패에 정리).
                    if (_hookedTier != FlairTier.None) StartBiteGlow();
                    break;
                case FishingPhase.Reeling:
                    StopRipples();        // 물결만 정리 — 전설 오라는 당기는 내내 유지
                    break;
                default:
                    StopRipples();        // Success/Fail/Idle/Casting — 물결 + 입질오라 모두 정리
                    StopBiteGlow();       // (포획이면 OnFishCaught의 '비행 오라'가 이어받음)
                    break;
            }
        }

        // ── 포획 → 물보라 + 비행 ───────────────────────────────────────────────
        private void HandleFishCaught(FishData fish, float contribution, bool isManual)
        {
            if (fish == null || fish.Icon == null) return;   // 보스/미연결 등 아이콘 없는 경우 스킵

            Vector3 baseOrigin = _originPicked ? _currentOrigin
                : SeaPoint + new Vector3(Random.Range(-_originJitter.x, _originJitter.x), 0f, 0f);
            // 다중포획으로 같은 프레임에 여러 마리면 겹치지 않게 흩뿌린다(시차는 FlyFish가 담당).
            Vector3 origin = baseOrigin + new Vector3(Random.Range(-_multiSpread, _multiSpread), Random.Range(-0.1f, 0.1f), 0f);

            if (_showSplash) StartCoroutine(PlaySplash(origin));

            // 전설/에픽: 오라(물고기 추종) + 반짝이 버스트 + 배너 (색·배너만 등급별).
            FlairTier tier = TierOf(fish);
            if (tier != FlairTier.None)
            {
                StartCoroutine(SparkleBurst(origin, tier));
                ShowBanner(tier);
            }

            StartCoroutine(FlyFish(fish.Icon, origin, tier));

            // 수동 성공: 추가 장식 물고기 N마리도 튀어올라 배로(보상 무관). 다중포획이면 같은 프레임 N회 호출되므로 1회만.
            if (isManual && Time.frameCount != _lastManualBurstFrame)
            {
                _lastManualBurstFrame = Time.frameCount;
                for (int i = 0; i < _manualExtraFish; i++)
                {
                    Vector3 o = baseOrigin + new Vector3(Random.Range(-_multiSpread, _multiSpread), Random.Range(-0.15f, 0.15f), 0f);
                    // 장식은 랜덤 물고기 스프라이트(풀 비면 잡은 물고기로 폴백). 전설 오라는 메인만(중복 방지).
                    Sprite deco = (_decoFishSprites != null && _decoFishSprites.Length > 0)
                        ? _decoFishSprites[Random.Range(0, _decoFishSprites.Length)]
                        : null;
                    if (deco == null) deco = fish.Icon;
                    StartCoroutine(FlyFish(deco, o, FlairTier.None));
                }
            }
        }

        private IEnumerator FlyFish(Sprite icon, Vector3 origin, FlairTier tier = FlairTier.None)
        {
            // 여러 마리 동시 발동(다중포획/수동 튀어오름) 시 겹침 방지 — 출발 시차(0~_launchStagger 랜덤).
            float stagger = Random.Range(0f, _launchStagger);
            if (stagger > 0f) yield return new WaitForSeconds(stagger);

            SpriteRenderer sr = RentFish();
            sr.sprite = icon;
            sr.color = Color.white;
            Transform tr = sr.transform;

            // 전설/에픽: 물고기 뒤에 깔려 같이 날아가는 오라(매 프레임 위치/크기 추종).
            SpriteRenderer glow = tier != FlairTier.None ? RentGlow() : null;
            Color glowBase = GlowColorFor(tier);

            Vector3 startBelow = origin + Vector3.down * _riseDepth;   // 수면 아래에서 출발
            // 아이콘 픽셀크기가 달라도 화면상 크기를 일정하게: 목표높이 ÷ 스프라이트 월드높이.
            float full = _fishTargetHeight / Mathf.Max(0.01f, icon.bounds.size.y);

            // 경로 다양화 — 비행시간/포물선 높이를 마리마다 살짝 랜덤(겹쳐 보이지 않게).
            float dur = _flightDuration * Random.Range(0.9f, 1.15f);
            float arc = _arcHeight * Random.Range(0.8f, 1.35f);

            float t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                Vector3 from = u < 0.18f
                    ? Vector3.Lerp(startBelow, origin, u / 0.18f)   // 0~18%: 수면 뚫고 솟음
                    : origin;
                Vector3 p = Vector3.Lerp(from, BoatPoint, u);       // 도착점 매 프레임 샘플 → 출렁이는 배 추종
                p.y += arc * 4f * u * (1f - u);                    // 포물선(가운데 정점, 마리마다 높이 랜덤)

                tr.position = p;
                tr.rotation = Quaternion.Euler(0f, 0f, _spinDegrees * u);

                float s = u < 0.18f ? Mathf.Lerp(0.1f, full, u / 0.18f)
                        : u > 0.85f ? Mathf.Lerp(full, full * 0.7f, (u - 0.85f) / 0.15f)
                        : full;
                tr.localScale = Vector3.one * s;

                // 전설 오라: 물고기 자리에 같이 두고, 살짝 맥동(반짝임).
                if (glow != null)
                {
                    glow.transform.position = p;
                    float pulse = 1f + 0.12f * Mathf.Sin(u * Mathf.PI * 8f);
                    glow.transform.localScale = Vector3.one * s * _legendaryGlowScale * pulse;
                    Color gc = glowBase;
                    gc.a = glowBase.a * (u > 0.85f ? Mathf.Lerp(1f, 0f, (u - 0.85f) / 0.15f) : 1f);
                    glow.color = gc;
                }

                t += Time.deltaTime;
                yield return null;
            }

            if (glow != null) ReturnGlow(glow);
            yield return PopOut(sr, 0.12f);
            ReturnFish(sr);
        }

        private IEnumerator PopOut(SpriteRenderer sr, float dur)
        {
            Transform tr = sr.transform;
            Vector3 baseScale = tr.localScale;
            Color c = sr.color;
            float t = 0f;
            while (t < dur)
            {
                float u = t / dur;
                tr.localScale = baseScale * (1f + 0.4f * u);   // 톡 커지며
                c.a = 1f - u;                                  // 사라짐
                sr.color = c;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── 물보라 (스프라이트 시트 프레임 재생, 폴백=원형 scale/fade) ───────────
        private IEnumerator PlaySplash(Vector3 at)
        {
            SpriteRenderer sr = RentSplash();
            sr.transform.position = at;
            sr.color = _splashTint;

            if (_splashFrames != null && _splashFrames.Length > 0)
            {
                int n = _splashFrames.Length;
                sr.transform.localScale = Vector3.one * _splashScale;
                float t = 0f;
                while (t < _splashDuration)
                {
                    int f = Mathf.Clamp((int)(t / _splashDuration * n), 0, n - 1);
                    sr.sprite = _splashFrames[f];   // 프레임 교체(시트가 확장+페이드를 이미 담음)
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // 폴백: 원형이 커지며 사라짐
                sr.sprite = FallbackCircle();
                Color c = _splashTint;
                float t = 0f;
                while (t < _splashDuration)
                {
                    float u = t / _splashDuration;
                    sr.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, _splashScale, u);
                    c.a = _splashTint.a * (1f - u);
                    sr.color = c;
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            ReturnSplash(sr);
        }

        // ── 전설 연출: 입질~당기기 노란 오라(물 위 고정, 맥동) ──────────────────
        private void StartBiteGlow()
        {
            StopBiteGlow();
            _biteGlowRoutine = StartCoroutine(BiteGlowLoop());
        }

        private void StopBiteGlow()
        {
            if (_biteGlowRoutine != null) { StopCoroutine(_biteGlowRoutine); _biteGlowRoutine = null; }
            if (_biteGlow != null) { ReturnGlow(_biteGlow); _biteGlow = null; }
        }

        // 입질 지점(_currentOrigin)에 노란 오라를 깔고 끝날 때까지 부드럽게 맥동시킨다.
        private IEnumerator BiteGlowLoop()
        {
            _biteGlow = RentGlow();
            Color glowBase = GlowColorFor(_hookedTier);
            float t = 0f;
            while (true)
            {
                _biteGlow.transform.position = _currentOrigin;
                float wave = Mathf.Sin(t * Mathf.PI * 3f);
                _biteGlow.transform.localScale = Vector3.one * _legendaryBiteGlowScale * (1f + 0.18f * wave);
                Color c = glowBase;
                c.a = glowBase.a * (0.7f + 0.3f * wave);
                _biteGlow.color = c;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── 전설/에픽 연출: 반짝이 버스트 + 배너 ───────────────────────────────
        // 솟아오른 지점에서 등급색 반짝이가 사방으로 흩날리며 위로 떠 페이드.
        private IEnumerator SparkleBurst(Vector3 at, FlairTier tier)
        {
            Color col = SparkleColorFor(tier);
            int n = Mathf.Max(0, _legendarySparkles);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float speed = Random.Range(1.4f, 2.8f);
                Vector3 vel = new Vector3(Mathf.Cos(ang) * speed, Mathf.Sin(ang) * speed * 0.6f + 1.6f, 0f);
                StartCoroutine(OneSparkle(at, vel, Random.Range(0.5f, 0.9f), Random.Range(0.12f, 0.22f), col));
            }
            yield break;
        }

        private IEnumerator OneSparkle(Vector3 at, Vector3 vel, float life, float size, Color col)
        {
            SpriteRenderer sr = RentSparkle();
            Vector3 pos = at;
            float t = 0f;
            while (t < life)
            {
                float u = t / life;
                vel.y -= 4.5f * Time.deltaTime;            // 가벼운 중력
                pos += vel * Time.deltaTime;
                sr.transform.position = pos;
                sr.transform.localScale = Vector3.one * size * (1f - 0.4f * u);
                Color c = col;
                c.a = col.a * (1f - u);
                sr.color = c;
                t += Time.deltaTime;
                yield return null;
            }
            ReturnSparkle(sr);
        }

        // 등급 배너('전설!'/'에픽!'): 있으면 톡 커졌다 원복 후 잠시 유지하다 사라짐(없으면 생략).
        private void ShowBanner(FlairTier tier)
        {
            GameObject banner = BannerFor(tier);
            if (banner == null) return;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(BannerPop(banner));
        }

        private IEnumerator BannerPop(GameObject banner)
        {
            var tr = banner.transform;
            banner.SetActive(true);
            float t = 0f;
            // 팝업(0 → 1.15 → 1.0)
            while (t < 0.22f)
            {
                float u = t / 0.22f;
                float s = u < 0.7f ? Mathf.Lerp(0.2f, 1.15f, u / 0.7f)
                                   : Mathf.Lerp(1.15f, 1f, (u - 0.7f) / 0.3f);
                tr.localScale = Vector3.one * s;
                t += Time.deltaTime;
                yield return null;
            }
            tr.localScale = Vector3.one;
            yield return new WaitForSeconds(Mathf.Max(0f, _legendaryBannerTime));
            // 살짝 커지며 사라짐
            t = 0f;
            while (t < 0.2f)
            {
                tr.localScale = Vector3.one * (1f + 0.3f * (t / 0.2f));
                t += Time.deltaTime;
                yield return null;
            }
            banner.SetActive(false);
            tr.localScale = Vector3.one;
        }

        // ── 입질 물결 링 ───────────────────────────────────────────────────────
        private void StartRipples()
        {
            if (_rippleRoutine != null) StopCoroutine(_rippleRoutine);
            _rippleRoutine = StartCoroutine(RippleLoop());
        }

        private void StopRipples()
        {
            if (_rippleRoutine != null) { StopCoroutine(_rippleRoutine); _rippleRoutine = null; }
        }

        private IEnumerator RippleLoop()
        {
            while (true)
            {
                StartCoroutine(OneRipple(_currentOrigin));
                yield return new WaitForSeconds(_rippleInterval);
            }
        }

        // 물결 링 1개: 작게→크게 퍼지며 페이드아웃.
        private IEnumerator OneRipple(Vector3 at)
        {
            SpriteRenderer sr = RentRing();
            sr.transform.position = at;
            float t = 0f;
            while (t < _rippleLife)
            {
                float u = t / _rippleLife;
                sr.transform.localScale = Vector3.one * Mathf.Lerp(_rippleStartScale, _rippleEndScale, u);
                Color c = _rippleColor;
                c.a = _rippleColor.a * (1f - u);   // 퍼질수록 옅어짐
                sr.color = c;
                t += Time.deltaTime;
                yield return null;
            }
            ReturnRing(sr);
        }

        // ── 풀 / 스프라이트 준비 ───────────────────────────────────────────────
        private Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject("FishCatchFX_Pool");
                    go.transform.SetParent(transform, false);
                    _poolRoot = go.transform;
                }
                return _poolRoot;
            }
        }

        private SpriteRenderer RentFish()
        {
            SpriteRenderer sr = _fishPool.Count > 0 ? _fishPool.Pop() : NewRenderer("FlyingFish", _sortingOrder);
            sr.enabled = true;
            return sr;
        }
        private void ReturnFish(SpriteRenderer sr) { sr.enabled = false; _fishPool.Push(sr); }

        private SpriteRenderer RentSplash()
        {
            SpriteRenderer sr = _splashPool.Count > 0 ? _splashPool.Pop() : NewRenderer("Splash", _sortingOrder - 2);
            sr.enabled = true;
            return sr;
        }
        private void ReturnSplash(SpriteRenderer sr) { sr.enabled = false; _splashPool.Push(sr); }

        private SpriteRenderer RentRing()
        {
            SpriteRenderer sr;
            if (_ringPool.Count > 0) sr = _ringPool.Pop();
            else
            {
                sr = NewRenderer("RippleRing", _sortingOrder - 3);
                sr.sprite = _rippleSprite != null ? _rippleSprite : FallbackCircle();
            }
            sr.enabled = true;
            return sr;
        }
        private void ReturnRing(SpriteRenderer sr) { sr.enabled = false; _ringPool.Push(sr); }

        // 전설 오라: 물고기 바로 뒤(sortingOrder-1)에 깔리는 금빛 원형 글로우. 색은 렌더러 tint로.
        private SpriteRenderer RentGlow()
        {
            SpriteRenderer sr;
            if (_glowPool.Count > 0) sr = _glowPool.Pop();
            else { sr = NewRenderer("LegendaryGlow", _sortingOrder - 1); sr.sprite = FallbackCircle(); }
            sr.color = _legendaryGlowColor;
            sr.enabled = true;
            return sr;
        }
        private void ReturnGlow(SpriteRenderer sr) { sr.enabled = false; _glowPool.Push(sr); }

        // 전설 반짝이: 작은 금빛 점. 물고기보다 위(sortingOrder+1)로 떠 보이게.
        private SpriteRenderer RentSparkle()
        {
            SpriteRenderer sr;
            if (_sparklePool.Count > 0) sr = _sparklePool.Pop();
            else { sr = NewRenderer("LegendarySparkle", _sortingOrder + 1); sr.sprite = FallbackCircle(); }
            sr.enabled = true;
            return sr;
        }
        private void ReturnSparkle(SpriteRenderer sr) { sr.enabled = false; _sparklePool.Push(sr); }

        private SpriteRenderer NewRenderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(PoolRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = _sortingLayer;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        // 물보라 시트를 cols×rows로 런타임 슬라이스(읽기 위쪽 행부터 = burst 프레임 순서).
        private void BuildSplashFrames()
        {
            if (_splashSheet == null) { _splashFrames = null; return; }
            int cols = Mathf.Max(1, _splashCols), rows = Mathf.Max(1, _splashRows);
            float w = _splashSheet.width / (float)cols;
            float h = _splashSheet.height / (float)rows;
            _splashFrames = new Sprite[cols * rows];
            int idx = 0;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    float x = col * w;
                    float y = _splashSheet.height - (row + 1) * h;   // 텍스처 원점이 좌하단 → 위 행부터 뒤집어 샘플
                    _splashFrames[idx++] = Sprite.Create(
                        _splashSheet, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), 100f);
                }
        }

        // 아트 미배정 시 폴백 원형(물보라/물결 공용). 한 번만 생성해 공유.
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
