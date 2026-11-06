using System.Collections.Generic;

// What one Ritual has done, counted on the host for the Ritual details panel - once since it was
// placed, and again for the current (or, between waves, the last) wave alone.
public class RitualStats
{
    public class Tally
    {
        public float Damage;
        public float Overkill;
        public int Kills;
        public int Casts;
        public readonly Dictionary<OnHitRune, int> RuneEffects = new();
    }

    public Tally SincePlaced { get; } = new();
    public Tally Wave { get; private set; } = new();

    // Mana Sentinels paid to repair it; repairs during Build are free and add nothing.
    public float RepairManaPaid { get; private set; }

    public void StartWave() => Wave = new Tally();

    public void RecordHit(float effective, float overkill, bool killed)
    {
        AddHit(SincePlaced, effective, overkill, killed);
        AddHit(Wave, effective, overkill, killed);
    }

    public void RecordCast()
    {
        SincePlaced.Casts++;
        Wave.Casts++;
    }

    public void RecordRuneEffect(OnHitRune rune)
    {
        AddRuneEffect(SincePlaced, rune);
        AddRuneEffect(Wave, rune);
    }

    public void RecordRepair(float mana) => RepairManaPaid += mana;

    private static void AddHit(Tally tally, float effective, float overkill, bool killed)
    {
        tally.Damage += effective;
        tally.Overkill += overkill;
        tally.Kills += killed ? 1 : 0;
    }

    private static void AddRuneEffect(Tally tally, OnHitRune rune) =>
        tally.RuneEffects[rune] = tally.RuneEffects.GetValueOrDefault(rune) + 1;
}
