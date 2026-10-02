using Godot;
using System.Collections.Generic;

// Owns every visual shown while an ice wall is being placed: the run of segment ghosts, the
// flat-capped rectangular zone the linked ritual must sit inside, and the floating cost label.
//
// Segment meshes are reused between frames rather than freed and rebuilt — placement updates every
// physics frame, and a long wall would otherwise churn hundreds of nodes per second while the
// cursor sits still. All segments share one CylinderMesh, since their size only depends on the
// spell's SegmentWidth.
public partial class WallPlacementPreview : Node3D
{
    private const float BorderThickness = 0.3f;

    private readonly List<MeshInstance3D> _segmentMeshes = new List<MeshInstance3D>();
    private MeshInstance3D _rangeFill;
    private MeshInstance3D _rangeBorder;
    private Label3D _costLabel;

    private CylinderMesh _segmentMesh;
    private float _segmentMeshWidth = -1f;
    private bool _frozen;

    // Set for a copy of another player's wall, which shows this whatever the wall's validity.
    public StandardMaterial3D FixedSegmentMaterial { get; set; }

    public void UpdateWall(Vector3 start, Vector3 end, float segmentWidth, IReadOnlyList<bool> segmentValid)
    {
        if (_frozen)
        {
            return;
        }

        int segmentCount = segmentValid.Count;
        ResizeSegmentPool(segmentCount, segmentWidth);

        bool allValid = true;
        for (int i = 0; i < segmentCount; i++)
        {
            if (!segmentValid[i])
            {
                allValid = false;
                break;
            }
        }

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 ground = IceWall.SegmentPosition(start, end, i, segmentCount);

            _segmentMeshes[i].Position = IceWall.SegmentBoxCenter(ground, segmentWidth);
            _segmentMeshes[i].MaterialOverride = FixedSegmentMaterial ?? (allValid ? PreviewMaterials.Valid : PreviewMaterials.Invalid);
        }
    }

    public void ShowCost(Vector3 start, Vector3 end, string text, bool affordable)
    {
        _costLabel ??= CreateCostLabel();

        _costLabel.Text = text;
        _costLabel.Modulate = affordable ? Colors.White : Colors.Red;
        _costLabel.GlobalPosition = (start + end) / 2f + Vector3.Up * 2f;
    }

    // Locks the wall in: segments stop updating and fade back, and the zone the linked ritual must
    // be placed within appears.
    public void Freeze(Vector3 start, Vector3 end, float range)
    {
        _frozen = true;

        foreach (MeshInstance3D mesh in _segmentMeshes)
        {
            mesh.MaterialOverride = FixedSegmentMaterial ?? PreviewMaterials.Faded;
        }

        BuildRangeZone(start, end, range);
    }

    public void Clear()
    {
        foreach (MeshInstance3D mesh in _segmentMeshes)
        {
            mesh.QueueFree();
        }

        _segmentMeshes.Clear();

        _rangeFill?.QueueFree();
        _rangeFill = null;

        _rangeBorder?.QueueFree();
        _rangeBorder = null;

        _costLabel?.QueueFree();
        _costLabel = null;

        _frozen = false;
    }

    private void ResizeSegmentPool(int count, float segmentWidth)
    {
        while (_segmentMeshes.Count > count)
        {
            int last = _segmentMeshes.Count - 1;
            _segmentMeshes[last].QueueFree();
            _segmentMeshes.RemoveAt(last);
        }

        while (_segmentMeshes.Count < count)
        {
            MeshInstance3D mesh = new MeshInstance3D { Mesh = GetSegmentMesh(segmentWidth) };
            AddChild(mesh);
            _segmentMeshes.Add(mesh);
        }
    }

    private CylinderMesh GetSegmentMesh(float width)
    {
        if (_segmentMesh == null || !Mathf.IsEqualApprox(_segmentMeshWidth, width))
        {
            _segmentMesh = new CylinderMesh
            {
                TopRadius = 0f,
                BottomRadius = width / 2f,
                Height = width * 2f,
                RadialSegments = 4
            };
            _segmentMeshWidth = width;
        }

        return _segmentMesh;
    }

    private Label3D CreateCostLabel()
    {
        Label3D label = new Label3D
        {
            FontSize = 32,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };

        AddChild(label);
        return label;
    }

    // The flat-capped rectangle the linked ritual must be placed inside — the same shape
    // RitualPlacer.ClampToWallRange constrains the cursor to.
    private void BuildRangeZone(Vector3 start, Vector3 end, float range)
    {
        Vector3 diff = end - start;
        float length = diff.Length();
        Vector3 direction = length > 0.0001f ? diff / length : Vector3.Forward;
        Vector3 perpendicular = new Vector3(-direction.Z, 0f, direction.X);
        Vector3 up = Vector3.Up * 0.03f;

        Vector3 ToWorld(float along, float across) => start + direction * along + perpendicular * across + up;

        Vector3[] inner =
        {
            ToWorld(0f, range),
            ToWorld(length, range),
            ToWorld(length, -range),
            ToWorld(0f, -range)
        };

        Vector3[] outer =
        {
            ToWorld(-BorderThickness, range + BorderThickness),
            ToWorld(length + BorderThickness, range + BorderThickness),
            ToWorld(length + BorderThickness, -range - BorderThickness),
            ToWorld(-BorderThickness, -range - BorderThickness)
        };

        SurfaceTool fillTool = new SurfaceTool();
        fillTool.Begin(Mesh.PrimitiveType.Triangles);
        MeshBuilder.AddQuad(fillTool, inner[0], inner[1], inner[2], inner[3]);

        _rangeFill = new MeshInstance3D
        {
            Mesh = fillTool.Commit(),
            MaterialOverride = PreviewMaterials.RangeFill
        };
        AddChild(_rangeFill);

        SurfaceTool borderTool = new SurfaceTool();
        borderTool.Begin(Mesh.PrimitiveType.Triangles);
        MeshBuilder.AddBorderRing(borderTool, inner, outer);

        _rangeBorder = new MeshInstance3D
        {
            Mesh = borderTool.Commit(),
            MaterialOverride = PreviewMaterials.RangeBorder
        };
        AddChild(_rangeBorder);
    }
}
