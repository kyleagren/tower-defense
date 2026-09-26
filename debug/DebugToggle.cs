using Godot;

public partial class DebugToggle : Node
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.PhysicalKeycode == Key.F1)
        {
            DebugSettings.ShowEnemyHealthBars = !DebugSettings.ShowEnemyHealthBars;

            foreach (Enemy enemy in LevelContext.Current.Enemies.Active)
            {
                enemy.HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;
            }
        }
        else if (keyEvent.PhysicalKeycode == Key.F2)
        {
            DebugSettings.ShowWeaponDamageNumbers = !DebugSettings.ShowWeaponDamageNumbers;
        }
        else if (keyEvent.PhysicalKeycode == Key.F3)
        {
            CharacterId next = PlayerProfiles.Local.SelectedCharacter == CharacterId.Soldier
                ? CharacterId.Tank
                : CharacterId.Soldier;

            PlayerProfiles.Local.SelectCharacter(next);
            GetTree().ReloadCurrentScene();
        }
        else if (keyEvent.PhysicalKeycode == Key.F4)
        {
            WaveController.Instance?.DebugLoadAndStartWaveFile("res://assets/waves/test_marksman.json");
        }
        else if (keyEvent.PhysicalKeycode == Key.F5)
        {
            DebugSettings.ShowTowerDamageNumbers = !DebugSettings.ShowTowerDamageNumbers;
        }
        else if (keyEvent.PhysicalKeycode == Key.F6)
        {
            Rune legendarySalvo = GD.Load<Rune>("res://assets/runes/multi_projectile/multi_projectile_rune_legendary.tres");

            RuneTome tome = new RuneTome();
            tome.SetTaughtRune(legendarySalvo);
            tome.Initialize(legendarySalvo.Rarity, DefaultItemIcons.Get(tome), CharacterId.AnySentinel, new Godot.Collections.Array<RolledAffix>());

            PlayerProfiles.Local.TryAddToInventory(tome);
        }
        else if (keyEvent.PhysicalKeycode == Key.F7)
        {
            KnockBackEnemiesAroundLocalSentinel();
        }
    }

    private const float DebugKnockbackRadius = 10f;
    private const float DebugKnockbackImpulse = 1600f;

    // Shoves every enemy near the local Sentinel straight away from it - exercises knockback without
    // the pylon or shield slam that will eventually drive it.
    private static void KnockBackEnemiesAroundLocalSentinel()
    {
        Sentinel sentinel = Sentinel.Local;
        if (sentinel == null)
        {
            return;
        }

        foreach (Enemy enemy in LevelContext.Current.Enemies.Active)
        {
            Vector3 offset = enemy.GlobalPosition - sentinel.GlobalPosition;
            offset.Y = 0f;

            if (offset == Vector3.Zero || offset.LengthSquared() > DebugKnockbackRadius * DebugKnockbackRadius)
            {
                continue;
            }

            enemy.ApplyKnockback(offset.Normalized() * DebugKnockbackImpulse);
        }
    }
}
