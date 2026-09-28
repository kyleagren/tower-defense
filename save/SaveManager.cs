using Godot;
using System.Collections.Generic;
using System.Linq;

// This machine's save profiles - each its own JSON file under ProfilesDirectory (see SaveFile) - and the
// one being played. Data is null until a profile is selected; everything that reads it lives in the hub
// or a level, and neither loads before one is.
//
// A save is never overwritten by one that can't be read. Each write goes to a temporary file that
// then replaces the real one, so a crash mid-write leaves the last good save; and every successful
// load copies what loaded into that day's backup, keeping the newest MaxDailyBackups days played, to
// restore from if a later save won't load.
//
// Profiles from before the JSON format are Godot .tres files. Each is converted once, at startup, and
// kept as JSON only if the new file reads back identically; the .tres stays beside it either way.
public partial class SaveManager : Node
{
    public const int MaxProfiles = 20;

    private const string ProfilesDirectory = "user://profiles/";
    private const string ProfilePrefix = "profile_";
    private const string Extension = ".json";
    private const string OldExtension = ".tres";
    private const string LegacySavePath = "user://save_data.tres";
    private const string SpellsDirectory = "res://assets/spells/";
    private const int InventorySize = 40;
    private const int MaxDailyBackups = 30;
    private const float SaveDebounceSeconds = 1f;

    // The Sentinels with a starter weapon. Each profile gets one per Sentinel, once.
    private static readonly CharacterId[] _starterWeaponSentinels = { CharacterId.Soldier, CharacterId.Tank };

    public static SaveManager Instance { get; private set; }
    public PlayerSaveData Data { get; private set; }

    // Which profile Data belongs to - 0 while none is selected.
    private int _profileId;

    // What of Data's file couldn't be loaded - written back with every save, so it's never lost.
    private List<QuarantineEntry> _quarantine = new();

    // Negative means nothing is pending. See RequestSave.
    private float _saveCountdown = -1f;

    public override void _Ready()
    {
        Instance = this;
        DirAccess.MakeDirRecursiveAbsolute(ProfilesDirectory);
        ConvertOldProfiles();

        // PlayerProfile's stat sheets are derived entirely from Data.Loadouts, so the node that owns
        // that data is the one that hooks up their invalidation. Autoloads are readied in
        // declaration order (EventBus first), so Instance is available here, and neither node ever
        // leaves the tree - so there's no matching unsubscribe.
        EventBus.Instance.InventoryChanged += PlayerProfiles.InvalidateAllSheets;
    }

    private static string PathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}{Extension}";
    private static string TempPathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}.tmp{Extension}";
    private static string UnreadablePathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}.unreadable{Extension}";

    // Named by local date (yyyy-MM-dd), which sorts oldest to newest as plain text.
    private static string BackupPathFor(int id, string date) => $"{ProfilesDirectory}{ProfilePrefix}{id}.bak.{date}{Extension}";

    // A profile from before the JSON format, and where it's kept once converted.
    private static string OldPathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}{OldExtension}";
    private static string ConvertedPathFor(int id) => $"{OldPathFor(id)}.converted";

    // Newest first, in either format - backups from before the JSON format still restore.
    private static List<string> BackupsOf(int id)
    {
        string prefix = $"{ProfilePrefix}{id}.bak.";

        return DirAccess.GetFilesAt(ProfilesDirectory)
            .Where(file => file.StartsWith(prefix) && (file.EndsWith(Extension) || file.EndsWith(OldExtension)))
            .OrderByDescending(file => file[..file.LastIndexOf('.')], System.StringComparer.Ordinal)
            .ThenBy(file => file.EndsWith(Extension) ? 0 : 1)
            .Select(file => ProfilesDirectory + file)
            .ToList();
    }

    private static IEnumerable<string> AllFilesOf(int id) =>
        new[] { PathFor(id), TempPathFor(id), UnreadablePathFor(id), OldPathFor(id), ConvertedPathFor(id) }.Concat(BackupsOf(id));

    private static IEnumerable<int> ProfileIds() =>
        DirAccess.GetFilesAt(ProfilesDirectory)
            .Select(ParseProfileId)
            .Where(id => id != null)
            .Select(id => id.Value)
            .Distinct();

    // A profile's own file, in either format - not a backup, temporary or set-aside copy.
    private static int? ParseProfileId(string file)
    {
        string extension = file.EndsWith(Extension) ? Extension : file.EndsWith(OldExtension) ? OldExtension : null;

        return extension != null && file.StartsWith(ProfilePrefix)
            && int.TryParse(file[ProfilePrefix.Length..^extension.Length], out int id) ? id : null;
    }

    // Null if the file is missing or can't be read. quarantine receives what couldn't be loaded; an old
    // .tres file has no quarantine of its own and loads whole or not at all.
    private static PlayerSaveData ReadProfileFile(string path, List<QuarantineEntry> quarantine)
    {
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        if (path.EndsWith(OldExtension))
        {
            return ResourceLoader.Load(path, null, ResourceLoader.CacheMode.Ignore) as PlayerSaveData;
        }

        return SaveFile.Read(FileAccess.GetFileAsString(path), quarantine);
    }

    // False, with the reason logged, if nothing was written. A profile that can't be encoded is a bug
    // in a saved class (SaveCodec says which), never a problem with the file.
    private static bool WriteProfileFile(string path, PlayerSaveData data, IEnumerable<QuarantineEntry> quarantine)
    {
        string text;

        try
        {
            text = SaveFile.Write(data, quarantine);
        }
        catch (System.InvalidOperationException exception)
        {
            GD.PushError($"SaveManager: {exception.Message}");
            return false;
        }

        FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);

        if (file == null)
        {
            GD.PushError($"SaveManager: couldn't open {path} ({FileAccess.GetOpenError()}).");
            return false;
        }

        file.StoreString(text);
        file.Close();
        return true;
    }

    // Most recently played first.
    public List<ProfileSummary> ListProfiles()
    {
        List<ProfileSummary> summaries = new();

        foreach (int id in ProfileIds())
        {
            PlayerSaveData data = ReadProfileFile(PathFor(id), new List<QuarantineEntry>());
            bool hasBackup = BackupsOf(id).Count > 0;

            summaries.Add(data == null
                ? new ProfileSummary(id, $"{ProfilePrefix}{id}", 0, 0, IsReadable: false, hasBackup)
                : new ProfileSummary(id, data.ProfileName, data.Gold, data.LastPlayedUnix, IsReadable: true, hasBackup));
        }

        return summaries.OrderByDescending(summary => summary.LastPlayedUnix).ToList();
    }

    public int? LastPlayedProfileId =>
        ListProfiles().Where(summary => summary.IsReadable).Select(summary => (int?)summary.Id).FirstOrDefault();

    public bool CanCreateProfile => ProfileIds().Count() < MaxProfiles;

    // One past the highest id in use.
    public int CreateProfile(string name)
    {
        int id = ProfileIds().DefaultIfEmpty(0).Max() + 1;
        WriteProfileFile(PathFor(id), new PlayerSaveData { ProfileName = name }, Enumerable.Empty<QuarantineEntry>());
        return id;
    }

    // For the --profile launch flag: that numbered profile, created under that number if it's new.
    public bool SelectOrCreateProfile(int id)
    {
        if (!FileAccess.FileExists(PathFor(id)) && !FileAccess.FileExists(OldPathFor(id)))
        {
            WriteProfileFile(PathFor(id), new PlayerSaveData { ProfileName = $"Profile {id}" }, Enumerable.Empty<QuarantineEntry>());
        }

        return SelectProfile(id);
    }

    // False if the file can't be read, in which case it's left exactly as it is. Anything set aside is
    // logged here, once per load.
    public bool SelectProfile(int id)
    {
        FlushPendingSave();
        List<QuarantineEntry> quarantine = new();
        PlayerSaveData data = ReadProfileFile(PathFor(id), quarantine);

        if (data == null)
        {
            return false;
        }

        foreach (QuarantineEntry entry in quarantine)
        {
            GD.PushWarning($"SaveManager: profile {id} has {entry.Where} set aside - {entry.Reason}");
        }

        KeepBackup(id);
        Data = data;
        _quarantine = quarantine;
        _profileId = id;
        EnsureDefaultContent(Data);
        Data.LastPlayedUnix = (long)Time.GetUnixTimeFromSystem();
        Save();
        PlayerProfiles.Reset();
        return true;
    }

    // The file just loaded, exactly as it is on disk, becomes today's backup - replacing any earlier
    // one from today - and anything past the newest MaxDailyBackups is removed.
    private static void KeepBackup(int id)
    {
        DirAccess.CopyAbsolute(PathFor(id), BackupPathFor(id, Time.GetDateStringFromSystem()));

        foreach (string expired in BackupsOf(id).Skip(MaxDailyBackups))
        {
            DirAccess.RemoveAbsolute(expired);
        }
    }

    // Puts the newest backup that reads back in place of an unreadable profile, written in the current
    // format whichever format the backup is in. The unreadable file is set aside rather than replaced,
    // in case a later fix can read it.
    public bool RestoreBackup(int id)
    {
        foreach (string backup in BackupsOf(id))
        {
            List<QuarantineEntry> quarantine = new();
            PlayerSaveData data = ReadProfileFile(backup, quarantine);

            if (data == null)
            {
                continue;
            }

            MoveToTrash(UnreadablePathFor(id));

            if (FileAccess.FileExists(PathFor(id)))
            {
                DirAccess.RenameAbsolute(PathFor(id), UnreadablePathFor(id));
            }

            return WriteProfileFile(PathFor(id), data, quarantine);
        }

        return false;
    }

    // To the recycle bin rather than erased - backups and all - so a mistaken delete can still be
    // recovered by hand.
    public void DeleteProfile(int id)
    {
        if (id == _profileId)
        {
            _saveCountdown = -1f;
            Data = null;
            _quarantine = new List<QuarantineEntry>();
            _profileId = 0;
            PlayerProfiles.Reset();
        }

        foreach (string path in AllFilesOf(id))
        {
            MoveToTrash(path);
        }
    }

    private static void MoveToTrash(string path)
    {
        if (FileAccess.FileExists(path))
        {
            OS.MoveToTrash(ProjectSettings.GlobalizePath(path));
        }
    }

    // The save from before profiles becomes profile 1; every profile still in the old format becomes
    // JSON.
    private static void ConvertOldProfiles()
    {
        if (FileAccess.FileExists(LegacySavePath) && !ProfileIds().Any())
        {
            ConvertOldProfile(LegacySavePath, 1, LegacySavePath + ".bak", "Profile 1");
        }

        foreach (int id in ProfileIds().ToList())
        {
            if (!FileAccess.FileExists(PathFor(id)) && FileAccess.FileExists(OldPathFor(id)))
            {
                ConvertOldProfile(OldPathFor(id), id, ConvertedPathFor(id), null);
            }
        }
    }

    // Kept as JSON only if the new file reads back to exactly what was written. Otherwise the new file
    // is removed and the old one left where it is, so the profile shows as unreadable until a fix can
    // convert it. name, when given, replaces the profile's own.
    private static void ConvertOldProfile(string oldPath, int id, string keepOldAt, string name)
    {
        PlayerSaveData data = ReadProfileFile(oldPath, new List<QuarantineEntry>());

        if (data == null)
        {
            GD.PushWarning($"SaveManager: {oldPath} couldn't be read, so it wasn't converted.");
            return;
        }

        if (name != null)
        {
            data.ProfileName = name;
        }

        if (!WriteProfileFile(PathFor(id), data, Enumerable.Empty<QuarantineEntry>()))
        {
            return;
        }

        List<QuarantineEntry> quarantine = new();
        PlayerSaveData readBack = ReadProfileFile(PathFor(id), quarantine);

        if (readBack == null || SaveFile.Write(readBack, quarantine) != FileAccess.GetFileAsString(PathFor(id)))
        {
            GD.PushError($"SaveManager: converting {oldPath} didn't read back the same, so it's been left as it was.");
            DirAccess.RemoveAbsolute(PathFor(id));
            return;
        }

        DirAccess.RenameAbsolute(oldPath, keepOldAt);
    }

    private static void EnsureDefaultContent(PlayerSaveData data)
    {
        // A list entry that couldn't be loaded leaves a gap (SaveCodec); only the slot-based lists,
        // Inventory and Tokens, have any use for one.
        RemoveGaps(data.KnownSpells);
        RemoveGaps(data.KnownRunes);
        RemoveGaps(data.Favorites);

        EnsureInventorySize(data);
        EnsureTokenSlots(data);

        foreach (string path in ListResourceFiles(SpellsDirectory))
        {
            EnsureKnownSpell(data, path);
        }

        foreach (Rune rune in RuneCatalog.AllOfRarity(Rarity.Common))
        {
            EnsureKnownRune(data, rune);
        }

        EnsureStarterWeapons(data);
    }

    private static void RemoveGaps<[MustBeVariant] T>(Godot.Collections.Array<T> list) where T : class
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
            }
        }
    }

    // Inventory is a fixed-size array so a player-chosen slot position is meaningful and stable -
    // pad it out to InventorySize with empty slots rather than growing it as items are added.
    private static void EnsureInventorySize(PlayerSaveData data)
    {
        while (data.Inventory.Count < InventorySize)
        {
            data.Inventory.Add(null);
        }
    }

    // Fixed-size like Inventory, for the same reason - "slot 2" is a meaningful, stable position
    // (the token bar UI), not just list membership.
    private static void EnsureTokenSlots(PlayerSaveData data)
    {
        while (data.Tokens.Count < GameRules.MaxTokens)
        {
            data.Tokens.Add(null);
        }
    }

    // ResourceLoader.ListDirectory rather than DirAccess - see RuneCatalog.ScanDirectory.
    private static string[] ListResourceFiles(string directoryPath)
    {
        return ResourceLoader.ListDirectory(directoryPath)
            .Where(entry => entry.EndsWith(".tres"))
            .Select(entry => directoryPath + entry)
            .ToArray();
    }

    private static void EnsureKnownSpell(PlayerSaveData data, string path)
    {
        Spell spell = GD.Load<Spell>(path);

        if (spell != null && !data.KnownSpells.Any(known => known == spell))
        {
            data.KnownSpells.Add(spell);
        }
    }

    private static void EnsureKnownRune(PlayerSaveData data, Rune rune)
    {
        if (rune != null && !data.KnownRunes.Any(entry => entry.Rune == rune))
        {
            data.KnownRunes.Add(new RuneInventoryEntry { Rune = rune, Unlimited = true });
        }
    }

    // Straight into the Sentinel's weapon slot when it's empty, otherwise into Inventory. With
    // Inventory full too it isn't marked given, so the next load tries again.
    private static void EnsureStarterWeapons(PlayerSaveData data)
    {
        foreach (CharacterId character in _starterWeaponSentinels)
        {
            if (data.StarterWeaponsGiven.Contains(character))
            {
                continue;
            }

            Weapon weapon = LootGenerator.GenerateStarterWeapon(character);

            if (!data.GetOrCreateLoadout(character).TryEquip(weapon))
            {
                int emptySlot = data.Inventory.IndexOf(null);

                if (emptySlot < 0)
                {
                    continue;
                }

                data.Inventory[emptySlot] = weapon;
            }

            data.StarterWeaponsGiven.Add(character);
        }
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

        if (Data == null)
        {
            return;
        }

        if (!WriteProfileFile(TempPathFor(_profileId), Data, _quarantine)
            || DirAccess.RenameAbsolute(TempPathFor(_profileId), PathFor(_profileId)) != Error.Ok)
        {
            GD.PushError($"SaveManager: couldn't write profile {_profileId}.");
        }
    }
}
