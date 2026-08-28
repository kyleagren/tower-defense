using Godot;
using System;
using System.Linq;

public partial class SaveManager : Node
{
    private const string SavePath = "user://save_data.tres";
    private const string SpellsDirectory = "res://assets/spells/";
    private const string RunesDirectory = "res://assets/runes/";

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

        foreach (string path in ListResourceFiles(SpellsDirectory))
        {
            changed |= EnsureKnownSpell(path);
        }

        foreach (string path in ListResourceFiles(RunesDirectory))
        {
            changed |= EnsureKnownRune(path);
        }

        return changed;
    }

    private static string[] ListResourceFiles(string directoryPath)
    {
        DirAccess dir = DirAccess.Open(directoryPath);

        if (dir == null)
        {
            return Array.Empty<string>();
        }

        return dir.GetFiles()
            .Where(fileName => fileName.EndsWith(".tres"))
            .Select(fileName => directoryPath + fileName)
            .ToArray();
    }

    private bool EnsureKnownSpell(string path)
    {
        Spell spell = GD.Load<Spell>(path);

        if (spell == null || Data.KnownSpells.Any(known => known == spell))
        {
            return false;
        }

        Data.KnownSpells.Add(spell);
        return true;
    }

    private bool EnsureKnownRune(string path)
    {
        Rune rune = GD.Load<Rune>(path);

        if (rune == null || Data.KnownRunes.Any(entry => entry.Rune == rune))
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
