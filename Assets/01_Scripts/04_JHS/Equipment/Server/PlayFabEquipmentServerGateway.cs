using System;
using UnityEngine;
using Newtonsoft.Json;
using Fisher.PlayerSystems;   // CSH 인벤 pull (FisherPlayerDataBridge/FisherRuntimeContext 공개 API만 호출)

namespace JHS.Equipment
{
    // IEquipmentServerGateway의 PlayFab CloudScript 구현.
    // YWJ의 PlayFabGateway.ExecuteCloudScript(공개 API)만 호출하고, 응답 FunctionResult를 직접 파싱한다.
    // (YWJ 코드 무수정 — 협업 규칙. 서브게이트웨이 래퍼 대신 ExecuteCloudScript를 직접 쓰는 이유는
    //  forwardScriptErrors=true로 서버 throw(골드부족·최대레벨)까지 콜백으로 받아 UI 로딩을 풀기 위함.)
    //
    // 골드: 동작 성공 후 GetCurrencyData로 잔액을 다시 읽어 PlayFabDataStore에 반영(서버 권위). 표시값은 store에서 읽음.
    public sealed class PlayFabEquipmentServerGateway : IEquipmentServerGateway
    {
        public long Gold => CurrentStoreGold();

        // 골드 잔액만 서버에서 재조회해 PlayFabDataStore에 반영(동작 없이 조회). 내부 RefreshGold 재사용.
        public void RefreshCurrency(Action<long> onDone) => RefreshGold(onDone);

        // 강화는 골드+재료 차감 → 둘 다 재조회. (재료는 CSH 인벤, navigation이 쓰는 YWJ RefreshInventoryData와 동일 경로)
        public void UpgradeEquipment(string equipmentId, Action<ServerOpResult> onDone)
            => CallOp("LevelUpEquipment", new { equipmentId }, refreshGold: true, refreshInventory: true, onDone);

        public void UpgradeShip(string shipId, Action<ServerOpResult> onDone)
            => CallOp("LevelUpShip", new { shipId }, refreshGold: true, refreshInventory: true, onDone);

        public void UnlockShip(string shipId, Action<ServerOpResult> onDone)
            => CallOp("UnlockShip", new { shipId }, refreshGold: true, refreshInventory: false, onDone);   // 해금은 골드만

        public void EquipShip(string shipId, Action<ServerOpResult> onDone)
            => CallOp("EquipShip", new { shipId }, refreshGold: false, refreshInventory: false, onDone);   // 장착은 변화 없음

        // ── CloudScript 호출 공통 ─────────────────────────────
        private void CallOp(string functionName, object param, bool refreshGold, bool refreshInventory, Action<ServerOpResult> onDone)
        {
            var pf = PlayFabGateway.Instance;
            if (pf == null) { onDone?.Invoke(ServerOpResult.Fail("PlayFabGateway.Instance 없음(미로그인)")); return; }

            pf.ExecuteCloudScript(functionName, param,
                result =>
                {
                    // 서버 핸들러가 throw → result.Error. throw한 실제 사유("Not enough gold" 등)는
                    // Message가 아니라 StackTrace에 담기므로 둘 다 합쳐 노출(디버깅용).
                    if (result.Error != null)
                    {
                        // 서버가 문자열을 throw(try/catch 없음)하면 Message/StackTrace가 비고 실제 사유는 Logs에만 남는다.
                        var e = result.Error;
                        string logs = "";
                        if (result.Logs != null)
                            foreach (var l in result.Logs) logs += $" [{l.Level}] {l.Message}";
                        onDone?.Invoke(ServerOpResult.Fail($"{e.Error}: {e.Message} | StackTrace={e.StackTrace} | Logs={logs}"));
                        return;
                    }

                    CloudOpResponse resp;
                    try { resp = JsonConvert.DeserializeObject<CloudOpResponse>(pf.SerializeFunctionResult(result)); }
                    catch (Exception e) { onDone?.Invoke(ServerOpResult.Fail($"응답 파싱 실패: {e.Message}")); return; }

                    if (resp == null || !resp.success)
                    {
                        onDone?.Invoke(ServerOpResult.Fail(resp?.message ?? resp?.error ?? "서버 응답 실패"));
                        return;
                    }

                    int newLevel = resp.ResolvedNewLevel;   // 신버전 data.newLevel 우선, 없으면 top-level
                    PostOpRefresh(newLevel, refreshGold, refreshInventory, onDone);
                },
                error => onDone?.Invoke(ServerOpResult.Fail(error != null ? error.GenerateErrorReport() : "통신 실패")),
                forwardScriptErrors: true);
        }

        // 골드 잔액을 서버에서 다시 읽어 PlayFabDataStore에 반영. 실패해도 기존 store 값으로 콜백.
        private void RefreshGold(Action<long> onDone)
        {
            var pf = PlayFabGateway.Instance;
            if (pf == null) { onDone?.Invoke(CurrentStoreGold()); return; }

            pf.ExecuteCloudScript("GetCurrencyData", null,
                result =>
                {
                    long gold = CurrentStoreGold();
                    try
                    {
                        if (result.Error == null)
                        {
                            var resp = JsonConvert.DeserializeObject<CurrencyResponse>(pf.SerializeFunctionResult(result));
                            var c = (resp != null && resp.success) ? resp.Resolved : null;
                            if (c != null)
                            {
                                gold = c.gold;
                                WriteStoreGold(c);
                            }
                        }
                    }
                    catch (Exception e) { Debug.LogWarning($"[EquipServer] 골드 갱신 파싱 실패: {e.Message}"); }
                    onDone?.Invoke(gold);
                },
                error => onDone?.Invoke(CurrentStoreGold()),
                forwardScriptErrors: true);
        }

        // 동작 성공 후 재조회: 골드 → 인벤토리 순으로 갱신한 뒤 onDone(=UI RefreshAll).
        // 둘 다 비동기라 끝난 뒤 콜백해야 UI가 최신 골드·재료를 읽는다(깜빡임 없음). 실패해도 onDone은 항상 호출.
        private void PostOpRefresh(int newLevel, bool refreshGold, bool refreshInventory, Action<ServerOpResult> onDone)
        {
            Action<long> afterGold = gold =>
            {
                if (refreshInventory) RefreshInventory(() => onDone?.Invoke(ServerOpResult.Ok(newLevel, gold)));
                else onDone?.Invoke(ServerOpResult.Ok(newLevel, gold));
            };

            if (refreshGold) RefreshGold(afterGold);
            else afterGold(CurrentStoreGold());
        }

        // 재료(인벤토리) 잔량을 서버에서 다시 받아 CSH 인벤까지 반영해 강화 직후 표시가 즉시 줄도록.
        // navigation이 하는 2단계를 그대로 수행: ① RefreshInventoryData(서버→PlayFabDataStore, YWJ)
        // ② PullInventoryFromPlayFabDataStore + NotifyRuntimeChanged(store→CSH 인벤·UI, CSH). 둘 다 공개 API만 호출.
        // 실패/미연결이어도 onDone 진행(다음 화면전환 때 어차피 갱신).
        private void RefreshInventory(Action onDone)
        {
            var pf = PlayFabGateway.Instance;
            if (pf == null || pf.Inventory == null) { onDone?.Invoke(); return; }

            pf.Inventory.RefreshInventoryData(
                () => { PullCshInventory(); onDone?.Invoke(); },
                error => { PullCshInventory(); onDone?.Invoke(); });   // 서버 실패해도 store 기준으로 한 번 당겨봄
        }

        // PlayFabDataStore → CSH InventoryService 반영 + 런타임 알림. (CSH가 화면전환 시 하는 pull과 동일)
        private static void PullCshInventory()
        {
            var bridge = UnityEngine.Object.FindFirstObjectByType<FisherPlayerDataBridge>();
            bridge?.PullInventoryFromPlayFabDataStore(force: true);
            var ctx = UnityEngine.Object.FindFirstObjectByType<FisherRuntimeContext>();
            ctx?.NotifyRuntimeChanged();
        }

        // ── PlayFabDataStore(서버 동기 캐시) 접근 ──────────────
        private static long CurrentStoreGold()
        {
            var store = PlayFabDataStore.Instance;
            var info = store != null ? store.GetPlayerInfo() : null;
            return info != null && info.currency != null ? info.currency.gold : 0;
        }

        private static void WriteStoreGold(CurrencyDto c)
        {
            var store = PlayFabDataStore.Instance;
            if (store == null) return;
            store.UpdateMoney(new MoneyJSONModel
            {
                gold = (int)c.gold,
                prismPearl = (int)c.prismPearl,
                pirateCoin = (int)c.pirateCoin
            });
        }

        // ── 응답 DTO ──
        // ⚠️ 원준 서버 응답 형태가 핸들러마다 다름:
        //  - LevelUp*     = {success, data:{newLevel,...}}              ← newLevel이 data 안
        //  - Unlock/Equip = {success, openedShipId/equippedShipId,...}  ← top-level(newLevel 없음, 미사용)
        //  둘 다 안전하게 지원(data 우선, 없으면 top-level).
        private sealed class CloudOpResponse
        {
            public bool success;
            public int newLevel;        // (구버전/일부 핸들러) top-level
            public OpData data;         // (신버전 LevelUp*) data.newLevel
            public string error;
            public string message;

            public int ResolvedNewLevel => data != null ? data.newLevel : newLevel;
        }

        private sealed class OpData
        {
            public int newLevel;
        }

        // ⚠️ GetCurrencyData: 신버전 {success, data:{gold,...}} / 구버전 {success, currency:{gold,...}}. 둘 다 지원.
        private sealed class CurrencyResponse
        {
            public bool success;
            public CurrencyDto data;      // 신버전
            public CurrencyDto currency;  // 구버전 호환
            public CurrencyDto Resolved => data ?? currency;
        }

        private sealed class CurrencyDto
        {
            public long gold;
            public long prismPearl;
            public long pirateCoin;
        }
    }
}
