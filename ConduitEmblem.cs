using UnityEngine;

namespace KingdomLeylineConduits
{
    // Shared pixel artwork for the native research/build icons and our tile UI.
    internal static class ConduitEmblem
    {
        internal const string IconId = "LeylineConduits_Seal";
        internal const int Size = 32;
        internal static readonly Color32[] Pixels = Draw();
        static Sprite sprite;
        internal static Sprite Sprite
        {
            get
            {
                if (sprite) return sprite;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) {
                    name = IconId, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(Pixels); texture.Apply(false, false);
                sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
                sprite.name = IconId; return sprite;
            }
        }
        static Color32[] Draw()
        {
            var pixels = new Color32[Size * Size];
            Color32 ink = new Color32(43, 29, 28, 255), bronze = new Color32(135, 83, 42, 255),
                gold = new Color32(219, 165, 79, 255), light = new Color32(255, 219, 137, 255),
                stone = new Color32(49, 62, 67, 255), blue = new Color32(51, 141, 177, 255),
                cyan = new Color32(110, 224, 230, 255), glow = new Color32(213, 255, 240, 255);
            void Dot(int x, int y, Color32 c) { if (x >= 0 && x < Size && y >= 0 && y < Size) pixels[y * Size + x] = c; }
            for (int y = 2; y < 30; y++) for (int x = 2; x < 30; x++)
            {
                float dx = x - 15.5f, dy = y - 15.5f, r = dx * dx + dy * dy;
                if (r > 190) continue;
                Dot(x, y, r > 161 ? ink : r > 121 ? (x + y > 30 ? gold : bronze) : stone);
                if (r >= 138 && r <= 155 && y > 15) Dot(x, y, light);
            }
            // Cardinal rivets and a two-way leyline through the seal.
            foreach (int x in new[] { 3, 27 }) { Dot(x, 15, light); Dot(x, 16, gold); }
            foreach (int y in new[] { 3, 27 }) { Dot(15, y, light); Dot(16, y, gold); }
            for (int x = 6; x < 26; x++) { Dot(x, 15, blue); Dot(x, 16, cyan); }
            for (int d = 0; d < 4; d++) {
                Dot(6 + d, 16 + d, gold); Dot(6 + d, 15 - d, bronze);
                Dot(25 - d, 16 + d, gold); Dot(25 - d, 15 - d, bronze);
            }
            // Beveled crystal, outlined in aged gold like a medieval talisman.
            for (int y = 6; y <= 25; y++)
            {
                int half = Mathf.Min(y - 6, 25 - y) / 2;
                for (int x = 15 - half; x <= 16 + half; x++)
                {
                    bool edge = x == 15 - half || x == 16 + half || y == 6 || y == 25;
                    Dot(x, y, edge ? (x < 16 ? light : bronze) : x < 16 ? cyan : blue);
                }
            }
            for (int y = 13; y <= 19; y++) Dot(15, y, glow);
            return pixels;
        }
    }
}
