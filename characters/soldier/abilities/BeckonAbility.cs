using Godot;

// README's Beckon (Characters): a field that drags enemies in and roots them. Shares its slot and
// cooldown with Purge.
public partial class BeckonAbility : PlacedFieldAbility
{
    // Newtons per meter from the center - a spring, so the pull eases off as enemies arrive, and
    // heavier enemies come in slower.
    [Export] public float PullStiffness { get; private set; } = 720f;

    public override string DisplayName => "Beckon";
    public override string Description => "A field at the crosshair drags enemies in and holds them there.";

    protected override void ConfigureField(EffectVolume field)
    {
        ((BeckonField)field).Configure(PullStiffness);
    }
}
