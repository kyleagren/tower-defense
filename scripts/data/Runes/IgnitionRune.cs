using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class IgnitionRune : ReactionRune
{
    [Export] public float BurnDamagePerSecond { get; private set; } = 20f;
    [Export] public float BurnDuration { get; private set; } = 3f;

    public override string Name => "Ignition";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Fire, Tag.Lightning };

    public override string Description => "When Fire and Lightning damage cross paths on the same enemy, ignites them into a burning Ailment.";

    public override void OnHit(Enemy target, DamagePacket incomingDamage)
    {
        Tag? incomingElement = incomingDamage.DominantElement;
        Tag? existingStatus = target.Status;

        bool isFireLightningPair =
            (incomingElement == Tag.Fire && existingStatus == Tag.Lightning) ||
            (incomingElement == Tag.Lightning && existingStatus == Tag.Fire);

        if (isFireLightningPair)
        {
            target.ApplyAilment(new BurnAilment(BurnDamagePerSecond, BurnDuration));
        }
    }
}
