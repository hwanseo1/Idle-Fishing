using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CrewGateway
{
    private readonly PlayFabGateway gateway;

    public CrewGateway(PlayFabGateway gateway)
    {
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public void RefreshCrewData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        gateway.ExecuteCloudScript("GetPlayerCrewData", null, 
            result =>
            {
                try
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"크루 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    string json = gateway.SerializeFunctionResult(result);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("크루 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"크루 원본 데이터: {json}");

                    // ✅ 올바른 응답 구조로 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<CrewDataWrapper>>(json);

                    if (response == null || !response.success)
                    {
                        Debug.LogError($"크루 응답 실패 또는 파싱 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    if (response.data?.CrewData == null)
                    {
                        Debug.LogError("크루 데이터가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"크루 개수: {response.data.CrewData.Count}");
                    PlayFabDataStore.Instance.UpdateCrew(response.data.CrewData);
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"크루 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            onError);
    }

    public async Task RefreshCrewDataAsync()
    {
        var result = await gateway.ExecuteCloudScriptAsync("GetPlayerCrewData", null);

        if (result.Error != null)
        {
            throw new Exception($"크루 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = gateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("크루 데이터가 비어있습니다.");
        }

        Debug.Log($"크루 원본 데이터: {json}");

        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<CrewDataWrapper>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"크루 응답 실패 또는 파싱 실패 (success: {response?.success})");
        }

        if (response.data?.CrewData == null)
        {
            throw new Exception("크루 데이터가 null입니다.");
        }

        if (PlayFabDataStore.Instance == null)
        {
            throw new Exception("PlayFabDataStore.Instance가 null입니다.");
        }

        Debug.Log($"크루 개수: {response.data.CrewData.Count}");
        PlayFabDataStore.Instance.UpdateCrew(response.data.CrewData);
    }

    public void SetCrewData(string crewId, object crewValue, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(crewId))
        {
            Debug.LogError("SetCrewData: crewId가 비어있습니다.");
            return;
        }

        if (crewValue == null)
        {
            Debug.LogError("SetCrewData: crewValue가 null입니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "SetCrewData",
            new
            {
                crewId = crewId,
                crewValue = crewValue
            },
            onSuccess,
            onError);
    }

    public void SetCrewDatas(object crews, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (crews == null)
        {
            Debug.LogError("SetCrewDatas: crews가 null입니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "SetCrewDatas",
            new
            {
                crews = crews
            },
            onSuccess,
            onError);
    }


    public void Promote(string crewId, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(crewId))
        {
            Debug.LogError("Promote: crewId가 비어있습니다.");
            return;
        }
        
        gateway.ExecuteCloudScript(
            "PromoteCrew",
            new
            {
                crewId = crewId
            },
            result =>
            {
                // 📋 로그 출력
                Debug.Log($"FunctionResult: {result.FunctionResult}");

                if (result.Logs != null)
                {
                    foreach (var log in result.Logs)
                    {
                        Debug.Log($"CloudScript Log: {log.Message}");
                    }
                }

                if (result.Error != null)
                {
                    Debug.LogError($"CloudScript Error: {result.Error.Message}");
                }
                else
                {
                    string json = gateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<PromoteCrewResponse>(json);

                    if (response != null && response.success)
                    {
                        UpdateLocalPromotedCrew(response);
                    }
                }

                // ✅ 원래 콜백 실행
                onSuccess?.Invoke(result);
            },
            onError);
    }


    // ✅ 크루 슬롯 업로드 메서드 추가
    public void UploadCrewSlots(
        Dictionary<string, CrewSlotInfo> crewSlots,
        Action<ExecuteCloudScriptResult> onSuccess = null)
    {
        if (crewSlots == null || crewSlots.Count == 0)
        {
            Debug.LogError("[SetCrewSlots] crewSlots가 비어있습니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "SetCrewSlots",
            new
            {
                crewSlots = crewSlots
            },
            result =>
            {
                if (result.Error != null)
                {
                    Debug.LogError($"[SetCrewSlots] CloudScript Error: {result.Error.Message}");
                    return;
                }

                Debug.Log($"[SetCrewSlots] Result: {result.FunctionResult}");
                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"[SetCrewSlots] 실패: {error.GenerateErrorReport()}");
            });
    }


    public void UnlockCrewSlot(
        Action<UnlockCrewSlotResponse> onSuccess = null,
        Action<string> onFail = null)
    {
        gateway.ExecuteCloudScript(
            "UnlockCrewSlot",
            new { },
            result =>
            {
                try
                {
                    string json = gateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<UnlockCrewSlotResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("UnlockCrewSlot failed");
                        return;
                    }

                    if (!response.allUnlocked)
                    {
                        UpdateLocalCrewSlot(response.unlockedSlotIndex);
                    }

                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    onFail?.Invoke(e.Message);
                }
            },
            error =>
            {
                onFail?.Invoke(error.ErrorMessage);
            });
    }

    private void UpdateLocalCrewSlot(string slotIndex)
    {
        var store = PlayFabDataStore.Instance;

        if (store == null)
        {
            Debug.LogError("[CrewSlotGateway] PlayFabDataStore.Instance가 null입니다.");
            return;
        }

        var playerInfo = store.GetPlayerInfo();

        if (playerInfo.crewSlot == null)
        {
            playerInfo.crewSlot = new CrewSlotJSONModel();
        }

        if (playerInfo.crewSlot.crewSlots == null)
        {
            playerInfo.crewSlot.crewSlots = new System.Collections.Generic.Dictionary<string, CrewSlotInfo>();
        }

        if (!playerInfo.crewSlot.crewSlots.ContainsKey(slotIndex))
        {
            playerInfo.crewSlot.crewSlots[slotIndex] = new CrewSlotInfo();
        }

        playerInfo.crewSlot.crewSlots[slotIndex].isUnlocked = true;
        playerInfo.crewSlot.crewSlots[slotIndex].equippedCrewId = null;

        store.UpdateCrewSlot(playerInfo.crewSlot.crewSlots);

        Debug.Log($"[CrewSlotGateway] 로컬 크루 슬롯 해금 완료: {slotIndex}");
    }

    private void UpdateLocalPromotedCrew(PromoteCrewResponse response)
    {
        var playerInfo = PlayFabDataStore.Instance.GetPlayerInfo();

        if (!playerInfo.crew.crews.ContainsKey(response.crewId))
            return;

        playerInfo.crew.crews[response.crewId].grade = response.currentGrade;
        playerInfo.crew.crews[response.crewId].lastPromotedAtUtc = DateTime.UtcNow.ToString("o");

        PlayFabDataStore.Instance.UpdateCrew(playerInfo.crew.crews);

        Debug.Log($"로컬 승급 반영 완료: {response.crewId} {response.previousGrade} → {response.currentGrade}");
    }
}