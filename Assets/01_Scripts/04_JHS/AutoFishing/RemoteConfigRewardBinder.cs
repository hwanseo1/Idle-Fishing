using System.Globalization;
using UnityEngine;
using JHS.Backend;   // BackendService (원격 설정 읽기)

namespace JHS.Fishing
{
    // 원격 설정값(BackendService.Config)을 자동낚시 보상에 연결하는 글루(seam).
    // AutoFishingController는 안 건드린다 — 이미 열려 있는 public AddRewardModifier()로만 끼운다(OCP/DIP).
    // 데모: PlayFab 콘솔/Mock에서 goldMultiplier를 바꾸면 재빌드 없이 자동낚시 보상 배수가 바뀜.
    //
    // ⚠️ 데모용 1값 연결(설계 §10 A-④). 키 이름은 'goldMultiplier'지만 실제로는 JHS가 소유한
    //    자동낚시 보상(기여도)에 곱한다 — 원격 설정이 게임플레이에 반영되는 통로를 증명하는 게 목적.
    public sealed class RemoteConfigRewardBinder : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _autoFishing;   // 없으면 씬에서 탐색
        [SerializeField] private string _key = "goldMultiplier";       // 읽어올 원격 설정 키
        [SerializeField] private float _fallback = 1f;                  // 설정 없을 때 배수(중립)

        private void Start()
        {
            if (_autoFishing == null) _autoFishing = FindFirstObjectByType<AutoFishingController>();
            if (_autoFishing == null)
            {
                Debug.LogWarning("[RemoteConfigReward] AutoFishingController 없음 — 보상배수 연결 안 함.");
                return;
            }

            // 모디파이어는 즉시 끼운다 — 값은 Apply마다 live로 읽으므로 부팅(async fetch) 전에 연결돼도 무방.
            _autoFishing.AddRewardModifier(new RemoteValueRewardModifier(GetMultiplier));

            // 확인 로그는 config가 준비된 뒤에 찍어야 정확하다.
            // (BackendService.Boot이 async라, Start 시점엔 Config가 아직 null → 여기서 찍으면 항상 fallback값으로 보임)
            var svc = BackendService.Instance;
            if (svc == null)
                Debug.Log($"[RemoteConfigReward] '{_key}' 연결됨 — BackendService 없음 → ×{_fallback:0.##} 고정.");
            else if (svc.IsReady)
                LogBound();
            else
                svc.OnReady += LogBound;   // 부팅 끝나면 정확한 값으로 1회 로그
        }

        private void LogBound()
        {
            if (BackendService.Instance != null) BackendService.Instance.OnReady -= LogBound;
            Debug.Log($"[RemoteConfigReward] '{_key}' → 자동낚시 보상배수 연결 (적용 ×{GetMultiplier():0.##}).");
        }

        private void OnDestroy()
        {
            if (BackendService.Instance != null) BackendService.Instance.OnReady -= LogBound;
        }

        // 매 보상 계산 때 호출 — 항상 최신 config를 읽는다(라이브 갱신도 반영).
        private float GetMultiplier()
        {
            var svc = BackendService.Instance;
            string raw = svc != null && svc.Config != null ? svc.Config.Get(_key, null) : null;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v > 0f
                ? v : _fallback;
        }
    }
}
