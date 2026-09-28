using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

// A profile's file: the format version it was written in, the profile, and whatever of it couldn't be
// loaded, kept raw in quarantine. Reading upgrades the whole file first, quarantine included, then puts
// each quarantined piece back before decoding - so a fix shipped later recovers them with no code of
// its own, and anything still failing is simply set aside again.
public static class SaveFile
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    public static string Write(PlayerSaveData data, IEnumerable<QuarantineEntry> quarantine)
    {
        JsonObject file = new()
        {
            ["version"] = SaveMigrations.CurrentVersion,
            ["profile"] = SaveCodec.EncodeProfile(data),
            ["quarantine"] = new JsonArray(quarantine.Select(entry => (JsonNode)entry.ToJson()).ToArray())
        };

        return file.ToJsonString(_writeOptions);
    }

    // Null for a file that can't be read at all - not JSON, or written by a newer build. quarantine
    // receives whatever is set aside afterwards, old and new.
    public static PlayerSaveData Read(string text, List<QuarantineEntry> quarantine)
    {
        JsonObject file;

        try
        {
            file = JsonNode.Parse(text) as JsonObject;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return null;
        }

        if (file == null || !SaveMigrations.TryUpgrade(file) || file["profile"] is not JsonObject profile)
        {
            return null;
        }

        if (file["quarantine"] is JsonArray setAside)
        {
            foreach (JsonNode node in setAside)
            {
                QuarantineEntry entry = QuarantineEntry.FromJson(node);

                if (!TryPutBack(profile, entry))
                {
                    quarantine.Add(entry);
                }
            }
        }

        return SaveCodec.DecodeProfile(profile, quarantine);
    }

    // An item goes to the first empty Inventory slot, wherever it came from. Anything else goes back
    // where it came from, never over something already there - except a plain field, whose saved
    // value was only ever a stand-in default.
    private static bool TryPutBack(JsonObject profile, QuarantineEntry entry)
    {
        if (entry.IsItem)
        {
            return profile[nameof(PlayerSaveData.Inventory)] is JsonArray inventory && TryFillGap(inventory, entry.Data);
        }

        if (entry.Field == null)
        {
            return false;
        }

        if (entry.Index != null)
        {
            if (profile[entry.Field] is not JsonArray list)
            {
                return false;
            }

            if (entry.Index < list.Count && list[entry.Index.Value] == null)
            {
                list[entry.Index.Value] = entry.Data;
                return true;
            }

            // Only the slot-based lists (Inventory, Tokens) keep gaps; in any other, position never
            // mattered.
            if (list.All(element => element != null))
            {
                list.Add(entry.Data);
                return true;
            }

            return false;
        }

        if (entry.Key != null)
        {
            if (profile[entry.Field] is not JsonObject map || map.ContainsKey(entry.Key))
            {
                return false;
            }

            map[entry.Key] = entry.Data;
            return true;
        }

        profile[entry.Field] = entry.Data;
        return true;
    }

    private static bool TryFillGap(JsonArray list, JsonNode data)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
            {
                list[i] = data;
                return true;
            }
        }

        return false;
    }
}
