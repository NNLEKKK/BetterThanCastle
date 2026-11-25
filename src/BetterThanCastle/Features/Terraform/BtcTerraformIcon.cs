using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Icon of the Terraforming task (task list, new task grid, task beacon): a yellow shovel over a stepped slope, drawn once into a
    /// 64x64 texture in the style of the vanilla white task icons (no image file: the mod ships as one DLL).
    /// </summary>
    internal static class BtcTerraformIcon
    {
        private static Sprite _sprite;
        private const float Scale = 0.72f;   // drawing size inside the 64 px cell (user: smaller)

        internal static Sprite Get()
        {
            if (_sprite != null) return _sprite;
            const int N = 64, SS = 4;
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.name = "BtcTerraformIcon";
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            if (Inside(32f + (x + (sx + 0.5f) / SS - 32f) / Scale, 32f + (y + (sy + 0.5f) / SS - 32f) / Scale)) hit++;
                    px[y * N + x] = new Color32(255, 197, 10, (byte)(255 * hit / (SS * SS)));   // CastleYellow
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _sprite = Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f);
            _sprite.name = "BtcTerraformIcon";
            return _sprite;
        }

        // y grows upwards (texture space)
        private static bool Inside(float x, float y)
        {
            // stepped slope rising to the right: three blocks with a 2 px gap between the steps
            if (x >= 4f && x < 23f && y >= 4f && y < 16f) return true;
            if (x >= 25f && x < 42f && y >= 4f && y < 26f) return true;
            if (x >= 44f && x < 60f && y >= 4f && y < 36f) return true;
            // shovel: handle from the upper left down to the blade, a T grip at its top
            if (Seg(x, y, 12f, 58f, 29f, 37f, 2.2f)) return true;
            if (Seg(x, y, 7f, 54f, 17f, 62f, 2.2f)) return true;
            // blade: a rounded spade below the handle, pointing down-right
            float bx = 32.5f, by = 31.5f;
            float u = (x - bx) * 0.7071f - (y - by) * 0.7071f;   // along the handle direction (down-right)
            float v = (x - bx) * 0.7071f + (y - by) * 0.7071f;
            return u > -4f && u < 7f && Mathf.Abs(v) < 6f - Mathf.Max(0f, u - 2f) * 0.9f;
        }

        private static bool Seg(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float dx = bx - ax, dy = by - ay;
            float k = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float ex = ax + dx * k - px, ey = ay + dy * k - py;
            return ex * ex + ey * ey <= r * r;
        }
    }
}
