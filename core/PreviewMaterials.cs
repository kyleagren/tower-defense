using Godot;

// Shared unshaded, translucent materials for placement previews and footprint markers.
public static class PreviewMaterials
{
    public static readonly StandardMaterial3D Valid = CreateGhost(new Color(0.2f, 1f, 0.3f, 0.5f));
    public static readonly StandardMaterial3D Invalid = CreateGhost(new Color(1f, 0.2f, 0.2f, 0.5f));
    public static readonly StandardMaterial3D Faded = CreateGhost(new Color(0.2f, 1f, 0.3f, 0.2f));
    public static readonly StandardMaterial3D OtherPlayer = CreateGhost(new Color(1f, 1f, 1f, 0.35f));

    public static readonly StandardMaterial3D RangeFill = CreateFlatOverlay(new Color(0.3f, 0.6f, 1f, 0.25f));
    public static readonly StandardMaterial3D RangeBorder = CreateFlatOverlay(new Color(0.5f, 0.8f, 1f, 0.9f));
    public static readonly StandardMaterial3D WallFootprint = CreateFlatOverlay(new Color(0.6f, 0.9f, 1f, 0.8f));

    public static readonly ShaderMaterial RangeVolume = CreateRangeVolume();

    public static StandardMaterial3D CreateGhost(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }

    // Flat, single-sided overlay quads whose triangle winding isn't hand-verified against a
    // specific viewing angle — cull disabled so they're visible from above regardless.
    public static StandardMaterial3D CreateFlatOverlay(Color color)
    {
        StandardMaterial3D material = CreateGhost(color);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return material;
    }

    private static ShaderMaterial CreateRangeVolume()
    {
        ShaderMaterial material = new() { Shader = GD.Load<Shader>("res://rituals/visual_effects/range_volume.gdshader") };
        material.SetShaderParameter("up_weight", GameRules.RitualRangeUpWeight);
        material.SetShaderParameter("down_weight", GameRules.RitualRangeDownWeight);
        return material;
    }
}
