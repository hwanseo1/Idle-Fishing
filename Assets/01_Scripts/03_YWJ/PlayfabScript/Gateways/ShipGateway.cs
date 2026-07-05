using System;
using System.Threading.Tasks;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class ShipGateway
{
    private readonly PlayFabGateway gateway;

    public ShipGateway(PlayFabGateway gateway)
    {
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public void RefreshShipData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        gateway.ExecuteCloudScript("GetPlayerShipData", null, 
            result =>
            {
                try
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"선박 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    string json = gateway.SerializeFunctionResult(result);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("선박 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"선박 원본 데이터: {json}");

                    // ✅ 올바른 응답 구조로 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<ShipDataWrapper>>(json);

                    if (response == null || !response.success)
                    {
                        Debug.LogError($"선박 응답 실패 또는 파싱 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    if (response.data?.ShipData == null)
                    {
                        Debug.LogError("선박 데이터가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"선박 개수: {response.data.ShipData.Count}");
                    PlayFabDataStore.Instance.UpdateShip(response.data.ShipData);
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"선박 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            onError);
    }

    public async Task RefreshShipDataAsync()
    {
        var result = await gateway.ExecuteCloudScriptAsync("GetPlayerShipData", null);

        if (result.Error != null)
        {
            throw new Exception($"선박 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = gateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("선박 데이터가 비어있습니다.");
        }

        Debug.Log($"선박 원본 데이터: {json}");

        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<ShipDataWrapper>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"선박 응답 실패 또는 파싱 실패 (success: {response?.success})");
        }

        if (response.data?.ShipData == null)
        {
            throw new Exception("선박 데이터가 null입니다.");
        }

        if (PlayFabDataStore.Instance == null)
        {
            throw new Exception("PlayFabDataStore.Instance가 null입니다.");
        }

        Debug.Log($"선박 개수: {response.data.ShipData.Count}");
        PlayFabDataStore.Instance.UpdateShip(response.data.ShipData);
    }

    public void Unlock(string shipId, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(shipId))
        {
            Debug.LogError("Unlock: shipId가 비어있습니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "UnlockShip",
            new
            {
                shipId = shipId
            },
            onSuccess,
            onError);
    }

    public void Equip(string shipId, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(shipId))
        {
            Debug.LogError("Equip: shipId가 비어있습니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "EquipShip",
            new
            {
                shipId = shipId
            },
            onSuccess,
            onError);
    }

    public void LevelUp(string shipId, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(shipId))
        {
            Debug.LogError("LevelUp: shipId가 비어있습니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "LevelUpShip",
            new
            {
                shipId = shipId
            },
            onSuccess,
            onError);
    }
}