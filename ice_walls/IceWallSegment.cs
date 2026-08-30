using Godot;

public partial class IceWallSegment : StaticBody3D
{
    [Export] public MeshInstance3D BodyMesh { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }

    private static readonly StandardMaterial3D _iceMaterial = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.6f, 0.85f, 1f)
    };

    private float _currentHealth;
    private bool _isBroken;

    public void Initialize(float maxHealth, float width)
    {
        _currentHealth = maxHealth;

        BodyMesh.Mesh = new CylinderMesh
        {
            TopRadius = 0f,
            BottomRadius = width / 2f,
            Height = width * 2f,
            RadialSegments = 4
        };
        BodyMesh.MaterialOverride = _iceMaterial;
        BodyMesh.Position = new Vector3(0f, width, 0f);

        if (BodyCollision.Shape is BoxShape3D boxShape)
        {
            boxShape.Size = new Vector3(width, width * 2f, width);
        }
        BodyCollision.Position = new Vector3(0f, width, 0f);
    }

    public void TakeDamage(float amount)
    {
        if (_isBroken)
        {
            return;
        }

        _currentHealth -= amount;
        if (_currentHealth <= 0f)
        {
            Break();
        }
    }

    public void Break()
    {
        if (_isBroken)
        {
            return;
        }

        _isBroken = true;
        QueueFree();
    }
}
