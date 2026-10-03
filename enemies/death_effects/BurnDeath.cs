using Godot;
using System.Collections.Generic;

// A Fire kill: the body chars as it falls, then burns away from scattered spots, the edge glowing and
// embers rising off it.
public class BurnDeath : DeathEffect
{
    private static readonly Material _bodyMaterial = GD.Load<Material>("res://enemies/death_effects/burn_body.tres");
    private static readonly PackedScene _embersScene = GD.Load<PackedScene>("res://enemies/death_effects/burn_embers.tscn");

    private const float CharTime = 0.5f;
    private const float BurnDelay = 0.3f;
    private const float BurnTime = 1.2f;
    private const float Tail = 0.3f;

    public override Material BodyMaterial => _bodyMaterial;
    public override float Duration => BurnDelay + BurnTime + Tail;

    public override void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse)
    {
        if (body.Count == 0)
        {
            return;
        }

        Tween tween = enemy.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(amount => SetOnAll(body, CharAmount, amount)), 0f, 1f, CharTime);
        tween.TweenMethod(Callable.From<float>(amount => SetOnAll(body, Dissolve, amount)), 0f, 1f, BurnTime)
            .SetDelay(BurnDelay);
        tween.TweenCallback(Callable.From(() => DeathParticles.Spawn(_embersScene, BodyBounds(body), Vector3.Zero)))
            .SetDelay(BurnDelay);
    }
}
