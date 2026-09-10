using Godot;
using System;

public partial class RitualPlacer : Node
{
    // Not exported - resolved from the parent instead of a wired NodePath, since this is always a
    // direct child of the character it belongs to and every PlayerCharacter subclass qualifies.
    // Wiring it as an export meant Godot had to type-check the assignment while building the scene
    // tree, which broke for the Sentinel's instance (living in the shared SentinelBody.tscn base
    // scene, inherited by Soldier/Tank): the base scene's root has no script yet at that point, so
    // the parent is still a bare CharacterBody3D and the cast fails. Same fix LookInteractor already
    // uses for the same reason.
    public PlayerCharacter Character { get; private set; }

    [Export] public PackedScene RitualScene { get; private set; }
    [Export] public float PlacementDistance = GameRules.RitualReach;
    [Export] public float GroundRaycastRange = 10f;

    // Which phase transition should cancel an in-progress placement for this placer's owner - the
    // Architect's own instance cancels on entering Combat (placement only happens during Build);
    // the Sentinel's instance (README section 17's mid-wave deployment) needs the opposite: cancel
    // on entering Build, since that's when the Sentinel itself goes inactive.
    [Export] public GamePhase CancelOnPhase = GamePhase.Combat;

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

    // Invoked once placement is actually confirmed (not on cancel) - lets a caller like
    // TokenDeployer know it should consume the token that was just spent.
    private Action _onConfirmed;

    private Vector3 _cursorPosition;
    private Vector3 _wallStartPosition;
    private Vector3 _wallEndPosition;
    private bool _wallCostApplied;
    private WallPlacementPreview _wallPreview;

    private BoxShape3D _segmentQueryBox;
    private PhysicsShapeQueryParameters3D _segmentQuery;

    private EconomyManager _economy;

    public override void _Ready()
    {
        Character = (PlayerCharacter)GetParent();
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        _economy = EconomyManager.Instance;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;

        // If the owning character is being torn down mid-placement (e.g. swapping bodies via a hub
        // station while a ghost is out), the ghost/wall preview would otherwise leak - they're
        // parented to the scene root, not to this node, so they have no other relationship to this
        // character being freed.
        if (IsPlacing)
        {
            CancelPlacement();
        }
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase == CancelOnPhase && IsPlacing)
        {
            CancelPlacement();
        }
    }

    public void BeginPlacement(Spell spell, Godot.Collections.Array<Rune> runes, Action onConfirmed = null)
    {
        if (IsPlacing)
        {
            return;
        }

        _pendingSpell = spell;
        _pendingRunes = runes;
        _pendingContext = SpellInstance.ResolvePreview(spell, runes);
        _onConfirmed = onConfirmed;
        _wallCostApplied = false;

        IsPlacing = true;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, true);

        if (_pendingContext.Wall != null)
        {
            _wallPreview = new WallPlacementPreview();
            GetTree().CurrentScene.AddChild(_wallPreview);
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

        if (Input.IsActionJustPressed("ui_cancel") || Input.IsActionJustPressed("alt_action"))
        {
            CancelPlacement();
        }
    }

    private void UpdateCursorPosition()
    {
        Vector3 aheadPoint = Character.GetCameraAimPoint(PlacementDistance);

        Vector3 rayStart = aheadPoint + Vector3.Up * GroundRaycastRange;
        Vector3 rayEnd = aheadPoint - Vector3.Up * GroundRaycastRange;

        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;
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
        WallCastData wallData = _pendingContext.Wall;
        bool[] segmentValid = ComputeSegmentValidity(_cursorPosition, _cursorPosition, wallData, ComputeAffordableSegmentCount(wallData));
        _wallPreview.UpdateWall(_cursorPosition, _cursorPosition, wallData, segmentValid);

        if (Input.IsActionJustPressed("confirm_placement"))
        {
            _wallStartPosition = _cursorPosition;
            _stage = Stage.WallEnd;
        }
    }

    private void ProcessWallEndStage()
    {
        WallCastData wallData = _pendingContext.Wall;
        int segmentCount = IceWall.ComputeSegmentCount(_wallStartPosition.DistanceTo(_cursorPosition), wallData.SegmentWidth);
        int affordableSegments = ComputeAffordableSegmentCount(wallData);

        bool[] segmentValid = ComputeSegmentValidity(_wallStartPosition, _cursorPosition, wallData, affordableSegments);
        _wallPreview.UpdateWall(_wallStartPosition, _cursorPosition, wallData, segmentValid);

        bool affordable = segmentCount <= affordableSegments;
        bool noOverlap = System.Array.TrueForAll(segmentValid, v => v);
        bool placementValid = affordable && noOverlap;

        ShowProjectedCost(_wallStartPosition, _cursorPosition, segmentCount, placementValid);

        if (placementValid && Input.IsActionJustPressed("confirm_placement"))
        {
            _wallEndPosition = _cursorPosition;
            ApplyWallCostScaling();

            ShowProjectedCost(_wallStartPosition, _wallEndPosition, segmentCount, affordable: true);
            _wallPreview.Freeze(_wallStartPosition, _wallEndPosition, _pendingContext.Range.Value);

            _stage = Stage.Ritual;
            BeginRitualGhost();
        }
    }

    // Mirrors UpdateGhostValidity's shape-overlap pattern, but per prospective segment position
    // along the line instead of a single point - a wall can overlap something in the middle
    // without overlapping at either end, so a single pass/fail cutoff (like affordability) isn't
    // enough here.
    // The shape and query params are built once and reused across segments and frames: this runs
    // every physics frame for the whole length of a wall placement, and allocating a BoxShape3D
    // plus a query per segment per frame churned dozens of engine objects a second while the
    // cursor sat still.
    private bool[] ComputeSegmentValidity(Vector3 start, Vector3 end, WallCastData wallData, int affordableSegments)
    {
        int segmentCount = IceWall.ComputeSegmentCount(start.DistanceTo(end), wallData.SegmentWidth);
        bool[] valid = new bool[segmentCount];

        PhysicsShapeQueryParameters3D query = GetSegmentQuery(wallData.SegmentWidth);
        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 ground = IceWall.SegmentPosition(start, end, i, segmentCount);
            query.Transform = new Transform3D(Basis.Identity, IceWall.SegmentBoxCenter(ground, wallData.SegmentWidth));

            bool overlaps = spaceState.IntersectShape(query).Count > 0;
            valid[i] = i < affordableSegments && !overlaps;
        }

        return valid;
    }

    private PhysicsShapeQueryParameters3D GetSegmentQuery(float segmentWidth)
    {
        if (_segmentQuery == null)
        {
            _segmentQueryBox = new BoxShape3D();
            _segmentQuery = new PhysicsShapeQueryParameters3D
            {
                Shape = _segmentQueryBox,
                CollisionMask = PhysicsLayers.Obstacles | PhysicsLayers.PlacementBlocked,
                CollideWithBodies = true,
                CollideWithAreas = false
            };
        }

        _segmentQueryBox.Size = IceWall.SegmentBoxSize(segmentWidth);
        return _segmentQuery;
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
            _ghost.AddChild(BuildRangeIndicator(_pendingContext.Range.Value));
        }
    }

    private void UpdateGhostValidity()
    {
        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;

        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = _ghostCollisionShape.Shape,
            Transform = _ghostCollisionShape.GlobalTransform,
            CollisionMask = PhysicsLayers.Obstacles | PhysicsLayers.PlacementBlocked,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> overlaps = spaceState.IntersectShape(query);
        bool overlapsOwnWall = _pendingContext.Wall != null && OverlapsOwnWallLine();

        _isValidPlacement = overlaps.Count == 0 && !overlapsOwnWall && _economy.CanAfford(_pendingContext);

        _ghostMesh.MaterialOverride = _isValidPlacement ? PreviewMaterials.Valid : PreviewMaterials.Invalid;
    }

    // The wall this tower belongs to has no physics presence yet at this point in placement - the
    // real IceWall only casts once an enemy triggers it, and WallFootprintMarker isn't built until
    // the whole placement is confirmed - so this checks the ghost's position against the wall's own
    // line directly, using the same discrete segment positions IceWall/WallFootprintMarker use.
    private bool OverlapsOwnWallLine()
    {
        WallCastData wallData = _pendingContext.Wall;
        int segmentCount = IceWall.ComputeSegmentCount(_wallStartPosition.DistanceTo(_wallEndPosition), wallData.SegmentWidth);

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 segmentPosition = IceWall.SegmentPosition(_wallStartPosition, _wallEndPosition, i, segmentCount);

            if (_ghost.GlobalPosition.DistanceTo(segmentPosition) < wallData.SegmentWidth)
            {
                return true;
            }
        }

        return false;
    }

    // Clamps an arbitrary world point to the nearest point inside the wall's flat-capped
    // rectangular placement zone (WallPlacementPreview draws the same shape).
    private Vector3 ClampToWallRange(Vector3 point)
    {
        Vector3 diff = _wallEndPosition - _wallStartPosition;
        float length = diff.Length();
        float range = _pendingContext.Range.Value;

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

    // The highest segment count still affordable given currently-held Mana and remaining
    // Capacity — since each extra segment adds a fixed cost, this is a clean cutoff rather than
    // something that needs checking per-segment independently.
    private int ComputeAffordableSegmentCount(WallCastData wallData)
    {
        if (_economy.Unlimited)
        {
            return int.MaxValue;
        }

        float manaPerSegment = wallData.ManaCostPerSegment * _pendingContext.ManaCost.Multiplier;
        float capacityPerSegment = wallData.CapacityCostPerSegment * _pendingContext.CapacityCost.Multiplier;
        int capacityRemaining = _economy.MaxCapacity - _economy.CapacityUsed;

        int manaMax = manaPerSegment > 0f
            ? Mathf.FloorToInt((_economy.Mana - _pendingContext.ManaCost.Value) / manaPerSegment)
            : int.MaxValue;

        int capacityMax = capacityPerSegment > 0f
            ? Mathf.FloorToInt((capacityRemaining - _pendingContext.CapacityCost.Value) / capacityPerSegment)
            : int.MaxValue;

        return Mathf.Max(0, Mathf.Min(manaMax, capacityMax));
    }

    // While the wall's size is still being adjusted this projects the total (base plus the
    // not-yet-committed segment cost) without touching _pendingContext. Once ApplyWallCostScaling
    // has folded that cost into the base, the resolved values already include it.
    private void ShowProjectedCost(Vector3 start, Vector3 end, int segmentCount, bool affordable)
    {
        WallCastData wallData = _pendingContext.Wall;

        float mana = _pendingContext.ManaCost.Value;
        float capacity = _pendingContext.CapacityCost.Value;

        if (!_wallCostApplied)
        {
            mana += segmentCount * wallData.ManaCostPerSegment * _pendingContext.ManaCost.Multiplier;
            capacity += segmentCount * wallData.CapacityCostPerSegment * _pendingContext.CapacityCost.Multiplier;
        }

        string text = $"Segments: {segmentCount}\nMana: {Mathf.CeilToInt(mana)}   Capacity: {Mathf.CeilToInt(capacity)}";
        _wallPreview.ShowCost(start, end, text, affordable);
    }

    // Folds the wall's per-segment cost into the base, so the rune-derived multipliers already
    // sitting on ManaCost/CapacityCost scale the whole total rather than just the flat base.
    private void ApplyWallCostScaling()
    {
        WallCastData wallData = _pendingContext.Wall;
        int segmentCount = IceWall.ComputeSegmentCount(_wallStartPosition.DistanceTo(_wallEndPosition), wallData.SegmentWidth);

        _pendingContext.ManaCost.Base += segmentCount * wallData.ManaCostPerSegment;
        _pendingContext.CapacityCost.Base += segmentCount * wallData.CapacityCostPerSegment;
        _wallCostApplied = true;
    }

    private void ConfirmPlacement()
    {
        Ritual placed = RitualScene.Instantiate<Ritual>();
        GetTree().CurrentScene.AddChild(placed);
        placed.GlobalTransform = _ghost.GlobalTransform;
        placed.Configure(_pendingSpell, _pendingRunes);
        placed.SetOwnerId(PlayerSentinels.Local);

        if (_pendingContext.Wall != null)
        {
            placed.SetWallEndpoints(_wallStartPosition, _wallEndPosition);

            WallFootprintMarker marker = new WallFootprintMarker();
            placed.AddChild(marker);
            marker.Initialize(_wallStartPosition, _wallEndPosition, _pendingContext.Wall.SegmentWidth);
        }

        (int manaCost, int capacityCost) = _economy.SpendOnRitual(_pendingContext);
        placed.SetCost(manaCost, capacityCost);

        Action onConfirmed = _onConfirmed;
        EndPlacement();
        onConfirmed?.Invoke();
    }

    private void CancelPlacement()
    {
        EndPlacement();
    }

    private void EndPlacement()
    {
        if (_wallPreview != null)
        {
            _wallPreview.Clear();
            _wallPreview.QueueFree();
            _wallPreview = null;
        }

        if (_ghost != null)
        {
            _ghost.QueueFree();
            _ghost = null;
        }

        _onConfirmed = null;
        IsPlacing = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, false);
    }

    private static MeshInstance3D BuildRangeIndicator(float range)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = range, BottomRadius = range, Height = 0.05f },
            MaterialOverride = PreviewMaterials.RangeFill,
            Position = new Vector3(0f, 0.03f, 0f)
        };
    }
}
