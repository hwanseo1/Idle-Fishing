using System;
using UnityEngine;

namespace JHS.Backend
{
    // 서버(Title Data)에서 받아오는 원격 설정 한 묶음. 게임코드는 이 DTO만 본다(PlayFab 모름, DIP).
    // [Serializable] — 캐시(PlayerPrefs JSON) · 동봉 기본 SO · (나중) PlayFab Title Data 파싱이 모두 이 타입 공유.
    // balance는 JsonUtility가 Dictionary를 직렬화 못 해서 배열로 둔다(항목 적어 선형조회로 충분).
    [Serializable]
    public sealed class RemoteConfig
    {
        public string minVersion = "0.0.0";          // 이 버전 미만 = 강제 차단
        public string recommendedVersion = "0.0.0";  // 이 버전 미만 = 권장 업데이트 안내
        public bool maintenance;                       // 점검 모드
        public string maintenanceMessage;             // 점검 안내 문구
        public string dataVersion = "0";              // 콘텐츠(원격 카탈로그) 버전 키. 바뀌면 Addressables 카탈로그 갱신 트리거(트랙 B)
        public BalanceEntry[] balance;                // 라이브 튜닝값 (MVP: 데모 1개, 범위는 차선호 결정)

        // 튜닝값 조회 (없으면 fallback). 게임 시스템은 이걸로만 원격값을 읽는다.
        public string Get(string key, string fallback = null)
        {
            if (balance != null)
                foreach (var e in balance)
                    if (e.key == key) return e.value;
            return fallback;
        }

        // 패치 도구 푸시 전 검증 — 유효하면 null, 아니면 사유. (클라가 JsonUtility로 읽으니 같은 파서로 검증)
        public static string Validate(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "빈 문자열";
            RemoteConfig c;
            try { c = JsonUtility.FromJson<RemoteConfig>(json); }
            catch (Exception e) { return $"역직렬화 실패: {e.Message}"; }
            if (c == null) return "파싱 결과 null";
            if (string.IsNullOrEmpty(c.minVersion)) return "minVersion 없음";
            if (string.IsNullOrEmpty(c.recommendedVersion)) return "recommendedVersion 없음";
            return null;
        }
    }

    [Serializable]
    public struct BalanceEntry
    {
        public string key;
        public string value;
    }
}
