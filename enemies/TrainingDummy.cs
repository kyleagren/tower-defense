using Godot;
using System.Collections.Generic;

// A permanent, unkillable target for testing Ritual behavior against - no movement, no death, no
// rewards, no loot. Dev-only; not part of the real enemy roster. Place directly in a level.
public partial class TrainingDummy : Enemy
{
    [Export] public Label3D DpsLabel { get; private set; }
    [Export] public float DpsWindowSeconds = 3f;

    private readonly List<(double time, float amount)> _recentHits = new();

    public override void Die(Vector3 ragdollImpulse)
    {
        CurrentHealth = MaxHealth;
        HealthBar.SetFraction(1f);
    }

    protected override void OnDamageTaken(DamagePacket amount)
    {
        base.OnDamageTaken(amount);
        _recentHits.Add((Time.GetTicksMsec() / 1000.0, amount.Total));
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        UpdateDpsDisplay();
    }

    private void UpdateDpsDisplay()
    {
        double now = Time.GetTicksMsec() / 1000.0;
        _recentHits.RemoveAll(hit => now - hit.time > DpsWindowSeconds);

        float totalDamage = 0f;
        foreach ((double _, float amount) in _recentHits)
        {
            totalDamage += amount;
        }

        DpsLabel.Text = $"{totalDamage / DpsWindowSeconds:F0} DPS";
    }
}
