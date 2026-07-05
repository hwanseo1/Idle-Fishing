using System;
using UnityEngine;

public class TutorialGateway
{
    private readonly PlayFabGateway _playFabGateway;

    public TutorialGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway;
    }

    public void GetTutorialData(
        Action<TutorialJSONModel> onSuccess = null,
        Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "GetTutorialData",
            new { },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = Newtonsoft.Json.JsonConvert
                        .DeserializeObject<PlayFabDataResponse<TutorialJSONModel>>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("GetTutorialData failed");
                        return;
                    }

                    TutorialJSONModel tutorialData = response.data ?? new TutorialJSONModel();

                    PlayFabDataStore.Instance.UpdateTutorial(tutorialData);

                    onSuccess?.Invoke(tutorialData);
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

    public void MarkTutorialShown(
        UnlockableFeature feature,
        Action<TutorialJSONModel> onSuccess = null,
        Action<string> onFail = null)
    {
        MarkTutorialShown(feature.ToString(), onSuccess, onFail);
    }


    public void MarkTutorialShown(
        string tutorialKey,
        Action<TutorialJSONModel> onSuccess = null,
        Action<string> onFail = null)
    {
        if (string.IsNullOrEmpty(tutorialKey))
        {
            onFail?.Invoke("tutorialKey is null or empty");
            return;
        }

        _playFabGateway.ExecuteCloudScript(
            "MarkTutorialShown",
            new
            {
                tutorialKey = tutorialKey
            },
            result =>
            {
                try
                {
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    var response = Newtonsoft.Json.JsonConvert
                        .DeserializeObject<PlayFabDataResponse<TutorialJSONModel>>(json);

                    if (response == null || response.success == false)
                    {
                        onFail?.Invoke("MarkTutorialShown failed");
                        return;
                    }

                    TutorialJSONModel tutorialData = response.data ?? new TutorialJSONModel();

                    PlayFabDataStore.Instance.UpdateTutorial(tutorialData);

                    onSuccess?.Invoke(tutorialData);
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