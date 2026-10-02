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

    // A new, empty stash tab for GameRules.StashTabGoldCost. Saved at once, like any gold trade.
    public bool TryBuyStashTab()
    {
        if (Data.Gold < GameRules.StashTabGoldCost)
        {
            return false;
        }

        Data.Gold -= GameRules.StashTabGoldCost;
        Data.Stash.Add(StashTab.CreateEmpty($"Tab {Data.Stash.Count + 1}"));
        CommitChange(saveNow: true);
        return true;
    }

    public void RenameStashTab(int index, string name)
    {
        Data.Stash[index].Name = name;
        CommitChange();
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
    // Returns the name it was actually saved under.
    public string AddFavorite(string name, Spell spell, IEnumerable<Rune> runes)
    {
        string uniqueName = UniqueFavoriteName(name);

        Data.Favorites.Add(new FavoriteLoadout
        {
            Name = uniqueName,
            Spell = spell,
            Runes = new Godot.Collections.Array<Rune>(runes)
        });

        Data.AutoPinFavorite(uniqueName);
        Save();
        return uniqueName;
    }

    // The favorite already holding exactly this build, if any: same spell, same runes at the same
    // tiers, in any sockets, since socket order doesn't change what a ritual does.
    public FavoriteLoadout FindFavorite(Spell spell, IEnumerable<Rune> runes)
    {
        List<Rune> wanted = runes.Where(rune => rune != null).ToList();
        return Data.Favorites.FirstOrDefault(favorite => favorite.Spell == spell && HoldsExactly(favorite.Runes, wanted));
    }

    private static bool HoldsExactly(IEnumerable<Rune> saved, List<Rune> wanted)
    {
        List<Rune> remaining = new List<Rune>(wanted);

        foreach (Rune rune in saved)
        {
            if (rune != null && !remaining.Remove(rune))
            {
                return false;
            }
        }

        return remaining.Count == 0;
    }

    public FavoriteLoadout FavoriteNamed(string name) =>
        string.IsNullOrEmpty(name) ? null : Data.Favorites.FirstOrDefault(favorite => favorite.Name == name);

    // An element's known spells in ring order: the ones the player arranged, as arranged, then the
    // rest in authored order - so a newly learned spell lands at the end without moving the others.
    public List<Spell> SpellRing(SpellWheelGroup group) => Data.KnownSpells
        .Where(spell => SpellWheelGroups.Of(spell) == group)
        .OrderBy(spell => Data.SpellOrder.IndexOf(spell) is int arranged and >= 0 ? arranged : int.MaxValue)
        .ThenBy(spell => spell.WheelOrder)
        .ThenBy(spell => spell.Name)
        .ToList();

    public void SetSpellRing(SpellWheelGroup group, IEnumerable<Spell> spells)
    {
        Data.SpellOrder = new Godot.Collections.Array<Spell>(Data.SpellOrder
            .Where(spell => spell != null && SpellWheelGroups.Of(spell) != group)
            .Concat(spells));
        Save();
    }

    // One per rune known, at the highest tier owned.
    public IEnumerable<Rune> HighestOwnedTiers => Data.KnownRunes
        .Select(entry => entry.Rune)
        .GroupBy(rune => rune.GetType())
        .Select(group => group.OrderByDescending(rune => rune.Rarity).First());

    // The ones the player arranged, as arranged, then the rest by name.
    public IEnumerable<Rune> InRuneOrder(IEnumerable<Rune> runes) => runes
        .OrderBy(RuneOrderIndex)
        .ThenBy(rune => rune.Name);

    private int RuneOrderIndex(Rune rune)
    {
        for (int i = 0; i < Data.RuneOrder.Count; i++)
        {
            if (Data.RuneOrder[i]?.GetType() == rune.GetType())
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    public void SetRuneOrder(IEnumerable<Rune> runes)
    {
        Data.RuneOrder = new Godot.Collections.Array<Rune>(runes);
        Save();
    }

    // A spell's favorites in the order its favorites ring shows them.
    public List<FavoriteLoadout> FavoritesOf(Spell spell) =>
        Data.Favorites.Where(favorite => favorite.Spell == spell).ToList();

    // Reorders one spell's favorites among the places they already hold in the full list, leaving
    // every other spell's favorites where they are.
    public void SetFavoriteOrder(Spell spell, IReadOnlyList<FavoriteLoadout> ordered)
    {
        int next = 0;

        for (int i = 0; i < Data.Favorites.Count; i++)
        {
            if (Data.Favorites[i].Spell == spell)
            {
                Data.Favorites[i] = ordered[next++];
            }
        }

        Save();
    }

    // False for an empty name or one another favorite already has. The pinned wheel and any token
    // named after the favorite follow it to the new name.
    public bool TryRenameFavorite(FavoriteLoadout favorite, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || (name != favorite.Name && FavoriteNamed(name) != null))
        {
            return false;
        }

        string oldName = favorite.Name;
        favorite.Name = name;
        RenameReferences(oldName, name);
        Save();
        return true;
    }

    // Its pin slot empties, and a token named after it falls back to its spell's name.
    public void DeleteFavorite(FavoriteLoadout favorite)
    {
        Data.Favorites.Remove(favorite);
        RenameReferences(favorite.Name, "");
        Save();
    }

    private void RenameReferences(string from, string to)
    {
        for (int slot = 0; slot < Data.PinnedFavorites.Count; slot++)
        {
            if (Data.PinnedFavorites[slot] == from)
            {
                Data.PinnedFavorites[slot] = to;
            }
        }

        bool tokensChanged = false;

        foreach (RuneLoadout token in Data.Tokens)
        {
            if (token != null && token.Name == from)
            {
                token.Name = to;
                tokensChanged = true;
            }
        }

        if (tokensChanged)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.TokensChanged);
        }
    }

    // Into a slot, replacing whatever's there; a favorite already pinned elsewhere moves. Deciding
    // what a slot holds takes charge of the wheel, so new favorites stop landing on it.
    public void PinFavorite(FavoriteLoadout favorite, int slot)
    {
        int current = Data.PinnedFavorites.IndexOf(favorite.Name);

        if (current == slot)
        {
            return;
        }

        if (current >= 0)
        {
            Data.PinnedFavorites[current] = "";
        }

        Data.PinnedFavorites[slot] = favorite.Name;
        Data.PinnedWheelCustomized = true;
        Save();
    }

    public void UnpinFavorite(FavoriteLoadout favorite)
    {
        int slot = Data.PinnedFavorites.IndexOf(favorite.Name);

        if (slot < 0)
        {
            return;
        }

        Data.PinnedFavorites[slot] = "";
        Data.PinnedWheelCustomized = true;
        Save();
    }

    // Rearranging what's already pinned leaves auto-fill running.
    public void SwapPins(int a, int b)
    {
        (Data.PinnedFavorites[a], Data.PinnedFavorites[b]) = (Data.PinnedFavorites[b], Data.PinnedFavorites[a]);
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
            candidate = $"{name} ({suffix})";
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
