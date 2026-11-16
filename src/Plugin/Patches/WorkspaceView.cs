using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    internal static class WorkspaceView
    {
        internal static void Postfix(Component view, WorkspaceObject element)
        {
            if (!(element?.Behaviour is LeylineConduit))
                return;
            if (element.IO.Count > 0 && element.IO[0].World)
            {
                var portParent = element.IO[0].World.transform.parent as RectTransform;
                if (portParent && portParent != view.transform)
                {
                    portParent.anchorMin = Vector2.zero;
                    portParent.anchorMax = Vector2.one;
                    portParent.offsetMin = portParent.offsetMax = Vector2.zero;
                }
            }
            // Keep Foundation's native prefab and ports; adapt only this type's presentation.
            foreach (var graphic in new[]
            {
                GameBuildingGraphic.Icon,
                GameBuildingGraphic.Progress,
                GameBuildingGraphic.Productivity,
                GameBuildingGraphic.OutputFull,
                GameBuildingGraphic.CraftButton,
                GameBuildingGraphic.UpgradeSlots
            })
            {
                var component = FoundationApi.WorkspaceVisuals.BuildingGraphic(view, graphic);
                if (!component)
                    continue;
                var nativeSection = component.transform;
                while (nativeSection.parent && nativeSection.parent != view.transform)
                    nativeSection = nativeSection.parent;
                foreach (var g in nativeSection.GetComponentsInChildren<Graphic>(true))
                    g.enabled = false;
                foreach (var b in nativeSection.GetComponentsInChildren<Button>(true))
                    b.enabled = false;
                FoundationApi.UiAccess.RemoveNativeTooltips(nativeSection.gameObject);
            }
            if (!view.GetComponent<ConduitWorld>())
                view.gameObject.AddComponent<ConduitWorld>();
        }
    }
}

