using KingdomFoundation.Api;

namespace KingdomLeylineConduits
{
    internal static class LoadedLayout
    {
        internal static void Postfix(WorkspaceObject element)
        {
            if (element?.Behaviour is LeylineConduit conduit)
                conduit.NormalizeLayout();
        }
    }
}

