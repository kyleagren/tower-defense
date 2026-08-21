using System.Collections.Generic;
using System.Linq;

public class SpellInstance
{
    // a definition is which Spell this instance is using. 
    private readonly Spell definition;
    private List<Rune> attachedRunes = new List<Rune>();

    public IReadOnlyList<Rune> AttachedRunes => attachedRunes;

    private float cooldownRemaining;
    public bool IsOnCooldown => cooldownRemaining > 0f;

    public SpellInstance(Spell definition)
    {
        this.definition = definition;
    }

    public void TickCooldown(float delta, float recoveryRate = 1f)
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= delta * recoveryRate;
        }
    }

    public void StartCooldown(float cooldownDuration)
    {
        cooldownRemaining = cooldownDuration;
    }

    public SpellCastContext Resolve()
    {
        SpellCastContext context = definition.CreateBaseContext();
        foreach (Rune rune in attachedRunes)
        {
            rune.Apply(context);
        }
        return context;
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