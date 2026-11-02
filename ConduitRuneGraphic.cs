using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    // A code-drawn pixel rune, sized to match the native UI pixel grid.
    public sealed class ConduitRuneGraphic : MaskableGraphic
    {
        static readonly string[] Pixels = {
            "0000000110000000", "0000001111000000", "0000011001100000", "0000110000110000",
            "0001100110011000", "0011000110001100", "0110000110000110", "1100001111000011",
            "1100001111000011", "0110000110000110", "0011000110001100", "0001100110011000",
            "0000110000110000", "0000011001100000", "0000001111000000", "0000000110000000"
        };
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); var rect = rectTransform.rect; float dx = rect.width / 16, dy = rect.height / 16;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                if (Pixels[y][x] != '1') continue;
                int index = helper.currentVertCount;
                float left = rect.xMin + x * dx, top = rect.yMax - y * dy;
                helper.AddVert(new Vector3(left, top), color, Vector2.zero);
                helper.AddVert(new Vector3(left + dx, top), color, Vector2.zero);
                helper.AddVert(new Vector3(left + dx, top - dy), color, Vector2.zero);
                helper.AddVert(new Vector3(left, top - dy), color, Vector2.zero);
                helper.AddTriangle(index, index + 1, index + 2); helper.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
