using System;
using UnityEngine;


public class RecruitGateway
{
    private readonly PlayFabGateway _playFabGateway;

    public RecruitGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway;
    }

    public void ConsumeRecruitCost(
        string recruitType,
        int drawCount,
        Action<ConsumeRecruitCostResponse> onSuccess,
        Action<string> onFail = null)
    {
        _playFabGateway.ExecuteCloudScript(
            "ConsumeRecruitCost",
            new
            {
                recruitType = recruitType, // "basic" or "premium"
                drawCount = drawCount      // 1 or 10
            },
           result =>
           {
               try
               {
                   string json = _playFabGateway.SerializeFunctionResult(result);

                   Debug.Log($"ConsumeRecruitCost Response: {json}");

                   var response = JsonUtility.FromJson<ConsumeRecruitCostResponse>(json);

                   if (response == null)
                   {
                       onFail?.Invoke("ConsumeRecruitCost response is null");
                       return;
                   }

                   if (response.success == false)
                   {
                       onFail?.Invoke(
                           string.IsNullOrEmpty(response.error)
                               ? "ConsumeRecruitCost failed"
                               : response.error);

                       return;
                   }
                   else
                   {
                       PlayFabGateway.Instance.Money.RefreshMoneyData();
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