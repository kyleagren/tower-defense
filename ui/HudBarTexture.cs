using Godot;

// A bordered rounded-rect bar, drawn once into an image. Shared by the HUD's vertical bars so they
// read as one family.
public static class HudBarTexture
{
    public static ImageTexture Build(int width, int height, int cornerRadius, float borderThickness, Color borderColor, Color fill)
    {
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool insideOuter = IsInsideRoundedRect(x + 0.5f, y + 0.5f, 0f, width, height, cornerRadius);
                bool insideFill = IsInsideRoundedRect(x + 0.5f, y + 0.5f, borderThickness, width, height, cornerRadius);
                Color pixel = insideFill ? fill : insideOuter ? borderColor : Colors.Transparent;
                image.SetPixel(x, y, pixel);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static bool IsInsideRoundedRect(float x, float y, float inset, int width, int height, int cornerRadius)
    {
        float left = inset;
        float top = inset;
        float right = width - inset;
        float bottom = height - inset;
        float radius = Mathf.Max(0f, cornerRadius - inset);

        if (x < left || x > right || y < top || y > bottom)
        {
            return false;
        }

        float cx = Mathf.Clamp(x, left + radius, right - radius);
        float cy = Mathf.Clamp(y, top + radius, bottom - radius);

        return new Vector2(x, y).DistanceTo(new Vector2(cx, cy)) <= radius;
    }
}
