using Godot;

public partial class TowerPlacer : Node
{
    [Export] public PlayerCharacter Architect { get; private set; }
    [Export] public PackedScene RitualScene { get; private set; }
    [Export] public float PlacementDistance = GameRules.TowerReach;
    [Export] public float GroundRaycastRange = 10f;

    public bool IsPlacing { get; private set; }

    private Ritual ghost;
    private MeshInstance3D ghostMesh;
    private CollisionShape3D ghostCollisionShape;
    private Spell pendingSpell;
    private Godot.Collections.Array<Rune> pendingRunes;
    private bool isValidPlacement;

    private static readonly StandardMaterial3D ValidMaterial = CreateGhostMaterial(new Color(0.2f, 1f, 0.3f, 0.5f));
    private static readonly StandardMaterial3D InvalidMaterial = CreateGhostMaterial(new Color(1f, 0.2f, 0.2f, 0.5f));
    private static readonly StandardMaterial3D RangeMaterial = CreateGhostMaterial(new Color(0.3f, 0.6f, 1f, 0.25f));

    private EconomyManager economy;

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        economy = GetTree().CurrentScene.GetNode<EconomyManager>("EconomyManager");
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

        pendingSpell = spell;
        pendingRunes = runes;

        ghost = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(ghost);
        ghost.SetProcess(false);
        ghost.SetPhysicsProcess(false);

        ghostMesh = ghost.GetNode<MeshInstance3D>("MeshInstance3D");
        ghostCollisionShape = ghost.GetNode<CollisionShape3D>("StaticBody3D/CollisionShape3D");
        ghostCollisionShape.Disabled = true;

        SpellInstance previewInstance = new SpellInstance(spell);
        foreach (Rune rune in runes)
        {
            if (rune != null)
            {
                previewInstance.TryAttachRune(rune);
            }
        }
        float effectiveRange = previewInstance.Resolve().Range;

        MeshInstance3D rangeIndicator = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = effectiveRange, BottomRadius = effectiveRange, Height = 0.05f },
            MaterialOverride = RangeMaterial,
            Position = new Vector3(0f, 0.03f, 0f)
        };
        ghost.AddChild(rangeIndicator);

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

        if (Input.IsActionJustPressed("confirm_placement") && isValidPlacement)
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
        Vector3 origin = Architect.Camera.GlobalPosition;
        Vector3 forward = -Architect.Camera.GlobalTransform.Basis.Z;
        float cameraSetback = origin.DistanceTo(Architect.GlobalPosition);
        Vector3 aheadPoint = origin + forward * (PlacementDistance + cameraSetback);

        Vector3 rayStart = aheadPoint + Vector3.Up * GroundRaycastRange;
        Vector3 rayEnd = aheadPoint - Vector3.Up * GroundRaycastRange;

        PhysicsDirectSpaceState3D spaceState = Architect.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
        query.CollisionMask = 1; // World only — ignore towers/obstacles so the ghost always lands at true ground height

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            ghost.GlobalPosition = (Vector3)result["position"];
        }
    }

    private void UpdateGhostValidity()
    {
        PhysicsDirectSpaceState3D spaceState = Architect.GetWorld3D().DirectSpaceState;

        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = ghostCollisionShape.Shape,
            Transform = ghostCollisionShape.GlobalTransform,
            CollisionMask = 16,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> overlaps = spaceState.IntersectShape(query);
        isValidPlacement = overlaps.Count == 0 && economy.CanAfford(pendingSpell, pendingRunes.Count);

        ghostMesh.MaterialOverride = isValidPlacement ? ValidMaterial : InvalidMaterial;
    }

    private void ConfirmPlacement()
    {
        Ritual placed = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(placed);
        placed.GlobalTransform = ghost.GlobalTransform;
        placed.Configure(pendingSpell, pendingRunes);

        int cost = economy.SpendOnTower(pendingSpell, pendingRunes.Count);
        placed.SetBudgetCost(cost);

        EndPlacement();
    }

    private void CancelPlacement()
    {
        EndPlacement();
    }

    private void EndPlacement()
    {
        ghost.QueueFree();
        ghost = null;
        IsPlacing = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, false);
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
