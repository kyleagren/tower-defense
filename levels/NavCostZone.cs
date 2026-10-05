using Godot;

[Tool]
[GlobalClass]
public partial class NavCostZone : Node3D
{
    private Vector3 _size = new(10f, 4f, 10f);
    private MeshInstance3D _preview;

    [Export]
    public Vector3 Size
    {
        get => _size;
        set
        {
            _size = value;
            UpdatePreview();
        }
    }

    [Export(PropertyHint.Range, "0.1,10,0.1,or_greater")] public float TravelCost { get; set; } = 3f;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        _preview = new MeshInstance3D
        {
            Mesh = new BoxMesh(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.5f, 0f, 0.15f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        // No Owner, so the preview is never saved into the scene.
        AddChild(_preview);
        UpdatePreview();
    }

    public bool Contains(Vector3 globalPoint)
    {
        Vector3 local = ToLocal(globalPoint);
        return Mathf.Abs(local.X) <= _size.X / 2f
            && Mathf.Abs(local.Y) <= _size.Y / 2f
            && Mathf.Abs(local.Z) <= _size.Z / 2f;
    }

    private void UpdatePreview()
    {
        if (_preview != null)
        {
            ((BoxMesh)_preview.Mesh).Size = _size;
        }
    }
}
