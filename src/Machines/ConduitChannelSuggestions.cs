using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KingdomFoundation.Api;

namespace KingdomLeylineConduits
{
    internal static class ConduitChannelSuggestions
    {
        const int MaxTraversalNodes = 24;

        internal static List<string> Collect(LeylineConduit conduit, ConduitMode activeMode, out int pipeCount)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pipeCount = 0;
            if (conduit == null)
                return results;

            if (activeMode == ConduitMode.Output)
            {
                AddExistingChannels(conduit, inputsOnly: true, results, seen);
                results.Sort(StringComparer.OrdinalIgnoreCase);
                return results;
            }

            AddPipeSuggestions(conduit, results, seen);
            pipeCount = results.Count;
            AddExistingChannels(conduit, inputsOnly: false, results, seen);
            return results;
        }

        internal static string Combine(string baseName, string postfix)
        {
            string cleanBase = Sanitize(baseName);
            string cleanPostfix = Sanitize(postfix);
            if (cleanBase.Length == 0)
                return "";
            if (cleanPostfix.Length == 0)
                return Truncate(cleanBase, LeylineConduit.ChannelLimit);

            int maxBase = LeylineConduit.ChannelLimit - cleanPostfix.Length - 1;
            if (maxBase <= 0)
                return Truncate(cleanPostfix, LeylineConduit.ChannelLimit);
            if (cleanBase.Length > maxBase)
                cleanBase = cleanBase.Substring(0, maxBase).TrimEnd();
            return cleanBase.Length == 0
                ? Truncate(cleanPostfix, LeylineConduit.ChannelLimit)
                : cleanBase + " " + cleanPostfix;
        }

        internal static string FormatDisplayName(string rawLabel, string fallbackName = null)
        {
            string candidate = rawLabel;
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.TrimStart().StartsWith("@", StringComparison.Ordinal))
            {
                string translated = FoundationApi.Presentation.Translate(candidate);
                if (!string.IsNullOrWhiteSpace(translated) && !translated.TrimStart().StartsWith("@", StringComparison.Ordinal))
                    candidate = translated;
            }
            else if (!string.IsNullOrWhiteSpace(fallbackName) && !fallbackName.TrimStart().StartsWith("@", StringComparison.Ordinal))
            {
                candidate = fallbackName;
            }

            if (string.IsNullOrWhiteSpace(candidate))
                candidate = fallbackName;
            if (string.IsNullOrWhiteSpace(candidate))
                return "";

            candidate = candidate.Trim();
            if (candidate.StartsWith("@", StringComparison.Ordinal))
                candidate = HumanizeToken(candidate.Substring(1));
            return Truncate(Sanitize(candidate), LeylineConduit.ChannelLimit);
        }

        static void AddPipeSuggestions(LeylineConduit conduit, List<string> results, HashSet<string> seen)
        {
            var start = conduit.Model;
            if (start == null)
                return;

            bool traceUpstream = conduit.Port == null || conduit.Port.IsInput;
            var queue = new Queue<WorkspaceObject>();
            var visited = new HashSet<WorkspaceObject> { start };
            EnqueueConnected(start, traceUpstream, queue, visited);

            var passthroughNames = new List<string>();
            int steps = 0;
            while (queue.Count > 0 && steps < MaxTraversalNodes)
            {
                steps++;
                var current = queue.Dequeue();
                if (current == null)
                    continue;

                if (current.Behaviour is LeylineConduit peerConduit)
                {
                    AddCandidate(peerConduit.Channel, results, seen);
                    continue;
                }

                if (IsRoutingNode(current))
                {
                    CollectBuildingNames(current, passthroughNames);
                    EnqueueConnected(current, traceUpstream, queue, visited);
                    continue;
                }

                CollectBuildingNames(current, results, seen);
            }

            for (int i = 0; i < passthroughNames.Count; i++)
                AddCandidate(passthroughNames[i], results, seen);
        }

        static void EnqueueConnected(WorkspaceObject node, bool traceUpstream, Queue<WorkspaceObject> queue, HashSet<WorkspaceObject> visited)
        {
            var ports = traceUpstream ? node.Inputs : node.Outputs;
            for (int i = 0; i < ports.Count; i++)
            {
                var pipe = ports[i].ConnectedPipe;
                if (pipe == null)
                    continue;
                var neighbor = traceUpstream ? pipe.ConnectedOutput?.Parent : pipe.ConnectedInput?.Parent;
                if (neighbor != null && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        static void CollectBuildingNames(WorkspaceObject building, List<string> target, HashSet<string> seen = null)
        {
            var production = building.Production;
            string label = FormatDisplayName(building.Label, building.Name);
            AddToList(label, target, seen);

            if (production == null)
                return;

            if (production.HasResource && production.Resource != null)
                AddToList(FormatDisplayName(production.Resource.PreviewDisplayName), target, seen);
            if (production.IsForest && production.SelectedForestResource != null)
                AddToList(FormatDisplayName(production.SelectedForestResource.PreviewDisplayName), target, seen);
            if (production.HasRecipe)
                AddToList(FormatDisplayName(production.RecipeDisplayName), target, seen);
            if (production.ContractedItem != null)
                AddToList(FormatDisplayName(production.ContractedItem.PreviewDisplayName), target, seen);
            if (!string.IsNullOrWhiteSpace(production.DefaultLabel))
                AddToList(FormatDisplayName(production.DefaultLabel), target, seen);
        }

        static void AddExistingChannels(LeylineConduit self, bool inputsOnly, List<string> results, HashSet<string> seen)
        {
            var kingdom = FoundationApi.Kingdom;
            if (kingdom == null)
                return;
            var workspaces = kingdom.Workspaces;
            for (int i = 0; i < workspaces.Count; i++)
            {
                var buildings = workspaces[i].Buildings;
                for (int j = 0; j < buildings.Count; j++)
                {
                    if (buildings[j]?.Behaviour is LeylineConduit peer
                        && peer != self
                        && (!inputsOnly || peer.Mode == ConduitMode.Input)
                        && !string.IsNullOrWhiteSpace(peer.Channel))
                    {
                        AddCandidate(peer.Channel, results, seen);
                    }
                }
            }
        }

        static bool IsRoutingNode(WorkspaceObject node)
        {
            if (node == null)
                return false;
            if (node.IsJunction)
                return true;
            string typeId = node.TypeId;
            return string.Equals(typeId, "Ledger", StringComparison.Ordinal)
                || string.Equals(typeId, "Filter", StringComparison.Ordinal)
                || string.Equals(typeId, "TrackSwitch", StringComparison.Ordinal)
                || string.Equals(typeId, "Timer", StringComparison.Ordinal);
        }

        static void AddCandidate(string value, List<string> results, HashSet<string> seen)
            => AddToList(Truncate(Sanitize(value), LeylineConduit.ChannelLimit), results, seen);

        static void AddToList(string value, List<string> target, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (seen != null && !seen.Add(value))
                return;
            if (seen == null && target.Contains(value))
                return;
            target.Add(value);
        }

        static string HumanizeToken(string token)
        {
            var builder = new StringBuilder(token.Length);
            bool capitalizeNext = true;
            for (int i = 0; i < token.Length; i++)
            {
                char ch = token[i];
                if (ch == '_' || ch == '-' || char.IsWhiteSpace(ch))
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                        builder.Append(' ');
                    capitalizeNext = true;
                    continue;
                }
                builder.Append(capitalizeNext ? char.ToUpper(ch, CultureInfo.InvariantCulture) : ch);
                capitalizeNext = false;
            }
            return builder.ToString().Trim();
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (!char.IsControl(ch))
                    builder.Append(ch);
            }
            return builder.ToString().Trim();
        }

        static string Truncate(string value, int limit)
            => value.Length <= limit ? value : value.Substring(0, limit).TrimEnd();
    }
}
