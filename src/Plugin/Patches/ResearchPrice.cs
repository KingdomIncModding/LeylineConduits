using KingdomFoundation.Api;

namespace KingdomLeylineConduits
{
    internal static class ResearchPrice
    {
        internal static void Before(GameHookCall call)
        {
            // Derive the exact price from Portal, including native prestige discounts.
            call.SetResult(ConduitCosts.ResearchPrice());
            call.Cancel();
        }
    }
}

