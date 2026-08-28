using Godot;
using System.Collections.Generic;

public partial class TagAnchorDisplay : Control
{
    [Export] public float Radius = 180f;
    [Export] public float DotSize = 6f;
    [Export] public Color InactiveColor = new Color(0.4f, 0.4f, 0.45f);
    [Export] public Color ActiveColor = new Color(1f, 0.85f, 0.3f);

    private HashSet<Tag> _activeTags = new HashSet<Tag>();

    public void SetActiveTags(HashSet<Tag> activeTags)
    {
        _activeTags = activeTags;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = Size / 2f;

        foreach (Tag tag in TagSigilGenerator.AllTags)
        {
            Vector2 position = TagSigilGenerator.GetAnchorPosition(tag, center, Radius);
            Color color = _activeTags.Contains(tag) ? ActiveColor : InactiveColor;
            DrawCircle(position, DotSize, color);
        }
    }
}
