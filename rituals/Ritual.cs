using Godot;
using System.Collections.Generic;

// A placed Ritual: its spell and runes, who owns it, and the cast loop. Everything else it does lives
// on its child components - Health, Targeting, Resonance, Readiness, RangeDisplay and StatsSync -
// wired in ritual.tscn.
public partial class Ritual : Node3D, IDamageable
{
    [Export] public Spell Spell { get; private set; }
    [Export] public Rune[] RuneSlots { get; private set; } = new Rune[GameRules.MaxRuneSlots];
    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;
    [Export] public Node3D SpawnPoint { get; private set; }
    [Export] public MeshInstance3D CoreMesh { get; private set; }
    [Export] public float ShroudedTransparency { get; private set; } = 0.6f;

    [Export] public RitualHealth Health { get; private set; }
    [Export] public RitualTargeting Targeting { get; private set; }
    [Export] public RitualResonance Resonance { get; private set; }
    [Export] public RitualReadiness Readiness { get; private set; }
    [Export] public RitualRangeDisplay RangeDisplay { get; private set; }
    [Export] public RitualStatsSync StatsSync { get; private set; }

    private static readonly StandardMaterial3D _brokenMaterial = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.35f, 0.35f, 0.35f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    private LevelContext _level;
    private SpellInstance _spellInstance;
    private StaticBody3D _body;
    private CharacterSheet _ownerSheet;
    private Material _activeMaterial;
    private float? _walkUpDistance;

    // A placement preview: shows the model and gives RitualPlacer a real collision shape to test,
    // but isn't a Ritual to anything else - it never registers, so enemies can't target it, and it
    // never casts. Set before the node enters the tree.
    public bool IsPreview { get; set; }

    // False until Configure runs - placement ghosts and scene-authored Rituals with no Spell assigned
    // both sit in this state.
    public bool IsConfigured => _spellInstance != null;

    public bool CanCast => _spellInstance?.CanCast == true;

    public int ManaCost { get; private set; }
    public int CapacityCost { get; private set; }

    public RitualStats Stats { get; } = new RitualStats();

    // What it's cost so far - its own price plus Sentinel repairs - for damage per mana.
    public float ManaPaid => ManaCost + Stats.RepairManaPaid;

    public Vector3 AimPoint => CoreMesh.GlobalPosition;

    // IDamageable only asks whether this can still take damage, which a broken Ritual can't.
    bool IDamageable.IsDestroyed => Health.IsBroken;

    public void TakeDamage(Hit hit) => Health.TakeDamage(hit);

    public bool IsShrouded => IsConfigured && ResolveWithOwner().Shrouded;

    // So a CastEffect can act on the target this Ritual already picked rather than searching again.
    public Enemy CurrentTarget => Targeting.Current;

    // Where a ground spell lands when the cast has no target enemy - a Resonant cast reaching toward
    // its source's target. Null outside such a cast.
    public Vector3? CastPoint => Resonance.CastPoint;

    // Where a spell that lands on a spot comes down: an Anchor Ritual's spot, else the target enemy,
    // else - for a Resonant cast with nothing in range - the ground point it reaches toward.
    public Vector3? LandingPoint(SpellCastContext context) =>
        context.FixedSpot != null ? (Vector3?)context.FixedSpot.Point : Targeting.Current?.GlobalPosition ?? Resonance.CastPoint;

    // How close an enemy on foot can get: the gap between this Ritual and the nearest navmesh point
    // walkable from the objective. Every lane reaches the objective, so that's every lane. Measured
    // once, since the navmesh doesn't change during a match.
    public float WalkUpDistance
    {
        get
        {
            _walkUpDistance ??= MeasureWalkUpDistance();
            return _walkUpDistance ?? 0f;
        }
    }

    // Null while the navigation map isn't queryable yet, so the next ask measures again.
    private float? MeasureWalkUpDistance()
    {
        DefendableObjective objective = _level.PrimaryObjective;
        if (objective == null)
        {
            return 0f;
        }

        Vector3[] path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, objective.GlobalPosition, GlobalPosition, true);
        if (path.Length == 0)
        {
            return null;
        }

        return path[^1].DistanceTo(GlobalPosition);
    }

    public override void _Ready()
    {
        _body = GetNode<StaticBody3D>("StaticBody3D");
        _activeMaterial = CoreMesh.MaterialOverride;
        Health.BrokenChanged += OnBrokenChanged;

        if (IsPreview)
        {
            SetPhysicsProcess(false);
            return;
        }

        _level = LevelContext.Current;
        _level.Rituals.Register(this);

        EventBus.Instance.InventoryChanged += OnInventoryChanged;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;

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
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        _level.Rituals.Unregister(this);
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Combat)
        {
            Stats.StartWave();
        }
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

    // The owner's sheet is held rather than re-fetched per frame: _PhysicsProcess calls this every
    // tick, and the lookup behind it walks PlayerProfiles -> a dictionary read. Both this and
    // SpellInstance's own cache are dropped together by OnInventoryChanged.
    public SpellCastContext ResolveWithOwner()
    {
        _ownerSheet ??= PlayerProfiles.Get(OwnerId).SelectedSheet;
        SpellCastContext context = _spellInstance.Resolve(_ownerSheet);

        // Stamped here rather than passed into SpellInstance, which stays ignorant of who owns it.
        context.Attribution = new Attribution { Player = OwnerId, Source = this };
        return context;
    }

    public void SetCost(int manaCost, int capacityCost)
    {
        ManaCost = manaCost;
        CapacityCost = capacityCost;
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

        // Rituals firing on cast time alone would just flicker the ring. Runes are fixed for the
        // Ritual's lifetime, so one that adds a cooldown is already counted here.
        Readiness.Setup(ResolveWithOwner().ResolvedCooldown > 0f, elementColor);
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
            if (node is MeshInstance3D meshInstance && !Health.HealthBar.IsAncestorOf(meshInstance))
            {
                meshInstance.Transparency = transparency;
            }
        }
    }

    // Broken at zero - greyed out until repaired.
    private void OnBrokenChanged(bool broken)
    {
        CoreMesh.MaterialOverride = broken ? _brokenMaterial : _activeMaterial;
    }

    // On the physics step, since targeting raycasts for line of sight. Only the host runs a Ritual;
    // everywhere else it's a copy.
    public override void _PhysicsProcess(double delta)
    {
        if (!IsConfigured || Health.IsBroken || !IsMultiplayerAuthority())
        {
            return;
        }

        SpellCastContext context = ResolveWithOwner();

        _spellInstance.Tick((float)delta);
        Targeting.Refresh(context, (float)delta);

        // A trigger rune's Ritual never casts on its own - only when pulsed.
        if (context.Trigger != null)
        {
            Resonance.Tick(context, (float)delta);
            return;
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

        return !Spell.RequiresTarget || Targeting.Current != null;
    }

    public void CastAtTarget(SpellCastContext context)
    {
        CastEffect effect = context.CastEffect;

        if (effect == null)
        {
            GD.PushError($"Spell '{Spell.Name}' sets no CastEffect in AddComponents and can never fire.");
            return;
        }

        if (context.FixedAim != null)
        {
            Targeting.AimAt(SpawnPoint.GlobalPosition + context.FixedAim.Direction * context.Range.Value);
        }
        else if (Targeting.Current != null)
        {
            Targeting.AimAt(Targeting.Current.AimPoint);
        }
        else if (Resonance.Aim.HasValue)
        {
            Targeting.AimAt(Resonance.Aim.Value);
        }

        context.ResonancePulse = new ResonancePulse { Origin = GlobalPosition, Caster = this };
        context.ChosenTarget = Targeting.Current;
        Stats.RecordCast();
        effect.Cast(context, SpawnPoint, this);

        if (effect.IsOngoing)
        {
            _spellInstance.StartCast(context.ResolvedCastTime, 0f);
            _spellInstance.BeginOngoingEffect();
            Readiness.Broadcast(0f, ongoing: true);
        }
        else
        {
            _spellInstance.StartCast(context.ResolvedCastTime, context.ResolvedCooldown);
            Readiness.Broadcast(context.ResolvedCastTime + context.ResolvedCooldown, ongoing: false);
        }
    }

    // Called by an ongoing CastEffect when whatever it left behind is gone - the cooldown starts now.
    public void ConcludeOngoingEffect()
    {
        float cooldown = ResolveWithOwner().ResolvedCooldown;
        _spellInstance.EndOngoingEffect(cooldown);
        Readiness.Broadcast(cooldown, ongoing: false);
    }

    public bool IsManual => IsConfigured && ResolveWithOwner().Trigger?.Manual == true;

    // Manual activation. Any player; a copy asks the host. Pressed while not ready, nothing happens,
    // and pressed with nothing in range, the cast goes off anyway - and is wasted.
    public void Activate()
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(1, MethodName.RequestActivate);
            return;
        }

        if (!IsManual || Health.IsBroken || !_spellInstance.CanCast)
        {
            return;
        }

        Targeting.DropIfDead();
        CastAtTarget(ResolveWithOwner());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestActivate()
    {
        if (NetworkSession.Instance.SenderSeat != default)
        {
            Activate();
        }
    }
}
