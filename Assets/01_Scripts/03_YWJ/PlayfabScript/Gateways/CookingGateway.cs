using System;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Threading.Tasks;

public class CookingGateway
{
    private readonly PlayFabGateway gateway;

    public CookingGateway(PlayFabGateway gateway)
    {
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public void RefreshCookingData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        gateway.ExecuteCloudScript("GetCookingData", null,
            result =>
            {
                try
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"요리 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    string json = gateway.SerializeFunctionResult(result);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("요리 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"요리 원본 데이터: {json}");

                    // ✅ 올바른 응답 구조로 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<CookingDataWrapper>>(json);

                    if (response == null || !response.success)
                    {
                        Debug.LogError($"요리 응답 실패 또는 파싱 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    if (response.data?.CookingSlotData == null)
                    {
                        Debug.LogError("요리 데이터가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"요리 개수: {response.data.CookingSlotData.cookSlots.Count}");
                    PlayFabDataStore.Instance.UpdateCooking(response.data.CookingSlotData.cookSlots);
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"요리 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            onError);
    }



    public async Task RefreshCookingDataAsync()
    {
        var result = await gateway.ExecuteCloudScriptAsync("GetCookingData", null);

        if (result.Error != null)
        {
            throw new Exception($"요리 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = gateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("요리 데이터가 비어있습니다.");
        }

        Debug.Log($"요리 원본 데이터: {json}");

        var response =
            Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<CookingDataWrapper>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"요리 응답 실패 또는 파싱 실패 (success: {response?.success})");
        }

        if (response.data?.CookingSlotData == null)
        {
            throw new Exception("요리 데이터가 null입니다.");
        }

        if (PlayFabDataStore.Instance == null)
        {
            throw new Exception("PlayFabDataStore.Instance가 null입니다.");
        }

        Debug.Log($"요리 개수: {response.data.CookingSlotData.cookSlots.Count}");

        PlayFabDataStore.Instance.UpdateCooking(response.data.CookingSlotData.cookSlots);
    }



    public void StartCooking(
        int slotIndex,
        string recipeId,
        int totalCount,
        Action<ExecuteCloudScriptResult> onSuccess = null,
        Action<PlayFabError> onError = null,
        bool forwardScriptErrors = false)
    {
        if (string.IsNullOrEmpty(recipeId))
        {
            Debug.LogError("StartCooking: recipeId가 비어있습니다.");
            return;
        }

        if (slotIndex < 0)
        {
            Debug.LogWarning($"StartCooking: slotIndex가 0 미만입니다. (slotIndex: {slotIndex})");
        }

        gateway.ExecuteCloudScript(
            "StartCooking",
            new
            {
                slotIndex = slotIndex,
                recipeId = recipeId,
                totalCount = totalCount
            },
            onSuccess,
            onError,
            forwardScriptErrors);
    }

    public void OpenCookingSlot(int slotIndex, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null, bool forwardScriptErrors = false)
    {
        if (slotIndex < 0)
        {
            Debug.LogError("OpenCookingSlot: slotIndex가 0 미만입니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "OpenCookingSlot",
            new
            {
                slotIndex = slotIndex
            },
            onSuccess,
            onError,
            forwardScriptErrors);
    }

    public void ClaimCooking(int slotIndex, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null, bool forwardScriptErrors = false)
    {
        if (slotIndex < 0)
        {
            Debug.LogError("ClaimCooking: slotIndex가 0 미만입니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "ClaimCooking",
            new
            {
                slotIndex = slotIndex
            },
            onSuccess,
            onError,
            forwardScriptErrors);
    }

    public void CancelCooking(int slotIndex, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null, bool forwardScriptErrors = false)
    {
        if (slotIndex < 0)
        {
            Debug.LogError("CancelCooking: slotIndex가 0 미만입니다.");
            return;
        }
        gateway.ExecuteCloudScript(
            "CancelCooking",
            new
            {
                slotIndex = slotIndex
            },
            onSuccess,
            onError,
            forwardScriptErrors);
    }

    public void SpeedupCooking(
        int slotIndex,
        string itemId,
        int seconds,
        string requestId,
        Action<ExecuteCloudScriptResult> onSuccess = null,
        Action<PlayFabError> onError = null,
        bool forwardScriptErrors = false)
    {
        if (slotIndex < 0)
        {
            Debug.LogError("SpeedupCooking: slotIndex가 0 미만입니다.");
            return;
        }

        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogError("SpeedupCooking: itemId가 비어있습니다.");
            return;
        }

        if (seconds <= 0)
        {
            Debug.LogError("SpeedupCooking: seconds는 0보다 커야 합니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "SpeedupCooking",
            new
            {
                slotIndex = slotIndex,
                itemId = itemId,
                seconds = seconds,
                requestId = requestId
            },
            onSuccess,
            onError,
            forwardScriptErrors);
    }
}
