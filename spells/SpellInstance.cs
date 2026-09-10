using System.Collections.Generic;
using System.Linq;

public class SpellInstance
{
    private readonly Spell _definition;
    private readonly List<Rune> _attachedRunes = new List<Rune>();
    private SpellCastContext _cachedContext;

    public IReadOnlyList<Rune> AttachedRunes => _attachedRunes;

    private float _castTimeRemaining;
    private float _cooldownRemaining;
    private bool _isEffectOngoing;

    public bool IsCasting => _castTimeRemaining > 0f;
    public bool IsOnCooldown => _cooldownRemaining > 0f;
    public bool CanCast => !IsCasting && !IsOnCooldown && !_isEffectOngoing;

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

    public SpellCastContext Resolve(CharacterSheet ownerSheet = null)
    {
        if (_cachedContext == null)
        {
            _cachedContext = _definition.CreateBaseContext();

            foreach (Rune rune in _attachedRunes)
            {
                rune.Apply(_cachedContext);
                _cachedContext.ManaCost.More *= rune.ManaCostMultiplier;
                _cachedContext.DamageScale.More *= rune.DamageMultiplier;
            }

            if (ownerSheet != null)
            {
                _cachedContext.ApplyEquipmentBonuses(ownerSheet);
            }
        }

        return _cachedContext;
    }

    // Forces the next Resolve() to rebuild from scratch - used when something outside the normal
    // rune-attach/detach flow needs the cached context thrown away, e.g. the owner's gear changing.
    public void InvalidateCache()
    {
        _cachedContext = null;
    }

    public void StartCast(float castTime, float cooldown)
    {
        _castTimeRemaining = castTime;
        _cooldownRemaining = cooldown;
    }

    // For a spell whose CastEffect is still ongoing after Cast() returns (e.g. a wall that has
    // to break or expire first), the real cooldown doesn't start until EndOngoingEffect is
    // called later - CanCast stays false for the whole in-between period.
    public void BeginOngoingEffect()
    {
        _isEffectOngoing = true;
    }

    public void EndOngoingEffect(float cooldown)
    {
        _isEffectOngoing = false;
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
