using Godot;
using System.Collections.Generic;

public partial class Soldier : Sentinel
{
    [Export] public float AttackRange = 30f;
    [Export] public PackedScene ImpactScene { get; private set; }
    [Export] public Node3D MuzzlePoint { get; private set; }
    [Export] public PackedScene TracerScene { get; private set; }
    [Export] public float NormalFov = 75f;
    [Export] public float AdsFov = 40f;
    [Export] public float NormalSpringLength = 5f;
    [Export] public float AdsSpringLength = 0.5f;
    [Export] public float AdsTransitionSpeed = 10f;
    [Export] public float AdsSensitivityMultiplier = 0.5f;

    public override CharacterId Id => CharacterId.Soldier;

    private float _fireCooldown;
    private readonly List<MeshInstance3D> _weaponMeshes = new List<MeshInstance3D>();
    private StandardMaterial3D _weaponTintMaterial;
    private WeakPointVisorAbility _visor;
    private Color? _weaponTint;

    // The rifle's tint for its synchronizer - transparent for none. Can arrive before _Ready, which
    // applies it.
    public Color SyncedWeaponTint
    {
        get => _weaponTint ?? Colors.Transparent;
        set => SetWeaponTint(value.A > 0f ? value : null);
    }

    public override void _Ready()
    {
        base._Ready();

        foreach (Ability ability in AllAbilities)
        {
            if (ability is WeakPointVisorAbility visor)
            {
                _visor = visor;
            }
        }

        foreach (Node node in Weapon.FindChildren("*", nameof(MeshInstance3D)))
        {
            _weaponMeshes.Add((MeshInstance3D)node);
        }

        SetWeaponTint(_weaponTint);
    }

    // Recolors the whole rifle, or restores its authored look with null. The material is this
    // Soldier's own - the scene's rifle material is shared by every Soldier instance.
    public void SetWeaponTint(Color? color)
    {
        _weaponTint = color;

        if (color.HasValue)
        {
            _weaponTintMaterial ??= new StandardMaterial3D { EmissionEnabled = true, EmissionEnergyMultiplier = 0.6f };
            _weaponTintMaterial.AlbedoColor = color.Value;
            _weaponTintMaterial.Emission = color.Value;
        }

        foreach (MeshInstance3D mesh in _weaponMeshes)
        {
            mesh.MaterialOverride = color.HasValue ? _weaponTintMaterial : null;
        }
    }

    protected override bool IsInCombatAction =>
        CanUseCombatInput && (Input.IsActionPressed("attack") || Input.IsActionPressed("alt_action"));

    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        if (UpdateDeath((float)delta))
        {
            return;
        }

        UpdateShieldRegen((float)delta);
        UpdateHealthRegen((float)delta);

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir);

        UpdateAim(delta);
        AimWeapon();

        _fireCooldown -= (float)delta;

        if (CanUseWeaponInput && Input.IsActionPressed("attack") && _fireCooldown <= 0f)
        {
            _fireCooldown = 1f / AttackSpeed;
            FireShot();
        }
    }

    private bool IsAiming => CanUseCombatInput && Input.IsActionPressed("alt_action");

    protected override float BaseFov => IsAiming ? AdsFov : NormalFov;

    private void UpdateAim(double delta)
    {
        bool isAiming = IsAiming;
        float targetSpringLength = isAiming ? AdsSpringLength : NormalSpringLength;
        float targetSensitivityMultiplier = isAiming ? AdsSensitivityMultiplier : 1f;
        float weight = Mathf.Min(1f, (float)delta * AdsTransitionSpeed);

        SpringArm.SpringLength = Mathf.Lerp(SpringArm.SpringLength, targetSpringLength, weight);
        LookSensitivity = Mathf.Lerp(LookSensitivity, targetSensitivityMultiplier, weight);
    }

    // How far the rifle may turn and tilt to meet the crosshair. The turn is kept small, so a point
    // close beside or below the Soldier can't swing it out to the side.
    private const float MaxWeaponYawDegrees = 25f;
    private const float MaxWeaponPitchDegrees = 80f;

    // Reused every tick, like LookInteractor's.
    private readonly PhysicsRayQueryParameters3D _aimQuery = new() { CollisionMask = PhysicsLayers.Enemies | PhysicsLayers.World };

    // The rifle points at whatever the crosshair is on - the same line a shot takes - so tracers
    // leave the muzzle toward where the shot actually goes. Set as a turn and a tilt in the body's
    // own frame rather than a look-at, so it never rolls and always stays upright.
    private void AimWeapon()
    {
        Vector3 origin = Camera.GlobalPosition;
        Vector3 end = origin - Camera.GlobalTransform.Basis.Z * AttackRange;

        _aimQuery.From = origin;
        _aimQuery.To = end;
        Godot.Collections.Dictionary result = GetWorld3D().DirectSpaceState.IntersectRay(_aimQuery);

        Vector3 target = result.Count > 0 ? (Vector3)result["position"] : end;
        Vector3 local = GlobalBasis.Inverse() * (target - Weapon.GlobalPosition);

        // A point at or behind the muzzle - looking down past the Soldier's own feet - has no sensible
        // way to be aimed at, so the rifle follows the camera instead.
        if (local.Z > -0.1f)
        {
            local = GlobalBasis.Inverse() * -Camera.GlobalTransform.Basis.Z;
        }

        float yaw = Mathf.Atan2(-local.X, -local.Z);
        float pitch = Mathf.Atan2(local.Y, new Vector2(local.X, local.Z).Length());

        Weapon.Rotation = new Vector3(
            Mathf.Clamp(pitch, -Mathf.DegToRad(MaxWeaponPitchDegrees), Mathf.DegToRad(MaxWeaponPitchDegrees)),
            Mathf.Clamp(yaw, -Mathf.DegToRad(MaxWeaponYawDegrees), Mathf.DegToRad(MaxWeaponYawDegrees)),
            0f);
    }

    private void FireShot()
    {
        Vector3 origin = Camera.GlobalPosition;
        Vector3 forward = -Camera.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * AttackRange;

        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Enemies | PhysicsLayers.World;

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
        Vector3 tracerEnd = end;

        if (result.Count > 0)
        {
            tracerEnd = (Vector3)result["position"];

            if (result["collider"].As<Node3D>() is Enemy enemy)
            {
                enemy.TakeDamage(new Hit { Damage = RollShotDamage(enemy, origin, end, AttackDamage), Attribution = HitAttribution });
            }

            ShowImpact(tracerEnd);
        }

        ShowTracer(tracerEnd);
    }

    // Headshot, crit roll and weak point for one enemy along a shot's line. Overdrive's piercing
    // shots call this once per enemy they pass through.
    public DamagePacket RollShotDamage(Enemy enemy, Vector3 rayOrigin, Vector3 rayEnd, float baseDamage)
    {
        DamagePacket damage = RollAttackDamage(baseDamage, enemy.IsHeadshot(rayOrigin, rayEnd));

        if (enemy.IsWeakPointHit(rayOrigin, rayEnd))
        {
            damage *= _visor?.WeakPointMultiplier ?? 1f;
        }

        return damage;
    }

    public void ShowImpact(Vector3 position)
    {
        Rpc(MethodName.PlayImpact, position);
    }

    public void ShowTracer(Vector3 end)
    {
        Rpc(MethodName.PlayTracer, end);
    }

    // On every machine, this Soldier's own included.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PlayImpact(Vector3 position)
    {
        if (ImpactScene == null)
        {
            return;
        }

        ImpactFlash impact = ImpactScene.Instantiate<ImpactFlash>();
        LevelContext.Current.LevelRoot.AddChild(impact);
        impact.Show(position);
    }

    // A puppet's tint arrives through its synchronizer, so its tracers are colored too.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PlayTracer(Vector3 end)
    {
        if (TracerScene == null)
        {
            return;
        }

        ArcVisual tracer = TracerScene.Instantiate<ArcVisual>();

        // A tinted rifle fires tinted tracers, so Overdrive's shots take its element's color. The
        // scene's material is shared by every tracer, so this instance gets its own.
        if (_weaponTint.HasValue)
        {
            tracer.SegmentMaterial = TintedTracerMaterial((StandardMaterial3D)tracer.SegmentMaterial, _weaponTint.Value);
        }

        LevelContext.Current.LevelRoot.AddChild(tracer);
        tracer.Show(MuzzlePoint.GlobalPosition, end);
    }

    private StandardMaterial3D _tracerTintMaterial;

    private StandardMaterial3D TintedTracerMaterial(StandardMaterial3D authored, Color tint)
    {
        _tracerTintMaterial ??= (StandardMaterial3D)authored.Duplicate();
        _tracerTintMaterial.AlbedoColor = tint;
        _tracerTintMaterial.Emission = tint;
        return _tracerTintMaterial;
    }
}
