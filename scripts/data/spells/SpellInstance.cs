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
            }

            _cachedContext.Damage = _cachedContext.Damage * (1f + _cachedContext.IncreasedDamagePercent) * _cachedContext.MoreDamageMultiplier;
            _cachedContext.CastTime /= _cachedContext.CastSpeedMultiplier;
            _cachedContext.Range = _cachedContext.Range * (1f + _cachedContext.IncreasedRangePercent) * _cachedContext.MoreRangeMultiplier;

            if (_cachedContext.Area != null)
            {
                _cachedContext.Area.AreaMultiplier = _cachedContext.Area.AreaMultiplier * (1f + _cachedContext.Area.IncreasedAreaPercent) * _cachedContext.Area.MoreAreaMultiplier;
            }

            if (_cachedContext.Projectile != null)
            {
                _cachedContext.Projectile.Speed = _cachedContext.Projectile.Speed * (1f + _cachedContext.Projectile.IncreasedSpeedPercent) * _cachedContext.Projectile.MoreSpeedMultiplier;
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

        bool hasRequiredTags = rune.RequiredTags.All(tag => _definition.Tags.Contains(tag));
        if (!hasRequiredTags) return false;

        bool hasRequiredAnyTag = rune.RequiredAnyTags.Count == 0 || rune.RequiredAnyTags.Any(tag => _definition.Tags.Contains(tag));
        if (!hasRequiredAnyTag) return false;

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
}
