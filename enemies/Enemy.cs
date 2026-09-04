using Godot;
using System.Collections.Generic;

public partial class Enemy : CharacterBody3D, IDamageable
{
    [Export] public CollisionShape3D BodyCollision { get; private set; }
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 100f;
    [Export] public float CenterMassHeight = 1f;
    [Export] public int ManaReward = 1;
    [Export] public int GoldReward = 1;
    [Export] public float DropChance = 0.3f;
    [Export] public int LootRolls = 1;
    [Export] public HealthBar3D HealthBar { get; private set; }
    [Export] public PackedScene FloatingTextScene { get; private set; }

    // TODO placeholder: remove once there's a real way to display an enemy's element/ailments (Section 15 iconography).
    private static readonly Dictionary<Tag, StandardMaterial3D> _statusMaterials = new Dictionary<Tag, StandardMaterial3D>
    {
        { Tag.Fire, CreateOverlayMaterial(new Color(1f, 0.5f, 0f)) },
        { Tag.Water, CreateOverlayMaterial(new Color(0.05f, 0.15f, 0.55f)) },
        { Tag.Cold, CreateOverlayMaterial(new Color(0.75f, 0.95f, 1f)) },
        { Tag.Lightning, CreateOverlayMaterial(new Color(1f, 0.9f, 0.1f)) }
    };

    private static readonly StandardMaterial3D _burnFlashMaterial = CreateOverlayMaterial(new Color(0.5f, 0f, 0f));
    private const float BurnFlashDuration = 0.1f;

    private static StandardMaterial3D CreateOverlayMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }

    private bool _isDead;
    private readonly List<Ailment> _ailments = new List<Ailment>();
    // TODO placeholder: remove alongside StatusMaterials/BurnFlashMaterial above.
    private readonly List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
    private float _burnFlashTimer;

    public bool IsDead => _isDead;
    public bool IsDestroyed => _isDead;
    public Tag? Status { get; private set; }

    // Remaining travel distance to the objective. Only meaningful for enemies that follow a lane
    // (WalkingEnemy updates it); anything else just leaves it at the default, which keeps it from
    // ever winning a ClosestToGoal targeting comparison.
    public float DistanceToGoal { get; protected set; } = float.MaxValue;

    protected float CurrentHealth { get; set; }

    public Vector3 CenterMass => GlobalPosition + Vector3.Up * CenterMassHeight;

    public Vector3 GetJitteredCenterMass(float jitterAmount)
    {
        Vector3 jitter = new Vector3(
            (float)GD.RandRange(-jitterAmount, jitterAmount),
            (float)GD.RandRange(-jitterAmount, jitterAmount),
            (float)GD.RandRange(-jitterAmount, jitterAmount)
        );

        return CenterMass + jitter;
    }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;

        // Group membership is for editor / remote-debugger inspection only — code paths that need
        // living enemies go through EnemyRegistry, which excludes ragdolls.
        AddToGroup("enemies");
        EnemyRegistry.Register(this);

        HealthBar.SetFraction(1f);
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;

        CollectMeshInstances(this);
    }

    // Safety net for enemies freed without dying — scene reload, in particular. Unregistering
    // twice is a no-op, so the Die() path unregistering first is unaffected.
    public override void _ExitTree()
    {
        EnemyRegistry.Unregister(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        TickAilments((float)delta);
        UpdateElementalVisual((float)delta); // TODO placeholder: remove once there's a real way to display this.
    }

    // Explicit implementation: the overload below carries extra optional parameters, so it can't
    // satisfy the interface signature on its own.
    void IDamageable.TakeDamage(DamagePacket damage, Vector3 impulse) => TakeDamage(damage, impulse);

    public void TakeDamage(DamagePacket amount, Vector3 ragdollImpulse = default, bool updateStatus = true, ReactionRune reaction = null)
    {
        if (_isDead)
        {
            return;
        }

        bool reactionTriggered = reaction?.OnHit(this, amount) ?? false;

        CurrentHealth -= amount.Total;
        HealthBar.SetFraction(CurrentHealth / MaxHealth);
        OnDamageTaken(amount);

        if (updateStatus)
        {
            if (reactionTriggered)
            {
                Status = null;
            }
            else
            {
                Tag? dominantElement = amount.DominantElement;
                if (dominantElement.HasValue)
                {
                    Status = dominantElement;
                }
            }
        }

        if (CurrentHealth <= 0f)
        {
            Die(ragdollImpulse);
        }
    }

    public void ApplyAilment(Ailment ailment)
    {
        if (ailment is BurnAilment newBurn)
        {
            BurnAilment existingBurn = _ailments.Find(a => a is BurnAilment) as BurnAilment;
            if (existingBurn != null)
            {
                newBurn.MergeFrom(existingBurn);
                _ailments.Remove(existingBurn);
            }
        }

        _ailments.Add(ailment);
    }

    // Universal death handling: unregister, hide the health bar, stop simulating, disable
    // collision, pay out rewards/loot, free after a delay. A subclass with more to do on death
    // (ragdoll physics) overrides this and calls base.Die() first — see WalkingEnemy. A subclass
    // that shouldn't die at all (a training dummy) overrides this and skips base entirely.
    public virtual void Die(Vector3 ragdollImpulse)
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        EnemyRegistry.Unregister(this);
        HealthBar.Visible = false;
        HealthBar.SetProcess(false);
        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, ManaReward, GoldReward, GlobalPosition, DropChance, LootRolls);

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        GetTree().CreateTimer(RagdollCleanupDelay).Timeout += QueueFree;
    }

    // No-op by default — only an enemy with a ragdoll-capable rig (WalkingEnemy) does anything here.
    public virtual void ApplyExtraRagdollImpulse(Vector3 impulse)
    {
    }

    // Spawns a floating damage number for this hit, colored by its dominant element. Virtual so a
    // subclass can also hook into "I just took damage" for its own purposes (TrainingDummy uses
    // this for DPS tracking) without overriding all of TakeDamage.
    protected virtual void OnDamageTaken(DamagePacket amount)
    {
        if (!DebugSettings.ShowFloatingCombatText || FloatingTextScene == null)
        {
            return;
        }

        FloatingCombatText text = FloatingTextScene.Instantiate<FloatingCombatText>();
        GetTree().CurrentScene.AddChild(text);
        text.Show(GetJitteredCenterMass(0.2f), amount.Total, DamageTypeColors.Get(amount.DominantElement));
    }

    private void TickAilments(float delta)
    {
        for (int i = _ailments.Count - 1; i >= 0; i--)
        {
            if (!_ailments[i].Tick(this, delta))
            {
                _ailments.RemoveAt(i);
            }
        }
    }

    // TODO placeholder block: remove this method and CollectMeshInstances once there's a real way to display an enemy's element/ailments.
    private void UpdateElementalVisual(float delta)
    {
        bool isBurning = _ailments.Exists(ailment => ailment is BurnAilment);

        if (isBurning)
        {
            _burnFlashTimer += delta;
            if (_burnFlashTimer >= 1f)
            {
                _burnFlashTimer -= 1f;
            }
        }
        else
        {
            _burnFlashTimer = 0f;
        }

        bool isFlashing = isBurning && _burnFlashTimer < BurnFlashDuration;

        Material overlay = null;
        if (isFlashing)
        {
            overlay = _burnFlashMaterial;
        }
        else if (Status.HasValue && _statusMaterials.TryGetValue(Status.Value, out StandardMaterial3D statusMaterial))
        {
            overlay = statusMaterial;
        }

        foreach (MeshInstance3D meshInstance in _meshInstances)
        {
            meshInstance.MaterialOverlay = overlay;
        }
    }

    private void CollectMeshInstances(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D meshInstance)
            {
                _meshInstances.Add(meshInstance);
            }

            CollectMeshInstances(child);
        }
    }
}
