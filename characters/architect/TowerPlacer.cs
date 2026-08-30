using Godot;
using System.Collections.Generic;

public partial class TowerPlacer : Node
{
    [Export] public PlayerCharacter Architect { get; private set; }
    [Export] public PackedScene RitualScene { get; private set; }
    [Export] public float PlacementDistance = GameRules.RitualReach;
    [Export] public float GroundRaycastRange = 10f;

    public bool IsPlacing { get; private set; }

    private enum Stage { Ritual, WallStart, WallEnd }
    private Stage _stage;

    private Ritual _ghost;
    private MeshInstance3D _ghostMesh;
    private CollisionShape3D _ghostCollisionShape;
    private Spell _pendingSpell;
    private Godot.Collections.Array<Rune> _pendingRunes;
    private SpellCastContext _pendingContext;
    private bool _isValidPlacement;

    private Vector3 _cursorPosition;
    private Vector3 _wallStartPosition;
    private Vector3 _wallEndPosition;
    private bool _wallCostApplied;
    private readonly List<MeshInstance3D> _wallPreviewMeshes = new List<MeshInstance3D>();
    private MeshInstance3D _wallRangeIndicator;
    private MeshInstance3D _wallRangeBorder;
    private Label3D _costPreviewLabel;

    private static readonly StandardMaterial3D _validMaterial = CreateGhostMaterial(new Color(0.2f, 1f, 0.3f, 0.5f));
    private static readonly StandardMaterial3D _invalidMaterial = CreateGhostMaterial(new Color(1f, 0.2f, 0.2f, 0.5f));
    private static readonly StandardMaterial3D _rangeMaterial = CreateFlatOverlayMaterial(new Color(0.3f, 0.6f, 1f, 0.25f));
    private static readonly StandardMaterial3D _rangeBorderMaterial = CreateFlatOverlayMaterial(new Color(0.5f, 0.8f, 1f, 0.9f));
    private static readonly StandardMaterial3D _wallPreviewFadedMaterial = CreateGhostMaterial(new Color(0.2f, 1f, 0.3f, 0.2f));

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

    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Combat && IsPlacing)
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
        _pendingContext = SpellInstance.ResolvePreview(spell, runes);
        _wallCostApplied = false;

        IsPlacing = true;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, true);

        if (_pendingContext.Wall != null)
        {
            _stage = Stage.WallStart;
        }
        else
        {
            _stage = Stage.Ritual;
            BeginRitualGhost();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsPlacing)
        {
            return;
        }

        UpdateCursorPosition();

        switch (_stage)
        {
            case Stage.WallStart:
                ProcessWallStartStage();
                break;
            case Stage.WallEnd:
                ProcessWallEndStage();
                break;
            case Stage.Ritual:
                ProcessRitualStage();
                break;
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            CancelPlacement();
        }
    }

    private void UpdateCursorPosition()
    {
        Vector3 aheadPoint = Architect.GetCameraAimPoint(PlacementDistance);

        Vector3 rayStart = aheadPoint + Vector3.Up * GroundRaycastRange;
        Vector3 rayEnd = aheadPoint - Vector3.Up * GroundRaycastRange;

        PhysicsDirectSpaceState3D spaceState = Architect.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
        // World only — ignore rituals/obstacles so the cursor always lands at true ground height
        query.CollisionMask = PhysicsLayers.World;

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            _cursorPosition = (Vector3)result["position"];
        }
    }

    private void ProcessWallStartStage()
    {
        RebuildWallPreview(_cursorPosition, _cursorPosition);

        if (Input.IsActionJustPressed("confirm_placement"))
        {
            _wallStartPosition = _cursorPosition;
            _stage = Stage.WallEnd;
        }
    }

    private void ProcessWallEndStage()
    {
        if (_costPreviewLabel == null)
        {
            _costPreviewLabel = BuildCostPreviewLabel();
        }

        RebuildWallPreview(_wallStartPosition, _cursorPosition);
        bool affordable = UpdateCostPreviewLabel(_wallStartPosition, _cursorPosition);

        if (affordable && Input.IsActionJustPressed("confirm_placement"))
        {
            _wallEndPosition = _cursorPosition;
            ApplyWallCostScaling();
            UpdateCostPreviewLabel(_wallStartPosition, _wallEndPosition);
            FadeWallPreview();
            BuildWallRangeIndicator();
            _stage = Stage.Ritual;
            BeginRitualGhost();
        }
    }

    private void ProcessRitualStage()
    {
        _ghost.GlobalPosition = _pendingContext.Wall != null ? ClampToWallRange(_cursorPosition) : _cursorPosition;
        UpdateGhostValidity();

        if (Input.IsActionJustPressed("confirm_placement") && _isValidPlacement)
        {
            ConfirmPlacement();
        }
    }

    private void BeginRitualGhost()
    {
        _ghost = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(_ghost);
        _ghost.SetProcess(false);
        _ghost.SetPhysicsProcess(false);

        _ghostMesh = _ghost.GetNode<MeshInstance3D>("MeshInstance3D");
        _ghostCollisionShape = _ghost.GetNode<CollisionShape3D>("StaticBody3D/CollisionShape3D");
        _ghostCollisionShape.Disabled = true;

        if (_pendingContext.Wall == null)
        {
            _ghost.AddChild(BuildRangeIndicator(_pendingContext.Range));
        }
    }

    private void UpdateGhostValidity()
    {
        PhysicsDirectSpaceState3D spaceState = Architect.GetWorld3D().DirectSpaceState;

        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = _ghostCollisionShape.Shape,
            Transform = _ghostCollisionShape.GlobalTransform,
            CollisionMask = PhysicsLayers.Obstacles,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> overlaps = spaceState.IntersectShape(query);

        _isValidPlacement = overlaps.Count == 0 && _economy.CanAfford(_pendingContext);

        _ghostMesh.MaterialOverride = _isValidPlacement ? _validMaterial : _invalidMaterial;
    }

    // Clamps an arbitrary world point to the nearest point inside the wall's flat-capped
    // rectangular placement zone (see BuildWallRangeIndicator for the same shape drawn as UI).
    private Vector3 ClampToWallRange(Vector3 point)
    {
        Vector3 diff = _wallEndPosition - _wallStartPosition;
        float length = diff.Length();
        float range = _pendingContext.Range;

        if (length < 0.0001f)
        {
            Vector3 toPoint = point - _wallStartPosition;
            return toPoint.Length() <= range ? point : _wallStartPosition + toPoint.Normalized() * range;
        }

        Vector3 direction = diff / length;
        Vector3 perpendicular = new Vector3(-direction.Z, 0f, direction.X);

        Vector3 offset = point - _wallStartPosition;
        float along = Mathf.Clamp(offset.Dot(direction), 0f, length);
        float across = Mathf.Clamp(offset.Dot(perpendicular), -range, range);

        return _wallStartPosition + direction * along + perpendicular * across;
    }

    private void BuildWallRangeIndicator()
    {
        const float BorderThickness = 0.3f;

        Vector3 diff = _wallEndPosition - _wallStartPosition;
        float length = diff.Length();
        Vector3 direction = length > 0.0001f ? diff / length : Vector3.Forward;
        Vector3 perpendicular = new Vector3(-direction.Z, 0f, direction.X);
        float range = _pendingContext.Range;
        Vector3 up = Vector3.Up * 0.03f;

        Vector3 ToWorld(float along, float across) => _wallStartPosition + direction * along + perpendicular * across + up;

        Vector3 innerA = ToWorld(0f, range);
        Vector3 innerB = ToWorld(length, range);
        Vector3 innerC = ToWorld(length, -range);
        Vector3 innerD = ToWorld(0f, -range);

        SurfaceTool fillTool = new SurfaceTool();
        fillTool.Begin(Mesh.PrimitiveType.Triangles);
        AddQuad(fillTool, innerA, innerB, innerC, innerD);

        _wallRangeIndicator = new MeshInstance3D
        {
            Mesh = fillTool.Commit(),
            MaterialOverride = _rangeMaterial
        };
        GetTree().CurrentScene.AddChild(_wallRangeIndicator);

        Vector3 outerA = ToWorld(-BorderThickness, range + BorderThickness);
        Vector3 outerB = ToWorld(length + BorderThickness, range + BorderThickness);
        Vector3 outerC = ToWorld(length + BorderThickness, -range - BorderThickness);
        Vector3 outerD = ToWorld(-BorderThickness, -range - BorderThickness);

        SurfaceTool borderTool = new SurfaceTool();
        borderTool.Begin(Mesh.PrimitiveType.Triangles);
        AddQuad(borderTool, outerA, outerB, innerB, innerA);
        AddQuad(borderTool, outerD, outerC, innerC, innerD);
        AddQuad(borderTool, outerA, outerD, innerD, innerA);
        AddQuad(borderTool, outerB, outerC, innerC, innerB);

        _wallRangeBorder = new MeshInstance3D
        {
            Mesh = borderTool.Commit(),
            MaterialOverride = _rangeBorderMaterial
        };
        GetTree().CurrentScene.AddChild(_wallRangeBorder);
    }

    private static void AddQuad(SurfaceTool surfaceTool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        surfaceTool.AddVertex(a);
        surfaceTool.AddVertex(b);
        surfaceTool.AddVertex(c);

        surfaceTool.AddVertex(a);
        surfaceTool.AddVertex(c);
        surfaceTool.AddVertex(d);
    }

    private void RebuildWallPreview(Vector3 start, Vector3 end)
    {
        ClearWallPreview();

        WallCastData wallData = _pendingContext.Wall;
        int segmentCount = IceWall.ComputeSegmentCount(start.DistanceTo(end), wallData.SegmentWidth);
        int affordableSegments = ComputeAffordableSegmentCount(wallData);

        for (int i = 0; i < segmentCount; i++)
        {
            float t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            Vector3 position = start.Lerp(end, t);
            bool affordable = i < affordableSegments;

            MeshInstance3D previewMesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = 0f,
                    BottomRadius = wallData.SegmentWidth / 2f,
                    Height = wallData.SegmentWidth * 2f,
                    RadialSegments = 4
                },
                MaterialOverride = affordable ? _validMaterial : _invalidMaterial,
                Position = position + Vector3.Up * wallData.SegmentWidth
            };

            GetTree().CurrentScene.AddChild(previewMesh);
            _wallPreviewMeshes.Add(previewMesh);
        }
    }

    // The highest segment count still affordable given currently-held Mana and remaining
    // Capacity — since each extra segment adds a fixed cost, this is a clean cutoff rather than
    // something that needs checking per-segment independently.
    private int ComputeAffordableSegmentCount(WallCastData wallData)
    {
        float manaMultiplier = (1f + _pendingContext.IncreasedManaCostPercent) * _pendingContext.MoreManaCostMultiplier;
        float capacityMultiplier = (1f + _pendingContext.IncreasedCapacityCostPercent) * _pendingContext.MoreCapacityCostMultiplier;

        float manaPerSegment = wallData.ManaCostPerSegment * manaMultiplier;
        float capacityPerSegment = wallData.CapacityCostPerSegment * capacityMultiplier;
        int capacityRemaining = _economy.MaxCapacity - _economy.CapacityUsed;

        int manaMax = manaPerSegment > 0f
            ? Mathf.FloorToInt((_economy.Mana - _pendingContext.ManaCost) / manaPerSegment)
            : int.MaxValue;

        int capacityMax = capacityPerSegment > 0f
            ? Mathf.FloorToInt((capacityRemaining - _pendingContext.CapacityCost) / capacityPerSegment)
            : int.MaxValue;

        return Mathf.Max(0, Mathf.Min(manaMax, capacityMax));
    }

    private void FadeWallPreview()
    {
        foreach (MeshInstance3D mesh in _wallPreviewMeshes)
        {
            mesh.MaterialOverride = _wallPreviewFadedMaterial;
        }
    }

    private void ClearWallPreview()
    {
        foreach (MeshInstance3D mesh in _wallPreviewMeshes)
        {
            mesh.QueueFree();
        }

        _wallPreviewMeshes.Clear();
    }

    private Label3D BuildCostPreviewLabel()
    {
        Label3D label = new Label3D
        {
            FontSize = 32,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };

        GetTree().CurrentScene.AddChild(label);
        return label;
    }

    // While the wall's size is still being adjusted, shows a projected total (base + the
    // not-yet-committed segment cost) without touching _pendingContext, and reports whether the
    // current size is still affordable. Once ApplyWallCostScaling has actually locked the cost
    // in, just reflects the real resolved values and reports affordable (it was, or we wouldn't
    // have reached that point).
    private bool UpdateCostPreviewLabel(Vector3 start, Vector3 end)
    {
        WallCastData wallData = _pendingContext.Wall;
        int segmentCount = IceWall.ComputeSegmentCount(start.DistanceTo(end), wallData.SegmentWidth);

        float projectedMana = _pendingContext.ManaCost;
        float projectedCapacity = _pendingContext.CapacityCost;
        bool affordable = true;

        if (!_wallCostApplied)
        {
            float manaMultiplier = (1f + _pendingContext.IncreasedManaCostPercent) * _pendingContext.MoreManaCostMultiplier;
            float capacityMultiplier = (1f + _pendingContext.IncreasedCapacityCostPercent) * _pendingContext.MoreCapacityCostMultiplier;

            projectedMana += segmentCount * wallData.ManaCostPerSegment * manaMultiplier;
            projectedCapacity += segmentCount * wallData.CapacityCostPerSegment * capacityMultiplier;

            affordable = segmentCount <= ComputeAffordableSegmentCount(wallData);
        }

        _costPreviewLabel.Text = $"Segments: {segmentCount}\nMana: {Mathf.CeilToInt(projectedMana)}   Capacity: {Mathf.CeilToInt(projectedCapacity)}";
        _costPreviewLabel.Modulate = affordable ? Colors.White : Colors.Red;
        _costPreviewLabel.GlobalPosition = (start + end) / 2f + Vector3.Up * 2f;

        return affordable;
    }

    // Applies the rune-derived cost multipliers (already resolved onto _pendingContext from the
    // selected runes) to the per-segment cost too, so a cost-affecting rune scales the whole
    // total consistently rather than just the flat base.
    private void ApplyWallCostScaling()
    {
        WallCastData wallData = _pendingContext.Wall;
        int segmentCount = IceWall.ComputeSegmentCount(_wallStartPosition.DistanceTo(_wallEndPosition), wallData.SegmentWidth);

        float manaMultiplier = (1f + _pendingContext.IncreasedManaCostPercent) * _pendingContext.MoreManaCostMultiplier;
        float capacityMultiplier = (1f + _pendingContext.IncreasedCapacityCostPercent) * _pendingContext.MoreCapacityCostMultiplier;

        _pendingContext.ManaCost += segmentCount * wallData.ManaCostPerSegment * manaMultiplier;
        _pendingContext.CapacityCost += segmentCount * wallData.CapacityCostPerSegment * capacityMultiplier;
        _wallCostApplied = true;
    }

    private void ConfirmPlacement()
    {
        Ritual placed = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(placed);
        placed.GlobalTransform = _ghost.GlobalTransform;
        placed.Configure(_pendingSpell, _pendingRunes);

        if (_pendingContext.Wall != null)
        {
            placed.SetWallEndpoints(_wallStartPosition, _wallEndPosition);

            WallFootprintMarker marker = new WallFootprintMarker();
            placed.AddChild(marker);
            marker.Initialize(_wallStartPosition, _wallEndPosition, _pendingContext.Wall.SegmentWidth);
        }

        (int manaCost, int capacityCost) = _economy.SpendOnRitual(_pendingContext);
        placed.SetCost(manaCost, capacityCost);

        EndPlacement();
    }

    private void CancelPlacement()
    {
        EndPlacement();
    }

    private void EndPlacement()
    {
        ClearWallPreview();

        if (_costPreviewLabel != null)
        {
            _costPreviewLabel.QueueFree();
            _costPreviewLabel = null;
        }

        if (_wallRangeIndicator != null)
        {
            _wallRangeIndicator.QueueFree();
            _wallRangeIndicator = null;
        }

        if (_wallRangeBorder != null)
        {
            _wallRangeBorder.QueueFree();
            _wallRangeBorder = null;
        }

        if (_ghost != null)
        {
            _ghost.QueueFree();
            _ghost = null;
        }

        IsPlacing = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, false);
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

    // Flat, single-sided overlay quads (fill + border) whose triangle winding isn't hand-verified
    // against a specific viewing angle — cull disabled so they're visible from above regardless.
    private static StandardMaterial3D CreateFlatOverlayMaterial(Color color)
    {
        StandardMaterial3D material = CreateGhostMaterial(color);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return material;
    }
}
