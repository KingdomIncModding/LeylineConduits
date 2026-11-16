using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFoundation.Api;
using KingdomFoundation.Json;
using UnityEngine;

namespace KingdomLeylineConduits
{
    public sealed class LeylineConduit : FrameworkWorkspaceNode
    {
        public const string TypeId = "LeylineConduit", TechId = "leyline_conduits";
        public const int Width = 64, Height = 64, ChannelLimit = 32;
        public const int PortOffset = 40; // Native first port: 30px header + 10px inset.
        public const string NodeDescription = "An advanced compact Portal with one connection and a named leyline across workshop tabs. Each port holds one item. Transfers cost the same Mana as a Portal, including Portal cost reductions.";
        public ConduitMode Mode { get; private set; }
        public string Channel { get; private set; } = "";
        public string SelectedIconId { get; private set; } = "";
        int nextOutput;
        internal bool Editing;

        public string IconName => SelectedIconId.Length > 0 ? SelectedIconId : ConduitEmblem.IconId;
        public Sprite Icon => ConduitIcons.Find(SelectedIconId) ?? ConduitEmblem.Sprite;
        public WorkspacePort Port => Mode == ConduitMode.Input ? Inputs[0] : Outputs[0];

        public LeylineConduit() : base(TypeId)
        {
            CostCurrencies = new[] { GameCurrency.Gold, GameCurrency.Ring };
            Size = new Vector2Int(Width, Height);
            RebuildPort();
        }

        void RebuildPort()
        {
            TrimInputs(0);
            TrimOutputs(0);
            if (Mode == ConduitMode.Input)
                AddInput(1, -PortOffset, Accept);
            else
                AddOutput(1, PortOffset);
        }

        bool Accept(GameInventoryItem item) => item != null && !Editing && Channel.Length > 0 && Port.CanBuffer;

        public bool Configure(ConduitMode mode, string name, out string reason)
            => Configure(mode, name, SelectedIconId, out reason);

        public bool Configure(ConduitMode mode, string name, string iconId, out string reason)
        {
            reason = null;
            iconId = iconId ?? "";
            if (iconId.Length > 0 && !ConduitIcons.Find(iconId))
            {
                reason = "Choose an available game icon.";
                return false;
            }
            if (!Enum.IsDefined(typeof(ConduitMode), mode))
            {
                reason = "Choose Input or Output.";
                return false;
            }
            name = (name ?? "").Trim();
            if (name.Length == 0)
            {
                reason = "Enter a leyline name.";
                return false;
            }
            if (name.Length > ChannelLimit || name.Any(char.IsControl))
            {
                reason = "Use 1\u201332 characters without line breaks.";
                return false;
            }
            if (mode != Mode && Port.ConnectedPipe != null)
            {
                reason = "Disconnect the pipe before changing mode.";
                return false;
            }
            if ((mode != Mode || !SameChannel(Channel, name)) && Port.HasBuffer)
            {
                reason = "Let the waiting item leave before retuning.";
                return false;
            }
            bool changeMode = mode != Mode;
            Mode = mode;
            Channel = name;
            SelectedIconId = iconId;
            nextOutput = 0;
            if (changeMode)
            {
                RebuildPort();
                if (World)
                    FoundationApi.WorkspaceVisuals.RefreshPorts(World);
            }
            return true;
        }

        internal static bool SameChannel(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        internal IEnumerable<LeylineConduit> Peers()
        {
            var kingdom = FoundationApi.Kingdom;
            if (Channel.Length == 0 || kingdom == null)
                yield break;
            foreach (var flow in kingdom.Workspaces)
            {
                var buildings = flow.Buildings;
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i]?.Behaviour is LeylineConduit peer && peer != this && SameChannel(peer.Channel, Channel))
                        yield return peer;
                }
            }
        }

        internal List<LeylineConduit> OutputPeers() => Peers().Where(p => p.Mode == ConduitMode.Output).ToList();

        internal static bool IsConnected(WorkspacePort output) => output?.ConnectedPipe?.ConnectedOutput == output
            && output.ConnectedPipe.ConnectedInput?.ConnectedPipe == output.ConnectedPipe;

        public override void Update(float delta)
        {
            if (Mode != ConduitMode.Input || Editing || Channel.Length == 0 || !Port.HasBuffer)
                return;
            var peers = OutputPeers();
            if (peers.Count == 0)
                return;
            var item = Port.BufferedItems[0];
            for (int offset = 0; offset < peers.Count; offset++)
            {
                int index = (nextOutput + offset) % peers.Count;
                var peer = peers[index];
                var target = peer.Outputs[0];
                if (peer.Editing || !target.CanBuffer || !IsConnected(target))
                    continue;
                var receiver = target.ConnectedPipe.ConnectedInput;
                bool receiverHasRoom = receiver.CanBuffer || receiver.BufferedItemCount == 0;
                if (!receiverHasRoom || !receiver.CanAcceptItem(item))
                    continue;
                int mana = ConduitCosts.TransferMana;
                if (!FoundationApi.WorkspaceAccess.TrySpend(new GameMoney(nameof(GameCurrency.Mana), mana)))
                    return;
                // Never clone or recreate an item: equipment quality, adventurer
                // identity, enchantments and attached mod data stay on this object.
                target.PrependBuffer(item);
                Port.ConsumeItem();
                if (World)
                    FoundationApi.WorkspaceAccess.ShowIncome(World, new GameMoney(nameof(GameCurrency.Mana), -mana));
                nextOutput = (index + 1) % peers.Count;
                return;
            }
        }

        internal string Status
        {
            get
            {
                if (Channel.Length == 0)
                    return "Name a leyline";
                if (Port.HasBuffer)
                {
                    var kingdom = FoundationApi.Kingdom;
                    var cost = ConduitCosts.TransferMana;
                    bool canAfford = kingdom != null && kingdom.CanAfford(new GameMoney(nameof(GameCurrency.Mana), cost));
                    return Mode == ConduitMode.Input && !canAfford ? "Needs " + cost + " Mana" : "Item waiting";
                }
                if (Mode == ConduitMode.Input)
                {
                    var peers = OutputPeers();
                    if (peers.Count == 0)
                        return "No output linked";
                    return peers.Any(p => !p.Editing && p.Port.CanBuffer && IsConnected(p.Outputs[0]))
                        ? "Leyline ready"
                        : "Outputs blocked";
                }
                if (Port.ConnectedPipe == null)
                    return "No destination";
                return Peers().Any(p => p.Mode == ConduitMode.Input) ? "Leyline ready" : "No input linked";
            }
        }

        public override bool HandleReopenBuyMenu()
        {
            if (World)
                World.GetComponent<ConduitWorld>()?.OpenSettings();
            return false;
        }

        protected override void CopySettingsTo(FrameworkWorkspaceNode copy)
        {
            var conduit = (LeylineConduit)copy;
            conduit.Mode = Mode;
            conduit.Channel = Channel;
            conduit.SelectedIconId = SelectedIconId;
            conduit.RebuildPort();
        }

        protected override void DataToJson(JsonObject data)
        {
            data["LeylineMode"] = (int)Mode;
            data["LeylineChannel"] = Channel;
            data["LeylineIcon"] = SelectedIconId;
        }

        protected override void DataFromJson(JsonObject data, GameWorkspace parent)
        {
            Mode = data["LeylineMode"].AsInteger == 1 ? ConduitMode.Output : ConduitMode.Input;
            var name = data["LeylineChannel"].AsString ?? "";
            Channel = new string(name.Where(c => !char.IsControl(c)).Take(ChannelLimit).ToArray()).Trim();
            var icon = data["LeylineIcon"].AsString ?? "";
            SelectedIconId = icon.Length > 0 && ConduitIcons.Find(icon) ? icon : "";
            RebuildPort();
        }

        internal void NormalizeLayout()
        {
            Size = new Vector2Int(Width, Height);
            Port.Position = Mode == ConduitMode.Input ? -PortOffset : PortOffset;
        }
    }
}

