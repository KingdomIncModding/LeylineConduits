using System;
using System.Collections.Generic;
using KingdomFoundation.Api;
using UnityEngine;

namespace KingdomLeylineConduits
{
    internal static class ConduitIcons
    {
        static Dictionary<Sprite, string> spriteLookup;

        internal static Sprite Find(string id)
        {
            if (string.IsNullOrEmpty(id) || string.Equals(id, ConduitEmblem.IconId, StringComparison.Ordinal))
                return null;
            return FoundationApi.Presentation.FindSprite(id);
        }

        internal static string FromItem(GameInventoryItem item)
        {
            if (item == null)
                return "";
            var itemSprite = item.Icon;
            string typeIcon = item.Type?.IconName;
            if (!string.IsNullOrEmpty(typeIcon))
            {
                var resolved = Find(typeIcon);
                if (resolved && (!itemSprite || resolved == itemSprite))
                    return typeIcon;
            }
            if (itemSprite)
            {
                spriteLookup ??= BuildSpriteIdLookup();
                if (spriteLookup.TryGetValue(itemSprite, out var matched))
                    return matched;
            }
            return !string.IsNullOrEmpty(typeIcon) && Find(typeIcon) ? typeIcon : "";
        }

        internal static List<(string icon, string name)> Catalog()
        {
            var entries = new List<(string icon, string name)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in FoundationApi.ItemAccess.Items)
            {
                if (string.IsNullOrEmpty(item.IconName) || !seen.Add(item.IconName) || !Find(item.IconName))
                    continue;
                entries.Add((item.IconName, item.PreviewDisplayName));
            }
            var bySprite = spriteLookup ??= BuildSpriteIdLookup();
            foreach (GameCurrency currency in Enum.GetValues(typeof(GameCurrency)))
            {
                var sprite = currency.GetIcon();
                if (!sprite || !bySprite.TryGetValue(sprite, out var iconId) || !seen.Add(iconId))
                    continue;
                entries.Add((iconId, currency.GetDisplayName()));
            }
            return entries;
        }

        static Dictionary<Sprite, string> BuildSpriteIdLookup()
        {
            var map = new Dictionary<Sprite, string>();
            foreach (var iconName in FoundationApi.Presentation.IconNames)
            {
                if (string.IsNullOrEmpty(iconName) || string.Equals(iconName, ConduitEmblem.IconId, StringComparison.Ordinal))
                    continue;
                var sprite = FoundationApi.Presentation.FindSprite(iconName);
                if (sprite && !map.ContainsKey(sprite))
                    map[sprite] = iconName;
            }
            return map;
        }
    }
}

