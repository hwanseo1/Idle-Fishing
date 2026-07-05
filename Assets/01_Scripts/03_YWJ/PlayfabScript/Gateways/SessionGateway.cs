using System;
using UnityEngine;

public class SessionGateway
{
    private readonly PlayFabGateway _playFabGateway;

    public SessionGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway;
    }

    public void SetCurrentSession(
        string sessionKey,
        Action onSuccess = null,
        Action<string> onFail = null)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            onFail?.Invoke("sessionKey is null or empty");
            return;
        }

        _playFabGateway.ExecuteCloudScript(
            "SetCurrentSession",
            new
            {
                sessionKey = sessionKey
            },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<SetCurrentSessionResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("SetCurrentSession failed");
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

    public void GetCurrentSession(
        Action<GetCurrentSessionResponse> onSuccess,
        Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "GetCurrentSession",
            new { },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = JsonUtility.FromJson<GetCurrentSessionResponse>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("GetCurrentSession failed");
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
}