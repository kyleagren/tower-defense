using Godot;

// Lightning Rail's look: the same jagged arcs the other lightning spells draw, struck once down the
// length of the beam as it fires. A child of the Beam, which is placed before this first runs. Arcs go
// to the beam's parent rather than under it, so each frees itself on its own timer like any other arc.
public partial class BeamArcs : Node3D
{
    [Export] public PackedScene ArcScene { get; private set; }
    [Export] public int ArcCount { get; private set; } = 2;
    // About this far between kinks, however long the beam turns out.
    [Export] public float SegmentLength { get; private set; } = 1f;

    private Beam _beam;
    private bool _struck;

    public override void _Ready()
    {
        _beam = GetParent<Beam>();
    }

    public override void _Process(double delta)
    {
        if (_struck)
        {
            return;
        }

        _struck = true;
        Vector3 from = _beam.GlobalPosition;
        Vector3 to = from - _beam.GlobalBasis.Z * _beam.Length;
        int segments = Mathf.Max(2, Mathf.CeilToInt(_beam.Length / SegmentLength));

        for (int i = 0; i < ArcCount; i++)
        {
            ArcVisual arc = ArcScene.Instantiate<ArcVisual>();
            arc.SegmentCount = segments;
            _beam.GetParent().AddChild(arc);
            arc.Show(from, to);
        }
    }
}
