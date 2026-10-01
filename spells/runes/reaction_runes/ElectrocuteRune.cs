using Godot;
using System.Collections.Generic;

// Water and Lightning meeting, either way round. The struck enemy and every enemy near it carrying
// the same Status are electrocuted, spending theirs too - shorter than a freeze, as the trade for
// spreading.
[GlobalClass]
public partial class ElectrocuteRune : ReactionRune
{
    [Export] public float ElectrocutedDuration { get; private set; } = 1f;
    [Export] public float RecoveryDuration { get; private set; } = 6f;
    [Export] public float SpreadRadius { get; private set; } = 4f;

    public override string Name => "Electrocute";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Water, Tag.Lightning };

    public override string Description =>
        $"When Water and Lightning meet on an enemy, consumes its Status to electrocute it for {ElectrocutedDuration:0.#}s - along with every enemy within {SpreadRadius:0.#}m carrying the same Status. Each can't be electrocuted again for {RecoveryDuration:0.#}s after.";

    public override bool OnHit(OnHitContext hit)
    {
        Tag? status = hit.Target.Status;
        Tag? incoming = hit.Damage.DominantElement;

        if (!(status == Tag.Water && incoming == Tag.Lightning) && !(status == Tag.Lightning && incoming == Tag.Water))
        {
            return false;
        }

        hit.Target.ApplyDebuff(new ElectrocutedAilment(ElectrocutedDuration, RecoveryDuration));

        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(hit.Target.GlobalPosition, SpreadRadius,
            enemy => enemy != hit.Target && !enemy.IsDead && enemy.Status == status))
        {
            enemy.ApplyDebuff(new ElectrocutedAilment(ElectrocutedDuration, RecoveryDuration));
            enemy.RemoveStatus();
        }

        return true;
    }
}
