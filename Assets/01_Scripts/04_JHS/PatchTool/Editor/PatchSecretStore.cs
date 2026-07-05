using UnityEditor;

namespace JHS.PatchTool
{
    // Secret Key를 EditorPrefs(이 PC 로컬)에 보관 — 프로젝트/git/빌드에 절대 안 들어감.
    // ⚠️ EditorPrefs는 평문(레지스트리/plist) 저장이라 공용 PC면 주의. 핵심은 "소스·에셋엔 두지 않는 것".
    public static class PatchSecretStore
    {
        private const string Key = "jhs.patchtool.secretkey.v1";

        public static string Load() => EditorPrefs.GetString(Key, "");
        public static void Save(string secret) => EditorPrefs.SetString(Key, secret ?? "");
        public static void Clear() => EditorPrefs.DeleteKey(Key);
    }
}
