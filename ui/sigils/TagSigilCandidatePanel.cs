using Godot;
using System.Collections.Generic;

public partial class TagSigilCandidatePanel : Control
{
    public enum OrderingStrategy
    {
        Angular,
        Star,
        ActivationOrder,
        SeededShuffle
    }

    [Export] public OrderingStrategy Strategy = OrderingStrategy.Angular;
    [Export] public float Radius = 80f;
    [Export] public Color LineColor = new Color(1f, 0.85f, 0.3f);
    [Export] public float LineWidth = 3f;

    private Vector2[] _points = System.Array.Empty<Vector2>();

    public void Refresh(HashSet<Tag> activeTags, List<Tag> activationOrder)
    {
        Vector2 center = Size / 2f;

        _points = Strategy switch
        {
            OrderingStrategy.Angular => TagSigilGenerator.AngularOrder(activeTags, center, Radius),
            OrderingStrategy.Star => TagSigilGenerator.StarOrder(activeTags, center, Radius),
            OrderingStrategy.ActivationOrder => TagSigilGenerator.ActivationOrder(activationOrder, center, Radius),
            OrderingStrategy.SeededShuffle => TagSigilGenerator.SeededShuffleOrder(activeTags, center, Radius),
            _ => System.Array.Empty<Vector2>()
        };

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_points.Length < 2)
        {
            return;
        }

        DrawPolyline(_points, LineColor, LineWidth);
    }
}
