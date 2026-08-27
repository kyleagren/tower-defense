using Godot;
using System.Linq;

public partial class SaveManager : Node
{
    private const string SavePath = "user://save_data.tres";

    public static SaveManager Instance { get; private set; }
    public PlayerSaveData Data { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public void Load()
    {
        Data = ResourceLoader.Exists(SavePath)
            ? ResourceLoader.Load<PlayerSaveData>(SavePath)
            : new PlayerSaveData();

        if (EnsureDefaultContent())
        {
            Save();
        }
    }

    private bool EnsureDefaultContent()
    {
        bool changed = false;

        changed |= EnsureKnownSpell("res://assets/spells/FireballSpell.tres");
        changed |= EnsureKnownSpell("res://assets/spells/LightningBoltSpell.tres");
        changed |= EnsureKnownRune("res://assets/runes/MultiProjectileRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/NovaProjectileRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/ChainRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/PierceRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/ReturnRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/IncreasedAreaRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/CastSpeedRune.tres");
        changed |= EnsureKnownRune("res://assets/runes/ForkRune.tres");

        return changed;
    }

    private bool EnsureKnownSpell(string path)
    {
        Spell spell = GD.Load<Spell>(path);

        if (Data.KnownSpells.Any(known => known == spell))
        {
            return false;
        }

        Data.KnownSpells.Add(spell);
        return true;
    }

    private bool EnsureKnownRune(string path)
    {
        Rune rune = GD.Load<Rune>(path);

        if (Data.KnownRunes.Any(entry => entry.Rune == rune))
        {
            return false;
        }

        Data.KnownRunes.Add(new RuneInventoryEntry { Rune = rune, Unlimited = true });
        return true;
    }

    public void Save()
    {
        ResourceSaver.Save(Data, SavePath);
    }
}
