using Godot;

public partial class TowerPlacer : Node
{
    [Export] public PlayerCharacter Architect { get; private set; }
    [Export] public PackedScene RitualScene { get; private set; }
    [Export] public float PlacementDistance = GameRules.TowerReach;
    [Export] public float GroundRaycastRange = 10f;

    public bool IsPlacing { get; private set; }

    private Ritual _ghost;
    private MeshInstance3D _ghostMesh;
    private CollisionShape3D _ghostCollisionShape;
    private Spell _pendingSpell;
    private Godot.Collections.Array<Rune> _pendingRunes;
    private bool _isValidPlacement;

    private static readonly StandardMaterial3D _validMaterial = CreateGhostMaterial(new Color(0.2f, 1f, 0.3f, 0.5f));
    private static readonly StandardMaterial3D _invalidMaterial = CreateGhostMaterial(new Color(1f, 0.2f, 0.2f, 0.5f));
    private static readonly StandardMaterial3D _rangeMaterial = CreateGhostMaterial(new Color(0.3f, 0.6f, 1f, 0.25f));

    private EconomyManager _economy;

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        _economy = EconomyManager.Instance;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        if (phase == WaveController.GamePhase.Combat && IsPlacing)
        {
            CancelPlacement();
        }
    }

    public void BeginPlacement(Spell spell, Godot.Collections.Array<Rune> runes)
    {
        if (IsPlacing)
        {
            return;
        }

        _pendingSpell = spell;
        _pendingRunes = runes;

        _ghost = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(_ghost);
        _ghost.SetProcess(false);
        _ghost.SetPhysicsProcess(false);

        _ghostMesh = _ghost.GetNode<MeshInstance3D>("MeshInstance3D");
        _ghostCollisionShape = _ghost.GetNode<CollisionShape3D>("StaticBody3D/CollisionShape3D");
        _ghostCollisionShape.Disabled = true;

        float effectiveRange = ComputePreviewRange(spell, runes);
        _ghost.AddChild(BuildRangeIndicator(effectiveRange));

        IsPlacing = true;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsPlacing)
        {
            return;
        }

        UpdateGhostPosition();
        UpdateGhostValidity();

        if (Input.IsActionJustPressed("confirm_placement") && _isValidPlacement)
        {
            ConfirmPlacement();
        }
        else if (Input.IsActionJustPressed("ui_cancel"))
        {
            CancelPlacement();
        }
    }

    private void UpdateGhostPosition()
    {
        Vector3 aheadPoint = Architect.GetCameraAimPoint(PlacementDistance);

        Vector3 rayStart = aheadPoint + Vector3.Up * GroundRaycastRange;
        Vector3 rayEnd = aheadPoint - Vector3.Up * GroundRaycastRange;

        PhysicsDirectSpaceState3D spaceState = Architect.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
        query.CollisionMask = 1; // World only — ignore towers/obstacles so the ghost always lands at true ground height

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            _ghost.GlobalPosition = (Vector3)result["position"];
        }
    }

    private void UpdateGhostValidity()
    {
        PhysicsDirectSpaceState3D spaceState = Architect.GetWorld3D().DirectSpaceState;

        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = _ghostCollisionShape.Shape,
            Transform = _ghostCollisionShape.GlobalTransform,
            CollisionMask = 16,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> overlaps = spaceState.IntersectShape(query);
        _isValidPlacement = overlaps.Count == 0 && _economy.CanAfford(_pendingSpell, _pendingRunes.Count);

        _ghostMesh.MaterialOverride = _isValidPlacement ? _validMaterial : _invalidMaterial;
    }

    private void ConfirmPlacement()
    {
        Ritual placed = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(placed);
        placed.GlobalTransform = _ghost.GlobalTransform;
        placed.Configure(_pendingSpell, _pendingRunes);

        int cost = _economy.SpendOnTower(_pendingSpell, _pendingRunes.Count);
        placed.SetBudgetCost(cost);

        EndPlacement();
    }

    private void CancelPlacement()
    {
        EndPlacement();
    }

    private void EndPlacement()
    {
        _ghost.QueueFree();
        _ghost = null;
        IsPlacing = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, false);
    }

    private static float ComputePreviewRange(Spell spell, Godot.Collections.Array<Rune> runes)
    {
        SpellInstance previewInstance = new SpellInstance(spell);
        foreach (Rune rune in runes)
        {
            if (rune != null)
            {
                previewInstance.TryAttachRune(rune);
            }
        }

        return previewInstance.Resolve().Range;
    }

    private static MeshInstance3D BuildRangeIndicator(float range)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = range, BottomRadius = range, Height = 0.05f },
            MaterialOverride = _rangeMaterial,
            Position = new Vector3(0f, 0.03f, 0f)
        };
    }

    private static StandardMaterial3D CreateGhostMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }
}
