using Godot;
using System;
using System.Linq;

public partial class SaveManager : Node
{
    private const string SavePath = "user://save_data.tres";
    private const string SpellsDirectory = "res://assets/spells/";
    private const string RunesDirectory = "res://assets/runes/";
    private const string ItemsDirectory = "res://assets/items/";
    private const int InventorySize = 40;
    private const float SaveDebounceSeconds = 1f;

    public static SaveManager Instance { get; private set; }
    public PlayerSaveData Data { get; private set; }

    // Negative means nothing is pending. See RequestSave.
    private float _saveCountdown = -1f;

    public override void _Ready()
    {
        Instance = this;
        Load();

        // CharacterSheetCache is derived entirely from Data.Loadouts, so the node that owns that
        // data is the one that hooks up its invalidation. Autoloads are readied in declaration
        // order (EventBus first), so Instance is available here, and neither node ever leaves the
        // tree - so there's no matching unsubscribe.
        EventBus.Instance.InventoryChanged += CharacterSheetCache.InvalidateAll;
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

        changed |= EnsureInventorySize();

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

    // Inventory is a fixed-size array so a player-chosen slot position is meaningful and stable -
    // pad it out to InventorySize with empty slots rather than growing it as items are added.
    private bool EnsureInventorySize()
    {
        bool changed = false;

        while (Data.Inventory.Count < InventorySize)
        {
            Data.Inventory.Add(null);
            changed = true;
        }

        return changed;
    }

    // One-time starter kit so the grid has something in it. Items are meant to be earned over
    // time, unlike spells/runes, so this seeds once instead of tracking each item forever.
    private bool EnsureStarterItems()
    {
        if (Data.Inventory.Any(item => item != null))
        {
            return false;
        }

        int searchFrom = 0;
        bool changed = false;

        foreach (string path in ListResourceFiles(ItemsDirectory))
        {
            Item item = GD.Load<Item>(path);

            if (item == null)
            {
                continue;
            }

            searchFrom = PlaceInNextEmptySlot(item, searchFrom);
            searchFrom = PlaceInNextEmptySlot((Item)item.Duplicate(), searchFrom);
            changed = true;
        }

        return changed;
    }

    // Returns the slot index just placed into, plus one, so the next call resumes searching from
    // there instead of rescanning from the start every time.
    private int PlaceInNextEmptySlot(Item item, int searchFrom)
    {
        for (int i = searchFrom; i < Data.Inventory.Count; i++)
        {
            if (Data.Inventory[i] == null)
            {
                Data.Inventory[i] = item;
                return i + 1;
            }
        }

        return Data.Inventory.Count;
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

    // Coalesces bursts of small changes into one write. Inventory interaction saves on every click,
    // swap, pickup and drop; serializing the whole PlayerSaveData synchronously on each of those is
    // a hitch waiting to happen once the save grows past its current few kilobytes. Callers that
    // genuinely need bytes on disk at that instant (match end, character selection) call Save().
    public void RequestSave()
    {
        _saveCountdown = SaveDebounceSeconds;
    }

    public override void _Process(double delta)
    {
        if (_saveCountdown < 0f)
        {
            return;
        }

        _saveCountdown -= (float)delta;

        if (_saveCountdown <= 0f)
        {
            FlushPendingSave();
        }
    }

    // A pending write must reach disk before the game goes away. Quitting finalizes the scene tree,
    // which frees the autoloads too, so this is the last point a pending save can still be written.
    public override void _ExitTree()
    {
        FlushPendingSave();
    }

    public void FlushPendingSave()
    {
        if (_saveCountdown < 0f)
        {
            return;
        }

        _saveCountdown = -1f;
        Save();
    }

    public void Save()
    {
        _saveCountdown = -1f;
        ResourceSaver.Save(Data, SavePath);
    }
}
