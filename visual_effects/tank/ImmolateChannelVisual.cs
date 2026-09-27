using Godot;
using System;
using System.Collections.Generic;

// Immolate's channel placeholder: a thin ring that starts at the edge of the radius and closes in on
// the Tank, and a small flame-colored disc under every enemy that would erupt if the channel ended
// right now.
public partial class ImmolateChannelVisual : Node3D
{
    [Export] public Color RingColor { get; private set; } = new Color(0.9f, 0.15f, 0.05f, 0.9f);
    [Export] public Color MarkerColor { get; private set; } = new Color(1f, 0.45f, 0.1f, 0.7f);
    [Export] public float RingWidth { get; private set; } = 0.1f;
    [Export] public float MarkerRadius { get; private set; } = 0.6f;

    // Above Vent's and Retaliation's circles, which can both be down under it.
    private const float GroundOffset = 0.09f;

    private readonly List<MeshInstance3D> _markers = new List<MeshInstance3D>();
    private MeshInstance3D _ring;
    private ArrayMesh _markerMesh;
    private StandardMaterial3D _markerMaterial;
    private float _radius;
    private float _duration;
    private float _elapsed;
    private Func<List<Enemy>> _findTargets;

    // Must be parented to the Tank.
    public void Begin(float radius, float duration, Func<List<Enemy>> findTargets)
    {
        // Its own process mode rather than the Tank's: on another player's screen the Tank is a
        // puppet that doesn't process, and this still has to close in and track its targets.
        ProcessMode = ProcessModeEnum.Pausable;

        Position = Vector3.Up * GroundOffset;
        _radius = radius;
        _duration = duration;
        _findTargets = findTargets;

        _ring = new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = GroundMesh.Material(RingColor)
        };
        AddChild(_ring);

        _markerMesh = GroundMesh.Disc(MarkerRadius);
        _markerMaterial = GroundMesh.Material(MarkerColor);
        SetProgress(0f);
        MarkTargets(_findTargets());
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        SetProgress(_duration > 0f ? Mathf.Min(_elapsed / _duration, 1f) : 1f);
        MarkTargets(_findTargets());
    }

    // 0 as the channel starts, 1 as it erupts. Rebuilt rather than scaled, so the ring keeps its
    // width as it closes in.
    private void SetProgress(float progress)
    {
        float outer = Mathf.Lerp(_radius, RingWidth, progress);
        _ring.Mesh = GroundMesh.Ring(Mathf.Max(outer - RingWidth, 0f), outer);
    }

    private void MarkTargets(List<Enemy> targets)
    {
        while (_markers.Count < targets.Count)
        {
            MeshInstance3D marker = new MeshInstance3D
            {
                Mesh = _markerMesh,
                MaterialOverride = _markerMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(marker);
            _markers.Add(marker);
        }

        for (int i = 0; i < _markers.Count; i++)
        {
            bool isUsed = i < targets.Count;
            _markers[i].Visible = isUsed;

            if (isUsed)
            {
                _markers[i].GlobalPosition = targets[i].GlobalPosition + Vector3.Up * GroundOffset;
            }
        }
    }
}
