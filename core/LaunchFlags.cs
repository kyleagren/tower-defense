using Godot;
using System.Collections.Generic;
using System.Linq;

// Development launch flags - any of them boots straight into the hub, skipping the main menu:
// --host, --join [address] (localhost when none follows), --profile <number>. Read from both argument
// lists, so a flag works whether or not it was passed after a "--" separator.
public static class LaunchFlags
{
    private const string DefaultJoinAddress = "127.0.0.1";

    private static readonly List<string> _args = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToList();

    public static bool Host => _args.Contains("--host");

    // Null without --join.
    public static string JoinAddress
    {
        get
        {
            int index = _args.IndexOf("--join");

            if (index < 0)
            {
                return null;
            }

            bool hasAddress = index + 1 < _args.Count && !_args[index + 1].StartsWith("--");
            return hasAddress ? _args[index + 1] : DefaultJoinAddress;
        }
    }

    // Null without --profile, or without a number after it.
    public static int? Profile
    {
        get
        {
            int index = _args.IndexOf("--profile");
            return index >= 0 && index + 1 < _args.Count && int.TryParse(_args[index + 1], out int id) ? id : null;
        }
    }

    public static bool SkipsMenu => Host || JoinAddress != null || Profile != null;
}
