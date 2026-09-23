using System.Collections.Generic;

// One player's persistent character data - loadouts, tokens, which Sentinel they're playing - and
// the stat sheets derived from it. The local player's is backed by the save file; another player's
// is the copy of their save they bring to the match. Gameplay resolves gear through the owning
// player's profile, never through SaveManager, so two players on the same Sentinel still resolve
// independently.
public class PlayerProfile
{
    private readonly Dictionary<CharacterId, CharacterSheet> _sheets = new();

    public PlayerProfile(PlayerId id, PlayerSaveData data)
    {
        Id = id;
        Data = data;
    }

    public PlayerId Id { get; }
    public PlayerSaveData Data { get; }
    public bool IsLocal => Id == PlayerProfiles.LocalId;

    public CharacterId SelectedCharacter => Data.LastPlayedCharacter;
    public CharacterSheet SelectedSheet => GetSheet(SelectedCharacter);

    public void SelectCharacter(CharacterId character)
    {
        Data.LastPlayedCharacter = character;
        Save();
    }

    // Only the local profile has a file behind it; a remote player's copy is theirs to persist.
    public void Save()
    {
        if (IsLocal)
        {
            SaveManager.Instance.Save();
        }
    }

    public EquippedLoadout GetLoadout(CharacterId character) => Data.GetOrCreateLoadout(character);

    public CharacterSheet GetSheet(CharacterId character)
    {
        if (!_sheets.TryGetValue(character, out CharacterSheet sheet))
        {
            sheet = Resolve(character);
            _sheets[character] = sheet;
        }

        return sheet;
    }

    public void InvalidateSheets()
    {
        _sheets.Clear();
    }

    private CharacterSheet Resolve(CharacterId character)
    {
        Dictionary<StatId, float> bonuses = new();
        Dictionary<StatId, float> localTotals = new();

        if (!Data.Loadouts.TryGetValue(character, out EquippedLoadout loadout))
        {
            return new CharacterSheet(bonuses, localTotals);
        }

        foreach (Item item in loadout.EquippedItems())
        {
            foreach (RolledAffix roll in item.Affixes)
            {
                // Armor/Shield (and the Vanguard hybrid roll) resolve locally per-item
                // (Armor.ResolvedValue) instead of stacking into the shared global pool every
                // other stat uses.
                if (roll.Stat == StatId.SentinelArmor || roll.Stat == StatId.SentinelShield
                    || roll.Stat == StatId.SentinelArmorAndShield
                    || roll.Stat == StatId.SentinelBaseCritChance || roll.Stat == StatId.SentinelMoreCritDamageBonus
                    || roll.Stat == StatId.SentinelIncreasedAttackSpeed)
                {
                    continue;
                }

                bonuses.TryGetValue(roll.Stat, out float current);
                bonuses[roll.Stat] = current + roll.Value;
            }

            if (item is Armor armor)
            {
                localTotals.TryGetValue(StatId.SentinelArmor, out float armorTotal);
                localTotals[StatId.SentinelArmor] = armorTotal + armor.ResolvedValue(StatId.SentinelArmor);

                localTotals.TryGetValue(StatId.SentinelShield, out float shieldTotal);
                localTotals[StatId.SentinelShield] = shieldTotal + armor.ResolvedValue(StatId.SentinelShield);
            }
        }

        return new CharacterSheet(bonuses, localTotals);
    }
}
