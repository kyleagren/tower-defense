using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Ritual : Node3D, IDamageable
{
    [Export] public Spell Spell { get; private set; }
    [Export] public Rune[] RuneSlots { get; private set; } = new Rune[GameRules.MaxRuneSlots];
    [Export] public Node3D SpawnPoint { get; private set; }
    [Export] public float TargetSearchInterval = 0.25f;
    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;
    [Export] public float MaxHealth = 100f;
    [Export] public HealthBar3D HealthBar { get; private set; }
    [Export] public MeshInstance3D CoreMesh { get; private set; }

    private static readonly StandardMaterial3D _disabledMaterial = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.35f, 0.35f, 0.35f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    private SpellInstance _spellInstance;
    private CharacterSheet _ownerSheet;
    private Enemy _currentTarget;
    private float _searchTimer;
    private int _manaCost;
    private int _capacityCost;
    private List<CastEffect> _castEffects;
    private bool _hasWarnedAboutCastEffects;
    private float _currentHealth;
    private bool _disabled;
    private Material _activeMaterial;

    public bool IsDestroyed => _disabled;
    public Vector3 AimPoint => CoreMesh.GlobalPosition;

    public Vector3? WallStart { get; private set; }
    public Vector3? WallEnd { get; private set; }

    // So a CastEffect can act on the target this Ritual already picked rather than searching again.
    public Enemy CurrentTarget => _currentTarget;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        _activeMaterial = CoreMesh.MaterialOverride;
        RitualRegistry.Register(this);
        HealthBar.SetFraction(1f);
        HealthBar.Visible = false;

        _castEffects = GetChildren().OfType<CastEffect>().ToList();

        foreach (CastEffect effect in _castEffects)
        {
            effect.Concluded += OnCastEffectConcluded;
        }

        EventBus.Instance.InventoryChanged += OnInventoryChanged;

        // A ritual placed by RitualPlacer is configured explicitly right after AddChild, so only
        // one authored into a scene with a Spell already assigned configures itself here.
        if (Spell != null)
        {
            Configure(Spell, RuneSlots);
        }
    }

    public override void _ExitTree()
    {
        EventBus.Instance.InventoryChanged -= OnInventoryChanged;
        RitualRegistry.Unregister(this);
    }

    // Equipment changing anywhere drops both cached layers - the resolved context and the owner
    // sheet it was resolved against. Cheap even when it wasn't really this Ritual's own owner,
    // since both are rebuilt lazily on next use rather than here.
    private void OnInventoryChanged()
    {
        _ownerSheet = null;
        _spellInstance?.InvalidateCache();
    }

    public void SetOwnerId(PlayerId owner)
    {
        OwnerId = owner;
        _ownerSheet = null;
    }

    // The owner's sheet is held rather than re-fetched per frame: _Process calls this every tick,
    // and the lookup behind it walks PlayerSentinels -> SaveManager -> a dictionary read. Both this
    // and SpellInstance's own cache are dropped together by OnInventoryChanged.
    private SpellCastContext ResolveWithOwner()
    {
        _ownerSheet ??= CharacterSheetCache.GetSheet(PlayerSentinels.Get(OwnerId));
        return _spellInstance.Resolve(_ownerSheet);
    }

    private void OnCastEffectConcluded()
    {
        _spellInstance.EndOngoingEffect(ResolveWithOwner().ResolvedCooldown);
    }

    public void TakeDamage(DamagePacket damage, Vector3 impulse = default)
    {
        if (_disabled)
        {
            return;
        }

        _currentHealth -= damage.Total;
        HealthBar.SetFraction(Mathf.Max(0f, _currentHealth) / MaxHealth);
        HealthBar.Visible = true;

        if (_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            _disabled = true;
            CoreMesh.MaterialOverride = _disabledMaterial;
        }
    }

    public void Repair()
    {
        float missingFraction = 1f - (_currentHealth / MaxHealth);

        if (missingFraction <= 0f)
        {
            return;
        }

        int repairCost = Mathf.CeilToInt(_manaCost * GameRules.RepairCostFraction * missingFraction);

        if (!EconomyManager.Instance.TrySpendMana(repairCost))
        {
            return;
        }

        _currentHealth = MaxHealth;
        _disabled = false;
        CoreMesh.MaterialOverride = _activeMaterial;
        HealthBar.SetFraction(1f);
        HealthBar.Visible = false;
    }

    public void SetCost(int manaCost, int capacityCost)
    {
        _manaCost = manaCost;
        _capacityCost = capacityCost;
    }

    public void SetWallEndpoints(Vector3 start, Vector3 end)
    {
        WallStart = start;
        WallEnd = end;
    }

    public void Destroy()
    {
        EconomyManager.Instance.RefundRitual(_manaCost, _capacityCost);
        QueueFree();
    }

    public void Configure(Spell spell, IReadOnlyList<Rune> runes)
    {
        Spell = spell;
        _spellInstance = new SpellInstance(spell);

        for (int i = 0; i < RuneSlots.Length; i++)
        {
            RuneSlots[i] = i < runes.Count ? runes[i] : null;
        }

        foreach (Rune rune in runes)
        {
            if (rune != null)
            {
                _spellInstance.TryAttachRune(rune);
            }
        }

        // Placeholder: colors the Ritual by its spell's dominant element instead of a flat scene
        // default, reusing the same element->color mapping floating combat text and enemy status
        // overlays already use. GetForSpell (not the raw Get) so a damage-less elemental spell like
        // Ice Wall still resolves to its Tag.Cold color instead of falling through to white/grey.
        Color elementColor = DamageTypeColors.GetForSpell(spell);
        _activeMaterial = new StandardMaterial3D { AlbedoColor = elementColor };
        CoreMesh.MaterialOverride = _activeMaterial;
    }

    public override void _Process(double delta)
    {
        // Null until Configure runs — placement ghosts and scene-authored rituals with no Spell
        // assigned both sit in this state.
        if (_spellInstance == null || _disabled)
        {
            return;
        }

        SpellCastContext context = ResolveWithOwner();

        _spellInstance.Tick((float)delta);
        _searchTimer -= (float)delta;

        bool hadTarget = _currentTarget != null;

        if (_currentTarget != null && (!IsInstanceValid(_currentTarget) || _currentTarget.IsDead))
        {
            _currentTarget = null;
        }

        bool justLostTarget = hadTarget && _currentTarget == null;

        if (_searchTimer <= 0f || justLostTarget)
        {
            _searchTimer = TargetSearchInterval;
            _currentTarget = EnemyTargeting.FindTarget(GlobalPosition, context.Range.Value, context.TargetPriority);
        }

        if (_spellInstance.CanCast && CanCastNow(context))
        {
            CastAtTarget(context);
        }
    }

    private bool CanCastNow(SpellCastContext context)
    {
        if (context.CastTrigger != null)
        {
            return context.CastTrigger.ShouldCast(this, context);
        }

        return !Spell.RequiresTarget || _currentTarget != null;
    }

    private void CastAtTarget(SpellCastContext context)
    {
        CastEffect effect = SelectCastEffect(context);

        if (effect == null)
        {
            return;
        }

        if (_currentTarget != null)
        {
            Vector3 targetPosition = _currentTarget.GlobalPosition;
            Vector3 levelAimPosition = new Vector3(targetPosition.X, SpawnPoint.GlobalPosition.Y, targetPosition.Z);
            SpawnPoint.LookAt(levelAimPosition, Vector3.Up);
        }

        effect.Cast(context, SpawnPoint, this);

        if (effect.IsOngoing)
        {
            _spellInstance.StartCast(context.ResolvedCastTime, 0f);
            _spellInstance.BeginOngoingEffect();
        }
        else
        {
            _spellInstance.StartCast(context.ResolvedCastTime, context.ResolvedCooldown);
        }
    }

    // Picks the one CastEffect child that handles this context. Which effect wins is otherwise
    // decided silently by child order in the scene, so both "none matched" (a ritual that burns
    // its cooldown firing nothing) and "several matched" (an ambiguous scene) get reported once.
    private CastEffect SelectCastEffect(SpellCastContext context)
    {
        CastEffect match = null;

        foreach (CastEffect candidate in _castEffects)
        {
            if (!candidate.CanHandle(context))
            {
                continue;
            }

            if (match == null)
            {
                match = candidate;
                continue;
            }

            WarnAboutCastEffectsOnce(
                $"Ritual '{Spell?.Name}' has more than one CastEffect that can handle its context " +
                $"({match.Name} and {candidate.Name}); using {match.Name} because it comes first in the scene.");
            break;
        }

        if (match == null)
        {
            WarnAboutCastEffectsOnce(
                $"Ritual '{Spell?.Name}' has no CastEffect that can handle its context and will never fire. " +
                "Add the matching CastEffect node to the ritual scene.");
        }

        return match;
    }

    private void WarnAboutCastEffectsOnce(string message)
    {
        if (_hasWarnedAboutCastEffects)
        {
            return;
        }

        _hasWarnedAboutCastEffects = true;
        GD.PushWarning(message);
    }
}
