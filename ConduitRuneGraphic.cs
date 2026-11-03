using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    // Draw the same full-color seal used by research and the build catalog.
    public sealed class ConduitRuneGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); var rect = rectTransform.rect; const int size = ConduitEmblem.Size;
            float dx = rect.width / size, dy = rect.height / size;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Color pixel = ConduitEmblem.Pixels[y * size + x];
                if (pixel.a == 0) continue;
                pixel *= color;
                int index = helper.currentVertCount;
                float left = rect.xMin + x * dx, bottom = rect.yMin + y * dy;
                helper.AddVert(new Vector3(left, bottom + dy), pixel, Vector2.zero);
                helper.AddVert(new Vector3(left + dx, bottom + dy), pixel, Vector2.zero);
                helper.AddVert(new Vector3(left + dx, bottom), pixel, Vector2.zero);
                helper.AddVert(new Vector3(left, bottom), pixel, Vector2.zero);
                helper.AddTriangle(index, index + 1, index + 2); helper.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
