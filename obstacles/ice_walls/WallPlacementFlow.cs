using Godot;

// Ice Wall's placement: the wall's start, then its end, then a linked Ritual inside the flat-capped
// zone within Range of the wall's line. Each prospective segment is checked against obstacles and
// affordability as the wall is dragged out, and the whole preview flips red if any fails.
public class WallPlacementFlow : PlacementFlow
{
    private enum Stage { Start, End, Done }

    private readonly RitualPlacer _placer;
    private readonly SpellCastContext _context;
    private readonly WallPlacementPreview _preview;
    private readonly RitualRoster _roster;
    private readonly EconomyManager _economy;

    private Stage _stage = Stage.Start;
    private Vector3 _start;
    private Vector3 _end;
    private WallPlacementData _data;

    private BoxShape3D _segmentQueryBox;
    private PhysicsShapeQueryParameters3D _segmentQuery;

    // context is resolved without any segments yet - per-segment cost is added on top of it here
    // while the wall is sized, and folded in for real once WallPlacementData exists.
    public WallPlacementFlow(RitualPlacer placer, SpellCastContext context)
    {
        _placer = placer;
        _context = context;
        _preview = new WallPlacementPreview();
        LevelContext.Current.LevelRoot.AddChild(_preview);
        _roster = LevelContext.Current.RitualRoster;
        _economy = LevelContext.Current.Economy;
    }

    public override bool IsPickingPoints => _stage != Stage.Done;
    public override bool ShowsRangeIndicator => false;
    public override PlacementData Data => _data;

    private WallCastData Wall => _context.Wall;

    public override void UpdatePoints(Vector3 cursor, bool confirmPressed)
    {
        if (_stage == Stage.Start)
        {
            bool[] startValid = ComputeSegmentValidity(cursor, cursor, ComputeAffordableChargedSegments());
            _preview.UpdateWall(cursor, cursor, Wall.SegmentWidth, startValid);
            _roster.MoveWallGhost(cursor, cursor, Wall.SegmentWidth);

            if (confirmPressed)
            {
                _start = cursor;
                _stage = Stage.End;
            }

            return;
        }

        int segmentCount = IceWall.ComputeSegmentCount(_start.DistanceTo(cursor), Wall.SegmentWidth);
        int affordableChargedSegments = ComputeAffordableChargedSegments();

        bool[] segmentValid = ComputeSegmentValidity(_start, cursor, affordableChargedSegments);
        _preview.UpdateWall(_start, cursor, Wall.SegmentWidth, segmentValid);
        _roster.MoveWallGhost(_start, cursor, Wall.SegmentWidth);

        bool affordable = IceWall.ChargedSegmentCount(segmentCount) <= affordableChargedSegments;
        bool placementValid = affordable && System.Array.TrueForAll(segmentValid, v => v);
        ShowProjectedCost(_start, cursor, segmentCount, placementValid);

        if (placementValid && confirmPressed)
        {
            _end = cursor;
            _data = new WallPlacementData(_start, _end);
            _stage = Stage.Done;
            _preview.Freeze(_start, _end, _context.Range.Value);
            _roster.FreezeWallGhost(_start, _end, Wall.SegmentWidth, _context.Range.Value);
        }
    }

    public override Vector3 ConstrainRitualPosition(Vector3 cursor) => ClampToWallRange(cursor);

    public override bool IsRitualPositionAllowed(Vector3 position) => !OverlapsOwnWallLine(position);

    public override void End()
    {
        _preview.Clear();
        _preview.QueueFree();
    }

    // Per prospective segment position along the line rather than one pass/fail check - a wall can
    // overlap something in the middle without overlapping at either end. Shape and query params are
    // built once and reused across segments and frames: this runs every physics frame for the whole
    // length of a wall, so allocating per segment per frame would churn dozens of engine objects a
    // second at rest.
    private bool[] ComputeSegmentValidity(Vector3 start, Vector3 end, int affordableChargedSegments)
    {
        int segmentCount = IceWall.ComputeSegmentCount(start.DistanceTo(end), Wall.SegmentWidth);
        bool[] valid = new bool[segmentCount];

        PhysicsShapeQueryParameters3D query = GetSegmentQuery(Wall.SegmentWidth);
        PhysicsDirectSpaceState3D spaceState = _placer.Character.GetWorld3D().DirectSpaceState;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 ground = IceWall.SegmentPosition(start, end, i, segmentCount);
            query.Transform = new Transform3D(Basis.Identity, IceWall.SegmentBoxCenter(ground, Wall.SegmentWidth));

            // Segment i is part of a wall i + 1 segments long, which is what it has to afford.
            bool overlaps = spaceState.IntersectShape(query).Count > 0;
            valid[i] = IceWall.ChargedSegmentCount(i + 1) <= affordableChargedSegments && !overlaps;
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
                CollisionMask = PhysicsLayers.Obstacles | PhysicsLayers.PlacementBlocked | PhysicsLayers.RitualHitbox,
                CollideWithBodies = true,
                CollideWithAreas = false
            };
        }

        _segmentQueryBox.Size = IceWall.SegmentBoxSize(segmentWidth);
        return _segmentQuery;
    }

    // How many charged segments (see IceWall.ChargedSegmentCount) are still affordable on top of the
    // base, given currently-held Mana and remaining Capacity. Negative means even the base - the
    // shortest possible wall - is out of reach.
    private int ComputeAffordableChargedSegments()
    {
        EconomyManager economy = _economy;

        if (economy.Unlimited)
        {
            return int.MaxValue;
        }

        float manaPerSegment = Wall.ManaCostPerSegment * _context.ManaCost.Multiplier;
        float capacityPerSegment = Wall.CapacityCostPerSegment * _context.CapacityCost.Multiplier;
        int capacityRemaining = economy.MaxCapacity - economy.CapacityUsed;

        int manaMax = manaPerSegment > 0f
            ? Mathf.FloorToInt((economy.Mana - _context.ManaCost.Value) / manaPerSegment)
            : int.MaxValue;

        int capacityMax = capacityPerSegment > 0f
            ? Mathf.FloorToInt((capacityRemaining - _context.CapacityCost.Value) / capacityPerSegment)
            : int.MaxValue;

        return Mathf.Min(manaMax, capacityMax);
    }

    private void ShowProjectedCost(Vector3 start, Vector3 end, int segmentCount, bool affordable)
    {
        int chargedSegments = IceWall.ChargedSegmentCount(segmentCount);
        float mana = _context.ManaCost.Value + chargedSegments * Wall.ManaCostPerSegment * _context.ManaCost.Multiplier;
        float capacity = _context.CapacityCost.Value + chargedSegments * Wall.CapacityCostPerSegment * _context.CapacityCost.Multiplier;

        string text = $"Segments: {segmentCount}\nMana: {Mathf.CeilToInt(mana)}   Capacity: {Mathf.CeilToInt(capacity)}";
        _preview.ShowCost(start, end, text, affordable);
    }

    // The wall has no physics presence yet during placement - the real IceWall only exists once an
    // enemy triggers it, and WallFootprintMarker isn't built until placement is confirmed - so the
    // ghost is checked against the wall's own line directly, at the same segment positions.
    private bool OverlapsOwnWallLine(Vector3 position)
    {
        int segmentCount = IceWall.ComputeSegmentCount(_start.DistanceTo(_end), Wall.SegmentWidth);

        for (int i = 0; i < segmentCount; i++)
        {
            if (position.DistanceTo(IceWall.SegmentPosition(_start, _end, i, segmentCount)) < Wall.SegmentWidth)
            {
                return true;
            }
        }

        return false;
    }

    // Clamps a point to the nearest point inside the wall's flat-capped rectangular zone -
    // WallPlacementPreview draws the same shape.
    private Vector3 ClampToWallRange(Vector3 point)
    {
        Vector3 diff = _end - _start;
        float length = diff.Length();
        float range = _context.Range.Value;

        if (length < 0.0001f)
        {
            Vector3 toPoint = point - _start;
            return toPoint.Length() <= range ? point : _start + toPoint.Normalized() * range;
        }

        Vector3 direction = diff / length;
        Vector3 perpendicular = new Vector3(-direction.Z, 0f, direction.X);

        Vector3 offset = point - _start;
        float along = Mathf.Clamp(offset.Dot(direction), 0f, length);
        float across = Mathf.Clamp(offset.Dot(perpendicular), -range, range);

        return _start + direction * along + perpendicular * across;
    }
}
