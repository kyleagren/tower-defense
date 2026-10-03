using Godot;
using System.Collections.Generic;

// A Shatter kill: the body freezes solid where it stands, violet cracks spread across it, and it
// bursts into shards and sparks.
public class ShatterDeath : DeathEffect
{
    private static readonly Material _bodyMaterial = GD.Load<Material>("res://enemies/death_effects/shatter_body.tres");
    private static readonly PackedScene _shardsScene = GD.Load<PackedScene>("res://enemies/death_effects/shatter_shards.tscn");

    private static readonly StringName Frost = "frost";
    private static readonly StringName Crack = "crack";

    private const float FrostTime = 0.25f;
    private const float CrackTime = 0.4f;
    // The body is already gone by now; this only covers copies hearing late.
    private const float Tail = 0.5f;

    public override Material BodyMaterial => _bodyMaterial;
    public override float Duration => FrostTime + CrackTime + Tail;
    public override bool KeepsPose => true;

    public override void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse)
    {
        if (body.Count == 0)
        {
            return;
        }

        Tween tween = enemy.CreateTween();
        tween.TweenMethod(Callable.From<float>(frost => SetOnAll(body, Frost, frost)), 0f, 1f, FrostTime);
        tween.TweenMethod(Callable.From<float>(crack => SetOnAll(body, Crack, crack)), 0f, 1f, CrackTime);
        tween.TweenCallback(Callable.From(() => Break(body)));
    }

    private static void Break(IReadOnlyList<MeshInstance3D> body)
    {
        DeathParticles.Spawn(_shardsScene, BodyBounds(body), Vector3.Zero);

        foreach (MeshInstance3D mesh in body)
        {
            mesh.Visible = false;
        }
    }
}
