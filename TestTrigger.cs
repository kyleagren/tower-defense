using Godot;
using System.Linq;

public partial class TestTrigger : Node3D
{
    [Export] private FireballSpell fireballSpell;
    [Export] private MultiProjectileRune multiProjectileRune;
    [Export] private ProjectileCastEffect projectileCastEffect;
    [Export] private Node3D spawnPoint;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("cast_spell"))
        {
            CastFireball();
        }

        if (@event.IsActionPressed("toggle_rune"))
        {
            ToggleMultiProjectileRune();
        }
    }

    private void ToggleMultiProjectileRune()
    {
        if (fireballSpell.AttachedRunes.Contains(multiProjectileRune))
        {
            fireballSpell.DetachRune(multiProjectileRune);
            GD.Print("Multi-Projectile detached.");
        }
        else
        {
            fireballSpell.TryAttachRune(multiProjectileRune);
            GD.Print("Multi-Projectile attached.");
        }
    }

    private void CastFireball()
    {
        SpellCastContext context = fireballSpell.Resolve();

        if (context is ProjectileCastContext projectileContext)
        {
            projectileCastEffect.Cast(projectileContext, spawnPoint);
        }
    }
}