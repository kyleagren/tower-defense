using Godot;
using System.Collections.Generic;

// A Cold kill: the enemy freezes where it stands and falls apart a piece at a time, then every piece
// crumbles to powder, leaving small piles of ice that sink into the ground.
public class FreezeDeath : DeathEffect
{
    private static readonly Material _bodyMaterial = GD.Load<Material>("res://enemies/death_effects/freeze_body.tres");
    private static readonly PackedScene _powderScene = GD.Load<PackedScene>("res://enemies/death_effects/frost_powder.tscn");

    private static readonly StandardMaterial3D _pileMaterial = new()
    {
        AlbedoColor = new Color(0.8f, 0.92f, 1f),
        Roughness = 0.4f
    };

    // Unit diameter. A pile is this flattened, centred on the ground so only its top half shows.
    private static readonly SphereMesh _pileMesh = new() { Radius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 8 };

    private static readonly StringName Frost = "frost";

    private static readonly BodyPart[] DropOrder =
    {
        BodyPart.ArmL, BodyPart.ArmR, BodyPart.Head, BodyPart.LegL, BodyPart.LegR, BodyPart.Torso
    };

    private const float FrostTime = 0.3f;
    private const float FirstDrop = 0.4f;
    private const float DropInterval = 0.2f;
    // kg·m/s - a nudge, so a piece topples off rather than dropping straight down.
    private const float DropImpulse = 0.5f;
    // After the last piece drops, long enough for it to land.
    private const float LandTime = 0.5f;
    private const float CrumbleTime = 0.6f;
    private const float PileHoldTime = 1f;
    private const float PileSinkTime = 2f;

    // A pile's width against the cube root of its piece's volume, and its height against its width.
    private const float PileWidth = 1f;
    private const float PileHeight = 0.3f;
    private const float GroundProbeDown = 3f;

    private static float CrumbleStart => FirstDrop + DropInterval * (DropOrder.Length - 1) + LandTime;

    public override Material BodyMaterial => _bodyMaterial;
    public override float Duration => CrumbleStart + CrumbleTime + PileHoldTime + PileSinkTime;
    public override bool KeepsPose => true;

    public override void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse)
    {
        if (body.Count == 0)
        {
            return;
        }

        Tween tween = enemy.CreateTween().SetParallel();
        // Physics, so Crumble can query the space for the ground.
        tween.SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenMethod(Callable.From<float>(frost => SetOnAll(body, Frost, frost)), 0f, 1f, FrostTime);

        for (int i = 0; i < DropOrder.Length; i++)
        {
            BodyPart part = DropOrder[i];
            tween.TweenCallback(Callable.From(() => enemy.DetachPart(part, Vector3.Zero, DropImpulse)))
                .SetDelay(FirstDrop + DropInterval * i);
        }

        tween.TweenMethod(Callable.From<float>(amount => SetOnAll(body, Dissolve, amount)), 0f, 1f, CrumbleTime)
            .SetDelay(CrumbleStart);
        tween.TweenCallback(Callable.From(() => Crumble(enemy, body))).SetDelay(CrumbleStart);
    }

    private static void Crumble(Enemy enemy, IReadOnlyList<MeshInstance3D> body)
    {
        DeathParticles.Spawn(_powderScene, BodyBounds(body), Vector3.Zero);
        PhysicsDirectSpaceState3D space = enemy.GetWorld3D().DirectSpaceState;

        foreach (MeshInstance3D piece in body)
        {
            Aabb box = piece.GetAabb();
            Vector3 center = piece.GlobalTransform * box.GetCenter();
            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(center, center + Vector3.Down * GroundProbeDown, PhysicsLayers.World);
            Godot.Collections.Dictionary hit = space.IntersectRay(query);

            if (hit.Count > 0)
            {
                float width = Mathf.Pow(box.Size.X * box.Size.Y * box.Size.Z, 1f / 3f) * PileWidth;
                GrowPile(enemy, (Vector3)hit["position"], width);
            }
        }
    }

    // Grows as its piece crumbles, then sinks out of sight.
    private static void GrowPile(Enemy enemy, Vector3 ground, float width)
    {
        float height = width * PileHeight;
        MeshInstance3D pile = new MeshInstance3D
        {
            Mesh = _pileMesh,
            MaterialOverride = _pileMaterial,
            TopLevel = true,
            Scale = Vector3.One * 0.001f
        };
        enemy.AddChild(pile);
        pile.GlobalPosition = ground;

        Tween tween = pile.CreateTween();
        tween.TweenProperty(pile, "scale", new Vector3(width, height * 2f, width), CrumbleTime);
        tween.TweenInterval(PileHoldTime);
        tween.TweenProperty(pile, "global_position", ground + Vector3.Down * height, PileSinkTime)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
    }
}
