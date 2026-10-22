using Godot;

// The range circle shown while a player Alt-looks at a Ritual - a lane for a fixed-aim Ritual, its spot
// for an Anchor one - plus the links to every Ritual it resonates with. Built fresh each time it's
// shown, so it matches the owner's current gear.
public partial class RitualRangeDisplay : Node3D
{
    private Ritual _ritual;
    private MeshInstance3D _rangeIndicator;
    private ResonanceOverlay _resonanceOverlay;

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
    }

    public void SetShown(bool shown)
    {
        if (!shown)
        {
            _rangeIndicator?.QueueFree();
            _rangeIndicator = null;
            _resonanceOverlay?.QueueFree();
            _resonanceOverlay = null;
            return;
        }

        if (_rangeIndicator != null || !_ritual.IsConfigured)
        {
            return;
        }

        SpellCastContext context = _ritual.ResolveWithOwner();

        if (context.FixedAim != null)
        {
            _rangeIndicator = RitualPlacer.BuildLaneIndicator(context.Range.Value, context.FixedAim.LaneHalfWidth);
            RitualPlacer.PointLaneIndicator(_rangeIndicator, context.FixedAim.Direction, context.Range.Value);
        }
        else if (context.FixedSpot != null)
        {
            _rangeIndicator = RitualPlacer.BuildRangeOutline(context.Range.Value);
            MeshInstance3D spot = RitualPlacer.BuildSpotIndicator(context.SpotRadius);
            spot.Position = context.FixedSpot.Point - _ritual.GlobalPosition - _rangeIndicator.Position;
            _rangeIndicator.AddChild(spot);
        }
        else
        {
            _rangeIndicator = RitualPlacer.BuildRangeIndicator(context.Range.Value);
        }

        AddChild(_rangeIndicator);

        if (context.Trigger?.Resonant == true)
        {
            _resonanceOverlay = new ResonanceOverlay();
            AddChild(_resonanceOverlay);
            _resonanceOverlay.Draw(context.ResonanceRadius, context.Range.Value, _ritual);
        }
    }
}
