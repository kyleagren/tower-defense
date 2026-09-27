using Godot;
using System.Collections.Generic;

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
    private float _currentHealth;
    private bool _disabled;
    private Material _activeMaterial;

    public bool IsDisabled => _disabled;

    // IDamageable only asks whether this can still take damage, which a disabled Ritual can't.
    bool IDamageable.IsDestroyed => IsDisabled;
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

        EventBus.Instance.InventoryChanged += OnInventoryChanged;

        // A Ritual the RitualRoster spawns is configured once it's ready, so only one authored into
        // a scene with a Spell already assigned configures itself here.
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
        SpellCastContext context = _spellInstance.Resolve(_ownerSheet);

        // Stamped here rather than passed into SpellInstance, which stays ignorant of who owns it.
        context.Attribution = new Attribution { Player = OwnerId, Source = this };
        return context;
    }

    // Called by an ongoing CastEffect when whatever it left behind is gone - the cooldown starts now.
    public void ConcludeOngoingEffect()
    {
        _spellInstance.EndOngoingEffect(ResolveWithOwner().ResolvedCooldown);
    }

    public void TakeDamage(Hit hit)
    {
        if (_disabled || !IsMultiplayerAuthority())
        {
            return;
        }

        SetHealth(Mathf.Max(0f, _currentHealth - hit.Damage.Total));
    }

    private float MissingHealthFraction => 1f - (_currentHealth / MaxHealth);

    // Half the Ritual's own cost, scaled by missing health. Zero at full health. The spell's own
    // cost only - placement extras like an Ice Wall's segments are left out, since the Ritual's own
    // health doesn't scale with them.
    public int RepairCost
    {
        get
        {
            float missingFraction = MissingHealthFraction;

            if (missingFraction <= 0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(ResolveWithOwner().ManaCost.Value * GameRules.RepairCostFraction * missingFraction);
        }
    }

    public void Repair()
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(1, MethodName.RequestRepair);
            return;
        }

        if (MissingHealthFraction <= 0f || !EconomyManager.Instance.TrySpendMana(RepairCost))
        {
            return;
        }

        SetHealth(MaxHealth);
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
        if (!IsMultiplayerAuthority())
        {
            RpcId(1, MethodName.RequestDestroy);
            return;
        }

        int manaRefund = Mathf.FloorToInt(_manaCost * (1f - GameRules.RepairCostFraction * MissingHealthFraction));
        EconomyManager.Instance.RefundRitual(manaRefund, _capacityCost);
        QueueFree();
    }

    // Any player can repair or tear down any Ritual.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestRepair()
    {
        if (NetworkSession.Instance.SeatOf(Multiplayer.GetRemoteSenderId()) != default)
        {
            Repair();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDestroy()
    {
        if (NetworkSession.Instance.SeatOf(Multiplayer.GetRemoteSenderId()) != default)
        {
            Destroy();
        }
    }

    // Only the host changes a Ritual's health; copies follow.
    private void SetHealth(float health)
    {
        ShowHealth(health);
        Rpc(MethodName.ReceiveHealth, health);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHealth(float health)
    {
        ShowHealth(health);
    }

    // Broken at zero - greyed out, and no longer casting or targetable until repaired.
    private void ShowHealth(float health)
    {
        _currentHealth = health;
        _disabled = health <= 0f;
        CoreMesh.MaterialOverride = _disabled ? _disabledMaterial : _activeMaterial;
        HealthBar.SetFraction(health / MaxHealth);
        HealthBar.Visible = health < MaxHealth;
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
        placement?.AttachTo(this, ResolveWithOwner());
    }

    // Resolved once here rather than per frame: a Ritual's rune loadout is fixed for its lifetime,
    // since runes are only socketed during Build. Leaving PhysicsLayers.Rituals is what lets enemies
    // and the player walk through; PhysicsLayers.RitualHitbox stays, so the ritual can still be shot,
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
        // assigned both sit in this state. Only the host runs a Ritual; everywhere else it's a copy.
        if (_spellInstance == null || _disabled || !IsMultiplayerAuthority())
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
        CastEffect effect = context.CastEffect;

        if (effect == null)
        {
            GD.PushError($"Spell '{Spell.Name}' sets no CastEffect in AddComponents and can never fire.");
            return;
        }

        if (_currentTarget != null)
        {
            AimSpawnPointAt(_currentTarget.AimPoint);
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

    // Straight at the target's center mass, so a projectile arrives where it's meant to hit. A
    // target almost directly above or below keeps the previous aim, since LookAt can't resolve a
    // direction parallel to its up vector - an enemy walking through a Shrouded Ritual can get there.
    private void AimSpawnPointAt(Vector3 target)
    {
        Vector3 toTarget = (target - SpawnPoint.GlobalPosition).Normalized();

        if (Mathf.Abs(toTarget.Dot(Vector3.Up)) > 0.999f)
        {
            return;
        }

        SpawnPoint.LookAt(target, Vector3.Up);
    }
}
