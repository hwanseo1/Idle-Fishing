using UnityEngine;

namespace JHS.Equipment
{
    // 보트 셰이더(천처럼 휘청, BoatWave) 켜고/끄기 토글.
    //  - 인스펙터 체크박스(_waveEnabled)로 즉시 on/off (에디터에서도 OnValidate로 바로 반영)
    //  - 런타임에선 SetWave(bool)/ToggleWave()로 제어 (예: 특정 상태에서 끄기)
    // 동작: 켜짐 = 웨이브 머티리얼, 꺼짐 = 기본 스프라이트 머티리얼로 SpriteRenderer.sharedMaterial 교체.
    // 둥둥/기울(Animator)·배 종류 스왑(BoatView)과는 독립 — 셰이더만 끈다.
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class BoatWaveToggle : MonoBehaviour
    {
        [Tooltip("체크 = 휘청 셰이더 ON / 해제 = OFF(기본 머티리얼)")]
        [SerializeField] private bool _waveEnabled = true;
        [SerializeField] private Material _waveMaterial;   // BoatWave.mat (휘청)
        [SerializeField] private Material _plainMaterial;  // Sprite-Unlit-Default (휘청 없음)
        [Tooltip("비우면 같은 오브젝트의 SpriteRenderer 사용")]
        [SerializeField] private SpriteRenderer _renderer;

        public bool WaveEnabled => _waveEnabled;

        private SpriteRenderer R => _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

        private void OnEnable() => Apply();

#if UNITY_EDITOR
        private void OnValidate() { if (isActiveAndEnabled) Apply(); }
#endif

        public void SetWave(bool on) { _waveEnabled = on; Apply(); }
        public void ToggleWave() => SetWave(!_waveEnabled);

        private void Apply()
        {
            var r = R;
            if (r == null) return;
            var m = _waveEnabled ? _waveMaterial : _plainMaterial;
            if (m != null) r.sharedMaterial = m;   // 대상 머티리얼 비어있으면 그대로 둠(안전)
        }
    }
}
