using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    internal static class PortColors
    {
        internal static void Postfix(Component view, WorkspacePort io)
        {
            if (!(io?.Parent?.Behaviour is LeylineConduit))
                return;
            var image = FoundationApi.WorkspaceVisuals.PortGraphic(view, GamePortGraphic.Sprite) as Image;
            if (!image)
                return;
            image.sprite = FoundationApi.WorkspaceVisuals.PortSprite(view, io.IsInput, io.HasBuffer ? 0 : 2);
        }
    }
}

