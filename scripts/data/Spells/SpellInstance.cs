using System.Collections.Generic;
using System.Linq;

public class SpellInstance
{
    // a definition is which Spell this instance is using.
    private readonly Spell definition;
    private List<Rune> attachedRunes = new List<Rune>();
    private SpellCastContext cachedContext;

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
        if (cachedContext == null)
        {
            cachedContext = definition.CreateBaseContext();
            foreach (Rune rune in attachedRunes)
            {
                rune.Apply(cachedContext);
            }

            cachedContext.Damage = cachedContext.Damage * (1f + cachedContext.IncreasedDamagePercent) * cachedContext.MoreDamageMultiplier;
            cachedContext.CastTime /= cachedContext.CastSpeedMultiplier;
        }

        return cachedContext;
    }

    public void StartCast(float castTime, float cooldown)
    {
        castTimeRemaining = castTime;
        cooldownRemaining = cooldown;
    }

    public bool TryAttachRune(Rune rune)
    {
        if (attachedRunes.Count >= GameRules.MaxRuneSlots) return false;

        bool hasRequiredTags = rune.RequiredTags.All(tag => definition.Tags.Contains(tag));
        if (!hasRequiredTags) return false;

        bool hasRequiredAnyTag = rune.RequiredAnyTags.Count == 0 || rune.RequiredAnyTags.Any(tag => definition.Tags.Contains(tag));
        if (!hasRequiredAnyTag) return false;

        bool alreadyHasThisType = attachedRunes.Any(r => r.GetType() == rune.GetType());
        if (alreadyHasThisType) return false;

        attachedRunes.Add(rune);
        cachedContext = null;
        return true;
    }

    public bool DetachRune(Rune rune)
    {
        bool removed = attachedRunes.Remove(rune);
        if (removed)
        {
            cachedContext = null;
        }

        return removed;
    }
}
