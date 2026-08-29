using Godot;
using System.Collections.Generic;

public partial class TagSigilCandidatePanel : Control
{
    [Export] public int Seed = 0;
    [Export] public float Radius = 80f;
    [Export] public Color LineColor = new Color(1f, 0.85f, 0.3f);
    [Export] public float LineWidth = 3f;

    private Vector2[] _segments = System.Array.Empty<Vector2>();

    public void Refresh(HashSet<Tag> activeTags)
    {
        Vector2 center = Size / 2f;
        _segments = TagSigilGenerator.RandomPairingEdges(activeTags, Seed, center, Radius);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_segments.Length < 2)
        {
            return;
        }

        DrawMultiline(_segments, LineColor, LineWidth);
    }
}
