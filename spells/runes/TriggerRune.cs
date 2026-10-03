using System;
using System.Collections.Generic;

// A rune that stops its Ritual casting on its own - it casts only when pulsed. A Ritual fields one.
public abstract partial class TriggerRune : Rune
{
    private static readonly Tag[] _tags = { Tag.Trigger };

    public override Type ExclusiveGroup => typeof(TriggerRune);

    public override IReadOnlyList<Tag> Tags => _tags;

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    // A spell whose own cast trigger decides when it casts (Ice Wall's) has no room for one.
    public override bool IsCompatibleWith(Spell spell) =>
        spell.CreateBaseContext().CastTrigger is not { AllowsTriggerRunes: false } && base.IsCompatibleWith(spell);
}
