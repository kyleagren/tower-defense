using Godot;
using System.Collections.Generic;

// A Lightning kill: the body strobes dark and jerks as the current runs through it, its skeleton
// showing through on every flash, then ends charred and crumbles to ash.
public class FryDeath : DeathEffect
{
    private static readonly Material _bodyMaterial = GD.Load<Material>("res://enemies/death_effects/fry_body.tres");
    private static readonly PackedScene _ashScene = GD.Load<PackedScene>("res://enemies/death_effects/ash_flakes.tscn");

    private static readonly Material _boneMaterial = new ShaderMaterial
    {
        Shader = GD.Load<Shader>("res://enemies/death_effects/xray_bone.gdshader")
    };

    private static readonly CylinderMesh _boneShaft = new() { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 };
    private static readonly SphereMesh _boneKnob = new() { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 };
    private static readonly BoxMesh _boneBlock = new() { Size = Vector3.One };

    private const float ShockTime = 0.6f;
    private const float StrobeInterval = 0.09f;
    private const float TwitchInterval = 0.08f;
    // kg·m/s on the root bone - small beside a Fireball's 20, so it jerks rather than flies.
    private const float TwitchImpulse = 3f;
    private const float CrumbleTime = 1f;
    private const float Tail = 0.3f;
    private const int RibCount = 3;

    public override Material BodyMaterial => _bodyMaterial;
    public override float Duration => ShockTime + CrumbleTime + Tail;

    public override void Play(Enemy enemy, IReadOnlyList<MeshInstance3D> body, Vector3 ragdollImpulse)
    {
        if (body.Count == 0)
        {
            return;
        }

        List<Node3D> skeleton = BuildSkeleton(enemy, body);

        Tween tween = enemy.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(amount => SetOnAll(body, CharAmount, amount)), 0f, 1f, ShockTime);

        // On every other step, dimming as it goes.
        int step = 0;
        for (float time = 0f; time < ShockTime; time += StrobeInterval, step++)
        {
            bool on = step % 2 == 0;
            float flash = on ? 1f - time / ShockTime * 0.6f : 0f;
            tween.TweenCallback(Callable.From(() => ShowFlash(body, skeleton, flash, on))).SetDelay(time);
        }

        tween.TweenCallback(Callable.From(() => ShowFlash(body, skeleton, 0f, false))).SetDelay(ShockTime);

        for (float time = TwitchInterval; time < ShockTime; time += TwitchInterval)
        {
            tween.TweenCallback(Callable.From(() => enemy.ApplyExtraRagdollImpulse(RandomTwitch()))).SetDelay(time);
        }

        tween.TweenMethod(Callable.From<float>(amount => SetOnAll(body, Dissolve, amount)), 0f, 1f, CrumbleTime)
            .SetDelay(ShockTime);
        tween.TweenCallback(Callable.From(() => DeathParticles.Spawn(_ashScene, BodyBounds(body), Vector3.Zero)))
            .SetDelay(ShockTime);
    }

    private static void ShowFlash(IReadOnlyList<MeshInstance3D> body, List<Node3D> skeleton, float flash, bool bonesShowing)
    {
        SetOnAll(body, Flash, flash);

        foreach (Node3D bones in skeleton)
        {
            bones.Visible = bonesShowing;
        }
    }

    // Any direction, leaning up so it jerks off the ground rather than into it.
    private static Vector3 RandomTwitch()
    {
        Vector3 direction = new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(0.0, 1.0), (float)GD.RandRange(-1.0, 1.0));
        return direction.Normalized() * TwitchImpulse;
    }

    // Bones inside each body part, sized from the part's own box so they fit any enemy: a skull in the
    // head, a spine and ribs in the torso, a long bone in everything else.
    private static List<Node3D> BuildSkeleton(Enemy enemy, IReadOnlyList<MeshInstance3D> body)
    {
        List<Node3D> skeleton = new List<Node3D>();
        MeshInstance3D head = enemy.BodyMeshFor(BodyPart.Head);
        MeshInstance3D torso = enemy.BodyMeshFor(BodyPart.Torso);

        foreach (MeshInstance3D mesh in body)
        {
            Aabb box = mesh.GetAabb();
            Node3D bones = new Node3D { Position = box.GetCenter(), Visible = false };
            mesh.AddChild(bones);

            if (mesh == head)
            {
                AddSkull(bones, box.Size);
            }
            else if (mesh == torso)
            {
                AddRibcage(bones, box.Size);
            }
            else
            {
                AddLongBone(bones, box.Size);
            }

            skeleton.Add(bones);
        }

        return skeleton;
    }

    private static void AddSkull(Node3D bones, Vector3 size)
    {
        float width = Mathf.Min(size.X, size.Z);
        AddBone(bones, _boneKnob, new Vector3(0f, size.Y * 0.08f, 0f), new Vector3(width * 0.6f, size.Y * 0.55f, width * 0.6f));
        AddBone(bones, _boneBlock, new Vector3(0f, -size.Y * 0.25f, 0f), new Vector3(width * 0.4f, size.Y * 0.12f, width * 0.35f));
    }

    private static void AddRibcage(Node3D bones, Vector3 size)
    {
        float thickness = Mathf.Min(size.X, size.Z) * 0.1f;
        AddBone(bones, _boneShaft, Vector3.Zero, new Vector3(thickness, size.Y * 0.9f, thickness));

        // Upper chest down, each a little narrower.
        for (int i = 0; i < RibCount; i++)
        {
            Vector3 position = new Vector3(0f, size.Y * (0.3f - i * 0.18f), 0f);
            AddBone(bones, _boneBlock, position, new Vector3(size.X * (0.75f - i * 0.1f), thickness, size.Z * 0.6f));
        }

        AddBone(bones, _boneBlock, new Vector3(0f, -size.Y * 0.4f, 0f), new Vector3(size.X * 0.6f, thickness * 1.5f, size.Z * 0.5f));
    }

    private static void AddLongBone(Node3D bones, Vector3 size)
    {
        float thickness = Mathf.Min(size.X, size.Z) * 0.25f;
        float length = size.Y * 0.8f;
        Vector3 knob = Vector3.One * thickness * 1.8f;

        AddBone(bones, _boneShaft, Vector3.Zero, new Vector3(thickness, length, thickness));
        AddBone(bones, _boneKnob, Vector3.Up * length / 2f, knob);
        AddBone(bones, _boneKnob, Vector3.Down * length / 2f, knob);
    }

    private static void AddBone(Node3D bones, Mesh mesh, Vector3 position, Vector3 scale)
    {
        bones.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = _boneMaterial,
            Position = position,
            Scale = scale,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
}
