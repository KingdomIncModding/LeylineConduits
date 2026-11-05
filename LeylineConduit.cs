using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Source.Flow;
using Assets.Source.Items;
using Assets.Source.Player;
using LightJson;
using UnityEngine;
using KingdomFoundation.Api;

namespace KingdomLeylineConduits
{
    public enum ConduitMode { Input, Output }

    public sealed class LeylineConduit : WorkspaceNode
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
        public override string Label => "Leyline Conduit";
        public override string IconName => SelectedIconId.Length > 0 ? SelectedIconId : ConduitEmblem.IconId;
        public override Sprite Icon => ConduitIcons.Find(SelectedIconId) ?? ConduitEmblem.Sprite;
        public override ElementCategory Category => ElementCategory.Utility;
        public override bool HasCraftButton => false;
        public override int UpgradeSlots => 0;
        public FlowIO Port => Mode == ConduitMode.Input ? (FlowIO)Inputs[0] : Outputs[0];

        public LeylineConduit() : base(TypeId)
        {
            RequiredTech = TechId;
            CostCurrency = CurrencyCollection.GoldRing;
            CostMultiplier = ConduitCosts.OriginalBuildMultiplier * ConduitCosts.BuildPortalMultiplier;
            Size = new Vector2Int(Width, Height);
            Description = NodeDescription;
            RebuildPort();
        }

        public override CurrencyCollection GetPurchaseCost(int numElements)
            => ConduitCosts.BuildPrice(numElements);

        void RebuildPort()
        {
            Inputs.Clear(); Outputs.Clear();
            if (Mode == ConduitMode.Input)
                Inputs.Add(new FlowInput(this, 0) { Position = -PortOffset, BufferSize = 1, Filter = Accept });
            else
                Outputs.Add(new FlowOutput(this, 0) { Position = PortOffset, BufferSize = 1, Filter = item => true });
        }

        bool Accept(InventoryItem item) => item != null && !Editing && Channel.Length > 0 && Port.CanBuffer;

        public bool Configure(ConduitMode mode, string name, out string reason)
            => Configure(mode, name, SelectedIconId, out reason);

        public bool Configure(ConduitMode mode, string name, string iconId, out string reason)
        {
            reason = null;
            iconId = iconId ?? "";
            if (iconId.Length > 0 && !ConduitIcons.Find(iconId)) { reason = "Choose an available game icon."; return false; }
            if (!Enum.IsDefined(typeof(ConduitMode), mode)) { reason = "Choose Input or Output."; return false; }
            name = (name ?? "").Trim();
            if (name.Length == 0) { reason = "Enter a leyline name."; return false; }
            if (name.Length > ChannelLimit || name.Any(char.IsControl))
            { reason = "Use 1–32 characters without line breaks."; return false; }
            if (mode != Mode && Port.ConnectedPipe != null)
            { reason = "Disconnect the pipe before changing mode."; return false; }
            if ((mode != Mode || !SameChannel(Channel, name)) && Port.HasBuffer)
            { reason = "Let the waiting item leave before retuning."; return false; }
            bool changeMode = mode != Mode;
            Mode = mode; Channel = name; SelectedIconId = iconId; nextOutput = 0;
            if (changeMode) { RebuildPort(); if (World) World.UpdateIO(); }
            return true;
        }

        internal static bool SameChannel(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        internal IEnumerable<LeylineConduit> Peers()
        {
            if (Channel.Length == 0 || GamePlayer.Current == null) yield break;
            foreach (var flow in GamePlayer.Current.Flows)
                foreach (var element in flow.Elements)
                    if (element is LeylineConduit peer && peer != this && SameChannel(peer.Channel, Channel)) yield return peer;
        }

        internal List<LeylineConduit> OutputPeers() => Peers().Where(p => p.Mode == ConduitMode.Output).ToList();
        internal static bool IsConnected(FlowOutput output) => output.ConnectedPipe?.ConnectedOutput == output
            && output.ConnectedPipe.ConnectedInput?.ConnectedPipe == output.ConnectedPipe;

        public override void Update(float delta)
        {
            if (Mode != ConduitMode.Input || Editing || Channel.Length == 0 || !Port.HasBuffer) return;
            var peers = OutputPeers();
            if (peers.Count == 0) return;
            for (int offset = 0; offset < peers.Count; offset++)
            {
                int index = (nextOutput + offset) % peers.Count;
                var peer = peers[index]; var target = peer.Outputs[0];
                if (peer.Editing || !target.CanBuffer || !IsConnected(target)) continue;
                var receiver = target.ConnectedPipe.ConnectedInput;
                if ((receiver.BufferSize > 0 && !receiver.CanBuffer) || !receiver.CanAcceptItem(Port.NextItem)) continue;
                int mana = ConduitCosts.TransferMana;
                if (!GamePlayer.Current.SpendCurrency(Currency.Mana, mana)) return;
                // Never clone or recreate an item: equipment quality, adventurer
                // identity, enchantments and attached mod data stay on this object.
                target.BufferedItems.Add(Port.NextItem);
                Port.ConsumeItem();
                if (World) World.ShowIncome(Currency.Mana, -mana);
                nextOutput = (index + 1) % peers.Count;
                return;
            }
        }

        internal string Status
        {
            get
            {
                if (Channel.Length == 0) return "Name a leyline";
                if (Port.HasBuffer) return Mode == ConduitMode.Input && !GamePlayer.Current.CanAfford(Currency.Mana, ConduitCosts.TransferMana)
                    ? "Needs " + ConduitCosts.TransferMana + " Mana" : "Item waiting";
                if (Mode == ConduitMode.Input)
                {
                    var peers = OutputPeers();
                    if (peers.Count == 0) return "No output linked";
                    return peers.Any(p => !p.Editing && p.Port.CanBuffer && IsConnected(p.Outputs[0])) ? "Leyline ready" : "Outputs blocked";
                }
                if (Port.ConnectedPipe == null) return "No destination";
                return Peers().Any(p => p.Mode == ConduitMode.Input) ? "Leyline ready" : "No input linked";
            }
        }

        public override bool HandleReopenBuyMenu()
        {
            if (World) World.GetComponent<ConduitWorld>()?.OpenSettings();
            return false;
        }
        protected override void CopySettingsTo(FlowElement copy)
        {
            var conduit = (LeylineConduit)copy;
            conduit.Mode = Mode; conduit.Channel = Channel; conduit.SelectedIconId = SelectedIconId; conduit.RebuildPort();
        }
        protected override void DataToJson(JsonObject data)
        { data["LeylineMode"] = (int)Mode; data["LeylineChannel"] = Channel; data["LeylineIcon"] = SelectedIconId; }
        protected override void DataFromJson(JsonObject data, FlowDiagram parent)
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
