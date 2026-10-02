using System.Collections.Generic;

public abstract partial class StatusTargetingRune : TargetingRune
{
    private static readonly Tag[] _tags = { Tag.Targeting, Tag.TargetStatus };

    public override IReadOnlyList<Tag> Tags => _tags;

    protected override TargetPriority Priority => TargetPriority.HasStatus;

    // Null takes any Status.
    protected abstract Tag? Element { get; }

    public override string Description =>
        $"This ritual targets enemies with {(Element.HasValue ? $"the {Element} Status" : "any Status")} first. With none in range, it targets as normal.";

    public override void Apply(SpellCastContext context)
    {
        base.Apply(context);
        context.TargetStatus = Element;
    }
}
