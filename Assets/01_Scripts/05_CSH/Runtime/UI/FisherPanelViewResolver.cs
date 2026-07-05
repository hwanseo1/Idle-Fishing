using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 런타임 패널 어댑터가 기존 Inspector ViewRoot만 바인딩하게 하는 read-only resolver입니다.
    /// 구조 생성/보정은 FisherStaticViewFactory/editor builder 책임이며 gameplay runtime에서는 호출하지 않습니다.
    /// </summary>
    internal static class FisherPanelViewResolver
    {
        public static bool TryResolveExistingView(
            GameObject panel,
            FisherPanelView assignedView,
            string panelRootName,
            string adapterName,
            FisherSlotLayout slotLayout,
            FisherUiArtProfile fallbackArtProfile,
            out FisherPanelView view,
            out FisherUiArtProfile artProfile)
        {
            view = assignedView;
            artProfile = fallbackArtProfile;

            if (view == null && panel != null)
            {
                view = FindViewRoot(panel, panelRootName);
            }

            if (view == null)
            {
                Debug.LogWarning("[" + adapterName + "] " + panelRootName + "/ViewRoot FisherPanelView가 없습니다. 런타임 UI 생성을 건너뜁니다.");
                return false;
            }

            if (view.ViewRoot == null)
            {
                Debug.LogWarning("[" + adapterName + "] FisherPanelView.ViewRoot 참조가 없습니다. 런타임 UI 생성을 건너뜁니다.");
                return false;
            }

            view.SlotLayout = slotLayout;
            view.SetRuntimePreserveInspectorLayout(true);
            artProfile = view.ResolveArtProfile(fallbackArtProfile);
            FisherRuntimeUi.SetActiveProfile(artProfile);
            return true;
        }

        private static FisherPanelView FindViewRoot(GameObject panel, string panelRootName)
        {
            if (panel == null)
            {
                return null;
            }

            Transform root = string.IsNullOrWhiteSpace(panelRootName) ? null : panel.transform.Find(panelRootName);
            Transform viewRoot = root == null ? null : root.Find("ViewRoot");
            FisherPanelView direct = viewRoot == null ? null : viewRoot.GetComponent<FisherPanelView>();
            if (direct != null)
            {
                return direct;
            }

            return panel.GetComponentInChildren<FisherPanelView>(true);
        }
    }
}
