using System;
using System.Threading.Tasks;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;


public class MoneyGateway
{
    private readonly PlayFabGateway _playFabGateway;
    
    public MoneyGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway ?? throw new ArgumentNullException(nameof(playFabGateway));
    }


    public void RefreshMoneyData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        _playFabGateway.ExecuteCloudScript("GetCurrencyData", null,
            result =>
            {
                try
                {
                    // Early return: CloudScript 에러 체크
                    if (result.Error != null)
                    {
                        Debug.LogError($"화폐 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    // Early return: JSON 직렬화
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("화폐 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"화폐 원본 데이터: {json}");

                    // ✅ 서버 응답 구조에 맞게 직접 파싱
                    // { "success": true, "data": { "gold": 12345, "prismPearl": 50, "pirateCoin": 3 } }
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<MoneyJSONModel>>(json);
                    
                    if (response == null || !response.success)
                    {
                        Debug.LogError($"화폐 응답 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    // Early return: 데이터 검증
                    if (response.data == null)
                    {
                        Debug.LogError("화폐 데이터가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    // Early return: DataStore 검증
                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    // ✅ 모든 검증 통과 - 데이터 업데이트
                    PlayFabDataStore.Instance.UpdateMoney(response.data);
                    Debug.Log($"화폐 업데이트 완료 - Gold: {response.data.gold}, PrismPearl: {response.data.prismPearl}, PirateCoin: {response.data.pirateCoin}");
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"화폐 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            error =>
            {
                Debug.LogError($"GetMoneyData 호출 실패: {error.GenerateErrorReport()}");
                onError?.Invoke(error);
            });
    }

    public async Task RefreshMoneyDataAsync()
    {
        var result = await _playFabGateway.ExecuteCloudScriptAsync("GetCurrencyData", null);

        if (result.Error != null)
        {
            throw new Exception($"화폐 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = _playFabGateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("화폐 데이터가 비어있습니다.");
        }

        Debug.Log($"화폐 원본 데이터: {json}");

        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<MoneyJSONModel>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"화폐 응답 실패 (success: {response?.success})");
        }

        if (response.data == null)
        {
            throw new Exception("화폐 데이터가 null입니다.");
        }

        if (PlayFabDataStore.Instance == null)
        {
            throw new Exception("PlayFabDataStore.Instance가 null입니다.");
        }

        PlayFabDataStore.Instance.UpdateMoney(response.data);
        Debug.Log($"화폐 업데이트 완료 - Gold: {response.data.gold}, PrismPearl: {response.data.prismPearl}, PirateCoin: {response.data.pirateCoin}");
    }
}