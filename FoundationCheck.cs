using System;
using System.IO;
using System.Linq;
using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Assets.Behaviour.UI.TechTree;
using Assets.Source.Flow;
using Assets.Source.Flow.Elements;
using Assets.Source.Items;
using Assets.Source.Player;
using BepInEx.Bootstrap;
using HarmonyLib;
using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    public sealed partial class LeylineConduitsPlugin
    {
        void FoundationModelChecks()
        {
            Check(Chainloader.PluginInfos.TryGetValue("urakka.kingdominc.foundation", out var plugin)
                && plugin.Metadata.Version >= new Version(0, 4, 1), "Foundation dependency loads before Leyline Conduits");
            Check(FoundationApi.TechNodes.TryGet(LeylineConduit.TechId, out var registered)
                && ReferenceEquals(registered, ResearchRegistration) && registered is INativeTechNode,
                "Legacy research ID is registered through Foundation's public research API");
            var native = ((INativeTechNode)registered).NativeNode;
            Check(native.Previous == null && native.Root == null && native.ForceRootNode
                && native.AllRequirements.Single().Identifier == "portal",
                "Standalone Mods research requires Portal without moving native research");
            Check(FoundationApi.WorkspaceNodes.TryGet(LeylineConduit.TypeId, out var workspace)
                && ReferenceEquals(workspace, WorkspaceRegistration) && workspace.CreateInstance() is LeylineConduit,
                "Foundation's workspace factory owns the legacy conduit type");
            var first = workspace.CreateInstance(); var second = workspace.CreateInstance();
            Check(first.Guid != second.Guid && first.Size == new Vector2Int(64, 64) && first.ElementType == "LeylineConduit",
                "Registered factory creates fresh sticker-sized native conduit instances");
            foreach (var method in new[] {
                AccessTools.Method(typeof(FlowElement), nameof(FlowElement.Create)),
                AccessTools.Method(typeof(FlowElement), nameof(FlowElement.GetAllTypes)),
                AccessTools.Method(typeof(FlowPrefabManager), nameof(FlowPrefabManager.Get)) })
            {
                var owners = Harmony.GetPatchInfo(method)?.Owners;
                Check(owners != null && owners.Contains("urakka.kingdominc.foundation") && !owners.Contains(Id),
                    "Foundation owns shared integration for " + method.DeclaringType.Name + "." + method.Name);
            }
            Check(Harmony.GetPatchInfo(AccessTools.Constructor(typeof(GamePlayer)))?.Owners.Contains(Id) != true,
                "Leyline Conduits no longer patches player construction for research registration");

            var player = GamePlayer.Current;
            var diagram = new FlowDiagram { Label = "Legacy conduit kingdom roundtrip" };
            var source = new Collector(); var sink = new Stockpile();
            var inlet = new LeylineConduit { Position = new Vector2Int(128, -64), Mirrored = true };
            var outlet = new LeylineConduit { Position = new Vector2Int(320, -64) };
            string icon = ItemType.Get("iron_ingot").IconName;
            inlet.Configure(ConduitMode.Input, "Legacy Iron", icon, out _);
            outlet.Configure(ConduitMode.Output, "legacy iron", out _);
            inlet.Port.BufferedItems.Add(Item("iron_sword")); outlet.Port.BufferedItems.Add(Item("iron_helm"));
            diagram.Elements.Add(source); diagram.Elements.Add(inlet); diagram.Elements.Add(outlet); diagram.Elements.Add(sink);
            diagram.Pipes.Add(Connect(source.Outputs[0], inlet.Inputs[0]));
            diagram.Pipes.Add(Connect(outlet.Outputs[0], sink.Inputs[0])); player.Flows.Add(diagram);
            var restored = GamePlayer.FromJson(player.ToJson());
            var loaded = restored.Flows.Last().Elements.OfType<LeylineConduit>().ToArray();
            Check(restored.HasTech("leyline_conduits") && loaded.Length == 2 && loaded[0].Mode == ConduitMode.Input
                && loaded[1].Mode == ConduitMode.Output && loaded[0].SelectedIconId == icon && loaded[0].Channel == "Legacy Iron"
                && loaded[0].Mirrored && loaded[0].Position == inlet.Position,
                "Full kingdom JSON retains existing research, conduit identity, modes, names and icons");
            Check(loaded[0].Port.NextItem.Type.Identifier == "iron_sword" && loaded[1].Port.NextItem.Type.Identifier == "iron_helm"
                && loaded[0].Inputs[0].ConnectedPipe?.ConnectedOutput.Parent is Collector
                && loaded[1].Outputs[0].ConnectedPipe?.ConnectedInput.Parent is Stockpile,
                "Foundation save loading preserves both waiting items and original pipe endpoints");
            player.Flows.Remove(diagram); GamePlayer.Current = player;
        }

        void FoundationWorldChecks(string directory)
        {
            var prefab = FlowPrefabManager.Instance.Get(LeylineConduit.TypeId);
            Check(GamePlayer.Current.Flows[0].Elements.OfType<LeylineConduit>().All(c =>
                Field<Image>(c.World, "_icon").transform.parent.GetComponentsInChildren<Graphic>(true).All(g => !g.enabled)
                && Field<Image>(c.World, "_progress").transform.parent.GetComponentsInChildren<Graphic>(true).All(g => !g.enabled)),
                "Native icon and progress backdrops stay hidden inside the compact tile");
            Check(prefab.Prefab.name == "WorkspaceNodePrefab:LeylineConduit" && prefab.Prefab.GetType() == typeof(WorldFlowElement),
                "Conduits use Foundation's native workspace prefab");
            Check(!prefab.Prefab.GetComponent<ConduitWorld>() && GamePlayer.Current.Flows[0].Elements.OfType<LeylineConduit>()
                .All(c => c.World.GetComponents<ConduitWorld>().Length == 1),
                "Compact presentation attaches once to each bound node without replacing framework assets");
            try
            {
                GameUI.Instance.ToggleTechMenu(true); var tree = TechTreeMenu.Instance;
                var baseTab = Resources.FindObjectsOfTypeAll<Button>().Single(b => b.name == "Base GameTab" && b.gameObject.activeInHierarchy);
                baseTab.onClick.Invoke();
                Check(!tree.Nodes.Any(n => n.Tech.Identifier == LeylineConduit.TechId), "Conduits are absent from Base Game research");
                var modsTab = Resources.FindObjectsOfTypeAll<Button>().Single(b => b.name == "ModsTab" && b.gameObject.activeInHierarchy);
                modsTab.onClick.Invoke();
                Resources.FindObjectsOfTypeAll<Button>().Single(b => b.name == "Mod:" + Id && b.gameObject.activeInHierarchy).onClick.Invoke();
                Check(tree.Nodes.Count(n => n.Tech.Identifier == LeylineConduit.TechId) == 1,
                    "Mods research shows exactly one Leyline Conduits card");
                var card = tree.Nodes.Single(n => n.Tech.Identifier == LeylineConduit.TechId);
                var standard = (UITechNode)AccessTools.Field(typeof(TechTreeMenu), "_elementNodePrefab").GetValue(tree);
                Check(card.GetType() == standard.GetType() && card.Tech.Icon == ConduitEmblem.Sprite,
                    "Mods research uses the native building card with the preserved rune icon");
                foreach (var node in tree.Nodes) node.UpdateNodeStatus();
                Check(card.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Leyline Conduits"),
                    "Research card renders the registered title");
                var camera = (Camera)AccessTools.Field(typeof(TechTreeMenu), "_camera").GetValue(tree);
                camera.transform.position = new Vector3(card.Tech.Position.x, card.Tech.Position.y, -10);
                CaptureFoundationTree(tree, Path.Combine(directory, "leyline-foundation-tech-tree.png"));
                baseTab.onClick.Invoke();
                Check(tree.Nodes.Any() && !tree.Nodes.Any(n => n.Tech.Identifier == LeylineConduit.TechId),
                    "Returning to Base Game restores native research without duplicate conduit entries");
            }
            finally { GameUI.Instance.ToggleTechMenu(false); WorldFlowCamera.Instance.SetZoomLevel(3); }
        }

        static void CaptureFoundationTree(TechTreeMenu tree, string path)
        {
            var camera = (Camera)AccessTools.Field(typeof(TechTreeMenu), "_camera").GetValue(tree);
            var overlays = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray(); var distances = overlays.Select(c => c.planeDistance).ToArray();
            var orders = overlays.Select(c => c.sortingOrder).ToArray();
            var previous = RenderTexture.active; var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            var oldTarget = camera.targetTexture; int mask = camera.cullingMask;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                foreach (var overlay in overlays) { overlay.renderMode = RenderMode.ScreenSpaceCamera; overlay.worldCamera = camera; overlay.planeDistance = 10; overlay.sortingOrder = 30000; }
                camera.cullingMask = -1; camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply(); File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                camera.targetTexture = oldTarget; camera.cullingMask = mask;
                for (int i = 0; i < overlays.Length; i++) { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i]; overlays[i].sortingOrder = orders[i]; }
                Canvas.ForceUpdateCanvases(); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
