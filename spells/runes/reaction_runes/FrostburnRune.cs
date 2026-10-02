using Godot;
using System.Collections.Generic;

// Cold consuming Fire, one way: anything else landing on a Fire Status does nothing here.
[GlobalClass]
public partial class FrostburnRune : ReactionRune
{
    [Export] public float DamagePercent { get; private set; } = 0.35f;
    [Export] public float SlowPercent { get; private set; } = 0.15f;
    [Export] public float Duration { get; private set; } = 4f;

    public override string Name => "Frostburn";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Cold };

    public override string Description => "When Cold damage meets a Fire Status, consumes it to frostburn the enemy: Cold damage over time and a slight slow.";

    public override string EffectLabel => "Frostburns";

    public override bool OnHit(OnHitContext hit)
    {
        if (hit.Target.Status != Tag.Fire || hit.Damage.DominantElement != Tag.Cold)
        {
            return false;
        }

        if (hit.Target.ApplyDebuff(new FrostburnAilment(hit.Damage.Total * DamagePercent, SlowPercent, Duration, hit.Attribution)))
        {
            CountEffect(hit.Attribution);
        }

        return true;
    }
}
