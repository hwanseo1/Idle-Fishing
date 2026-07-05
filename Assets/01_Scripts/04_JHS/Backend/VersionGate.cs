namespace JHS.Backend
{
    // 부팅 판정 결과.
    public enum GateDecision
    {
        Ok,                // 통과
        RecommendUpdate,   // 권장버전 미만 — 안내만(플레이 가능)
        BlockedOutdated,   // 최소버전 미만 — 차단
        Maintenance        // 점검 중 — 차단
    }

    // 클라 버전 + RemoteConfig → 부팅 판정. 네트워크/PlayFab 모름(순수 로직, 단위테스트 쉬움).
    public static class VersionGate
    {
        public static GateDecision Evaluate(string clientVersion, RemoteConfig config)
        {
            if (config == null) return GateDecision.Ok;   // config 못 받음 → 통과(상위 fallback이 기본값 책임)
            if (config.maintenance) return GateDecision.Maintenance;
            if (Compare(clientVersion, config.minVersion) < 0) return GateDecision.BlockedOutdated;
            if (Compare(clientVersion, config.recommendedVersion) < 0) return GateDecision.RecommendUpdate;
            return GateDecision.Ok;
        }

        // semver 비교: a<b → -1 / a==b → 0 / a>b → 1. ("1.2.0" vs "1.10.0" 도 정수 비교라 정확)
        public static int Compare(string a, string b)
        {
            int[] pa = Parse(a), pb = Parse(b);
            int n = pa.Length > pb.Length ? pa.Length : pb.Length;
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length ? pa[i] : 0;
                int y = i < pb.Length ? pb[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        private static int[] Parse(string v)
        {
            if (string.IsNullOrEmpty(v)) return System.Array.Empty<int>();
            string[] parts = v.Split('.');
            int[] nums = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                int.TryParse(parts[i], out nums[i]);
            return nums;
        }
    }
}
