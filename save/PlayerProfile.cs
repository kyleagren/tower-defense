using System.Collections.Generic;
using System.Linq;

// One player's persistent character data - loadouts, tokens, which Sentinel they're playing - the
// stat sheets derived from it, and the rules for changing it. The local player's is backed by the
// save file; another player's is the copy of their save they bring to the match. Gameplay resolves
// gear through the owning player's profile, never through SaveManager, so two players on the same
// Sentinel still resolve independently; and every write goes through here, so saving and notifying
// happen one way.
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
        CommitChange(saveNow: true);
    }

    // Only the local profile has a file behind it; a remote player's copy is theirs to persist.
    public void Save()
    {
        if (IsLocal)
        {
            SaveManager.Instance.Save();
        }
    }

    // Every change to this player's items, loadouts, runes or gold ends here: persist it, and tell the
    // rest of the game - InventoryChanged is what drops every stat sheet and every Ritual's cached
    // context. Debounced by default for routine shuffling; saveNow for a change worth protecting from
    // a crash the instant it happens, like a gold trade.
    public void CommitChange(bool saveNow = false)
    {
        if (IsLocal)
        {
            if (saveNow)
            {
                SaveManager.Instance.Save();
            }
            else
            {
                SaveManager.Instance.RequestSave();
            }
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
    }

    // First empty slot, lowest index first. False when Inventory is full - the caller decides what
    // happens to the item then (leave it in the world, drop it at the player's feet).
    public bool TryAddToInventory(Item item)
    {
        int emptySlotIndex = Data.Inventory.IndexOf(null);

        if (emptySlotIndex < 0)
        {
            return false;
        }

        Data.Inventory[emptySlotIndex] = item;
        CommitChange();
        return true;
    }

    // A world pickup: straight into its equipment slot when the current Sentinel can use it and that
    // slot is empty, otherwise into Inventory. Never replaces something already equipped.
    public bool PickUp(Item item)
    {
        if (item.IsCompatibleWith(SelectedCharacter) && GetLoadout(SelectedCharacter).TryEquip(item))
        {
            CommitChange();
            return true;
        }

        return TryAddToInventory(item);
    }

    // Whether PickUp would find item a place.
    public bool CanPickUp(Item item) =>
        (item.IsCompatibleWith(SelectedCharacter) && GetLoadout(SelectedCharacter).HasOpenSlotFor(item))
        || Data.Inventory.Contains(null);

    public bool KnowsRune(Rune rune) => Data.KnownRunes.Any(entry => entry.Rune == rune);

    // Learning a tier also learns every lower tier of the same rune not yet known, so owned tiers are
    // always a contiguous range from Common up. False when the tome teaches nothing new - it stays put.
    public bool LearnRuneTome(int inventoryIndex)
    {
        if (Data.Inventory[inventoryIndex] is not RuneTome tome || tome.TaughtRune == null || KnowsRune(tome.TaughtRune))
        {
            return false;
        }

        LearnRune(tome.TaughtRune);

        foreach (Rune lowerTier in RuneCatalog.GetRunesOfType(tome.TaughtRune).Where(rune => rune.Rarity < tome.TaughtRune.Rarity))
        {
            LearnRune(lowerTier);
        }

        Data.Inventory[inventoryIndex] = null;
        CommitChange();
        return true;
    }

    private void LearnRune(Rune rune)
    {
        if (!KnowsRune(rune))
        {
            Data.KnownRunes.Add(new RuneInventoryEntry { Rune = rune, Unlimited = true });
        }
    }

    public bool TrySpendGold(int amount)
    {
        if (Data.Gold < amount)
        {
            return false;
        }

        Data.Gold -= amount;
        CommitChange(saveNow: true);
        return true;
    }

    public void AddGold(int amount)
    {
        Data.Gold += amount;
        CommitChange(saveNow: true);
    }

    // Saved immediately rather than debounced: an item on the table isn't in Inventory, so losing the
    // write to a crash would lose the item outright.
    public void SetCraftingSlotItem(Item item)
    {
        Data.CraftingSlotItem = item;
        CommitChange(saveNow: true);
    }

    public void SetToken(int index, RuneLoadout token)
    {
        Data.Tokens[index] = token;
        Save();
        EventBus.Instance.EmitSignal(EventBus.SignalName.TokensChanged);
    }

    // A name already in use gets a numeric suffix rather than being refused or overwriting the old one.
    public void AddFavorite(string name, Spell spell, IEnumerable<Rune> runes)
    {
        Data.Favorites.Add(new FavoriteLoadout
        {
            Name = UniqueFavoriteName(name),
            Spell = spell,
            Runes = new Godot.Collections.Array<Rune>(runes)
        });

        Save();
    }

    private string UniqueFavoriteName(string name)
    {
        if (Data.Favorites.All(favorite => favorite.Name != name))
        {
            return name;
        }

        int suffix = 1;
        string candidate;

        do
        {
            candidate = $"{name}({suffix})";
            suffix++;
        } while (Data.Favorites.Any(favorite => favorite.Name == candidate));

        return candidate;
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

    // A remote player's sheets aren't derived here - they're what that player last sent.
    public void InvalidateSheets()
    {
        if (IsLocal)
        {
            _sheets.Clear();
        }
    }

    // Another player's gear as they last shared it.
    public void ReceiveSheet(CharacterId character, CharacterSheet sheet)
    {
        Data.LastPlayedCharacter = character;
        _sheets[character] = sheet;
        CommitChange();
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
