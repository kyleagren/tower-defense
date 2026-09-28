using Godot;
using System.Collections.Generic;
using System.Linq;

// This machine's save profiles - each its own file under ProfilesDirectory - and the one being played.
// Data is null until a profile is selected; everything that reads it lives in the hub or a level,
// and neither loads before one is.
//
// A save is never overwritten by one that can't be read. Each write goes to a temporary file that
// then replaces the real one, so a crash mid-write leaves the last good save; and every successful
// load copies what loaded into that day's backup, keeping the newest MaxDailyBackups days played, to
// restore from if a later save won't load.
public partial class SaveManager : Node
{
    public const int MaxProfiles = 20;

    private const string ProfilesDirectory = "user://profiles/";
    private const string ProfilePrefix = "profile_";
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

    // Negative means nothing is pending. See RequestSave.
    private float _saveCountdown = -1f;

    public override void _Ready()
    {
        Instance = this;
        DirAccess.MakeDirRecursiveAbsolute(ProfilesDirectory);
        MigrateLegacySave();

        // PlayerProfile's stat sheets are derived entirely from Data.Loadouts, so the node that owns
        // that data is the one that hooks up their invalidation. Autoloads are readied in
        // declaration order (EventBus first), so Instance is available here, and neither node ever
        // leaves the tree - so there's no matching unsubscribe.
        EventBus.Instance.InventoryChanged += PlayerProfiles.InvalidateAllSheets;
    }

    private static string PathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}.tres";

    // Each keeps ".tres" last so ResourceSaver still writes the text format; the extra suffix keeps
    // them out of ProfileIds.
    private static string TempPathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}.tmp.tres";
    private static string UnreadablePathFor(int id) => $"{ProfilesDirectory}{ProfilePrefix}{id}.unreadable.tres";

    // Named by local date (yyyy-MM-dd), which sorts oldest to newest as plain text.
    private static string BackupPathFor(int id, string date) => $"{ProfilesDirectory}{ProfilePrefix}{id}.bak.{date}.tres";

    // Newest first.
    private static List<string> BackupsOf(int id)
    {
        string prefix = $"{ProfilePrefix}{id}.bak.";

        return DirAccess.GetFilesAt(ProfilesDirectory)
            .Where(file => file.StartsWith(prefix) && file.EndsWith(".tres"))
            .OrderByDescending(file => file, System.StringComparer.Ordinal)
            .Select(file => ProfilesDirectory + file)
            .ToList();
    }

    private static IEnumerable<string> AllFilesOf(int id) =>
        new[] { PathFor(id), TempPathFor(id), UnreadablePathFor(id) }.Concat(BackupsOf(id));

    private static IEnumerable<int> ProfileIds()
    {
        foreach (string file in DirAccess.GetFilesAt(ProfilesDirectory))
        {
            if (file.StartsWith(ProfilePrefix) && file.EndsWith(".tres")
                && int.TryParse(file[ProfilePrefix.Length..^".tres".Length], out int id))
            {
                yield return id;
            }
        }
    }

    private static PlayerSaveData LoadProfileFile(string path, ResourceLoader.CacheMode cacheMode) =>
        FileAccess.FileExists(path) ? ResourceLoader.Load(path, null, cacheMode) as PlayerSaveData : null;

    // Most recently played first. Read around the resource cache, since none of these are kept.
    public List<ProfileSummary> ListProfiles()
    {
        List<ProfileSummary> summaries = new();

        foreach (int id in ProfileIds())
        {
            PlayerSaveData data = LoadProfileFile(PathFor(id), ResourceLoader.CacheMode.Ignore);
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
        WriteNewProfile(id, name);
        return id;
    }

    // For the --profile launch flag: that numbered profile, created under that number if it's new.
    public bool SelectOrCreateProfile(int id)
    {
        if (!FileAccess.FileExists(PathFor(id)))
        {
            WriteNewProfile(id, $"Profile {id}");
        }

        return SelectProfile(id);
    }

    // Default content arrives on first select, the same as for any profile.
    private static void WriteNewProfile(int id, string name)
    {
        ResourceSaver.Save(new PlayerSaveData { ProfileName = name }, PathFor(id));
    }

    // False if the file can't be read, in which case it's left exactly as it is. Replace rather than
    // the cache's usual reuse: a profile deleted and recreated under the same id would otherwise load
    // as the deleted one.
    public bool SelectProfile(int id)
    {
        FlushPendingSave();
        PlayerSaveData data = LoadProfileFile(PathFor(id), ResourceLoader.CacheMode.Replace);

        if (data == null)
        {
            return false;
        }

        KeepBackup(id);
        Data = data;
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

    // Puts the newest backup that reads back in place of an unreadable profile. The unreadable file is
    // set aside rather than replaced, in case a later fix can read it.
    public bool RestoreBackup(int id)
    {
        foreach (string backup in BackupsOf(id))
        {
            if (LoadProfileFile(backup, ResourceLoader.CacheMode.Ignore) == null)
            {
                continue;
            }

            MoveToTrash(UnreadablePathFor(id));
            DirAccess.RenameAbsolute(PathFor(id), UnreadablePathFor(id));
            DirAccess.CopyAbsolute(backup, PathFor(id));
            return true;
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

    // A save from before profiles becomes the first profile, and the old file is kept beside it,
    // renamed. One that won't load is left where it is.
    private static void MigrateLegacySave()
    {
        if (!FileAccess.FileExists(LegacySavePath) || ProfileIds().Any())
        {
            return;
        }

        PlayerSaveData data = LoadProfileFile(LegacySavePath, ResourceLoader.CacheMode.Ignore);

        if (data == null)
        {
            GD.PushWarning($"SaveManager: {LegacySavePath} couldn't be read, so it wasn't turned into a profile.");
            return;
        }

        data.ProfileName = "Profile 1";
        ResourceSaver.Save(data, PathFor(1));
        DirAccess.RenameAbsolute(LegacySavePath, LegacySavePath + ".bak");
    }

    private static void EnsureDefaultContent(PlayerSaveData data)
    {
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

        if (ResourceSaver.Save(Data, TempPathFor(_profileId)) != Error.Ok
            || DirAccess.RenameAbsolute(TempPathFor(_profileId), PathFor(_profileId)) != Error.Ok)
        {
            GD.PushError($"SaveManager: couldn't write profile {_profileId}.");
        }
    }
}
