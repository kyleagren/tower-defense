using System.Collections.Generic;
using System.Linq;

public class SpellInstance
{
    // a definition is which Spell this instance is using.
    private readonly Spell _definition;
    private List<Rune> _attachedRunes = new List<Rune>();
    private SpellCastContext _cachedContext;

    public IReadOnlyList<Rune> AttachedRunes => _attachedRunes;

    private float _castTimeRemaining;
    private float _cooldownRemaining;

    public bool IsCasting => _castTimeRemaining > 0f;
    public bool IsOnCooldown => _cooldownRemaining > 0f;
    public bool CanCast => !IsCasting && !IsOnCooldown;

    public SpellInstance(Spell definition)
    {
        _definition = definition;
    }

    public void Tick(float delta, float recoveryRate = 1f)
    {
        if (_castTimeRemaining > 0f)
        {
            _castTimeRemaining -= delta * recoveryRate;
        }
        else if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= delta * recoveryRate;
        }
    }

    public SpellCastContext Resolve()
    {
        if (_cachedContext == null)
        {
            _cachedContext = _definition.CreateBaseContext();

            foreach (Rune rune in _attachedRunes)
            {
                rune.Apply(_cachedContext);
                _cachedContext.ManaCost.More *= rune.ManaCostMultiplier;
            }
        }

        return _cachedContext;
    }

    public void StartCast(float castTime, float cooldown)
    {
        _castTimeRemaining = castTime;
        _cooldownRemaining = cooldown;
    }

    public bool TryAttachRune(Rune rune)
    {
        if (_attachedRunes.Count >= GameRules.MaxRuneSlots) return false;
        if (!rune.IsCompatibleWith(_definition.Tags)) return false;

        bool alreadyHasThisType = _attachedRunes.Any(r => r.GetType() == rune.GetType());
        if (alreadyHasThisType) return false;

        _attachedRunes.Add(rune);
        _cachedContext = null;
        return true;
    }

    public bool DetachRune(Rune rune)
    {
        bool removed = _attachedRunes.Remove(rune);
        if (removed)
        {
            _cachedContext = null;
        }

        return removed;
    }

    public static SpellCastContext ResolvePreview(Spell spell, IEnumerable<Rune> runes)
    {
        SpellInstance instance = new SpellInstance(spell);
        foreach (Rune rune in runes)
        {
            if (rune != null)
            {
                instance.TryAttachRune(rune);
            }
        }

        return instance.Resolve();
    }
}
