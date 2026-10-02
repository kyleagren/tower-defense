using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ChillRune : OnHitRune
{
    [Export] public float SlowPercent { get; private set; } = 0.25f;
    [Export] public float Duration { get; private set; } = 3f;

    public override string Name => "Chill";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Cold };

    private static readonly Tag[] _tags = { Tag.Utility };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Cold damage also slows the enemies it hits.";

    public override string EffectLabel => "Enemies chilled";

    // Never consumes a Status: it reads the incoming hit's own element, not anything already on the
    // enemy, which is what separates it from a reaction.
    public override bool OnHit(OnHitContext hit)
    {
        if (hit.Damage.ColdDamage > 0f && hit.Target.ApplyDebuff(new ChillAilment(SlowPercent, Duration)))
        {
            CountEffect(hit.Attribution);
        }

        return false;
    }
}
