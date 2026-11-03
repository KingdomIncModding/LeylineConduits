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

namespace KingdomLeylineConduits
{
    [BepInPlugin(Id, "Leyline Conduits", Version)]
    public sealed partial class LeylineConduitsPlugin : BaseUnityPlugin
    {
        public const string Id = "urakka.kingdominc.leylineconduits", Version = "0.3.0";
        Harmony harmony;
        internal static bool Diagnostic => Environment.GetCommandLineArgs().Contains("-leyline-smoke");
        void Awake()
        {
            harmony = new Harmony(Id);
            // Keep disposable runs offline even if a later patch fails during startup.
            if (Diagnostic) harmony.CreateClassProcessor(typeof(Offline)).Patch();
            try { harmony.PatchAll(Assembly.GetExecutingAssembly()); }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                if (Diagnostic)
                {
                    var directory = System.IO.Path.Combine(Paths.GameRootPath, "_Mods", "LeylineConduits", "Verification");
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "runtime-tests.json"),
                        new LightJson.JsonObject { { "passed", false }, { "version", Version }, { "error", ex.ToString() } }.ToString(true));
                }
                throw;
            }
            Logger.LogInfo("Leyline Conduits ready: compact named Input/Output ports.");
            if (Diagnostic) { Application.runInBackground = true; StartCoroutine(RuntimeCheck()); }
        }
        void Start()
        {
            EnsureResearch();
            // TechNode initialization needs the game's language settings, which
            // are unavailable during BepInEx Awake. Patch it after native startup.
            harmony.Patch(AccessTools.Method(typeof(TechNode), nameof(TechNode.GetCost)),
                prefix: new HarmonyMethod(typeof(ResearchPrice), "Prefix"));
        }
        void OnDestroy() => harmony?.UnpatchSelf();
        internal static void EnsureResearch()
        {
            if (TechNode.Get(LeylineConduit.TechId) != null) return;
            var previous = TechNode.Get("portal");
            if (previous == null) throw new InvalidOperationException("Portal research is unavailable.");
            var research = new TechNode(LeylineConduit.TechId) {
                Name = "Leyline Conduits", Description = "An advanced, compact Portal upgrade. Link named leylines across workshop tabs. Transfers use the same Mana cost and cost-reduction research as Portals.",
                NodeType = TechNodeType.FlowElement, Previous = previous,
                RelativePosition = new Vector2Int(-300, 0), IconName = ConduitEmblem.IconId,
                CostTier = previous.CostTier
            };
            ConduitCosts.ApplyResearch(research);
            TechNode.Add(research);
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
        [HarmonyPatch(typeof(GamePlayer), MethodType.Constructor)]
        static class Research { static void Prefix() => EnsureResearch(); }
        [HarmonyPatch(typeof(FlowElement), nameof(FlowElement.Create))]
        static class Factory
        {
            static bool Prefix(string name, ref FlowElement __result)
            {
                if (name == LeylineConduit.TypeId) { __result = new LeylineConduit(); return false; }
                var native = typeof(FlowElement).Assembly.GetType("Assets.Source.Flow.Elements." + name);
                if (native == null) return true;
                __result = (FlowElement)Activator.CreateInstance(native); return false;
            }
        }
        [HarmonyPatch(typeof(FlowElement), nameof(FlowElement.GetAllTypes))]
        static class Catalog
        {
            static LeylineConduit preview;
            static void Postfix(ref IEnumerable<FlowElement> __result) => __result = Include(__result);
            static IEnumerable<FlowElement> Include(IEnumerable<FlowElement> originals)
            {
                bool found = false;
                foreach (var element in originals) { found |= element.ElementType == LeylineConduit.TypeId; yield return element; }
                if (!found) yield return preview ?? (preview = new LeylineConduit());
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
        [HarmonyPatch(typeof(FlowPrefabManager), nameof(FlowPrefabManager.Get))]
        static class Prefab
        {
            static FlowPrefabManager owner; static FlowPrefabSet custom;
            static bool Prefix(FlowPrefabManager __instance, string id, ref FlowPrefabSet __result)
            {
                if (id != LeylineConduit.TypeId) return true;
                if (!custom || owner != __instance)
                {
                    var original = __instance.Get("Collector");
                    var root = new GameObject("LeylineConduitAssets"); root.transform.SetParent(__instance.transform, false); root.SetActive(false);
                    custom = UnityEngine.Object.Instantiate(original, root.transform, false); custom.Identifier = id; custom.name = id;
                    var world = UnityEngine.Object.Instantiate(original.Prefab, root.transform, false); world.name = "LeylineConduitPrefab";
                    ((RectTransform)world.transform).sizeDelta = new Vector2(LeylineConduit.Width, LeylineConduit.Height);
                    if (world.GetComponent<WorldFlowCollector>()) UnityEngine.Object.DestroyImmediate(world.GetComponent<WorldFlowCollector>());
                    // Collector reserves space for its heading above the ports.
                    // The sticker tile has no heading; ports use the full tile.
                    var portParent = Field<RectTransform>(world, "_ioParent");
                    portParent.anchorMin = Vector2.zero; portParent.anchorMax = Vector2.one;
                    portParent.offsetMin = portParent.offsetMax = Vector2.zero;
                    var background = Field<Image>(world, "_background");
                    foreach (var graphic in world.GetComponentsInChildren<Graphic>(true)) if (graphic != background) graphic.enabled = false;
                    foreach (var button in world.GetComponentsInChildren<Button>(true)) button.enabled = false;
                    foreach (var tooltip in world.GetComponentsInChildren<TooltipSource>(true)) tooltip.enabled = false;
                    world.gameObject.AddComponent<ConduitWorld>();
                    AccessTools.Field(typeof(FlowPrefabSet), "<Prefab>k__BackingField").SetValue(custom, world); owner = __instance;
                }
                __result = custom; return false;
            }
        }
        internal static T Field<T>(WorldFlowElement world, string name) where T : class => AccessTools.Field(typeof(WorldFlowElement), name).GetValue(world) as T;
    }
}
