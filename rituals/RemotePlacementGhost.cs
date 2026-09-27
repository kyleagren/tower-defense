using Godot;
using System.Linq;

// Where another player is about to place: a see-through copy of what they're placing - the wall
// they're laying out first, for a spell with one, then the Ritual itself - with their seat above it.
// The Ritual eases toward each position they send, since those arrive only several times a second.
public partial class RemotePlacementGhost : Node3D
{
    private const float FollowRate = 15f;
    private const float LabelHeight = 2.5f;

    private Label3D _label;
    private Node3D _ritual;
    private Vector3 _ritualTarget;
    private WallPlacementPreview _wall;

    public void Initialize(PlayerId owner)
    {
        _label = new Label3D
        {
            Text = owner.ToString(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false
        };

        AddChild(_label);
    }

    public void ShowRitual(PackedScene ritualScene, float range, Vector3 position)
    {
        _ritual?.QueueFree();
        _ritual = new Node3D { Position = position };
        _ritualTarget = position;

        Ritual ritual = ritualScene.Instantiate<Ritual>();
        ritual.IsPreview = true;
        _ritual.AddChild(ritual);
        ritual.GetNode<CollisionShape3D>("StaticBody3D/CollisionShape3D").Disabled = true;
        ritual.GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = PreviewMaterials.OtherPlayer;

        if (range > 0f)
        {
            _ritual.AddChild(RitualPlacer.BuildRangeIndicator(range));
        }

        AddChild(_ritual);
        _label.Visible = true;
    }

    public void MoveRitual(Vector3 position)
    {
        _ritualTarget = position;
    }

    public void ShowWall(Vector3 start, Vector3 end, float segmentWidth)
    {
        if (_wall == null)
        {
            _wall = new WallPlacementPreview { FixedSegmentMaterial = PreviewMaterials.OtherPlayer };
            AddChild(_wall);
        }

        int segmentCount = IceWall.ComputeSegmentCount(start.DistanceTo(end), segmentWidth);
        _wall.UpdateWall(start, end, segmentWidth, Enumerable.Repeat(true, segmentCount).ToArray());
        _label.Position = (start + end) / 2f + Vector3.Up * LabelHeight;
        _label.Visible = true;
    }

    public void FreezeWall(Vector3 start, Vector3 end, float segmentWidth, float range)
    {
        ShowWall(start, end, segmentWidth);
        _wall.Freeze(start, end, range);
    }

    public override void _Process(double delta)
    {
        if (_ritual == null)
        {
            return;
        }

        _ritual.Position = _ritual.Position.Lerp(_ritualTarget, 1f - Mathf.Exp(-FollowRate * (float)delta));
        _label.Position = _ritual.Position + Vector3.Up * LabelHeight;
    }
}
