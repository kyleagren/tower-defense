using Godot;

// A burst of particles over a dead enemy's body: every emitter under it spreads its particles through
// a box the size of the body, then the whole thing frees itself. Parented to the level rather than the
// body, so particles still in the air outlive the corpse.
public partial class DeathParticles : Node3D
{
    // Long enough for the last particle to finish.
    [Export] public float Lifetime { get; private set; } = 2f;

    // The emitters' -Z is turned to face heading; zero leaves them unturned.
    public static void Spawn(PackedScene scene, Aabb bounds, Vector3 heading)
    {
        DeathParticles particles = scene.Instantiate<DeathParticles>();
        LevelContext.Current.LevelRoot.AddChild(particles);
        particles.Emit(bounds, heading);
    }

    private void Emit(Aabb bounds, Vector3 heading)
    {
        GlobalPosition = bounds.GetCenter();

        if (heading != Vector3.Zero)
        {
            GlobalBasis = Basis.LookingAt(heading, Vector3.Up);
        }

        foreach (Node child in GetChildren())
        {
            if (child is GpuParticles3D emitter && emitter.ProcessMaterial is ParticleProcessMaterial shared)
            {
                // A copy - the scene's process material is shared by every burst.
                ParticleProcessMaterial material = (ParticleProcessMaterial)shared.Duplicate();
                material.EmissionBoxExtents = bounds.Size / 2f;
                emitter.ProcessMaterial = material;
                emitter.Restart();
            }
        }

        GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
    }
}
