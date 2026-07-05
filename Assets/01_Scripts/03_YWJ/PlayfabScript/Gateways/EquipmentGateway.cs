using System;
using System.Threading.Tasks;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class EquipmentGateway
{
    private readonly PlayFabGateway gateway;

    public EquipmentGateway(PlayFabGateway gateway)
    {
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public void RefreshEquipmentData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        gateway.ExecuteCloudScript("GetPlayerEquipmentData", null, 
            result =>
            {
                try
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"장비 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    string json = gateway.SerializeFunctionResult(result);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("장비 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"장비 원본 데이터: {json}");

                    // ✅ 올바른 응답 구조로 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<EquipmentDataWrapper>>(json);

                    if (response == null || !response.success)
                    {
                        Debug.LogError($"장비 응답 실패 또는 파싱 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    if (response.data?.EquipmentData == null)
                    {
                        Debug.LogError("장비 데이터가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"장비 개수: {response.data.EquipmentData.Count}");
                    PlayFabDataStore.Instance.UpdateEquipment(response.data.EquipmentData);
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"장비 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            onError);
    }

    public async Task RefreshEquipmentDataAsync()
    {
        var result = await gateway.ExecuteCloudScriptAsync("GetPlayerEquipmentData", null);

        if (result.Error != null)
        {
            throw new Exception($"장비 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = gateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("장비 데이터가 비어있습니다.");
        }

        Debug.Log($"장비 원본 데이터: {json}");

        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<EquipmentDataWrapper>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"장비 응답 실패 또는 파싱 실패 (success: {response?.success})");
        }

        if (response.data?.EquipmentData == null)
        {
            throw new Exception("장비 데이터가 null입니다.");
        }

        if (PlayFabDataStore.Instance == null)
        {
            throw new Exception("PlayFabDataStore.Instance가 null입니다.");
        }

        Debug.Log($"장비 개수: {response.data.EquipmentData.Count}");
        PlayFabDataStore.Instance.UpdateEquipment(response.data.EquipmentData);
    }

    public void LevelUp(string equipmentId, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(equipmentId))
        {
            Debug.LogError("LevelUp: equipmentId가 비어있습니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "LevelUpEquipment",
            new
            {
                equipmentId = equipmentId
            },
            onSuccess,
            onError);
    }
}