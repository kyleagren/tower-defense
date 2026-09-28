// What the profile picker shows for one save profile, read without switching to it. An unreadable
// profile has no name or gold to show, and may have a backup to restore instead.
public readonly record struct ProfileSummary(int Id, string Name, int Gold, long LastPlayedUnix, bool IsReadable, bool HasBackup);
