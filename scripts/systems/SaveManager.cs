using Godot;

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
        if (ResourceLoader.Exists(SavePath))
        {
            Data = ResourceLoader.Load<PlayerSaveData>(SavePath);
        }
        else
        {
            Data = new PlayerSaveData();
            Data.KnownSpells.Add(GD.Load<Spell>("res://assets/spells/FireballSpell.tres"));
            Data.KnownSpells.Add(GD.Load<Spell>("res://assets/spells/LightningBoltSpell.tres"));
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = GD.Load<Rune>("res://assets/runes/MultiProjectileRune.tres"), Unlimited = true });
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = GD.Load<Rune>("res://assets/runes/NovaProjectileRune.tres"), Unlimited = true });
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = GD.Load<Rune>("res://assets/runes/ChainRune.tres"), Unlimited = true });
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = GD.Load<Rune>("res://assets/runes/PierceRune.tres"), Unlimited = true });
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = GD.Load<Rune>("res://assets/runes/ReturnRune.tres"), Unlimited = true });
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = GD.Load<Rune>("res://assets/runes/IncreasedAreaRune.tres"), Unlimited = true });
            Save();
        }
    }

    public void Save()
    {
        ResourceSaver.Save(Data, SavePath);
    }
}
