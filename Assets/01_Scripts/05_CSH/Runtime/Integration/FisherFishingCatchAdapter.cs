using JHS.Fishing;
using RMS.Data;
using Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// JHS 낚시 포획 이벤트를 Fisher 가방/도감 지급 흐름으로 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FisherFishingCatchAdapter : MonoBehaviour
    {
        #region Inspector References

        [SerializeField] private FisherRuntimeContext _context;
        [SerializeField] private AutoFishingController _autoFishingController;
        [SerializeField] private int _catchItemCount = 1;
        [SerializeField] private bool _registerCollectionDiscovery = true;
        [SerializeField] private bool _warnIfAutoFishingControllerMissing = false;
        [SerializeField] private bool _warnWhenCatchGrantBlocked = true;
        [SerializeField] private RuntimeState[] _blockedRuntimeStates =
        {
            RuntimeState.BOSSSTAGE,
            RuntimeState.MULTIMATCHING,
            RuntimeState.MULTISTAGE
        };
        [SerializeField] private string[] _blockedSceneNameFragments =
        {
            "MultiPlay",
            "multiplay_arena"
        };

        private bool _autoFishingControllerResolvedLogged;
        private RuntimeStateController _runtimeStateController;
        private string _lastBlockedGrantLogKey;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        #endregion

        #region Configuration

        /// <summary>
        /// 부트스트래퍼나 테스트 코드에서 런타임 컨텍스트를 주입합니다.
        /// </summary>
        public void Configure(FisherRuntimeContext context)
        {
            _context = context;
            ResolveReferences();
            Subscribe();
        }

        #endregion

        #region Event Binding

        private void Subscribe()
        {
            if (_autoFishingController == null)
            {
                return;
            }

            _autoFishingController.OnFishCaught -= OnFishCaught;
            _autoFishingController.OnFishCaught += OnFishCaught;
        }

        private void Unsubscribe()
        {
            if (_autoFishingController != null)
            {
                _autoFishingController.OnFishCaught -= OnFishCaught;
            }
        }

        private void ResolveReferences()
        {
            if (_context == null)
            {
                Debug.LogWarning("[FisherFishingCatchAdapter] FisherRuntimeContext is not assigned. Wire it under the CSH control tower.", this);
            }

            if (_autoFishingController == null)
            {
                _autoFishingController = ResolveAutoFishingControllerInScene();
            }

            if (_runtimeStateController == null)
            {
                _runtimeStateController = FindFirstObjectByType<RuntimeStateController>();
            }

            if (_autoFishingController == null)
            {
                if (_warnIfAutoFishingControllerMissing)
                {
                    Debug.LogWarning("[FisherFishingCatchAdapter] AutoFishingController is not assigned. Wire it explicitly if fish catch sync is needed.", this);
                }
            }
            else if (!_autoFishingControllerResolvedLogged)
            {
                _autoFishingControllerResolvedLogged = true;
                Debug.Log("[FisherFishingCatchAdapter] AutoFishingController connected: " + GetPath(_autoFishingController.transform), this);
            }

            if (_catchItemCount < 1)
            {
                _catchItemCount = 1;
            }
        }

        /// <summary>
        /// Prefab에서는 씬 오브젝트를 직접 참조할 수 없으므로, 배치된 씬과 현재 active scene에서 낚시 컨트롤러를 조회합니다.
        /// </summary>
        private AutoFishingController ResolveAutoFishingControllerInScene()
        {
            AutoFishingController controller = ResolveAutoFishingControllerInScene(gameObject.scene);
            if (controller != null)
            {
                return controller;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene != gameObject.scene)
            {
                return ResolveAutoFishingControllerInScene(activeScene);
            }

            return null;
        }

        private static AutoFishingController ResolveAutoFishingControllerInScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                AutoFishingController controller = roots[i].GetComponentInChildren<AutoFishingController>(true);
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// 런타임 연결 로그에서 어떤 기존 씬 오브젝트에 붙었는지 확인하기 위한 경로 문자열입니다.
        /// </summary>
        private static string GetPath(Transform target)
        {
            if (target == null)
            {
                return "(null)";
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        #endregion

        #region Catch Handling

        private void OnFishCaught(FishData fishData, float contribution, bool isManual)
        {
            ServiceResult result = ApplyCaughtFish(fishData, _catchItemCount);
            if (!result.Success && result.MessageKey == "fishing.grant_blocked")
            {
                LogCatchGrantBlockedOnce(fishData == null ? string.Empty : fishData.FishId, result.FailureReason);
                return;
            }

            Debug.Log("[FisherFishingCatchAdapter] Catch " +
                      (fishData == null ? "null" : fishData.FishId) +
                      " manual=" + isManual +
                      " contribution=" + contribution.ToString("0.##") +
                      " result=" + result.MessageKey);
        }

        /// <summary>
        /// RMS FishData의 fishId를 Fisher itemId로 보고 가방 지급과 도감 발견을 갱신합니다.
        /// </summary>
        public ServiceResult ApplyCaughtFish(FishData fishData, int count = 1)
        {
            if (fishData == null)
            {
                return ServiceResult.Fail("Caught fish data is null.", "fishing.null_fish");
            }

            return ApplyCaughtFishId(fishData.FishId, count);
        }

        /// <summary>
        /// 포획된 fishId/itemId를 지정 수량만큼 지급합니다.
        /// 희귀도, 골드 보상, 포획 수량 산정은 이 어댑터가 아니라 낚시/밸런스 쪽 책임입니다.
        /// </summary>
        public ServiceResult ApplyCaughtFishId(string fishId, int count = 1)
        {
            if (!CanGrantCaughtFish(out string blockReason))
            {
                return ServiceResult.Fail(blockReason, "fishing.grant_blocked");
            }

            if (_context == null || !_context.IsReady)
            {
                return ServiceResult.Fail("Fisher runtime context is not ready.", "fishing.context_not_ready");
            }

            if (string.IsNullOrWhiteSpace(fishId))
            {
                return ServiceResult.Fail("Caught fishId is empty.", "fishing.empty_fish_id");
            }

            if (count <= 0)
            {
                return ServiceResult.Fail("Caught fish count must be positive.", "fishing.invalid_count");
            }

            ServiceResult add = _context.InventoryService.TryAddItem(fishId, count);
            if (!add.Success)
            {
                return add;
            }

            FisherPlayerDataBridge bridge = FisherPlayerDataBridgeResolver.Resolve(_context, this);
            if (bridge != null && !bridge.SyncItemDeltasToPlayerData(add))
            {
                Debug.LogWarning("[FisherFishingCatchAdapter] PlayerData item sync failed after catch: " + fishId);
            }

            if (_registerCollectionDiscovery)
            {
                _context.CollectionService.TryRegisterCatalogDiscovery(fishId);
            }

            _context.NotifyRuntimeChanged();
            return add;
        }

        /// <summary>
        /// 멀티/보스 등 일반 낚시가 아닌 상태에서 CSH 가방 지급이 섞이지 않도록 차단합니다.
        /// </summary>
        private bool CanGrantCaughtFish(out string blockReason)
        {
            if (IsBlockedRuntimeState(out blockReason))
            {
                return false;
            }

            if (IsBlockedScene(out blockReason))
            {
                return false;
            }

            blockReason = string.Empty;
            return true;
        }

        /// <summary>
        /// RuntimeState가 명확히 멀티/특수 진행 상태면 지급하지 않습니다.
        /// </summary>
        private bool IsBlockedRuntimeState(out string blockReason)
        {
            RuntimeStateController controller = _runtimeStateController;
            if (controller == null)
            {
                controller = FindFirstObjectByType<RuntimeStateController>();
                _runtimeStateController = controller;
            }

            if (controller == null || _blockedRuntimeStates == null)
            {
                blockReason = string.Empty;
                return false;
            }

            RuntimeState currentState = controller.CurrentState;
            for (int i = 0; i < _blockedRuntimeStates.Length; i++)
            {
                if (_blockedRuntimeStates[i] == currentState)
                {
                    blockReason = "Fishing catch grant is blocked in runtime state: " + currentState;
                    return true;
                }
            }

            blockReason = string.Empty;
            return false;
        }

        /// <summary>
        /// CSH prefab이 멀티 전용 씬에 남아 있을 때 active scene 기준으로 지급을 막습니다.
        /// </summary>
        private bool IsBlockedScene(out string blockReason)
        {
            if (_blockedSceneNameFragments == null || _blockedSceneNameFragments.Length == 0)
            {
                blockReason = string.Empty;
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Scene objectScene = gameObject.scene;

            for (int i = 0; i < _blockedSceneNameFragments.Length; i++)
            {
                string fragment = _blockedSceneNameFragments[i];
                if (string.IsNullOrWhiteSpace(fragment))
                {
                    continue;
                }

                if (SceneNameContains(activeScene, fragment) || SceneNameContains(objectScene, fragment))
                {
                    blockReason = "Fishing catch grant is blocked in scene: " + activeScene.name;
                    return true;
                }
            }

            blockReason = string.Empty;
            return false;
        }

        private static bool SceneNameContains(Scene scene, string fragment)
        {
            return scene.IsValid() &&
                   !string.IsNullOrEmpty(scene.name) &&
                   scene.name.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogCatchGrantBlockedOnce(string fishId, string reason)
        {
            if (!_warnWhenCatchGrantBlocked)
            {
                return;
            }

            string logKey = fishId + "|" + reason;
            if (_lastBlockedGrantLogKey == logKey)
            {
                return;
            }

            _lastBlockedGrantLogKey = logKey;
            Debug.LogWarning("[FisherFishingCatchAdapter] Catch grant blocked: " + reason + " fishId=" + fishId, this);
        }

        #endregion
    }
}
