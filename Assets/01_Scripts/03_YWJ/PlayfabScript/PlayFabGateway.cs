using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using Runtime;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PlayFabGateway : MonoBehaviour
{
    public static PlayFabGateway Instance { get; private set; }
    public event Action<JObject> CshRuntimeStateReceived;
    public JObject LastCshRuntimeState { get; private set; }

    public InventoryGateway Inventory { get; private set; }
    public CrewGateway Crew { get; private set; }
    public ShipGateway Ship { get; private set; }
    public EquipmentGateway Equipment { get; private set; }
    public CookingGateway Cooking { get; private set; }
    public ShopGateway Shop { get; private set; }
    public MoneyGateway Money { get; private set; }
    public StageGateway Stage { get; private set; }
    public LoginTimeGateway LoginTime { get; private set; }
    public RecruitGateway Recruit { get; private set; }
    public MultiplayGateway MultiplayLimit { get; private set; }
    public SessionGateway Session { get; private set; }
    public TutorialGateway Tutorial { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = GetPersistentRootForDontDestroyOnLoad();
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;
        GameObject persistentRoot = GetPersistentRootForDontDestroyOnLoad();
        DontDestroyOnLoad(persistentRoot);

        InitializeGateways();
    }

    private void InitializeGateways()
    {
        try
        {
            Inventory = new InventoryGateway(this);
            Crew = new CrewGateway(this);
            Ship = new ShipGateway(this);
            Equipment = new EquipmentGateway(this);
            Cooking = new CookingGateway(this);
            Shop = new ShopGateway(this);
            Money = new MoneyGateway(this);
            Stage = new StageGateway(this);
            LoginTime = new LoginTimeGateway(this);
            Recruit = new RecruitGateway(this);
            MultiplayLimit = new MultiplayGateway(this);
            Session = new SessionGateway(this);
            Tutorial = new TutorialGateway(this);
            Debug.Log("PlayFabGateway 초기화 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"Gateway 초기화 실패: {e.Message}\n{e.StackTrace}");
        }
    }

    public void ExecuteCloudScript(
        string functionName,
        object parameter,
        Action<ExecuteCloudScriptResult> onSuccess = null,
        Action<PlayFabError> onError = null,
        bool forwardScriptErrors = false)
    {
        if (string.IsNullOrEmpty(functionName))
        {
            Debug.LogError("ExecuteCloudScript: functionName이 비어있습니다.");
            return;
        }

        PlayFabClientAPI.ExecuteCloudScript(
            new ExecuteCloudScriptRequest
            {
                FunctionName = functionName,
                FunctionParameter = parameter,
                GeneratePlayStreamEvent = true
            },
            result =>
            {
                if (result.Error != null)
                {
                    Debug.LogError($"CloudScript Error: {functionName}\n" +
                                   $"Error: {result.Error.Error}\n" +
                                   $"Message: {result.Error.Message}\n" +
                                   $"StackTrace: {result.Error.StackTrace}");
                    if (forwardScriptErrors)
                    {
                        onSuccess?.Invoke(result);
                    }

                    return;
                }

                Debug.Log($"CloudScript Success: {functionName}");
                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"CloudScript Failed: {functionName}\n{error.GenerateErrorReport()}");
                onError?.Invoke(error);
            });
    }



    // 0622 추가, 대기 후 실행을 위해 Task 기반으로 ExecuteCloudScript를 호출하는 비동기 메서드
    public Task<ExecuteCloudScriptResult> ExecuteCloudScriptAsync(
    string functionName,
    object parameter,
    bool forwardScriptErrors = false)
    {
        var tcs = new TaskCompletionSource<ExecuteCloudScriptResult>();

        ExecuteCloudScript(
            functionName,
            parameter,
            onSuccess: result =>
            {
                if (result.Error != null && !forwardScriptErrors)
                {
                    tcs.TrySetException(new Exception(
                        $"CloudScript Error: {functionName} / {result.Error.Message}"
                    ));
                    return;
                }

                tcs.TrySetResult(result);
            },
            onError: error =>
            {
                tcs.TrySetException(new Exception(error.GenerateErrorReport()));
            },
            forwardScriptErrors: forwardScriptErrors
        );

        return tcs.Task;
    }



    /// <summary>
    /// 로그인 시 플레이어 데이터 초기화
    /// </summary>
    public void LoginInit(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        Debug.Log("InitPlayerData 호출 중...");

        ExecuteCloudScript("InitPlayerData", null,
            result =>
            {
                Debug.Log("InitPlayerData 완료, 모든 플레이어 데이터 로드 시작...");

                // InitPlayerData가 성공하면 RefreshAllPlayerData 호출
                RefreshAllPlayerData(
                    onSuccess: () =>
                    {
                        Debug.Log("로그인 초기화 완료!");

                        // 0623 추가: 로그인 완료 플래그 설정
                        HasAlreadyLoginCache.HasAlreadyLogin = true; // 로그인 완료 플래그 설정

                        onSuccess?.Invoke();
                    },
                    onError: onError
                );
            },
            onError);
    }

    /// <summary>
    /// 모든 플레이어 데이터를 한 번에 가져옴 (통합)
    /// </summary>
    public void RefreshAllPlayerData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        Debug.Log("[PlayFabGateway] 모든 플레이어 데이터 새로고침 시작...");
        LastCshRuntimeState = null;
        PlayFabDataStore.Instance?.MarkServerSnapshotRefreshPending();

        ExecuteCloudScript("GetPlayerAllData", null,
            result =>
            {
                try
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"[PlayFabGateway] 플레이어 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    string json = SerializeFunctionResult(result);

                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("[PlayFabGateway] 플레이어 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"[PlayFabGateway] 플레이어 원본 데이터:\n{json}");

                    // 통합 응답 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<AllPlayerDataWrapper>>(json);

                    if (response == null || !response.success)
                    {
                        Debug.LogError($"[PlayFabGateway] 응답 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    if (response.data == null)
                    {
                        Debug.LogError("[PlayFabGateway] data가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("[PlayFabGateway] PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    // 각 데이터를 PlayFabDataStore에 저장
                    SaveAllPlayerData(response.data);

                    Debug.Log("[PlayFabGateway] 모든 플레이어 데이터 저장 완료!");
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlayFabGateway] 플레이어 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            onError);
    }

    /// <summary>
    /// 통합 데이터를 각각 저장
    /// </summary>
    private void SaveAllPlayerData(AllPlayerDataWrapper data)
    {
        // 1. 인벤토리 저장
        if (data.inventory != null)
        {
            var allInventory = new Dictionary<string, InventoryInfo>();

            // FishInventory 변환
            if (data.inventory.FishInventory != null)
            {
                foreach (var item in data.inventory.FishInventory)
                {
                    allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                }
            }

            // FoodInventory 변환
            if (data.inventory.FoodInventory != null)
            {
                foreach (var item in data.inventory.FoodInventory)
                {
                    allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                }
            }

            // IngredientInventory 변환
            if (data.inventory.IngredientInventory != null)
            {
                foreach (var item in data.inventory.IngredientInventory)
                {
                    allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                }
            }

            // OddmentInventory 변환
            if (data.inventory.OddmentInventory != null)
            {
                foreach (var item in data.inventory.OddmentInventory)
                {
                    allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                }
            }

            Debug.Log($"[PlayFabGateway] 인벤토리 저장: {allInventory.Count}개 아이템");
            PlayFabDataStore.Instance.UpdateInventory(allInventory);
        }

        // 2. 크루 저장
        if (data.crew != null)
        {
            Debug.Log($"[PlayFabGateway] 크루 저장: {data.crew.Count}명");
            PlayFabDataStore.Instance.UpdateCrew(data.crew);
        }

        // 2.1. 크루 슬롯 저장
        if (data.crewSlot != null)
        {
            Debug.Log($"[PlayFabGateway] 크루 슬롯 저장: {data.crewSlot.crewSlots.Count}개");
            PlayFabDataStore.Instance.UpdateCrewSlot(data.crewSlot.crewSlots);
        }

        // 3. 선박 저장
        if (data.ship != null)
        {
            Debug.Log($"[PlayFabGateway] 선박 저장: {data.ship.Count}척");
            PlayFabDataStore.Instance.UpdateShip(data.ship);
        }

        // 4. 장비 저장
        if (data.equipment != null)
        {
            Debug.Log($"[PlayFabGateway] 장비 저장: {data.equipment.Count}개");
            PlayFabDataStore.Instance.UpdateEquipment(data.equipment);
        }

        // 5. 요리 슬롯 저장
        if (data.cookSlot != null && data.cookSlot.cookSlots != null)
        {
            Debug.Log($"[PlayFabGateway] 요리 저장: {data.cookSlot.cookSlots.Count}개");
            PlayFabDataStore.Instance.UpdateCooking(data.cookSlot.cookSlots);
        }

        // 6. 화폐 저장
        if (data.currency != null)
        {
            Debug.Log($"[PlayFabGateway] 화폐 저장: {data.currency}");
            PlayFabDataStore.Instance.UpdateMoney(data.currency);
        }

        // 7. 스테이지 저장
        if (data.stage != null)
        {
            Debug.Log($"[PlayFabGateway] 스테이지 저장: 현재 스테이지 {data.stage.currentStageId}, 기여도 {data.stage.contribution}");
            PlayFabDataStore.Instance.UpdateStage(data.stage);
        }

        // 8. 멀티플레이 제한 저장
        if (data.multiplayLimit != null)
        {
            Debug.Log($"[PlayFabGateway] 멀티플레이 제한 저장: {data.multiplayLimit.playCount}/{data.multiplayLimit.maxPlayCount}, canPlay: {data.multiplayLimit.canPlay}");
            PlayFabDataStore.Instance.UpdateMultiplayLimit(data.multiplayLimit);
        }

        // 9. 튜토리얼 데이터 저장
        if (data.tutorialData != null)
        {
            Debug.Log($"[PlayFabGateway] 튜토리얼 데이터 저장: {data.tutorialData}");
            PlayFabDataStore.Instance.UpdateTutorial(data.tutorialData);
        }

        if (data.cshRuntimeState != null)
        {
            LastCshRuntimeState = data.cshRuntimeState;
            CshRuntimeStateReceived?.Invoke(data.cshRuntimeState);
        }
    }

    public string SerializeFunctionResult(ExecuteCloudScriptResult result)
    {
        try
        {
            if (result?.FunctionResult == null)
            {
                Debug.LogWarning("SerializeFunctionResult: FunctionResult가 null입니다.");
                return string.Empty;
            }

            return PluginManager
                .GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer)
                .SerializeObject(result.FunctionResult);
        }
        catch (Exception e)
        {
            Debug.LogError($"FunctionResult 직렬화 실패: {e.Message}\n{e.StackTrace}");
            return string.Empty;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private GameObject GetPersistentRootForDontDestroyOnLoad()
    {
        return transform.root == null ? gameObject : transform.root.gameObject;
    }
}
