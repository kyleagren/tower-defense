using Godot;
using System;
using System.Collections.Generic;

public partial class TagSigilCandidatePanel : Control
{
    [Export] public float Radius = 80f;
    [Export] public Color LineColor = new Color(1f, 0.85f, 0.3f);
    [Export] public float LineWidth = 3f;

    private Vector2[] _segments = Array.Empty<Vector2>();

    public void SetPairing(IReadOnlyList<Tag> active, int[] targets)
    {
        if (targets == null || active.Count < 2)
        {
            _segments = Array.Empty<Vector2>();
        }
        else
        {
            Vector2 center = Size / 2f;
            _segments = new Vector2[active.Count * 2];

            for (int i = 0; i < active.Count; i++)
            {
                _segments[i * 2] = TagSigilGenerator.GetAnchorPosition(active[i], center, Radius);
                _segments[i * 2 + 1] = TagSigilGenerator.GetAnchorPosition(active[targets[i]], center, Radius);
            }
        }

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
