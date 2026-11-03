using Assets.Source.Player;
using Assets.Source.Flow.Elements;

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
        static Portal portal;
        static Portal NativePortal => portal ?? (portal = new Portal());
        internal static int TransferMana => NativePortal.InputCost;
        internal static CurrencyCollection ResearchPrice() => TechNode.Get("portal").GetCost() * ResearchPortalMultiplier;
        internal static CurrencyCollection BuildPrice(int count) => NativePortal.GetPurchaseCost(count) * BuildPortalMultiplier;
        internal static void ApplyResearch(TechNode research)
        {
            research.CostMultiplier = ResearchPortalMultiplier;
            research.CostCurrency = CurrencyCollection.GoldMana;
            research.ResetCost();
        }
    }
}
