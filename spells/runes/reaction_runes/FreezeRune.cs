using Godot;
using System.Collections.Generic;

// Cold and Water meeting, either way round: frozen solid. A Frozen already in its slot - active or
// recovering - turns a new one away, but the Status is spent either way.
[GlobalClass]
public partial class FreezeRune : ReactionRune
{
    [Export] public float FrozenDuration { get; private set; } = 2f;
    [Export] public float RecoveryDuration { get; private set; } = 6f;

    public override string Name => "Freeze";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Cold, Tag.Water };

    public override string Description =>
        $"When Cold and Water meet on an enemy, consumes its Status to freeze it solid for {FrozenDuration:0.#}s. It can't be frozen again for {RecoveryDuration:0.#}s after.";

    public override string EffectLabel => "Freezes";

    public override bool OnHit(OnHitContext hit)
    {
        Tag? status = hit.Target.Status;
        Tag? incoming = hit.Damage.DominantElement;

        if (!(status == Tag.Cold && incoming == Tag.Water) && !(status == Tag.Water && incoming == Tag.Cold))
        {
            return false;
        }

        if (hit.Target.ApplyDebuff(new FrozenAilment(FrozenDuration, RecoveryDuration)))
        {
            CountEffect(hit.Attribution);
        }

        return true;
    }
}
