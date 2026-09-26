using Godot;

// README's Purge (Characters): a field that throws enemies out in pulses. Shares its slot and
// cooldown with Beckon.
public partial class PurgeAbility : PlacedFieldAbility
{
    // kg·m/s, like any knockback - divided by each enemy's mass.
    [Export] public float PushImpulse { get; private set; } = 1600f;

    public override string DisplayName => "Purge";
    public override string Description => "A field at the crosshair throws enemies out in pulses.";

    protected override void ConfigureField(EffectVolume field)
    {
        ((PurgeField)field).Configure(-Character.GlobalBasis.Z, PushImpulse);
    }
}
