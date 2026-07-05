using UnityEngine;

namespace JHS.Backend
{
    // 빌드에 동봉되는 기본 config = 3단 fallback의 ① 바닥. 서버도 캐시도 없을 때(첫 실행 오프라인 등) 이걸로 부팅.
    // 보수적으로 채울 것: minVersion 낮게(차단 안 함), maintenance off → 동봉 기본값만으로도 무조건 통과되게.
    [CreateAssetMenu(menuName = "Data/Default Remote Config", fileName = "DefaultRemoteConfig")]
    public sealed class DefaultRemoteConfig : ScriptableObject
    {
        [SerializeField] private RemoteConfig _config = new RemoteConfig();

        public RemoteConfig Value => _config;
    }
}
