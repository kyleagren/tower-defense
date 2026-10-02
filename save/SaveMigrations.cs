using System;
using System.Linq;
using System.Text.Json.Nodes;

// Upgrades an older save file one format version at a time. Each step takes the whole file - profile
// and quarantine alike - from the version before it to its own. A version is only a label; the order
// is this list's. Pre-release versions count up from FirstVersion and are cleared at 1.0, when saves
// from before release are thrown away.
public static class SaveMigrations
{
    private const string FirstVersion = "0.1";

    // e.g. ("0.3", file => ForEachObject(file, obj => { ...rename a key or value... }))
    private static readonly (string Version, Action<JsonObject> Upgrade)[] _steps =
    {
        // Inventory and stash tabs became item grids; their old slot lists are emptied, not repacked.
        ("0.2", file =>
        {
            if (file["profile"] is not JsonObject profile)
            {
                return;
            }

            profile.Remove(nameof(PlayerSaveData.Inventory));

            if (profile[nameof(PlayerSaveData.Stash)] is JsonArray tabs)
            {
                foreach (JsonNode tab in tabs)
                {
                    (tab as JsonObject)?.Remove(nameof(StashTab.Items));
                }
            }
        })
    };

    public static string CurrentVersion => _steps.Length == 0 ? FirstVersion : _steps[^1].Version;

    // False for a version this build doesn't know - most likely a save from a newer build, which is
    // left alone rather than guessed at.
    public static bool TryUpgrade(JsonObject file)
    {
        if (file["version"] is not JsonValue value || !value.TryGetValue(out string version))
        {
            return false;
        }

        int start = 0;

        if (version != FirstVersion)
        {
            int index = Array.FindIndex(_steps, step => step.Version == version);

            if (index < 0)
            {
                return false;
            }

            start = index + 1;
        }

        for (int i = start; i < _steps.Length; i++)
        {
            _steps[i].Upgrade(file);
        }

        file["version"] = CurrentVersion;
        return true;
    }

    // Every object in the file however deeply nested, for a step that renames something wherever it
    // appears. Children are visited after their parent, from a snapshot, so a visit may change them.
    public static void ForEachObject(JsonNode node, Action<JsonObject> visit)
    {
        switch (node)
        {
            case JsonObject obj:
                visit(obj);

                foreach (JsonNode child in obj.Select(pair => pair.Value).ToList())
                {
                    ForEachObject(child, visit);
                }

                break;

            case JsonArray array:
                foreach (JsonNode child in array.ToList())
                {
                    ForEachObject(child, visit);
                }

                break;
        }
    }
}
