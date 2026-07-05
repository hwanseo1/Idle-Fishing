using UnityEngine;

namespace JHS.Fishing
{
    // 배 하부를 '물에 잠긴' 것처럼 보이게 하는 런타임 수면 오버레이.
    // 핵심: 선체를 '가리지' 않는다. 배 스프라이트를 복제(_hullTint)해 커스텀 셰이더(JHS/SubmergedTint)로
    // 렌더한다. 셰이더가 배 알파(선체 실루엣) + 월드 수면선 아래에만 반투명 바다색을 평평하게 출력하므로
    // 나무 선체는 그대로 보이되 하부만 물든다. SpriteMask 미사용 → URP UniversalRenderer에서도 동작.
    //
    //  - 수면선(_WaterlineY)은 배의 '실제 렌더 바운즈' 기준(하단 + 높이×_submersionFraction)으로 매 프레임 계산.
    //    → 스테이지/스케일/배 종류로 배 위치가 달라져도 항상 하부에 정확히 맞는다(고정 월드Y의 함정 회피).
    //  - 배 본체(_boatRenderer) sprite를 복제본에 복사 → 배 교체에도 실루엣이 따라감.
    //  - _backgroundRenderer의 현재 sprite로 스테이지별 바다색을 셰이더 _Color에 구동(MaterialPropertyBlock).
    // 배·배경 원본(타 모듈)은 무수정.
    [DisallowMultipleComponent]
    public class BoatWaterlineOverlay : MonoBehaviour
    {
        // 배경 sprite -> 그 배경의 바다색. StageBackgroundController가 교체하는 배경마다 한 줄씩 등록.
        [System.Serializable]
        public class SeaTint
        {
            public Sprite background;
            public Color waterColor = new Color(0.16f, 0.45f, 0.72f, 1f);
        }

        [Header("배 본체 / 침수 틴트(셰이더 복제본)")]
        [Tooltip("배 본체 SpriteRenderer(BoatVisual). 이 sprite를 틴트 복제본에 복사하고, 이 바운즈로 수면선을 잡는다")]
        [SerializeField] private SpriteRenderer _boatRenderer;
        [Tooltip("침수 틴트 복제본. JHS/SubmergedTint 머티리얼 사용. 배 위에 겹쳐 둘 것")]
        [SerializeField] private SpriteRenderer _hullTint;
        [Tooltip("(선택) 흰 물보라 수면선. 없으면 비워둠")]
        [SerializeField] private SpriteRenderer _foamLine;

        // 배 스프라이트마다 하단 여백/장식이 달라(예: 펭귄선박은 선체 아래에 물에 뜬 펭귄이 그려져 바운즈가 더 내려감)
        // 배별로 잠김 비율을 따로 줄 수 있게 오버라이드. 목록에 없으면 기본값(_submersionFraction) 사용.
        [System.Serializable]
        public class BoatSubmersion
        {
            public Sprite boat;
            [Range(0f, 0.6f)] public float submersionFraction = 0.08f;
        }

        [Header("수면선 위치(배 바운즈 기준)")]
        [Tooltip("배 하단에서 위로 '스프라이트 높이의 몇 %'까지 잠기게 할지 (기본 0.08). 배별 예외는 아래 오버라이드")]
        [Range(0f, 0.6f)]
        [SerializeField] private float _submersionFraction = 0.08f;
        [Tooltip("배별 잠김 비율 예외(펭귄선박처럼 선체 아래 장식이 있는 배)")]
        [SerializeField] private BoatSubmersion[] _boatOverrides;
        [Tooltip("수면선 경계 페이드(월드 유닛)")]
        [SerializeField] private float _fadeWorld = 0.10f;

        [Header("배경 연동 (원준 바다 SpriteRenderer)")]
        [Tooltip("StageBackgroundController가 sprite를 교체하는 바다 배경 SpriteRenderer")]
        [SerializeField] private SpriteRenderer _backgroundRenderer;

        [Header("배경별 바다색 매핑")]
        [SerializeField] private SeaTint[] _seaTints;
        [Tooltip("매핑에 없는 배경일 때 쓸 기본 바다색")]
        [SerializeField] private Color _fallbackColor = new Color(0.16f, 0.45f, 0.72f, 1f);
        [Tooltip("침수 틴트 진하기(0=선체 그대로, 1=바다색 꽉)")]
        [Range(0f, 1f)]
        [SerializeField] private float _tintStrength = 0.72f;
        [Tooltip("색 전환 속도(초당 Lerp 계수)")]
        [SerializeField] private float _colorLerpSpeed = 3f;

        [Header("수면선 애니메이션(옵션)")]
        [SerializeField] private float _bobAmplitude = 0.03f;
        [SerializeField] private float _bobSpeed = 1.5f;
        [SerializeField] private float _driftSpeed = 0.03f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int WaterlineId = Shader.PropertyToID("_WaterlineY");
        private static readonly int FadeId = Shader.PropertyToID("_FadeY");

        private Color _currentTint;
        private Color _targetTint;
        private Sprite _lastBackground;
        private MaterialPropertyBlock _tintMpb;
        private Vector3 _foamBasePos;
        private float _phase;
        private MaterialPropertyBlock _foamMpb;

        private void Awake()
        {
            if (_foamLine != null) _foamBasePos = _foamLine.transform.localPosition;
            var bg = CurrentBackground();
            _lastBackground = bg;
            _targetTint = WithStrength(ResolveColor(bg));
            _currentTint = _targetTint;
            MirrorSprite();
            ApplyTint();
        }

        private void Update()
        {
            MirrorSprite();

            var bg = CurrentBackground();
            if (bg != _lastBackground) { _lastBackground = bg; _targetTint = WithStrength(ResolveColor(bg)); }

            if (_currentTint != _targetTint)
                _currentTint = Color.Lerp(_currentTint, _targetTint, Time.deltaTime * _colorLerpSpeed);

            ApplyTint();   // 색 + 수면선을 매 프레임 갱신(배 위치/스케일/교체 대응)
            AnimateFoam();
        }

        // 배 본체 sprite를 틴트 복제본에 복사(배 교체 시 실루엣도 따라감).
        private void MirrorSprite()
        {
            if (_boatRenderer != null && _hullTint != null && _hullTint.sprite != _boatRenderer.sprite)
                _hullTint.sprite = _boatRenderer.sprite;
        }

        // 셰이더 _Color + _WaterlineY를 MaterialPropertyBlock으로 구동(머티리얼 에셋은 안 건드림).
        // 수면선은 배의 실제 바운즈(하단 + 높이×fraction)로 잡아 위치/스케일 무관하게 하부에 맞춘다.
        private void ApplyTint()
        {
            if (_hullTint == null) return;
            if (_tintMpb == null) _tintMpb = new MaterialPropertyBlock();
            _hullTint.GetPropertyBlock(_tintMpb);
            _tintMpb.SetColor(ColorId, _currentTint);
            if (_boatRenderer != null)
            {
                var b = _boatRenderer.bounds;
                float frac = ResolveFraction(_boatRenderer.sprite);
                _tintMpb.SetFloat(WaterlineId, b.min.y + frac * b.size.y);
                _tintMpb.SetFloat(FadeId, _fadeWorld);
            }
            _hullTint.SetPropertyBlock(_tintMpb);
        }

        // 배경 바다색 -> 침수 틴트색. 빨강/초록을 눌러 더 '깊은 파랑'으로. 알파는 틴트 진하기.
        private Color WithStrength(Color s)
            => new Color(s.r * 0.65f, s.g * 0.62f, s.b * 0.98f, _tintStrength);

        // 현재 배 스프라이트에 맞는 잠김 비율(오버라이드 우선, 없으면 기본).
        private float ResolveFraction(Sprite boat)
        {
            if (boat != null && _boatOverrides != null)
                foreach (var o in _boatOverrides)
                    if (o != null && o.boat == boat) return o.submersionFraction;
            return _submersionFraction;
        }

        private Sprite CurrentBackground()
            => _backgroundRenderer != null ? _backgroundRenderer.sprite : null;

        private Color ResolveColor(Sprite bg)
        {
            if (bg != null && _seaTints != null)
                foreach (var t in _seaTints)
                    if (t != null && t.background == bg) return t.waterColor;
            return _fallbackColor;
        }

        private void AnimateFoam()
        {
            if (_foamLine == null) return;
            _phase += Time.deltaTime * _bobSpeed;

            var p = _foamBasePos;
            p.y += Mathf.Sin(_phase) * _bobAmplitude;
            _foamLine.transform.localPosition = p;

            if (_driftSpeed != 0f && _foamLine.sprite != null)
            {
                if (_foamMpb == null) _foamMpb = new MaterialPropertyBlock();
                _foamLine.GetPropertyBlock(_foamMpb);
                float off = Mathf.Repeat(_phase * _driftSpeed, 1f);
                _foamMpb.SetVector("_MainTex_ST", new Vector4(1f, 1f, off, 0f));
                _foamLine.SetPropertyBlock(_foamMpb);
            }
        }
    }
}
