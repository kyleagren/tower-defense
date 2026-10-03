using Godot;
using System.Collections.Generic;

// A Wind kill: the limbs are torn off and the body is worn away from the side the wind struck, flecks
// streaming off downwind.
public class GustDeath : DeathEffect
{
    private static readonly Material _bodyMaterial = GD.Load<Material>("res://enemies/death_effects/gust_body.tres");
    private static readonly PackedScene _flecksScene = GD.Load<PackedScene>("res://enemies/death_effects/wind_flecks.tscn");

    private static readonly StringName SweepDirection = "sweep_direction";
    private static readonly StringName SweepFrom = "sweep_from";
    private static readonly StringName SweepSpan = "sweep_span";

    private static readonly BodyPart[] Limbs = { BodyPart.ArmL, BodyPart.ArmR, BodyPart.LegL, BodyPart.LegR };

    // kg·m/s per limb - enough to scatter them a short way, not launch them.
    private const float LimbImpulse = 2.5f;

    private const float GustDelay = 0.1f;
    private const float GustTime = 1.1f;
    private const float Tail = 0.3f;

    public override Material BodyMaterial => _bodyMaterial;
    public override float Duration => GustDelay + GustTime + Tail;

    public override void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse)
    {
        if (body.Count == 0)
        {
            return;
        }

        // A killing hit with no push has no direction of its own, so the wind picks one.
        Vector3 downwind = new Vector3(ragdollImpulse.X, 0f, ragdollImpulse.Z);
        downwind = downwind.LengthSquared() > 0.0001f
            ? downwind.Normalized()
            : Vector3.Forward.Rotated(Vector3.Up, GD.Randf() * Mathf.Tau);

        SetOnAll(body, SweepDirection, downwind);
        UpdateSweep(body, downwind);

        Tween tween = enemy.CreateTween().SetParallel();
        // The sweep's extent is refreshed as it runs, since the ragdoll keeps moving.
        tween.TweenMethod(Callable.From<float>(amount =>
        {
            UpdateSweep(body, downwind);
            SetOnAll(body, Dissolve, amount);
        }), 0f, 1f, GustTime).SetDelay(GustDelay);
        tween.TweenCallback(Callable.From(() => DeathParticles.Spawn(_flecksScene, BodyBounds(body), downwind)))
            .SetDelay(GustDelay);
        tween.TweenCallback(Callable.From(() =>
        {
            foreach (BodyPart limb in Limbs)
            {
                enemy.DetachPart(limb, downwind, LimbImpulse);
            }
        })).SetDelay(GustDelay);
    }

    // The body's extent along the wind, so the sweep runs from its upwind edge to its downwind one.
    private static void UpdateSweep(IReadOnlyList<MeshInstance3D> body, Vector3 downwind)
    {
        Aabb bounds = BodyBounds(body);
        float from = float.MaxValue;
        float to = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            float along = bounds.GetEndpoint(i).Dot(downwind);
            from = Mathf.Min(from, along);
            to = Mathf.Max(to, along);
        }

        SetOnAll(body, SweepFrom, from);
        SetOnAll(body, SweepSpan, Mathf.Max(to - from, 0.01f));
    }
}
