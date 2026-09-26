using Godot;

public partial class IceWallSegment : StaticBody3D, IDamageable
{
    [Export] public MeshInstance3D BodyMesh { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }

    private static readonly StandardMaterial3D _iceMaterial = new StandardMaterial3D
    {
        AlbedoColor = DamageTypeColors.Get(Tag.Cold)
    };

    private LevelContext _level;
    private IceWall _wall;
    private bool _isBroken;

    public bool IsDestroyed => _isBroken;
    public IceWall Wall => _wall;
    public Vector3 AimPoint => BodyMesh.GlobalPosition;

    public override void _Ready()
    {
        _level = LevelContext.Current;
        _level.IceWallSegments.Register(this);
    }

    // Safety net for segments freed without breaking. Unregistering twice is a no-op, so the
    // Break() path unregistering first is unaffected.
    public override void _ExitTree()
    {
        _level.IceWallSegments.Unregister(this);
    }

    public void Initialize(IceWall wall, float width)
    {
        _wall = wall;

        BodyMesh.Mesh = new CylinderMesh
        {
            TopRadius = 0f,
            BottomRadius = width / 2f,
            Height = width * 2f,
            RadialSegments = 4
        };
        BodyMesh.MaterialOverride = _iceMaterial;
        BodyMesh.Position = new Vector3(0f, width, 0f);

        // A fresh shape per segment: the one authored in ice_wall_segment.tscn is shared by every
        // instance, so resizing it in place would resize every other segment already standing.
        BodyCollision.Shape = new BoxShape3D { Size = IceWall.SegmentBoxSize(width) };
        BodyCollision.Position = IceWall.SegmentBoxCenter(Vector3.Zero, width);
    }

    public void TakeDamage(Hit hit)
    {
        if (_isBroken)
        {
            return;
        }

        // Hitting any one segment damages the whole wall's shared pool - segments break
        // together, not individually, once that pool is spent.
        _wall.TakeDamage(hit.Damage);
    }

    public void Break()
    {
        if (_isBroken)
        {
            return;
        }

        _isBroken = true;
        _level.IceWallSegments.Unregister(this);
        QueueFree();
    }
}
