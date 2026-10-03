using Godot;
using System.Collections.Generic;

// A permanent, unkillable target for testing Ritual behavior against - no movement, no death, no
// rewards, no loot. Dev-only; not part of the real enemy roster. Place directly in a level.
public partial class TrainingDummy : Enemy
{
    [Export] public Label3D DpsLabel { get; private set; }
    [Export] public float DpsWindowSeconds = 3f;

    private readonly List<(double time, float amount)> _recentHits = new();
    private int _displayedDps = -1;

    public override void Die(Vector3 ragdollImpulse, DeathStyle style)
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

        // Copies show the host's readout, synced onto DpsLabel.
        if (IsSimulatedHere)
        {
            UpdateDpsDisplay();
        }
    }

    // Runs every physics frame, so it avoids the two things that would otherwise cost something per
    // tick for a dev-only readout: RemoveAll's per-call closure (hits are appended in time order, so
    // expiring them is a walk off the front) and rebuilding an identical label string.
    private void UpdateDpsDisplay()
    {
        double now = Time.GetTicksMsec() / 1000.0;

        int expired = 0;
        while (expired < _recentHits.Count && now - _recentHits[expired].time > DpsWindowSeconds)
        {
            expired++;
        }

        if (expired > 0)
        {
            _recentHits.RemoveRange(0, expired);
        }

        float totalDamage = 0f;
        foreach ((double _, float amount) in _recentHits)
        {
            totalDamage += amount;
        }

        int dps = Mathf.RoundToInt(totalDamage / DpsWindowSeconds);

        if (dps == _displayedDps)
        {
            return;
        }

        _displayedDps = dps;
        DpsLabel.Text = $"{dps} DPS";
    }
}
