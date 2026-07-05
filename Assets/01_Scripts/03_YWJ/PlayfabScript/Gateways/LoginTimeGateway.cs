using System;
using PlayFab.ClientModels;
using UnityEngine;

public class LoginTimeGateway
{
    private readonly PlayFabGateway _playFabGateway;

    public LoginTimeGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway;
    }

    public void GetElapsedTimeSinceLastLogin(Action<ElapsedLoginTimeResponse> onSuccess, Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "GetElapsedTimeSinceLastLogin",
            new { },
            result =>
            {
                try
                {
                    var json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<ElapsedLoginTimeResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("GetElapsedTimeSinceLastLogin failed");
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

    public void SaveLastLoginTime(Action onSuccess = null, Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "SaveLastLoginTime",
            new { },
            result =>
            {
                try
                {
                    var json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<SaveLastLoginTimeResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("SaveLastLoginTime failed");
                        return;
                    }

                    onSuccess?.Invoke();
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
}