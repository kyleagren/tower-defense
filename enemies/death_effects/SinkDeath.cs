using Godot;
using System.Collections.Generic;

// A Water kill: the body soaks dark and glossy as it falls, then sinks into the ground as though the
// ground were water, rings spreading on the surface where it goes under.
public class SinkDeath : DeathEffect
{
    private static readonly Material _bodyMaterial = new ShaderMaterial
    {
        Shader = GD.Load<Shader>("res://enemies/death_effects/sink_body.gdshader")
    };

    private static readonly Material _rippleMaterial = new ShaderMaterial
    {
        Shader = GD.Load<Shader>("res://enemies/death_effects/sink_ripple.gdshader")
    };

    private static readonly StringName Wetness = "wetness";
    private static readonly StringName SinkDepth = "sink_depth";
    private static readonly StringName GroundHeight = "ground_height";
    private static readonly StringName RippleAge = "age";
    private static readonly StringName RippleFade = "fade";

    private const float SoakTime = 0.4f;
    // Long enough for the ragdoll to land before the body starts going under.
    private const float SettleTime = 0.6f;
    private const float SinkTime = 1.8f;
    private const float RippleFadeTime = 0.5f;

    // The ripples' width against the body's widest extent.
    private const float RippleSpread = 1.6f;
    // Lifts the ripples off the ground so they don't fight it for depth.
    private const float RippleLift = 0.02f;
    private const float GroundProbeUp = 0.5f;
    private const float GroundProbeDown = 3f;

    public override Material BodyMaterial => _bodyMaterial;
    public override float Duration => SettleTime + SinkTime + RippleFadeTime;

    public override void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse)
    {
        if (body.Count == 0)
        {
            return;
        }

        Tween tween = enemy.CreateTween();
        // Physics, so BeginSinking can query the space for the ground.
        tween.SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenMethod(Callable.From<float>(wetness => SetOnAll(body, Wetness, wetness)), 0f, 1f, SoakTime);
        tween.TweenInterval(SettleTime - SoakTime);
        tween.TweenCallback(Callable.From(() => BeginSinking(enemy, body)));
    }

    private static void BeginSinking(Enemy enemy, IReadOnlyList<MeshInstance3D> body)
    {
        Aabb bounds = BodyBounds(body);
        Vector3 center = bounds.GetCenter();

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            center + Vector3.Up * GroundProbeUp, center + Vector3.Down * GroundProbeDown, PhysicsLayers.World);
        Godot.Collections.Dictionary hit = enemy.GetWorld3D().DirectSpaceState.IntersectRay(query);

        // Nothing under it - knocked off an edge - so there's no ground to sink into.
        if (hit.Count == 0)
        {
            return;
        }

        float groundHeight = ((Vector3)hit["position"]).Y;
        float depth = Mathf.Max(bounds.End.Y - groundHeight, 0f);
        SetOnAll(body, GroundHeight, groundHeight);

        float rippleSize = Mathf.Max(bounds.Size.X, bounds.Size.Z) * RippleSpread;
        MeshInstance3D ripple = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(rippleSize, rippleSize) },
            MaterialOverride = _rippleMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true
        };
        enemy.AddChild(ripple);
        ripple.GlobalPosition = new Vector3(center.X, groundHeight + RippleLift, center.Z);

        float rippleTime = SinkTime + RippleFadeTime;

        Tween tween = enemy.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(sink => SetOnAll(body, SinkDepth, sink)), 0f, depth, SinkTime)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenMethod(Callable.From<float>(age => ripple.SetInstanceShaderParameter(RippleAge, age)), 0f, rippleTime, rippleTime);
        tween.TweenMethod(Callable.From<float>(fade => ripple.SetInstanceShaderParameter(RippleFade, fade)), 1f, 0f, RippleFadeTime)
            .SetDelay(SinkTime);
    }

}
