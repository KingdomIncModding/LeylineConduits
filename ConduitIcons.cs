using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KingdomLeylineConduits
{
    internal static class ConduitIcons
    {
        internal static Sprite Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { return SpriteLibrary.Get(id); }
            catch (KeyNotFoundException) { return null; }
            catch (ArgumentException) { return null; }
        }
        internal static List<(string icon, string name)> Catalog() => SpriteLibrary.GetUsableIconNames()
            .Where(option => !string.IsNullOrEmpty(option.icon) && Find(option.icon))
            .GroupBy(option => option.icon, StringComparer.Ordinal)
            .Select(group => group.First()).ToList();
    }
}
