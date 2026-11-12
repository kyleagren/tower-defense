using Godot;

// Shakes its parent camera through the camera's offsets, which move the view without moving the camera
// itself - so aim, which rays from the camera's position, is untouched.
public partial class CameraShake : Node
{
    [Export] public float MaxOffset { get; private set; } = 0.35f;
    // Trauma lost per second.
    [Export] public float Recovery { get; private set; } = 1.5f;
    [Export] public float Frequency { get; private set; } = 25f;

    private Camera3D _camera;
    private FastNoiseLite _noise;
    private float _trauma;
    private float _time;

    public override void _Ready()
    {
        _camera = GetParent<Camera3D>();
        _noise = new FastNoiseLite { Frequency = Frequency };
    }

    // Shakes whichever camera this machine is looking through, less the further it is from the origin.
    public static void FromImpact(Node source, Vector3 origin, float strength, float reach)
    {
        if (!DebugSettings.ScreenShake)
        {
            return;
        }

        Camera3D camera = source.GetViewport().GetCamera3D();

        if (camera == null)
        {
            return;
        }

        float falloff = 1f - camera.GlobalPosition.DistanceTo(origin) / reach;

        if (falloff <= 0f)
        {
            return;
        }

        foreach (Node child in camera.GetChildren())
        {
            if (child is CameraShake shake)
            {
                shake.AddTrauma(strength * falloff);
                return;
            }
        }
    }

    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Min(1f, _trauma + amount);
    }

    // Squaring the trauma keeps small shakes subtle and big ones violent.
    public override void _Process(double delta)
    {
        if (_trauma <= 0f)
        {
            return;
        }

        _time += (float)delta;
        _trauma = Mathf.Max(0f, _trauma - Recovery * (float)delta);
        float amount = _trauma * _trauma * MaxOffset;

        _camera.HOffset = _noise.GetNoise2D(_time, 0f) * amount;
        _camera.VOffset = _noise.GetNoise2D(_time, 100f) * amount;
    }
}
