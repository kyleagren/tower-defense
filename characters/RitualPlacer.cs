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

    // Which phase transition should cancel an in-progress placement for this placer's owner - the
    // Architect's own instance cancels on entering Combat (placement only happens during Build);
    // the Sentinel's instance (README's mid-wave token deployment) needs the opposite: cancel
    // on entering Build, since that's when the Sentinel itself goes inactive.
    [Export] public GamePhase CancelOnPhase = GamePhase.Combat;

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

    // Set when placement starts with confirm_placement already held - the click that picked what's
    // being placed. That press can't also confirm it; confirming needs a fresh one.
    private bool _confirmHeldSinceStart;

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

    private void ProcessRitualStage()
    {
        _ghost.GlobalPosition = _flow.ConstrainRitualPosition(_cursorPosition);
        _resonanceOverlay?.Draw(_pendingContext.ResonanceRadius, _pendingContext.Range.Value, null);
        UpdateGhostValidity();
        LevelContext.Current.RitualRoster.MoveGhost(_ghost.GlobalPosition);

        if (ConfirmJustPressed() && _isValidPlacement)
        {
            ConfirmPlacement();
        }
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

        if (_flow.ShowsRangeIndicator)
        {
            _ghost.AddChild(BuildRangeIndicator(_pendingContext.Range.Value));
        }

        if (_flow.ShowsRangeIndicator && _pendingContext.Trigger?.Resonant == true)
        {
            _resonanceOverlay = new ResonanceOverlay();
            _ghost.AddChild(_resonanceOverlay);
        }

        LevelContext.Current.RitualRoster.ShowGhost(_flow.ShowsRangeIndicator ? _pendingContext.Range.Value : 0f, _flow.ConstrainRitualPosition(_cursorPosition));
    }

    private void UpdateGhostValidity()
    {
        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;

        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = _ghostCollisionShape.Shape,
            Transform = _ghostCollisionShape.GlobalTransform,
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
        LevelContext.Current.RitualRoster.RequestPlacement(_pendingSpell, _pendingRunes, _ghost.GlobalTransform, _flow.Data, _onConfirmed);
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

        _onConfirmed = null;
        IsPlacing = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeChanged, false);
    }

    public static MeshInstance3D BuildRangeIndicator(float range)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = range, BottomRadius = range, Height = 0.05f },
            MaterialOverride = PreviewMaterials.RangeFill,
            Position = new Vector3(0f, 0.03f, 0f)
        };
    }
}
