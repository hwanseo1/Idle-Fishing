using System;

namespace JHS.Equipment
{
    // 강화/해금/장착을 "서버 권위"로 처리하는 통로. 구현은 PlayFab CloudScript(PlayFabEquipmentServerGateway).
    // 골드 차감·레벨업·해금 판정은 전부 서버가 하고, 클라는 요청 후 결과(서버값)만 반영한다.
    // ※ 동기 IGoldWallet과 달리 모든 동작은 비동기(콜백) — CloudScript 왕복이 있기 때문.
    public interface IEquipmentServerGateway
    {
        // 현재 골드(서버에서 동기화된 잔액). 표시·사전검사용. 진실은 서버.
        long Gold { get; }

        // 장비 강화. 서버가 골드(추후 재료)를 차감하고 레벨+1. 응답의 newServerLevel(1-base)을 돌려준다.
        void UpgradeEquipment(string equipmentId, Action<ServerOpResult> onDone);

        // 배 강화(스킬 레벨). LevelUpShip. 응답 newServerLevel(1-base).
        void UpgradeShip(string shipId, Action<ServerOpResult> onDone);

        // 배 해금. UnlockShip. 서버가 해금 골드 차감 + isOpened=true.
        void UnlockShip(string shipId, Action<ServerOpResult> onDone);

        // 배 장착. EquipShip. 골드 변화 없음.
        void EquipShip(string shipId, Action<ServerOpResult> onDone);

        // 골드 잔액만 서버에서 다시 읽어 동기화(동작 없이 조회). 화면 열 때 스테일 캐시 방지용. onDone(잔액).
        void RefreshCurrency(Action<long> onDone);
    }

    // 서버 동작 1건의 결과. 실패면 success=false + error(사유). newServerLevel은 강화 계열에서만 유효(1-base).
    public struct ServerOpResult
    {
        public bool success;
        public int newServerLevel;   // 강화 응답의 newLevel(서버 1-base). 해금/장착은 0(미사용).
        public long newGold;         // 동작 후 골드 잔액(모르면 -1).
        public string error;         // 실패 사유(성공이면 null).

        public static ServerOpResult Ok(int newServerLevel, long newGold)
            => new ServerOpResult { success = true, newServerLevel = newServerLevel, newGold = newGold, error = null };

        public static ServerOpResult Fail(string error)
            => new ServerOpResult { success = false, newServerLevel = 0, newGold = -1, error = error };
    }

    // 로그인 로드용 배 1척 서버 스냅샷. level은 서버 1-base. (어댑터가 PlayFabDataStore에서 변환해 채움)
    public struct ShipSnapshot
    {
        public int level;
        public bool isOpened;
        public bool equipped;
    }
}
