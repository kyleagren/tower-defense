using Godot;

// Holding advanced_tooltip shows the range circle of whichever Ritual the character is looking at,
// from much further away than repair or destroy can reach. Terrain blocks the look.
public partial class RitualRangeViewer : Node
{
    [Export] public float ViewRange { get; private set; } = 60f;

    // Resolved from the parent, as LookInteractor does, so it works in an inherited base scene.
    private PlayerCharacter _character;
    private Ritual _shown;

    // Reused every tick rather than made fresh, as LookInteractor's is.
    private readonly PhysicsRayQueryParameters3D _query = new()
    {
        CollisionMask = PhysicsLayers.RitualHitbox | PhysicsLayers.World
    };

    public override void _Ready()
    {
        _character = (PlayerCharacter)GetParent();
    }

    public override void _ExitTree()
    {
        Show(null);
    }

    public override void _PhysicsProcess(double delta)
    {
        bool viewing = _character.CanAct && Input.IsActionPressed("advanced_tooltip");
        Show(viewing ? FindRitualAhead() : null);
    }

    private Ritual FindRitualAhead()
    {
        _query.From = _character.Camera.GlobalPosition;
        _query.To = _character.GetCameraAimPoint(ViewRange);

        Godot.Collections.Dictionary result = _character.GetWorld3D().DirectSpaceState.IntersectRay(_query);
        return result.Count == 0 ? null : result["collider"].As<Node3D>()?.GetParent() as Ritual;
    }

    private void Show(Ritual ritual)
    {
        if (ritual == _shown)
        {
            return;
        }

        if (IsInstanceValid(_shown))
        {
            _shown.RangeDisplay.SetShown(false);
        }

        _shown = ritual;
        _shown?.RangeDisplay.SetShown(true);

        if (_character.IsLocallyControlled)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.InspectedRitualChanged, _shown);
        }
    }
}
