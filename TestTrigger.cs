using Godot;
using System.Linq;

public partial class TestTrigger : Node3D
{
    [Export] public FireballSpell FireballSpell { get; private set; }
    [Export] public MultiProjectileRune MultiProjectileRune { get; private set; }
    [Export] public NovaProjectileRune NovaProjectileRune { get; private set; }
    [Export] public ProjectileCastEffect ProjectileCastEffect { get; private set; }
    [Export] public Node3D SpawnPoint { get; private set; }

    private SpellInstance fireballInstance;

    public override void _Ready()
    {
        fireballInstance = new SpellInstance(FireballSpell);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("cast_spell"))
        {
            CastFireball();
        }

        if (@event.IsActionPressed("toggle_rune"))
        {
            ToggleRune(MultiProjectileRune, "Multi-Projectile");
        }

        if (@event.IsActionPressed("toggle_nova"))
        {
            ToggleRune(NovaProjectileRune, "Nova-Projectile");
        }
    }

    private void ToggleRune(Rune rune, string label)
    {
        if (fireballInstance.AttachedRunes.Contains(rune))
        {
            fireballInstance.DetachRune(rune);
            GD.Print($"{label} detached.");
        }
        else
        {
            fireballInstance.TryAttachRune(rune);
            GD.Print($"{label} attached.");
        }
    }

    private void CastFireball()
    {
        SpellCastContext context = fireballInstance.Resolve();

        if (context is ProjectileCastContext projectileContext)
        {
            ProjectileCastEffect.Cast(projectileContext, SpawnPoint);
        }
    }
}