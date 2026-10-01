using Godot;
using System.Collections.Generic;
using System.Linq;

// Wind reacting with whatever Status it finds. A hit led by Wind, landing on an enemy with a Status
// while another enemy stands close by, takes that Status and bursts it outward as damage of its
// element - an ordinary hit, so it sets the Status again on everything it touches. Built into Wind
// itself rather than a rune, so any Wind hit swirls, Sentinel's or Ritual's. Burst hits carry no
// Wind and no runes, so a swirl can never set off another.
public static class Swirl
{
    // Called once the hit has fully resolved, with the Status it found on arrival.
    public static void TryTrigger(Enemy struck, Hit hit, Tag? statusBefore)
    {
        if (!statusBefore.HasValue || !CanSwirl(hit))
        {
            return;
        }

        List<Enemy> caught = EnemyTargeting.FindAllWithinFlat(struck.GlobalPosition, GameRules.SwirlRadius, enemy => !enemy.IsDead);

        // A lone enemy would only lose its Status and get it straight back.
        if (!caught.Any(enemy => enemy != struck))
        {
            return;
        }

        struck.RemoveStatus();

        DamagePacket burst = new DamagePacket { Origin = hit.Damage.Origin, Source = hit.Damage.Source };
        burst.AddElement(statusBefore.Value, hit.Damage.WindDamage * GameRules.SwirlDamageFraction);

        foreach (Enemy enemy in caught)
        {
            enemy.TakeDamage(new Hit { Damage = burst, Attribution = hit.Attribution });
        }
    }

    // Wind has to be the largest share of the hit, Physical included. A damage-over-time tick or a
    // mirrored copy isn't a fresh hit, so neither swirls.
    private static bool CanSwirl(Hit hit)
    {
        if (hit.Type == HitType.DamageOverTime || hit.Type == HitType.Mirrored)
        {
            return false;
        }

        DamagePacket damage = hit.Damage;
        float largestOther = Mathf.Max(Mathf.Max(damage.PhysicalDamage, damage.FireDamage),
            Mathf.Max(Mathf.Max(damage.WaterDamage, damage.ColdDamage), damage.LightningDamage));

        return damage.WindDamage > 0f && damage.WindDamage >= largestOther;
    }
}
