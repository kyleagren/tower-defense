using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CooldownRecoveryRune : Rune
{
    [Export] public float IncreasedRecoveryPercent { get; private set; } = 0.15f;

    public override string Name => "Tempo";

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    private static readonly Tag[] _tags = { Tag.Cooldown };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => $"Cooldowns recover {Mathf.RoundToInt(IncreasedRecoveryPercent * 100f)}% faster.";

    // Only a spell's own cooldown counts - one a rune adds (Manual activation) stays as it is.
    public override bool IsCompatibleWith(Spell spell) => spell.BaseCooldown > 0f && base.IsCompatibleWith(spell);

    public override void Apply(SpellCastContext context)
    {
        context.CooldownRecoveryRate.Increased += IncreasedRecoveryPercent;
    }
}
