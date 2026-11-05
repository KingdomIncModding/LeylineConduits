using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Behaviour.Flow;
using Assets.Behaviour.Flow.Elements;
using Assets.Source.Flow;
using Assets.Source.Player;
using Behaviour.UI.Tooltip;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingdomFoundation.Api;

namespace KingdomLeylineConduits
{
    [BepInPlugin(Id, "Leyline Conduits", Version)]
    [BepInDependency("urakka.kingdominc.foundation", "0.5.0")]
    public sealed partial class LeylineConduitsPlugin : BaseUnityPlugin
    {
        public const string Id = "urakka.kingdominc.leylineconduits", Version = "0.4.0";
        Harmony harmony;
        ITechNode research;
        IWorkspaceNodeType workspace;
        static LeylineConduitsPlugin instance;
        internal static ITechNode ResearchRegistration => instance?.research;
        internal static IWorkspaceNodeType WorkspaceRegistration => instance?.workspace;
        internal static bool Diagnostic => Environment.GetCommandLineArgs().Contains("-leyline-smoke");
        void Awake()
        {
            instance = this;
            harmony = new Harmony(Id);
            // Keep disposable runs offline even if a later patch fails during startup.
            if (Diagnostic) harmony.CreateClassProcessor(typeof(Offline)).Patch();
            try { RegisterFoundation(); harmony.PatchAll(Assembly.GetExecutingAssembly()); }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                workspace?.Dispose(); research?.Dispose(); harmony.UnpatchSelf();
                if (Diagnostic)
                {
                    var directory = System.IO.Path.Combine(Paths.GameRootPath, "_Mods", "LeylineConduits", "Verification");
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "runtime-tests.json"),
                        new LightJson.JsonObject { { "passed", false }, { "version", Version }, { "error", ex.ToString() } }.ToString(true));
                }
                throw;
            }
            Logger.LogInfo("Leyline Conduits ready through Foundation: compact named Input/Output ports.");
            if (Diagnostic) { Application.runInBackground = true; StartCoroutine(RuntimeCheck()); }
        }
        void Start()
        {
            // TechNode initialization needs the game's language settings, which
            // are unavailable during BepInEx Awake. Patch it after native startup.
            harmony.Patch(AccessTools.Method(typeof(TechNode), nameof(TechNode.GetCost)),
                prefix: new HarmonyMethod(typeof(ResearchPrice), "Prefix"));
        }
        void OnDestroy()
        {
            workspace?.Dispose(); research?.Dispose(); harmony?.UnpatchSelf();
            if (instance == this) instance = null;
        }
        void RegisterFoundation()
        {
            var previous = TechNode.Get("portal");
            if (previous == null) throw new InvalidOperationException("Portal research is unavailable.");
            // Retain both legacy IDs so purchased research and saved conduits load unchanged.
            research = FoundationApi.CreateTechNode(new TechNodeDefinition(
                LeylineConduit.TechId, "Leyline Conduits", new Vector2(540, -320),
                "Link named leylines across tabs.\nUses Portal's Mana transfer cost.",
                icon: ConduitEmblem.Sprite,
                research: new TechNodeOptions(costTier: previous.CostTier,
                    costMultiplier: ConduitCosts.ResearchPortalMultiplier, requirements: new[] { previous.Identifier })));
            ConduitCosts.ApplyResearch(((INativeTechNode)research).NativeNode);
            workspace = FoundationApi.RegisterWorkspaceNode(new WorkspaceNodeDefinition(
                LeylineConduit.TypeId, "Leyline Conduit", () => new LeylineConduit(), LeylineConduit.TechId,
                size: new Vector2Int(LeylineConduit.Width, LeylineConduit.Height),
                description: LeylineConduit.NodeDescription, icon: ConduitEmblem.Sprite,
                iconName: ConduitEmblem.IconId,
                costMultiplier: ConduitCosts.OriginalBuildMultiplier * ConduitCosts.BuildPortalMultiplier));
        }
        static class ResearchPrice
        {
            static bool Prefix(TechNode __instance, ref CurrencyCollection __result)
            {
                if (__instance.Identifier != LeylineConduit.TechId) return true;
                // Derive the exact price from Portal, including native prestige discounts.
                __result = ConduitCosts.ResearchPrice(); return false;
            }
        }
        [HarmonyPatch(typeof(SpriteLibrary), nameof(SpriteLibrary.Get), new[] { typeof(string) })]
        static class Emblem
        {
            static bool Prefix(string name, ref Sprite __result)
            {
                if (name != ConduitEmblem.IconId) return true;
                __result = ConduitEmblem.Sprite; return false;
            }
        }
        [HarmonyPatch(typeof(WorldFlowIO), nameof(WorldFlowIO.SetIO))]
        static class PortColors
        {
            static void Postfix(WorldFlowIO __instance, FlowIO io)
            {
                if (!(io.Parent is LeylineConduit)) return;
                var sprites = (Sprite[])AccessTools.Field(typeof(WorldFlowIO), io is FlowOutput ? "_outputSprites" : "_inputSprites").GetValue(__instance);
                ((Image)AccessTools.Field(typeof(WorldFlowIO), "_sprite").GetValue(__instance)).sprite = sprites[io.HasBuffer ? 0 : 2];
            }
        }
        [HarmonyPatch(typeof(FlowElement), nameof(FlowElement.FromJson))]
        static class SavedLayout
        {
            static void Postfix(FlowElement __result) { if (__result is LeylineConduit conduit) conduit.NormalizeLayout(); }
        }
        [HarmonyPatch(typeof(WorldFlowElement), nameof(WorldFlowElement.SetElement))]
        static class WorkspaceView
        {
            static void Prefix(WorldFlowElement __instance, FlowElement element)
            {
                if (!(element is LeylineConduit)) return;
                // Keep Foundation's native prefab and ports; adapt only this type's presentation.
                var portParent = Field<RectTransform>(__instance, "_ioParent");
                portParent.anchorMin = Vector2.zero; portParent.anchorMax = Vector2.one;
                portParent.offsetMin = portParent.offsetMax = Vector2.zero;
                foreach (var name in new[] { "_labelContainer", "_label", "_icon", "_progress", "_productivity", "_outputFull", "_craftButton", "_upgradeSlots" })
                {
                    var component = AccessTools.Field(typeof(WorldFlowElement), name)?.GetValue(__instance) as Component;
                    if (!component) continue;
                    // The default prefab wraps icons and progress bars in separate backdrops.
                    var nativeSection = component.transform;
                    while (nativeSection.parent && nativeSection.parent != __instance.transform) nativeSection = nativeSection.parent;
                    foreach (var graphic in nativeSection.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
                    foreach (var button in nativeSection.GetComponentsInChildren<Button>(true)) button.enabled = false;
                    foreach (var tooltip in nativeSection.GetComponentsInChildren<TooltipSource>(true)) tooltip.enabled = false;
                }
            }
            static void Postfix(WorldFlowElement __instance, FlowElement element)
            {
                if (element is LeylineConduit && !__instance.GetComponent<ConduitWorld>())
                    __instance.gameObject.AddComponent<ConduitWorld>();
            }
        }
        internal static T Field<T>(WorldFlowElement world, string name) where T : class => AccessTools.Field(typeof(WorldFlowElement), name).GetValue(world) as T;
    }
}
