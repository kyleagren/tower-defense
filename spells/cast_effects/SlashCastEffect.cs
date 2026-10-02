using Godot;

// Several quick hits on one target, each drawn as a slash across it. Hits after the first wait on a
// physics-step timer that pauses with the game, and are dropped if the target is gone by then.
public class SlashCastEffect : CastEffect
{
    // How far a slash tilts off horizontal. A slash draws from its upper end down; mirroring the
    // tilt starts one at the top-left and the next at the top-right, so a pair reads as a tall X.
    private const float SlashAngle = Mathf.Pi * 0.3f;
    private const float FirstTilt = -SlashAngle;
    private const float SecondTilt = Mathf.Pi + SlashAngle;

    // How far in front of the body the slash is drawn, so the body doesn't hide it.
    private const float FrontClearance = 0.15f;

    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        SlashCastData data = context.Slash;
        Vector3 origin = spawnPoint.GlobalPosition;
        Enemy target = EnemyTargeting.FindTarget(origin, context, sightFrom: origin);

        if (data == null || target == null)
        {
            return;
        }

        context.ChosenTarget = target;
        SpellHit spellHit = context.SpellHit;
        DamagePacket damage = context.Damage;
        float ragdollForce = context.RagdollForce.Value;

        Strike(target, origin, damage, ragdollForce, spellHit, data, 0);

        for (int i = 1; i < data.HitCount; i++)
        {
            int index = i;
            spawnPoint.GetTree().CreateTimer(data.HitInterval * i, processAlways: false, processInPhysics: true).Timeout +=
                () => Strike(target, origin, damage, ragdollForce, spellHit, data, index);
        }
    }

    private static void Strike(Enemy target, Vector3 origin, DamagePacket damage, float ragdollForce, SpellHit spellHit, SlashCastData data, int index)
    {
        if (!GodotObject.IsInstanceValid(target) || target.IsDead)
        {
            return;
        }

        Vector3 center = target.GetJitteredCenterMass(0f);
        Vector3 facing = center - origin;
        target.TakeDamage(spellHit.CreateHit(damage, facing.Normalized() * ragdollForce));

        if (data.VisualScene != null)
        {
            Vector3 towardRitual = -new Vector3(facing.X, 0f, facing.Z).Normalized();
            Vector3 front = center + towardRitual * (BodyRadius(target) + FrontClearance);
            // Waits for the slashes still to come, so they all clear together.
            float extraHold = (data.HitCount - 1 - index) * data.HitInterval;
            LevelContext.Current.SpellEffects.ShowSlash(data.VisualScene, front, facing, index % 2 == 0 ? FirstTilt : SecondTilt, extraHold);
        }
    }

    private static float BodyRadius(Enemy enemy) =>
        enemy.BodyCollision.Shape is CapsuleShape3D capsule ? capsule.Radius : 0.4f;
}
