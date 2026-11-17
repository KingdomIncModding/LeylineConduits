using System;
using System.Collections.Generic;
using BepInEx;
using KingdomFoundation.Api;
using UnityEngine;

namespace KingdomLeylineConduits
{
    [BepInPlugin(Id, "Leyline Conduits", Version)]
    [BepInDependency("urakka.kingdominc.foundation", "0.14.5")]
    public sealed class LeylineConduitsPlugin : BaseUnityPlugin
    {
        public const string Id = "urakka.kingdominc.leylineconduits", Version = "0.4.2";
        FoundationModContext context;
        readonly List<IDisposable> hooks = new List<IDisposable>();
        ITechNode research;
        IWorkspaceNodeType workspace;
        static LeylineConduitsPlugin instance;
        internal static ITechNode ResearchRegistration => instance?.research;
        internal static IWorkspaceNodeType WorkspaceRegistration => instance?.workspace;

        void Awake()
        {
            instance = this;
            try
            {
                context = FoundationApi.ForMod(Id);
                RegisterFoundation();
                RegisterHooks();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                ReleaseHooks();
                context?.Dispose();
                context = null;
                throw;
            }
            Logger.LogInfo("Leyline Conduits ready through Foundation: compact named Input/Output ports.");
        }

        void OnDestroy()
        {
            ReleaseHooks();
            context?.Dispose();
            context = null;
            if (instance == this)
                instance = null;
        }

        void ReleaseHooks()
        {
            foreach (var hook in hooks)
                hook.Dispose();
            hooks.Clear();
        }

        void RegisterHooks()
        {
            hooks.Add(FoundationApi.GameHooks.SubscribeFiltered(
                Id,
                GameHookPoints.ResearchCost,
                new GameHookFilter(subjectId: LeylineConduit.TechId),
                before: ResearchPrice.Before));
            hooks.Add(FoundationApi.GameHooks.Subscribe(
                Id,
                GameHookPoints.BuildingBound,
                after: call => WorkspaceView.Postfix(call.View, call.Argument<WorkspaceObject>(0))));
            hooks.Add(FoundationApi.GameHooks.Subscribe(
                Id,
                GameHookPoints.PortBound,
                after: call => PortColors.Postfix(call.View, call.Argument<WorkspacePort>(0))));
            hooks.Add(FoundationApi.GameHooks.Subscribe(
                Id,
                GameHookPoints.BuildingDeserialize,
                after: call => LoadedLayout.Postfix(call.Result<WorkspaceObject>())));
        }

        void RegisterFoundation()
        {
            var portal = FoundationApi.ResearchAccess.Find("portal")
                ?? throw new InvalidOperationException("Portal research is unavailable.");
            // Retain both legacy IDs so purchased research and saved conduits load unchanged.
            research = context.Register(new TechNodeDefinition(
                LeylineConduit.TechId,
                "Leyline Conduits",
                Id,
                "Leyline Conduits",
                new Vector2(540, -320),
                "Link named leylines across tabs.\nUses Portal's Mana transfer cost.",
                icon: ConduitEmblem.Sprite,
                research: TechNodeOptions.Create(
                    costTier: portal.CostTier,
                    costMultiplier: ConduitCosts.ResearchPortalMultiplier,
                    requirements: new[] { portal.Identifier })));
            var researchNode = FoundationApi.ResearchAccess.Find(research.Id);
            if (researchNode != null)
            {
                researchNode.SetCostCurrencies(new[] { "Gold", "Mana" });
                researchNode.ResetCost();
            }
            workspace = context.Register(new FrameworkWorkspaceNodeDefinition(
                LeylineConduit.TypeId,
                "Leyline Conduit",
                () => new LeylineConduit(),
                LeylineConduit.TechId,
                size: new Vector2Int(LeylineConduit.Width, LeylineConduit.Height),
                description: LeylineConduit.NodeDescription,
                icon: ConduitEmblem.Sprite,
                iconName: ConduitEmblem.IconId,
                costMultiplier: ConduitCosts.OriginalBuildMultiplier * ConduitCosts.BuildPortalMultiplier));
        }
    }
}

