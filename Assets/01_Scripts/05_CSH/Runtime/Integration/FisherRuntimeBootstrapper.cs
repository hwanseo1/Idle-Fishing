using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 작업 씬에 이미 존재하는 패널을 Fisher 서비스 UI 어댑터와 연결합니다.
    /// </summary>
    public static class FisherRuntimeBootstrapper
    {
        #region Scene Gate

        private const string WorkSceneName = "05_CSH";
        private const string MainSceneName = "00_MainScene";

        #endregion

        #region Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!ShouldBootstrap(activeScene))
            {
                return;
            }

            FisherRuntimeControlTower controlTower = FindComponentInScene<FisherRuntimeControlTower>(activeScene);
            if (controlTower == null)
            {
                Debug.LogWarning("[FisherRuntimeBootstrapper] " + activeScene.name + " requires a pre-wired FisherRuntimeControlTower. Runtime auto-wiring skipped.");
                return;
            }

            controlTower.Bootstrap();
        }

        #endregion

        #region Scene Guard

        private static bool ShouldBootstrap(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            return string.Equals(scene.name, WorkSceneName, StringComparison.Ordinal) ||
                   string.Equals(scene.name, MainSceneName, StringComparison.Ordinal);
        }

        #endregion

        #region Scene Search

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// CSH control tower 아래에서 FisherPlayerDataBridge 위치가 바뀌어도 패널들이 같은 bridge를 쓰게 합니다.
    /// </summary>
    internal static class FisherPlayerDataBridgeResolver
    {
        private const string CshRootName = "CSH";

        public static FisherPlayerDataBridge Resolve(FisherRuntimeContext context, Component owner = null)
        {
            FisherPlayerDataBridge bridge = ResolveFromContext(context);
            if (bridge == null && owner != null)
            {
                bridge = owner.GetComponent<FisherPlayerDataBridge>();
            }

            return bridge;
        }

        private static FisherPlayerDataBridge ResolveFromContext(FisherRuntimeContext context)
        {
            if (context == null)
            {
                return null;
            }

            FisherPlayerDataBridge bridge = context.GetComponent<FisherPlayerDataBridge>();
            if (bridge != null)
            {
                return bridge;
            }

            Transform controlRoot = ResolveControlRoot(context.transform);
            return controlRoot == null ? null : controlRoot.GetComponentInChildren<FisherPlayerDataBridge>(true);
        }

        private static Transform ResolveControlRoot(Transform contextTransform)
        {
            Transform current = contextTransform;
            Transform fallback = contextTransform;
            while (current != null)
            {
                fallback = current;
                if (string.Equals(current.name, CshRootName, StringComparison.Ordinal))
                {
                    return current;
                }

                current = current.parent;
            }

            return fallback;
        }
    }
}
