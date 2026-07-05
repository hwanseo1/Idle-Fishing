using System;
using System.Threading.Tasks;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;


public class StageGateway
{
    private readonly PlayFabGateway _playFabGateway;

    public StageGateway(PlayFabGateway playFabGateway)
    {
        _playFabGateway = playFabGateway ?? throw new ArgumentNullException(nameof(playFabGateway));
    }

    public void RefreshStageData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        _playFabGateway.ExecuteCloudScript("GetStageData", null,
            result =>
            {
                try
                {
                    // Early return: CloudScript 에러 체크
                    if (result.Error != null)
                    {
                        Debug.LogError($"스테이지 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }
                    // Early return: JSON 직렬화
                    string json = _playFabGateway.SerializeFunctionResult(result);
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("스테이지 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }
                    Debug.Log($"스테이지 원본 데이터: {json}");
                    // Early return: JSON 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<StageJSONModel>>(json);
                    if (response == null || !response.success)
                    {
                        Debug.LogError($"스테이지 응답 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }
                    // Early return: 데이터 검증
                    if (response.data == null)
                    {
                        Debug.LogError("스테이지 데이터가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }
                    // DataStore 업데이트
                    PlayFabDataStore.Instance.UpdateStage(response.data);
                    onSuccess?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"스테이지 데이터 처리 중 예외 발생: {ex.Message}");
                    onError?.Invoke(null);
                }
            });
    }

    public async Task RefreshStageDataAsync()
    {
        var result = await _playFabGateway.ExecuteCloudScriptAsync("GetStageData", null);

        if (result.Error != null)
        {
            throw new Exception($"스테이지 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = _playFabGateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("스테이지 데이터가 비어있습니다.");
        }

        Debug.Log($"스테이지 원본 데이터: {json}");

        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<StageJSONModel>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"스테이지 응답 실패 (success: {response?.success})");
        }

        if (response.data == null)
        {
            throw new Exception("스테이지 데이터가 null입니다.");
        }

        PlayFabDataStore.Instance.UpdateStage(response.data);
    }

    /// <summary>
    /// StageID와 기여도를 서버에 저장합니다.
    /// </summary>
    /// <param name="stageId">저장할 스테이지 ID</param>
    /// <param name="contribution">기여도</param>
    /// <param name="onSuccess">성공 시 콜백</param>
    /// <param name="onError">실패 시 콜백</param>
    public void SaveStageData(string stageId, float contribution, Action<ExecuteCloudScriptResult> onSuccess = null, Action<PlayFabError> onError = null)
    {
        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogError("SaveStageData: stageId가 비어있습니다.");
            return;
        }

        if (contribution < 0)
        {
            Debug.LogWarning($"SaveStageData: contribution이 음수입니다. (값: {contribution})");
        }

        _playFabGateway.ExecuteCloudScript(
            "SaveStageData",
            new
            {
                currentStageId = stageId,
                contribution = contribution
            },
            result =>
            {
                Debug.Log($"스테이지 데이터 저장 성공 - StageID: {stageId}, 기여도: {contribution}");
                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"스테이지 데이터 저장 실패 - StageID: {stageId}, 기여도: {contribution}\n{error.GenerateErrorReport()}");
                onError?.Invoke(error);
            });
    }
}