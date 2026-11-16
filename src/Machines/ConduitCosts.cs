using System;
using KingdomFoundation.Api;

namespace KingdomLeylineConduits
{
    internal static class ConduitCosts
    {
        // Archived 0.2.0 balance: GoldMana, Enchanter tier, gold multiplier 1.
        // The retired testing profile was 1 copper for research and construction.
        internal const double OriginalBuildMultiplier = .25;
        internal const double OriginalResearchMultiplier = .5;
        internal const double ResearchPortalMultiplier = 4;
        internal const double BuildPortalMultiplier = 2;

        internal static int TransferMana
        {
            get
            {
                var kingdom = FoundationApi.Kingdom;
                if (kingdom == null)
                    return 12;
                var tier2 = FoundationApi.ResearchAccess.Find("portal_cost_reduce_2");
                if (tier2 != null && kingdom.HasResearch(tier2))
                    return 3;
                var tier1 = FoundationApi.ResearchAccess.Find("portal_cost_reduce");
                if (tier1 != null && kingdom.HasResearch(tier1))
                    return 6;
                return 12;
            }
        }

        internal static GameMoney ResearchPrice()
        {
            var portal = FoundationApi.ResearchAccess.Find("portal");
            var result = new GameMoney();
            if (portal == null)
                return result;
            var baseCost = portal.GetCost();
            foreach (var entry in baseCost.Entries)
                result.Add(entry.CurrencyId, (long)Math.Round(entry.Amount * ResearchPortalMultiplier));
            return result;
        }
    }
}

