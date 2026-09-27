using Godot;
using System.Collections.Generic;

// Each Sentinel's SentinelDefinition, by CharacterId. A lookup rather than an export on each
// Sentinel scene, since the character sheet needs one with no Sentinel in the level to ask.
public static class SentinelCatalog
{
    private static readonly Dictionary<CharacterId, string> _definitionPaths = new()
    {
        [CharacterId.Soldier] = "res://assets/sentinels/soldier_definition.tres",
        [CharacterId.Tank] = "res://assets/sentinels/tank_definition.tres"
    };

    // Read every frame (regen reads MaxHealth), so each is loaded once and kept.
    private static readonly Dictionary<CharacterId, SentinelDefinition> _loaded = new();

    public static SentinelDefinition Definition(CharacterId character)
    {
        if (!_loaded.TryGetValue(character, out SentinelDefinition definition))
        {
            definition = GD.Load<SentinelDefinition>(_definitionPaths[character]);
            _loaded[character] = definition;
        }

        return definition;
    }

    // Slot names come from a Sentinel's ability nodes, so each Sentinel records its own when it's
    // ready. Lets item text name the right abilities while the Architect has taken over and no
    // Sentinel is in the level.
    private static readonly Dictionary<CharacterId, string[]> _slotNames = new();

    public static void RecordSlotNames(CharacterId character, string[] names)
    {
        _slotNames[character] = names;
    }

    // Null until a Sentinel of that kind has existed this session.
    public static string SlotName(CharacterId character, int slot) =>
        _slotNames.TryGetValue(character, out string[] names) && slot < names.Length ? names[slot] : null;
}
