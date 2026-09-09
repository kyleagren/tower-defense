using Godot;
using System;
using System.Linq;

public partial class SaveManager : Node
{
    private const string SavePath = "user://save_data.tres";
    private const string SpellsDirectory = "res://assets/spells/";
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

        foreach (Rune rune in RuneCatalog.AllOfRarity(Rarity.Common))
        {
            changed |= EnsureKnownRune(rune);
        }

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

    private bool EnsureKnownRune(Rune rune)
    {
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
            // Save() directly, not FlushPendingSave() - the decrement above can leave _saveCountdown
            // at a tiny negative number rather than exactly 0, and FlushPendingSave's own "< 0f means
            // nothing pending" guard would then wrongly treat that as already-flushed and no-op.
            // This branch already knows a save is due (the early return above only lets non-negative
            // countdowns reach here), so it can save unconditionally.
            Save();
        }
    }

    // Catches the OS close request (window X, Alt+F4, taskbar close) as early as possible - before
    // quit() even starts tearing down the scene tree - so a pending debounced save isn't racing the
    // shutdown. _ExitTree below is the backstop for any other path that frees this node.
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            FlushPendingSave();
        }
    }

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
