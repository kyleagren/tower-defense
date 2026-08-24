using System.Collections.Generic;
using System.Linq;

public class SpellInstance
{
    // a definition is which Spell this instance is using.
    private readonly Spell definition;
    private List<Rune> attachedRunes = new List<Rune>();

    public IReadOnlyList<Rune> AttachedRunes => attachedRunes;

    private float castTimeRemaining;
    private float cooldownRemaining;

    public bool IsCasting => castTimeRemaining > 0f;
    public bool IsOnCooldown => cooldownRemaining > 0f;
    public bool CanCast => !IsCasting && !IsOnCooldown;

    public SpellInstance(Spell definition)
    {
        this.definition = definition;
    }

    public void Tick(float delta, float recoveryRate = 1f)
    {
        if (castTimeRemaining > 0f)
        {
            castTimeRemaining -= delta * recoveryRate;
        }
        else if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= delta * recoveryRate;
        }
    }

    public SpellCastContext Resolve()
    {
        SpellCastContext context = definition.CreateBaseContext();
        foreach (Rune rune in attachedRunes)
        {
            rune.Apply(context);
        }

        context.Damage = context.Damage * (1f + context.IncreasedDamagePercent) * context.MoreDamageMultiplier;

        return context;
    }

    public void StartCast(float castTime, float cooldown)
    {
        castTimeRemaining = castTime;
        cooldownRemaining = cooldown;
    }

    public bool TryAttachRune(Rune rune)
    {
        bool hasRequiredTags = rune.RequiredTags.All(tag => definition.Tags.Contains(tag));
        if (!hasRequiredTags) return false;

        bool alreadyHasThisType = attachedRunes.Any(r => r.GetType() == rune.GetType());
        if (alreadyHasThisType) return false;

        attachedRunes.Add(rune);
        return true;
    }

    public bool DetachRune(Rune rune)
    {
        return attachedRunes.Remove(rune);
    }
}
