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
            Data.KnownSpells.Add(GD.Load<Spell>("res://assets/FireballSpell.tres"));
            Save();
        }
    }

    public void Save()
    {
        ResourceSaver.Save(Data, SavePath);
    }
}
