using System;
using System.Collections.Generic;
using UnityEngine;

public class MultiplayGateway
{
    private readonly PlayFabGateway _playFabGateway;

    public MultiplayGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway;
    }

    public void GetMultiplayLimit(
        Action<MultiplayLimitJSONModel> onSuccess = null,
        Action<string> onFail = null)
    {
        ExecuteAndSave(
            "GetMultiplayLimit",
            onSuccess,
            onFail);
    }

    public void CheckAndConsumeMultiplayCount(
        Action<MultiplayLimitResponse> onSuccess = null,
        Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "CheckAndConsumeMultiplayCount",
            new { },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<MultiplayLimitResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("CheckAndConsumeMultiplayCount failed");
                        return;
                    }

                    PlayFabDataStore.Instance.UpdateMultiplayLimit(response.data);
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    onFail?.Invoke(e.Message);
                }
            },
            error => onFail?.Invoke(error.ErrorMessage));
    }

    public void DebugResetMultiplayCount(
        Action<MultiplayLimitJSONModel> onSuccess = null,
        Action<string> onFail = null)
    {
        ExecuteAndSave(
            "DebugResetMultiplayCount",
            onSuccess,
            onFail);
    }


    public void GiveMultiplayReward(
        int personalContribution,
        int totalContribution,
        Action<MultiplayRewardResponse> onSuccess = null,
        Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "GiveMultiplayReward",
            new
            {
                personalContribution = personalContribution,
                totalContribution = totalContribution
            },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<MultiplayRewardResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("GiveMultiplayReward failed");
                        return;
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


private void ExecuteAndSave(
        string functionName,
        Action<MultiplayLimitJSONModel> onSuccess,
        Action<string> onFail)
    {
        _playFabGateway.ExecuteCloudScript(
            functionName,
            new { },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<MultiplayLimitResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke($"{functionName} failed");
                        return;
                    }

                    PlayFabDataStore.Instance.UpdateMultiplayLimit(response.data);
                    onSuccess?.Invoke(response.data);
                }
                catch (Exception e)
                {
                    onFail?.Invoke(e.Message);
                }
            },
            error => onFail?.Invoke(error.ErrorMessage));
    }
}