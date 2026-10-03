using Godot;

// Holds the cast until an enemy in range that the Ritual can see carries Status, so a spell that only
// strikes marked enemies never fires into an unmarked crowd. Needs the Ritual's own target too, since
// that's where the cast is centered.
[GlobalClass]
public partial class StatusInRangeCastTrigger : CastTrigger
{
    [Export] public Tag Status { get; set; }

    public override bool AllowsTriggerRunes => true;

    public override bool ShouldCast(Ritual caster, SpellCastContext context)
    {
        bool Carries(Enemy enemy) => enemy.Status == Status;

        // Anchored, only a carrier inside its spot counts.
        if (context.FixedSpot != null)
        {
            return EnemyTargeting.FindNearest(context.FixedSpot.Point, context.SpotRadius, predicate: Carries) != null;
        }

        return caster.CurrentTarget != null
            && EnemyTargeting.FindNearest(caster.GlobalPosition, context.Range.Value,
                predicate: Carries, sightFrom: caster.SpawnPoint.GlobalPosition) != null;
    }
}
