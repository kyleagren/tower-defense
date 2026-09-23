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
    [Export] public float ShroudedTransparency { get; private set; } = 0.6f;

    private static readonly StandardMaterial3D _disabledMaterial = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.35f, 0.35f, 0.35f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    private LevelContext _level;
    private SpellInstance _spellInstance;
    private StaticBody3D _body;
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

    // So a CastEffect can act on the target this Ritual already picked rather than searching again.
    public Enemy CurrentTarget => _currentTarget;

    public bool IsShrouded => _spellInstance != null && ResolveWithOwner().Shrouded;

    // A placement preview: shows the model and gives RitualPlacer a real collision shape to test,
    // but isn't a Ritual to anything else - it never registers, so enemies can't target it, and it
    // never casts. Set before the node enters the tree.
    public bool IsPreview { get; set; }

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        _body = GetNode<StaticBody3D>("StaticBody3D");
        _activeMaterial = CoreMesh.MaterialOverride;
        HealthBar.SetFraction(1f);
        HealthBar.Visible = false;

        if (IsPreview)
        {
            SetProcess(false);
            return;
        }

        _level = LevelContext.Current;
        _level.Rituals.Register(this);

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
        if (IsPreview)
        {
            return;
        }

        EventBus.Instance.InventoryChanged -= OnInventoryChanged;
        _level.Rituals.Unregister(this);
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
        _spellInstance?.InvalidateCache();
    }

    // The owner's sheet is held rather than re-fetched per frame: _Process calls this every tick,
    // and the lookup behind it walks PlayerProfiles -> a dictionary read. Both this and
    // SpellInstance's own cache are dropped together by OnInventoryChanged.
    private SpellCastContext ResolveWithOwner()
    {
        _ownerSheet ??= PlayerProfiles.Get(OwnerId).SelectedSheet;
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

    private float MissingHealthFraction => 1f - (_currentHealth / MaxHealth);

    public void Repair()
    {
        float missingFraction = MissingHealthFraction;

        if (missingFraction <= 0f)
        {
            return;
        }

        // The spell's own cost only - placement extras like an Ice Wall's segments are left out,
        // since the Ritual's own health doesn't scale with them.
        float ownCost = ResolveWithOwner().ManaCost.Value;
        int repairCost = Mathf.CeilToInt(ownCost * GameRules.RepairCostFraction * missingFraction);

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

    // Mana back shrinks with damage by the same fraction repair charges, so a healthy Ritual
    // refunds everything and a broken one only part - tearing a broken Ritual down and rebuilding
    // it is never a way around repairing it. Capacity always returns in full: it's a ceiling on
    // what can be built, not a currency, so shorting it would lose it for the rest of the match.
    public void Destroy()
    {
        int manaRefund = Mathf.FloorToInt(_manaCost * (1f - GameRules.RepairCostFraction * MissingHealthFraction));
        EconomyManager.Instance.RefundRitual(manaRefund, _capacityCost);
        QueueFree();
    }

    public void Configure(Spell spell, IReadOnlyList<Rune> runes, PlacementData placement = null)
    {
        Spell = spell;
        _spellInstance = new SpellInstance(spell, placement);

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

        ApplyShroud();
    }

    // Resolved once here rather than per frame: a Ritual's rune loadout is fixed for its lifetime,
    // since runes are only socketed during Build. Leaving PhysicsLayers.Rituals is what lets enemies
    // and the player walk through; PhysicsLayers.RitualHitbox stays, so the tower can still be shot,
    // repaired and built around.
    private void ApplyShroud()
    {
        bool shrouded = IsShrouded;

        _body.CollisionLayer = shrouded
            ? _body.CollisionLayer & ~PhysicsLayers.Rituals
            : _body.CollisionLayer | PhysicsLayers.Rituals;

        float transparency = shrouded ? ShroudedTransparency : 0f;

        // Per-instance rather than through the materials, which are sub-resources shared by every
        // Ritual in the scene. owned: false to reach into instanced sub-scenes; the health bar is
        // excluded because its no-depth-test setup reads wrong faded.
        foreach (Node node in FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false))
        {
            if (node is MeshInstance3D meshInstance && !HealthBar.IsAncestorOf(meshInstance))
            {
                meshInstance.Transparency = transparency;
            }
        }
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
