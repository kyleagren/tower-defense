using Godot;

// The direction picked for a Ritual that fires in a fixed direction (FixedDirectionRune). Flat.
public class AimPlacementData : PlacementData
{
    public const string NetworkKind = "aim";

    public AimPlacementData(Vector3 direction)
    {
        Direction = direction;
    }

    public Vector3 Direction { get; }

    public override void ApplyTo(SpellCastContext context)
    {
        if (context.FixedAim != null)
        {
            context.FixedAim.Direction = Direction;
        }
    }

    public override Godot.Collections.Array ToNetwork() => new() { NetworkKind, Direction };

    public static AimPlacementData Decode(Godot.Collections.Array data) => new(data[1].AsVector3());
}
