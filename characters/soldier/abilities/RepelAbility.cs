using Godot;

// design_reference.md's Repel (Characters): a field that throws enemies out in pulses. Shares its slot and
// cooldown with Converge.
public partial class RepelAbility : PlacedFieldAbility
{
    // kg·m/s, like any knockback - divided by each enemy's mass.
    [Export] public float PushImpulse { get; private set; } = 1600f;

    public override string DisplayName => "Repel";
    public override string Description => "A field at the crosshair throws enemies out in pulses.";

    protected override void ConfigureField(EffectVolume field)
    {
        ((RepelField)field).Configure(-Character.GlobalBasis.Z, PushImpulse);
    }
}
