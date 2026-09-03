using Godot;
using System;
using System.Linq;

public partial class SaveManager : Node
{
    private const string SavePath = "user://save_data.tres";
    private const string SpellsDirectory = "res://assets/spells/";
    private const string RunesDirectory = "res://assets/runes/";
    private const string ItemsDirectory = "res://assets/items/";

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
            : null;

        if (Data == null)
        {
            GD.PushWarning("SaveManager: existing save failed to load (likely a missing referenced resource) - starting fresh.");
            Data = new PlayerSaveData();
        }

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

        changed |= EnsureStarterItems();

        return changed;
    }

    // One-time starter kit so the grid has something in it. Items are meant to be earned over
    // time, unlike spells/runes, so this seeds once instead of tracking each item forever.
    private bool EnsureStarterItems()
    {
        if (Data.OwnedItems.Count > 0)
        {
            return false;
        }

        foreach (string path in ListResourceFiles(ItemsDirectory))
        {
            Item item = GD.Load<Item>(path);

            if (item == null)
            {
                continue;
            }

            Data.OwnedItems.Add(item);
            Data.OwnedItems.Add((Item)item.Duplicate());
        }

        return Data.OwnedItems.Count > 0;
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
