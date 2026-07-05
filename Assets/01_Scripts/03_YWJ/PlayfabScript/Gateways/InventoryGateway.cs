using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

[Serializable]
public class InventoryAction
{
    public string action;
    public string itemId;
    public int amount;
}

public class InventoryGateway
{
    private readonly PlayFabGateway gateway;
    
    // 각 인벤토리별 액션 딕셔너리 (Add, Remove, Sell)
    private readonly Dictionary<string, InventoryActionQueues> queues = new();
    private readonly object queueLock = new object();

    private const string FishInventory = "FishInventory";
    private const string FoodInventory = "FoodInventory";
    private const string IngredientInventory = "IngredientInventory";
    private const string OddmentInventory = "OddmentInventory";

    private const string FishPrefix = "fish_";
    private const string FoodPrefix = "food_";
    private const string IngredientPrefix = "mat_";
    private const string TicketPrefix = "ticket_";
    private const string BoxPrefix = "box_";
    private const string FragmentPrefix = "fragment_";

    private bool isProcessing = false;

    // 각 액션별 Dictionary 관리 클래스
    private class InventoryActionQueues
    {
        public Dictionary<string, int> AddQueue = new();
        public Dictionary<string, int> RemoveQueue = new();
        public Dictionary<string, int> SellQueue = new();
    }

    public InventoryGateway(PlayFabGateway gateway)
    {
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        queues[FishInventory] = new InventoryActionQueues();
        queues[FoodInventory] = new InventoryActionQueues();
        queues[IngredientInventory] = new InventoryActionQueues();
        queues[OddmentInventory] = new InventoryActionQueues();

    }

    public bool HasPendingActions()
    {
        lock (queueLock)
        {
            foreach (InventoryActionQueues actionQueues in queues.Values)
            {
                if (actionQueues.AddQueue.Count > 0 ||
                    actionQueues.RemoveQueue.Count > 0 ||
                    actionQueues.SellQueue.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void RefreshInventoryData(Action onSuccess = null, Action<PlayFabError> onError = null)
    {
        gateway.ExecuteCloudScript("GetPlayerInventoryData", null, 
            result =>
            {
                try
                {
                    if (result.Error != null)
                    {
                        Debug.LogError($"인벤토리 데이터 가져오기 실패: {result.Error.Message}");
                        onError?.Invoke(null);
                        return;
                    }

                    string json = gateway.SerializeFunctionResult(result);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        Debug.LogError("인벤토리 데이터가 비어있습니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"인벤토리 원본 데이터: {json}");

                    // ✅ 서버 응답 구조에 맞게 파싱
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<InventoryDataWrapper>>(json);

                    if (response == null || !response.success)
                    {
                        Debug.LogError($"인벤토리 응답 실패 (success: {response?.success})");
                        onError?.Invoke(null);
                        return;
                    }

                    if (response.data == null)
                    {
                        Debug.LogError("인벤토리 data가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    // ✅ 서버 형식(Dictionary<string, int>)을 클라이언트 형식(Dictionary<string, InventoryInfo>)으로 변환
                    var allInventory = new Dictionary<string, InventoryInfo>();

                    // FishInventory 변환
                    if (response.data.FishInventory != null)
                    {
                        foreach (var item in response.data.FishInventory)
                        {
                            allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                        }
                    }

                    // FoodInventory 변환
                    if (response.data.FoodInventory != null)
                    {
                        foreach (var item in response.data.FoodInventory)
                        {
                            allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                        }
                    }

                    // IngredientInventory 변환
                    if (response.data.IngredientInventory != null)
                    {
                        foreach (var item in response.data.IngredientInventory)
                        {
                            allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                        }
                    }

                    // OddmentInventory 변환
                    if (response.data.OddmentInventory != null)
                    {
                        foreach (var item in response.data.OddmentInventory)
                        {
                            allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
                        }
                    }

                    if (PlayFabDataStore.Instance == null)
                    {
                        Debug.LogError("PlayFabDataStore.Instance가 null입니다.");
                        onError?.Invoke(null);
                        return;
                    }

                    Debug.Log($"인벤토리 총 아이템 개수: {allInventory.Count}");
                    foreach (var item in allInventory)
                    {
                        Debug.Log($"  - {item.Key}: {item.Value.itemCount}개");
                    }

                    PlayFabDataStore.Instance.UpdateInventory(allInventory);
                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"인벤토리 데이터 처리 중 오류: {e.Message}\n{e.StackTrace}");
                    onError?.Invoke(null);
                }
            },
            onError);
    }

    public async Task RefreshInventoryDataAsync()
    {
        var result = await gateway.ExecuteCloudScriptAsync("GetPlayerInventoryData", null);

        if (result.Error != null)
        {
            throw new Exception($"인벤토리 데이터 가져오기 실패: {result.Error.Message}");
        }

        string json = gateway.SerializeFunctionResult(result);

        if (string.IsNullOrEmpty(json))
        {
            throw new Exception("인벤토리 데이터가 비어있습니다.");
        }

        Debug.Log($"인벤토리 원본 데이터: {json}");

        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayFabDataResponse<InventoryDataWrapper>>(json);

        if (response == null || !response.success)
        {
            throw new Exception($"인벤토리 응답 실패 (success: {response?.success})");
        }

        if (response.data == null)
        {
            throw new Exception("인벤토리 data가 null입니다.");
        }

        var allInventory = new Dictionary<string, InventoryInfo>();

        // FishInventory 변환
        if (response.data.FishInventory != null)
        {
            foreach (var item in response.data.FishInventory)
            {
                allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
            }
        }

        // FoodInventory 변환
        if (response.data.FoodInventory != null)
        {
            foreach (var item in response.data.FoodInventory)
            {
                allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
            }
        }

        // IngredientInventory 변환
        if (response.data.IngredientInventory != null)
        {
            foreach (var item in response.data.IngredientInventory)
            {
                allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
            }
        }

        // OddmentInventory 변환
        if (response.data.OddmentInventory != null)
        {
            foreach (var item in response.data.OddmentInventory)
            {
                allInventory[item.Key] = new InventoryInfo { itemCount = item.Value };
            }
        }

        if (PlayFabDataStore.Instance == null)
        {
            throw new Exception("PlayFabDataStore.Instance가 null입니다.");
        }

        Debug.Log($"인벤토리 총 아이템 개수: {allInventory.Count}");
        PlayFabDataStore.Instance.UpdateInventory(allInventory);
    }

    public void Add(string itemId, int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Add: amount는 0보다 커야 합니다. (itemId: {itemId}, amount: {amount})");
            return;
        }
        EnqueueToDictionary("Add", itemId, amount);
    }

    public void Remove(string itemId, int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Remove: amount는 0보다 커야 합니다. (itemId: {itemId}, amount: {amount})");
            return;
        }
        EnqueueToDictionary("Remove", itemId, amount);
    }

    public void Sell(string itemId, int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Sell: amount는 0보다 커야 합니다. (itemId: {itemId}, amount: {amount})");
            return;
        }
        EnqueueToDictionary("Sell", itemId, amount);
    }

    public void UseBox(
        string boxItemId,
        int count,
        string selectedItemId = null,
        string selectedInventoryKey = null,
        string requestId = null,
        Action<ExecuteCloudScriptResult> onSuccess = null,
        Action<PlayFabError> onError = null,
        bool forwardScriptErrors = false)
    {
        if (string.IsNullOrWhiteSpace(boxItemId))
        {
            Debug.LogWarning("UseBox: boxItemId가 비어있습니다.");
            return;
        }

        if (count <= 0)
        {
            Debug.LogWarning($"UseBox: count는 0보다 커야 합니다. (boxItemId: {boxItemId}, count: {count})");
            return;
        }

        string safeRequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId;
        gateway.ExecuteCloudScript(
            "UseBox",
            new
            {
                boxItemId,
                itemId = boxItemId,
                count,
                selectedItemId = string.IsNullOrWhiteSpace(selectedItemId) ? null : selectedItemId,
                selectedInventoryKey = string.IsNullOrWhiteSpace(selectedInventoryKey) ? null : selectedInventoryKey,
                requestId = safeRequestId
            },
            result =>
            {
                Debug.Log($"UseBox CloudScript 결과: {result.FunctionResult}");
                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"UseBox 실패: {error.GenerateErrorReport()}");
                onError?.Invoke(error);
            },
            forwardScriptErrors);
    }

    private void EnqueueToDictionary(string action, string itemId, int amount)
    {
        try
        {
            string inventoryKey = GetInventoryKeyByItemId(itemId);

            lock (queueLock)
            {
                var actionQueues = queues[inventoryKey];
                Dictionary<string, int> targetDict = null;

                switch (action)
                {
                    case "Add":
                        targetDict = actionQueues.AddQueue;
                        break;
                    case "Remove":
                        targetDict = actionQueues.RemoveQueue;
                        break;
                    case "Sell":
                        targetDict = actionQueues.SellQueue;
                        break;
                    default:
                        Debug.LogError($"알 수 없는 액션: {action}");
                        return;
                }

                // 기존 수량에 합산
                if (targetDict.ContainsKey(itemId))
                {
                    targetDict[itemId] += amount;
                    Debug.Log($"Queue 합산: {inventoryKey} / {action} / {itemId} x{amount} (합계: {targetDict[itemId]})");
                }
                else
                {
                    targetDict[itemId] = amount;
                    Debug.Log($"Queue 추가: {inventoryKey} / {action} / {itemId} x{amount}");
                }

            }
        }
        catch (Exception e)
        {
            Debug.LogError($"EnqueueToDictionary 실패: {e.Message}");
        }
    }

    private string GetInventoryKeyByItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            throw new ArgumentException("itemId가 비어있습니다.", nameof(itemId));

        if (itemId.StartsWith(FishPrefix))
            return FishInventory;

        if (itemId.StartsWith(FoodPrefix))
            return FoodInventory;

        if (itemId.StartsWith(IngredientPrefix))
            return IngredientInventory;

        if (itemId.StartsWith(TicketPrefix))
            return OddmentInventory;

        if (itemId.StartsWith(BoxPrefix))
            return OddmentInventory;
        
        if (itemId.StartsWith(FragmentPrefix))
            return OddmentInventory;

        throw new ArgumentException($"알 수 없는 itemId prefix입니다: {itemId}", nameof(itemId));
    }

    /// <summary>
    /// 현재 대기 중인 모든 인벤토리 queue를 빈 queue 경고 없이 순차적으로 서버에 반영합니다.
    /// </summary>
    public void FlushAll(Action<ExecuteCloudScriptResult> onSuccess = null, Action<string> onFailure = null)
    {
        if (isProcessing)
        {
            Debug.LogWarning("이미 Flush가 진행 중입니다.");
            onFailure?.Invoke("이미 Flush가 진행 중입니다.");
            return;
        }

        List<string> pendingInventoryKeys = GetPendingInventoryKeys();
        if (pendingInventoryKeys.Count == 0)
        {
            Debug.Log("Inventory FlushAll: 처리할 Queue가 없습니다.");
            onSuccess?.Invoke(null);
            return;
        }

        FlushPendingInventoryKeys(pendingInventoryKeys, 0, onSuccess, onFailure);
    }

    private List<string> GetPendingInventoryKeys()
    {
        List<string> pendingInventoryKeys = new();
        lock (queueLock)
        {
            foreach (var pair in queues)
            {
                InventoryActionQueues actionQueues = pair.Value;
                if (actionQueues.AddQueue.Count > 0 ||
                    actionQueues.RemoveQueue.Count > 0 ||
                    actionQueues.SellQueue.Count > 0)
                {
                    pendingInventoryKeys.Add(pair.Key);
                }
            }
        }

        return pendingInventoryKeys;
    }

    private void FlushPendingInventoryKeys(
        List<string> inventoryKeys,
        int index,
        Action<ExecuteCloudScriptResult> onSuccess,
        Action<string> onFailure)
    {
        if (inventoryKeys == null || index >= inventoryKeys.Count)
        {
            onSuccess?.Invoke(null);
            return;
        }

        string inventoryKey = inventoryKeys[index];
        Flush(
            inventoryKey,
            result =>
            {
                if (index >= inventoryKeys.Count - 1)
                {
                    onSuccess?.Invoke(result);
                    return;
                }

                FlushPendingInventoryKeys(inventoryKeys, index + 1, onSuccess, onFailure);
            },
            onFailure);
    }

    public void Flush(string inventoryKey, Action<ExecuteCloudScriptResult> onSuccess = null, Action<string> onFailure = null)
    {
        if (!queues.ContainsKey(inventoryKey))
        {
            string message = $"Invalid inventoryKey: {inventoryKey}";
            Debug.LogError(message);
            onFailure?.Invoke(message);
            return;
        }

        List<InventoryAction> actions;
        
        lock (queueLock)
        {
            var actionQueues = queues[inventoryKey];

            // Dictionary를 InventoryAction 리스트로 변환
            actions = new List<InventoryAction>();

            // Add 액션 변환
            foreach (var kvp in actionQueues.AddQueue)
            {
                actions.Add(new InventoryAction
                {
                    action = "Add",
                    itemId = kvp.Key,
                    amount = kvp.Value
                });
            }

            // Remove 액션 변환
            foreach (var kvp in actionQueues.RemoveQueue)
            {
                actions.Add(new InventoryAction
                {
                    action = "Remove",
                    itemId = kvp.Key,
                    amount = kvp.Value
                });
            }

            // Sell 액션 변환
            foreach (var kvp in actionQueues.SellQueue)
            {
                actions.Add(new InventoryAction
                {
                    action = "Sell",
                    itemId = kvp.Key,
                    amount = kvp.Value
                });
            }

            Debug.Log($"Inventory Flush 호출됨 / {inventoryKey} / 총 액션 수: {actions.Count}");

            if (actions.Count == 0)
            {
                Debug.Log($"{inventoryKey} Queue가 비어 있어서 서버 호출 안 함");
                onSuccess?.Invoke(null);
                return;
            }

            // Dictionary 초기화
            actionQueues.AddQueue.Clear();
            actionQueues.RemoveQueue.Clear();
            actionQueues.SellQueue.Clear();

            foreach (var a in actions)
            {
                Debug.Log(
                    $"[Flush Payload] " +
                    $"inventoryKey={inventoryKey}, " +
                    $"action={a.action}, " +
                    $"itemId={a.itemId}, " +
                    $"amount={a.amount}");
            }
        }

        isProcessing = true;

        gateway.ExecuteCloudScript(
            "HandleInventoryItems",
            new
            {
                inventoryKey = inventoryKey,
                actions = actions
            },
            result =>
            {
                isProcessing = false;

                if (result.Error != null)
                {
                    Debug.LogError($"Error: {result.Error.Error}");
                    Debug.LogError($"Message: {result.Error.Message}");
                    Debug.LogError($"StackTrace: {result.Error.StackTrace}");

                    // 실패 시 롤백
                    lock (queueLock)
                    {
                        var actionQueues = queues[inventoryKey];
                        foreach (var action in actions)
                        {
                            Dictionary<string, int> targetDict = action.action switch
                            {
                                "Add" => actionQueues.AddQueue,
                                "Remove" => actionQueues.RemoveQueue,
                                "Sell" => actionQueues.SellQueue,
                                _ => null
                            };

                            if (targetDict != null)
                            {
                                if (targetDict.ContainsKey(action.itemId))
                                    targetDict[action.itemId] += action.amount;
                                else
                                    targetDict[action.itemId] = action.amount;
                            }
                        }
                    }
                    onFailure?.Invoke($"HandleInventoryItems CloudScript 오류: {result.Error.Message}");
                    return;
                }

                Debug.Log($"CloudScript 결과: {result.FunctionResult}");
                ApplyActionsToLocalInventory(actions); // 로컬 인벤토리에 반영
                onSuccess?.Invoke(result);
            },
            error =>
            {
                isProcessing = false;
                string message = $"HandleInventoryItems 실패: {error.GenerateErrorReport()}";
                Debug.LogError(message);
                
                // 실패 시 롤백
                lock (queueLock)
                {
                    var actionQueues = queues[inventoryKey];
                    foreach (var action in actions)
                    {
                        Dictionary<string, int> targetDict = action.action switch
                        {
                            "Add" => actionQueues.AddQueue,
                            "Remove" => actionQueues.RemoveQueue,
                            "Sell" => actionQueues.SellQueue,
                            _ => null
                        };

                        if (targetDict != null)
                        {
                            if (targetDict.ContainsKey(action.itemId))
                                targetDict[action.itemId] += action.amount;
                            else
                                targetDict[action.itemId] = action.amount;
                        }
                    }
                }
                onFailure?.Invoke(message);
            },
            forwardScriptErrors: true);
    }


    private void ApplyActionsToLocalInventory(List<InventoryAction> actions)
    {
        if (actions == null || actions.Count == 0)
            return;

        if (PlayFabDataStore.Instance == null)
        {
            Debug.LogError("[InventoryGateway] PlayFabDataStore.Instance가 없습니다.");
            return;
        }

        PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

        if (player == null)
        {
            Debug.LogError("[InventoryGateway] PlayerInfo가 없습니다.");
            return;
        }

        if (player.inventory == null)
            player.inventory = new InventoryJSONModel();

        if (player.inventory.inventoryItems == null)
            player.inventory.inventoryItems = new Dictionary<string, InventoryInfo>();

        Dictionary<string, InventoryInfo> inventory =
            new Dictionary<string, InventoryInfo>(player.inventory.inventoryItems);

        foreach (InventoryAction action in actions)
        {
            if (string.IsNullOrEmpty(action.itemId))
                continue;

            if (!inventory.TryGetValue(action.itemId, out InventoryInfo info))
            {
                info = new InventoryInfo { itemCount = 0 };
                inventory[action.itemId] = info;
            }

            switch (action.action)
            {
                case "Add":
                    info.itemCount += action.amount;
                    break;

                case "Remove":
                case "Sell":
                    info.itemCount -= action.amount;
                    break;
            }

            if (info.itemCount <= 0)
                inventory.Remove(action.itemId);
        }

        PlayFabDataStore.Instance.UpdateInventory(inventory);

        Debug.Log($"[InventoryGateway] 로컬 인벤토리 즉시 반영 완료 ({actions.Count}개 Action)");
    }
}
