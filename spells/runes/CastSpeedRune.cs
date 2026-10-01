using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CastSpeedRune : Rune
{
    [Export] public float IncreasedCastSpeedPercent { get; private set; } = 0.15f;

    public override string Name => "Haste";

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    private static readonly Tag[] _tags = { Tag.Cooldown };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Reduces cast time, letting this ritual act more often.";

    // A spell's own cooldown dwarfs its cast time, so Haste only fits one without. A cooldown a rune
    // adds (Manual activation) doesn't change that.
    public override bool IsCompatibleWith(Spell spell) => spell.BaseCooldown <= 0f && base.IsCompatibleWith(spell);

    public override void Apply(SpellCastContext context)
    {
        context.CastSpeed.Increased += IncreasedCastSpeedPercent;
    }
}
