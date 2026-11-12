using Godot;

// Where a meteor lands: the blast, a shockwave of fire racing out across the ground with a scorch left
// to fade, rock debris and embers thrown up, a flash lighting the area, and a shake for anyone close.
public partial class MeteorLanding : Node3D, IImpactVisual
{
    [Export] public ExplosionVisual Explosion { get; private set; }
    [Export] public MeshInstance3D Ground { get; private set; }
    [Export] public GpuParticles3D[] Bursts { get; private set; } = System.Array.Empty<GpuParticles3D>();
    [Export] public OmniLight3D Flash { get; private set; }
    [Export] public float FlashEnergy { get; private set; } = 8f;
    [Export] public float FlashTime { get; private set; } = 0.35f;
    // The flash's reach as a multiple of the blast radius.
    [Export] public float FlashReach { get; private set; } = 3f;
    [Export] public float WaveTime { get; private set; } = 0.45f;
    // The shockwave and scorch's size as a multiple of the blast radius.
    [Export] public float GroundSpread { get; private set; } = 1.3f;
    [Export] public float ScorchTime { get; private set; } = 6f;
    [Export] public float ShakeStrength { get; private set; } = 0.6f;
    [Export] public float ShakeReach { get; private set; } = 35f;

    private const float GroundOffset = 0.06f;
    // Past 1 so the fire carries on off the edge and burns out rather than stopping at it.
    private const float WaveEnd = 1.3f;

    public void Show(Vector3 position, float radius)
    {
        GlobalPosition = position;
        Explosion.Show(position, radius);

        float spread = radius * GroundSpread;
        Ground.Position = Vector3.Up * GroundOffset;
        Ground.Scale = new Vector3(spread, 1f, spread);
        Ground.SetInstanceShaderParameter("wave", 0f);
        Ground.SetInstanceShaderParameter("scorch_fade", 0f);

        Flash.OmniRange = radius * FlashReach;
        Flash.LightEnergy = FlashEnergy;

        foreach (GpuParticles3D burst in Bursts)
        {
            burst.Restart();
        }

        CameraShake.FromImpact(this, position, ShakeStrength, ShakeReach);

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(Flash, "light_energy", 0f, FlashTime)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(wave => Ground.SetInstanceShaderParameter("wave", wave)), 0f, WaveEnd, WaveTime)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(fade => Ground.SetInstanceShaderParameter("scorch_fade", fade)), 0f, 1f, ScorchTime)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
