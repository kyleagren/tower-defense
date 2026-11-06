using Godot;
using System;

public partial class RitualPlacer : Node
{
    // Not exported: a typed [Export] against a node from an inherited base scene fails its cast at
    // scene-build time, since the base scene's root has no script yet and the parent is still a bare
    // CharacterBody3D. Resolved from the parent instead, which is safe because this is always a
    // direct child of the character that owns it.
    public PlayerCharacter Character { get; private set; }

    [Export] public PackedScene RitualScene { get; private set; }
    [Export] public float PlacementDistance = GameRules.RitualReach;
    [Export] public float GroundRaycastRange = 10f;

    // A surface steeper than this isn't ground: a reticle on a wall's face steps back off it.
    private const float MinGroundNormalY = 0.7f;

    // The overlap test runs this far above the ghost, so the surface it stands on doesn't count as
    // an obstacle - level geometry is World and Obstacles at once.
    private const float PlacementClearance = 0.05f;

    // How far above a point the search for the ground under it starts - high enough to find the top
    // of a step or a rise, low enough that a room's ceiling doesn't count as its floor.
    private const float GroundSearchHeight = 2f;

    // How far off a wall's face to step before looking for the ground below.
    private const float WallStepBack = 0.5f;

    // How far the reticle reaches while aiming a spot - as far as the eye can, since
    // the pick is clamped to the Ritual's range anyway, so there's no walking over to it.
    [Export] public float AimReach = 60f;

    public bool IsPlacing { get; private set; }

    private Ritual _ghost;
    private MeshInstance3D _ghostMesh;
    private CollisionShape3D _ghostCollisionShape;
    private ResonanceOverlay _resonanceOverlay;
    private Spell _pendingSpell;
    private Godot.Collections.Array<Rune> _pendingRunes;
    private CharacterSheet _ownerSheet;
    private SpellCastContext _pendingContext;
    private PlacementFlow _flow;
    private bool _isValidPlacement;

    // Set when a stage begins with confirm_placement already held - the press that started it can't
    // also confirm it; confirming needs a fresh one.
    private bool _confirmHeldSinceStart;

    // For a spell that fires in a fixed direction or lands on a fixed spot: once the Ritual's own
    // position is confirmed, the next stage picks the direction or the spot from there. A lane
    // indicator stands in for the range circle; a spot shows the range's edge and its own area.
    private bool _isAiming;
    private MeshInstance3D _rangeIndicator;
    private MeshInstance3D _laneIndicator;
    private MeshInstance3D _spotIndicator;
    private PlacementData _aimPick;

    // Invoked once the Ritual has actually been placed (not on cancel, or a placement the host turns
    // down) - lets a caller like TokenDeployer know it should consume the token that was just spent.
    private Action _onConfirmed;

    private Vector3 _cursorPosition;
    private EconomyManager _economy;

    public override void _Ready()
    {
        Character = (PlayerCharacter)GetParent();
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        _economy = LevelContext.Current.Economy;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;

        // If the owning character is being torn down mid-placement (e.g. swapping bodies via a hub
        // station while a ghost is out), the ghost and any flow preview would otherwise leak - they're
        // parented to the scene root, not to this node.
        if (IsPlacing)
        {
            CancelPlacement();
        }
    }

    // A placement started under one phase's rules - a build in Build, a token in Combat - never
    // carries into the next.
    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (IsPlacing)
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
        _ownerSheet = PlayerProfiles.Get(Character.OwnerId).SelectedSheet;
        _pendingContext = SpellInstance.ResolvePreview(spell, runes, _ownerSheet);
        _flow = spell.CreatePlacementFlow(this, _pendingContext);
        _onConfirmed = onConfirmed;
        _confirmHeldSinceStart = Input.IsActionPressed("confirm_placement");

        IsPlacing = true;
        LevelContext.Current.RitualRoster.BeginGhost();
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, true);

        if (!_flow.IsPickingPoints)
        {
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

        if (_ghost == null)
        {
            ProcessFlowPoints();
        }
        else if (_isAiming)
        {
            ProcessAimStage();
        }
        else
        {
            ProcessRitualStage();
        }

        if (Input.IsActionJustPressed("ui_cancel") || Input.IsActionJustPressed("alt_action"))
        {
            CancelPlacement();
        }
    }

    // Input goes to the flow until it has every point it needs. The ghost appears on the tick it
    // finishes, resolved against what was picked so its cost and range already include it.
    private void ProcessFlowPoints()
    {
        _flow.UpdatePoints(_cursorPosition, ConfirmJustPressed());

        if (_flow.IsPickingPoints)
        {
            return;
        }

        _pendingContext = SpellInstance.ResolvePreview(_pendingSpell, _pendingRunes, _ownerSheet, _flow.Data);
        BeginRitualGhost();
    }

    private void UpdateCursorPosition()
    {
        // World only — ignore rituals/obstacles so the cursor always lands at true ground height
        _cursorPosition = CursorAlongView(_isAiming ? AimReach : PlacementDistance);
    }

    // The first ground straight along the camera's view, within reach - a wall's face steps back
    // off it onto the ground below. Looking past everything: while aiming, the point that far along
    // the view at the Ritual's height (the range clamp pulls it in); while placing, the ground under
    // that point.
    private Vector3 CursorAlongView(float reach)
    {
        Vector3 far = Character.GetCameraAimPoint(reach);
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(Character.Camera.GlobalPosition, far, PhysicsLayers.World);
        Godot.Collections.Dictionary result = Character.GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (result.Count == 0)
        {
            if (_isAiming)
            {
                far.Y = _ghost.GlobalPosition.Y;
                return far;
            }

            return DropToGround(far) ?? _cursorPosition;
        }

        Vector3 position = (Vector3)result["position"];
        Vector3 normal = (Vector3)result["normal"];

        return normal.Y >= MinGroundNormalY ? position : DropToGround(position + normal * WallStepBack) ?? position;
    }

    // The terrain under point, searching from GroundSearchHeight above it down to GroundRaycastRange
    // below - null with none there.
    private Vector3? DropToGround(Vector3 point)
    {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            point + Vector3.Up * GroundSearchHeight, point - Vector3.Up * GroundRaycastRange, PhysicsLayers.World);
        Godot.Collections.Dictionary result = Character.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return result.Count == 0 ? null : (Vector3)result["position"];
    }

    private void ProcessRitualStage()
    {
        _ghost.GlobalPosition = _flow.ConstrainRitualPosition(_cursorPosition);
        _resonanceOverlay?.Draw(_pendingContext.ResonanceRadius, _pendingContext.Range.Value, null);
        UpdateGhostValidity();
        LevelContext.Current.RitualRoster.MoveGhost(_ghost.GlobalPosition);

        // Until it's aimed, the lane points away from the player.
        if (_laneIndicator != null)
        {
            PointLaneIndicator(_laneIndicator, FlatDirection(Character.GlobalPosition, _ghost.GlobalPosition), _pendingContext.Range.Value);
        }

        if (ConfirmJustPressed() && _isValidPlacement)
        {
            if (_pendingContext.FixedAim != null || _pendingContext.FixedSpot != null)
            {
                BeginAiming();
            }
            else
            {
                ConfirmPlacement();
            }
        }
    }

    private void BeginAiming()
    {
        _isAiming = true;
        _confirmHeldSinceStart = true;

        // A spot is picked inside the range, so the range shrinks to its edge and the spot's own
        // area follows the reticle.
        if (_pendingContext.FixedSpot != null)
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.Visible = false;
            }

            _ghost.AddChild(BuildRangeOutline(_pendingContext.Range.Value));
            _spotIndicator = BuildSpotIndicator(_pendingContext.SpotRadius);
            _ghost.AddChild(_spotIndicator);
        }
    }

    private void ProcessAimStage()
    {
        if (_pendingContext.FixedSpot != null)
        {
            ProcessSpotAim();
        }
        else
        {
            ProcessDirectionAim();
        }
    }

    // The lane runs the way the camera faces. Aiming from the Ritual to the reticle would swing it back
    // toward the player whenever the view lands short of the Ritual.
    private void ProcessDirectionAim()
    {
        Vector3 direction = FlatDirection(Vector3.Zero, -Character.Camera.GlobalTransform.Basis.Z);

        if (direction == Vector3.Zero)
        {
            return;
        }

        PointLaneIndicator(_laneIndicator, direction, _pendingContext.Range.Value);

        if (ConfirmJustPressed())
        {
            _aimPick = new AimPlacementData(direction);
            ConfirmPlacement();
        }
    }

    private void ProcessSpotAim()
    {
        Vector3 offset = _cursorPosition - _ghost.GlobalPosition;
        offset.Y = 0f;
        float range = _pendingContext.Range.Value;

        if (offset.Length() > range)
        {
            offset = offset.Normalized() * range;
        }

        Vector3 spot = new Vector3(_ghost.GlobalPosition.X + offset.X, _cursorPosition.Y, _ghost.GlobalPosition.Z + offset.Z);
        spot = DropToGround(spot) ?? spot;
        _spotIndicator.GlobalPosition = spot + new Vector3(0f, 0.03f, 0f);

        if (ConfirmJustPressed())
        {
            _aimPick = new SpotPlacementData(spot);
            ConfirmPlacement();
        }
    }

    // Zero when the two points are on top of each other.
    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 offset = to - from;
        offset.Y = 0f;
        return offset.LengthSquared() < 0.01f ? Vector3.Zero : offset.Normalized();
    }

    private bool ConfirmJustPressed()
    {
        if (_confirmHeldSinceStart)
        {
            _confirmHeldSinceStart = Input.IsActionPressed("confirm_placement");
            return false;
        }

        return Input.IsActionJustPressed("confirm_placement");
    }

    private void BeginRitualGhost()
    {
        _ghost = RitualScene.Instantiate<Ritual>();
        _ghost.IsPreview = true;
        LevelContext.Current.LevelRoot.AddChild(_ghost);

        _ghostMesh = _ghost.GetNode<MeshInstance3D>("MeshInstance3D");
        _ghostCollisionShape = _ghost.GetNode<CollisionShape3D>("StaticBody3D/CollisionShape3D");
        _ghostCollisionShape.Disabled = true;

        bool fixedAim = _pendingContext.FixedAim != null;

        if (_flow.ShowsRangeIndicator && fixedAim)
        {
            _laneIndicator = BuildLaneIndicator(_pendingContext.Range.Value, _pendingContext.FixedAim.LaneHalfWidth);
            _ghost.AddChild(_laneIndicator);
        }
        else if (_flow.ShowsRangeIndicator)
        {
            _rangeIndicator = BuildRangeIndicator(_pendingContext.Range.Value);
            _ghost.AddChild(_rangeIndicator);
        }

        if (_flow.ShowsRangeIndicator && _pendingContext.Trigger?.Resonant == true)
        {
            _resonanceOverlay = new ResonanceOverlay();
            _ghost.AddChild(_resonanceOverlay);
        }

        float sharedRange = _flow.ShowsRangeIndicator && !fixedAim ? _pendingContext.Range.Value : 0f;
        LevelContext.Current.RitualRoster.ShowGhost(sharedRange, _flow.ConstrainRitualPosition(_cursorPosition));
    }

    private void UpdateGhostValidity()
    {
        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;

        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = _ghostCollisionShape.Shape,
            Transform = _ghostCollisionShape.GlobalTransform.Translated(Vector3.Up * PlacementClearance),
            CollisionMask = PhysicsLayers.Obstacles | PhysicsLayers.PlacementBlocked | PhysicsLayers.RitualHitbox,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> overlaps = spaceState.IntersectShape(query);

        _isValidPlacement = overlaps.Count == 0
            && _flow.IsRitualPositionAllowed(_ghost.GlobalPosition)
            && _economy.CanAfford(_pendingContext);

        _ghostMesh.MaterialOverride = _isValidPlacement ? PreviewMaterials.Valid : PreviewMaterials.Invalid;
    }

    private void ConfirmPlacement()
    {
        // A spell with placement picks of its own (Ice Wall) can't take a fixed direction or spot, so
        // at most one of these is set.
        LevelContext.Current.RitualRoster.RequestPlacement(_pendingSpell, _pendingRunes, _ghost.GlobalTransform, _flow.Data ?? _aimPick, _onConfirmed);
        EndPlacement();
    }

    public void CancelPlacement()
    {
        EndPlacement();
    }

    private void EndPlacement()
    {
        _flow?.End();
        _flow = null;
        LevelContext.Current.RitualRoster.HideGhost();

        if (_ghost != null)
        {
            _ghost.QueueFree();
            _ghost = null;
        }

        _resonanceOverlay = null;
        _isAiming = false;
        _rangeIndicator = null;
        _laneIndicator = null;
        _spotIndicator = null;
        _aimPick = null;

        _onConfirmed = null;
        IsPlacing = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, false);
    }

    // The range's true shape, painted onto whatever falls inside it (range_volume.gdshader).
    public static MeshInstance3D BuildRangeIndicator(float range) => BuildRangeVolume(range, 1f);

    // Just the edge of the range, so a spot's own area reads clearly inside it.
    public static MeshInstance3D BuildRangeOutline(float range) => BuildRangeVolume(range, 0f);

    // The box only has to contain the shape: range out and up, range / DownWeight down.
    private static MeshInstance3D BuildRangeVolume(float range, float fillStrength)
    {
        float reachUp = range / GameRules.RitualRangeUpWeight;
        float reachDown = range / GameRules.RitualRangeDownWeight;
        float centreHeight = (reachUp - reachDown) / 2f;

        MeshInstance3D volume = new()
        {
            Mesh = new BoxMesh { Size = new Vector3(range * 2f, reachUp + reachDown, range * 2f) },
            MaterialOverride = PreviewMaterials.RangeVolume,
            Position = new Vector3(0f, centreHeight, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        volume.SetInstanceShaderParameter("range", range);
        volume.SetInstanceShaderParameter("base_offset", -centreHeight);
        volume.SetInstanceShaderParameter("fill_strength", fillStrength);
        return volume;
    }

    public static MeshInstance3D BuildSpotIndicator(float radius)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.05f },
            MaterialOverride = PreviewMaterials.Valid
        };
    }

    // A Ritual that fires in a fixed direction reaches along a lane rather than all around it.
    public static MeshInstance3D BuildLaneIndicator(float range, float halfWidth)
    {
        return new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(halfWidth * 2f, 0.05f, range) },
            MaterialOverride = PreviewMaterials.RangeFill
        };
    }

    // Lays the lane out from the Ritual along direction. Rituals are never rotated, so the Ritual's
    // local space lines up with the world's.
    public static void PointLaneIndicator(MeshInstance3D indicator, Vector3 direction, float range)
    {
        if (direction == Vector3.Zero)
        {
            return;
        }

        indicator.Basis = Basis.LookingAt(direction, Vector3.Up);
        indicator.Position = direction * (range / 2f) + new Vector3(0f, 0.03f, 0f);
    }
}
