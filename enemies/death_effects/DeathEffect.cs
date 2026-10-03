using Godot;
using System.Collections.Generic;

// What happens to a dead enemy's body. One instance per style is shared by every enemy dying that
// way, so anything per corpse lives on the body's meshes as instance shader parameters or in the
// tweens an effect runs on the enemy.
public abstract class DeathEffect
{
    protected static readonly StringName CharAmount = "char_amount";
    protected static readonly StringName Flash = "flash";
    protected static readonly StringName Dissolve = "dissolve";

    private static readonly Dictionary<DeathStyle, DeathEffect> _effects = new()
    {
        [DeathStyle.Burn] = new BurnDeath(),
        [DeathStyle.Freeze] = new FreezeDeath(),
        [DeathStyle.Shatter] = new ShatterDeath(),
        [DeathStyle.Fry] = new FryDeath(),
        [DeathStyle.Sink] = new SinkDeath(),
        [DeathStyle.Gust] = new GustDeath()
    };

    // Null for Plain - the body just ragdolls.
    public static DeathEffect For(DeathStyle style) => _effects.GetValueOrDefault(style);

    // Replaces each body mesh's own material, which is passed in as base_color.
    public abstract Material BodyMaterial { get; }

    // How long the body stays. Each runs a little past its effect, since copies hear of the death
    // after the host, whose freeing the body removes it everywhere.
    public abstract float Duration { get; }

    // Holds the pose the enemy died in rather than ragdolling.
    public virtual bool KeepsPose => false;

    public abstract void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse);

    protected static Aabb BodyBounds(IReadOnlyList<MeshInstance3D> body)
    {
        Aabb bounds = body[0].GlobalTransform * body[0].GetAabb();

        for (int i = 1; i < body.Count; i++)
        {
            bounds = bounds.Merge(body[i].GlobalTransform * body[i].GetAabb());
        }

        return bounds;
    }

    protected static void SetOnAll(IReadOnlyList<MeshInstance3D> body, StringName parameter, Variant value)
    {
        foreach (MeshInstance3D mesh in body)
        {
            mesh.SetInstanceShaderParameter(parameter, value);
        }
    }
}
