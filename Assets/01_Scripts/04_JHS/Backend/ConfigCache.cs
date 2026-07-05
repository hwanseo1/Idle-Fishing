using UnityEngine;

namespace JHS.Backend
{
    // 마지막으로 성공한 config를 PlayerPrefs(JSON)에 저장/복원 = 3단 fallback의 ② 캐시.
    // 라이브 성공할 때마다 갱신 → 다음 부팅에서 서버가 죽어도 "마지막에 봤던 설정"으로 게이팅 유지.
    public static class ConfigCache
    {
        private const string Key = "jhs.backend.config.cache.v1";

        public static void Save(RemoteConfig config)
        {
            if (config == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(config));
            PlayerPrefs.Save();
        }

        // 없거나 파싱 실패하면 null (호출측이 동봉 기본값으로 더 내려감).
        public static RemoteConfig Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try { return JsonUtility.FromJson<RemoteConfig>(PlayerPrefs.GetString(Key)); }
            catch { return null; }
        }

        public static void Clear() => PlayerPrefs.DeleteKey(Key);   // 테스트용(캐시 없는 경로 확인)
    }
}
