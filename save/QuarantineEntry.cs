using System.Text.Json.Nodes;

// A piece of a save that couldn't be loaded, kept exactly as it was written so nothing in a save is
// ever silently dropped. SaveFile tries to put it back on every load.
public class QuarantineEntry
{
    // Where it was, for the log - e.g. "Inventory[12]", "Loadouts.Tank.Weapon".
    public string Where { get; init; }
    public string Reason { get; init; }
    public JsonNode Data { get; init; }

    // An item goes back into the first empty Inventory slot, wherever it came from.
    public bool IsItem { get; init; }

    // Anything else goes back where it came from: the PlayerSaveData property, and its list position
    // or dictionary key within it.
    public string Field { get; init; }
    public int? Index { get; init; }
    public string Key { get; init; }

    public JsonObject ToJson() => new()
    {
        ["where"] = Where,
        ["reason"] = Reason,
        ["item"] = IsItem,
        ["field"] = Field,
        ["index"] = Index,
        ["key"] = Key,
        ["data"] = Data?.DeepClone()
    };

    public static QuarantineEntry FromJson(JsonNode node) => new()
    {
        Where = (string)node["where"],
        Reason = (string)node["reason"],
        IsItem = (bool?)node["item"] ?? false,
        Field = (string)node["field"],
        Index = (int?)node["index"],
        Key = (string)node["key"],
        Data = node["data"]?.DeepClone()
    };
}
