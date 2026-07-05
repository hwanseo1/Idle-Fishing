using System;
using System.Collections;
using PlayFab;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace RMS.Multiplay
{
    public class MultiPlaySceneLoader : MonoBehaviour
    {
        private Coroutine _loadRoutine;

        public void LoadMultiPlayScene()
        {
            if (_loadRoutine != null)
            {
                return;
            }

            _loadRoutine = StartCoroutine(LoadMultiPlaySceneRoutine());
        }

        private System.Collections.IEnumerator LoadMultiPlaySceneRoutine()
        {
            bool canLoad = false;
            yield return FisherInventorySceneTransitionGuard.FlushAndRefreshBeforeSceneLoad(
                this,
                "LoadScene(1)",
                result => canLoad = result);

            _loadRoutine = null;
            if (!canLoad)
            {
                Debug.LogWarning("[RMS.MultiPlaySceneLoader] 멀티 씬 이동 전 인벤토리 동기화에 실패해 LoadScene(1)을 중단합니다.");
                yield break;
            }

            SceneManager.LoadScene(1);
        }
    }

    internal static class FisherInventorySceneTransitionGuard
    {
        private const float OperationTimeoutSeconds = 15f;

        public static IEnumerator FlushAndRefreshBeforeSceneLoad(
            MonoBehaviour owner,
            string reason,
            Action<bool> onComplete)
        {
            PlayFabGateway gateway = PlayFabGateway.Instance;
            InventoryGateway inventory = gateway == null ? null : gateway.Inventory;

            if (inventory == null)
            {
                onComplete?.Invoke(true);
                yield break;
            }

            bool hasPending = inventory.HasPendingActions();

            if (!hasPending)
            {
                onComplete?.Invoke(true);
                yield break;
            }

            if (!PlayFabClientAPI.IsClientLoggedIn())
            {
                Debug.LogWarning("[RMS.SceneTransitionGuard] Pending inventory queue exists but PlayFab client is not logged in. reason=" + reason);
                onComplete?.Invoke(false);
                yield break;
            }

            bool flushSucceeded = false;
            string flushFailure = string.Empty;
            yield return WaitForFlush(owner, inventory, reason, value => flushSucceeded = value, value => flushFailure = value);

            if (!flushSucceeded)
            {
                Debug.LogWarning("[RMS.SceneTransitionGuard] Inventory flush before scene load failed: " + flushFailure);
                onComplete?.Invoke(false);
                yield break;
            }

            bool refreshSucceeded = false;
            string refreshFailure = string.Empty;
            yield return WaitForInventoryRefresh(owner, inventory, reason, value => refreshSucceeded = value, value => refreshFailure = value);

            if (!refreshSucceeded)
            {
                Debug.LogWarning("[RMS.SceneTransitionGuard] Inventory refresh before scene load failed: " + refreshFailure);
                onComplete?.Invoke(false);
                yield break;
            }

            onComplete?.Invoke(true);
        }

        private static IEnumerator WaitForFlush(
            MonoBehaviour owner,
            InventoryGateway inventory,
            string reason,
            Action<bool> onComplete,
            Action<string> onFailure)
        {
            bool completed = false;
            bool succeeded = false;
            string failure = string.Empty;

            inventory.FlushAll(
                _ =>
                {
                    if (completed)
                    {
                        return;
                    }

                    succeeded = true;
                    completed = true;
                },
                message =>
                {
                    if (completed)
                    {
                        return;
                    }

                    failure = string.IsNullOrWhiteSpace(message) ? "flush-failed" : message;
                    completed = true;
                });

            yield return WaitForCompletion(owner, "flush", reason, () => completed);

            if (!completed)
            {
                failure = "flush-timeout";
                completed = true;
            }

            onComplete?.Invoke(succeeded);
            onFailure?.Invoke(failure);
        }

        private static IEnumerator WaitForInventoryRefresh(
            MonoBehaviour owner,
            InventoryGateway inventory,
            string reason,
            Action<bool> onComplete,
            Action<string> onFailure)
        {
            bool completed = false;
            bool succeeded = false;
            string failure = string.Empty;

            inventory.RefreshInventoryData(
                () =>
                {
                    if (completed)
                    {
                        return;
                    }

                    succeeded = true;
                    completed = true;
                },
                error =>
                {
                    if (completed)
                    {
                        return;
                    }

                    failure = error == null ? "refresh-failed" : error.ErrorMessage;
                    completed = true;
                });

            yield return WaitForCompletion(owner, "refresh", reason, () => completed);

            if (!completed)
            {
                failure = "refresh-timeout";
                completed = true;
            }

            onComplete?.Invoke(succeeded);
            onFailure?.Invoke(failure);
        }

        private static IEnumerator WaitForCompletion(
            MonoBehaviour owner,
            string operation,
            string reason,
            Func<bool> isCompleted)
        {
            float deadline = Time.unscaledTime + OperationTimeoutSeconds;
            while (!isCompleted() && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            if (!isCompleted())
            {
                Debug.LogWarning("[RMS.SceneTransitionGuard] Inventory " + operation + " before scene load timed out. reason=" + reason);
            }
        }
    }
}

