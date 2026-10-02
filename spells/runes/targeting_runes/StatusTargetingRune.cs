using System.Collections.Generic;

public abstract partial class StatusTargetingRune : TargetingRune
{
    private static readonly Tag[] _tags = { Tag.Targeting, Tag.TargetStatus };

    public override IReadOnlyList<Tag> Tags => _tags;

    protected override TargetPriority Priority => TargetPriority.HasStatus;

    // Null takes any Status.
    protected abstract Tag? Element { get; }

    private string StatusName => Element.HasValue ? $"the {Element} Status" : "a Status";

    protected override string TargetingDescription => $"This ritual targets enemies with {StatusName} first. With none in range, it targets as normal.";

    public override string Description => $"{TargetingDescription} Deals {MoreDamageToTarget * 100f:0.#}% more damage to its target if it has {StatusName}.";

    public static bool Carries(Enemy enemy, Tag? status) => enemy.Status.HasValue && (status == null || enemy.Status == status);

    public override bool BoostsHitOn(Enemy target) => Carries(target, Element);

    public override void Apply(SpellCastContext context)
    {
        base.Apply(context);
        context.TargetStatus = Element;
    }
}
