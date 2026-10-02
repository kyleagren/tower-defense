using Godot;

// Holds the cast until an enemy in range that the Ritual can see carries Status, so a spell that only
// strikes marked enemies never fires into an unmarked crowd. Needs the Ritual's own target too, since
// that's where the cast is centered.
[GlobalClass]
public partial class StatusInRangeCastTrigger : CastTrigger
{
    [Export] public Tag Status { get; set; }

    public override bool ShouldCast(Ritual caster, SpellCastContext context) =>
        caster.CurrentTarget != null
        && EnemyTargeting.FindNearest(caster.GlobalPosition, context.Range.Value,
            predicate: enemy => enemy.Status == Status, sightFrom: caster.SpawnPoint.GlobalPosition) != null;
}
