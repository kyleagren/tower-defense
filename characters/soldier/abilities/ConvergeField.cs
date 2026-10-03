using Godot;

// Converge's field: pulls every enemy inside toward its center like a spring and roots it there. The
// root is a short ailment each tick refreshes, so an enemy that leaves - or outlives the field -
// walks free a moment later without anything having to release it. A rooted enemy still attacks
// whatever comes within reach.
public partial class ConvergeField : EffectVolume
{
    // How many ticks a root survives unrefreshed, so one late tick doesn't free an enemy.
    private const float RootTicks = 3f;
    // Inside this an enemy has arrived and stops being pulled, rather than nudged every tick.
    private const float ArrivalRadius = 0.5f;

    // Motes drawn in from the rim, spiralling into the core.
    [Export] public GpuParticles3D Motes { get; private set; }

    private float _pullStiffness;

    protected override bool AffectsOnEntry => true;

    public void Configure(float pullStiffness)
    {
        _pullStiffness = pullStiffness;
    }

    public override void Initialize(Vector3 position, float radius, float duration)
    {
        base.Initialize(position, radius, duration);
        SizeMotes(radius);
    }

    // The motes start at the rim and reach the core as their lifetime ends, so the inward pull is
    // whatever covers the radius in that time. A fresh copy of the scene's material, since that one is
    // shared by every field.
    private void SizeMotes(float radius)
    {
        float lifetime = (float)Motes.Lifetime;
        float pull = 2f * radius / (lifetime * lifetime);

        ParticleProcessMaterial material = (ParticleProcessMaterial)Motes.ProcessMaterial.Duplicate();
        material.EmissionRingRadius = radius;
        material.EmissionRingInnerRadius = radius * 0.85f;
        material.RadialAccelMin = -pull;
        material.RadialAccelMax = -pull * 0.85f;
        material.TangentialAccelMin = pull * 0.3f;
        material.TangentialAccelMax = pull * 0.5f;

        Motes.ProcessMaterial = material;
        Motes.VisibilityAabb = new Aabb(new Vector3(-radius, -1f, -radius), new Vector3(radius * 2f, 4f, radius * 2f));
    }

    protected override void OnTick(Enemy enemy)
    {
        enemy.ApplyDebuff(new ConvergedDebuff(TickInterval * RootTicks));

        Vector3 offset = GlobalPosition - enemy.GlobalPosition;
        offset.Y = 0f;

        if (offset.Length() > ArrivalRadius)
        {
            enemy.ApplyKnockback(offset * _pullStiffness * TickInterval);
        }
    }
}
