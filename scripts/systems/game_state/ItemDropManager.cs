using Godot;

// Rolls a chance to drop items wherever an enemy dies. Mirrors EconomyManager's shape: a plain
// Node subscribed to EventBus.EnemyKilled, doing its own thing independent of the reward payout.
// How many rolls happen and how likely each one is comes from the enemy itself (LootRolls/
// DropChance on Enemy.cs), the same way ManaReward/GoldReward already vary per enemy type - a
// bigger enemy is just authored with a higher LootRolls, no changes needed here.
public partial class ItemDropManager : Node
{
    [Export] public PackedScene WorldItemScene { get; private set; }

    // Ring drops are slot-agnostic - LootGenerator.CreateItemForSlot maps both LeftRing and
    // RightRing to the same Ring class, so only one appears here to avoid double-weighting rings
    // against every other item type.
    private static readonly EquipmentSlot[] _droppableSlots =
    {
        EquipmentSlot.Weapon,
        EquipmentSlot.Helmet,
        EquipmentSlot.Chest,
        EquipmentSlot.Legs,
        EquipmentSlot.Boots,
        EquipmentSlot.Gloves,
        EquipmentSlot.Belt,
        EquipmentSlot.Amulet,
        EquipmentSlot.LeftRing,
        EquipmentSlot.Relic
    };

    private static readonly CharacterId[] _sentinels =
        { CharacterId.Soldier, CharacterId.Tank, CharacterId.Assassin, CharacterId.Templar };

    // Common is far likelier than Legendary. Rarity.Unique is never rolled here, matching
    // LootGenerator's own rule that uniques are hand-authored, not generated.
    private static readonly (Rarity Rarity, int Weight)[] _rarityWeights =
    {
        (Rarity.Common, 100),
        (Rarity.Uncommon, 45),
        (Rarity.Rare, 18),
        (Rarity.Epic, 6),
        (Rarity.Legendary, 1)
    };

    private static readonly System.Random _random = new();

    public override void _Ready()
    {
        EventBus.Instance.EnemyKilled += OnEnemyKilled;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.EnemyKilled -= OnEnemyKilled;
    }

    // PoE-style: quantity (LootRolls) increases the number of independent drop-chance rolls per
    // kill, not the size of a single roll - each roll either drops one item or doesn't, so a kill
    // can end up with anywhere from 0 to LootRolls items.
    private void OnEnemyKilled(int reward, int goldReward, Vector3 position, float dropChance, int lootRolls)
    {
        for (int i = 0; i < lootRolls; i++)
        {
            if (_random.NextDouble() < dropChance)
            {
                SpawnDrop(position);
            }
        }
    }

    private void SpawnDrop(Vector3 position)
    {
        EquipmentSlot slot = _droppableSlots[_random.Next(_droppableSlots.Length)];
        Rarity rarity = RollRarity();
        CharacterId equippableCharacter = slot == EquipmentSlot.Weapon
            ? _sentinels[_random.Next(_sentinels.Length)]
            : CharacterId.AnySentinel;

        Item item = LootGenerator.Generate(slot, rarity, equippableCharacter);

        WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();
        worldItem.Initialize(item);
        GetTree().CurrentScene.AddChild(worldItem);
        worldItem.GlobalPosition = position + RandomGroundOffset();
    }

    // Nudges each drop a bit so multiple items from one kill don't land exactly on top of each
    // other and become impossible to pick out individually.
    private Vector3 RandomGroundOffset()
    {
        float x = (float)(_random.NextDouble() * 0.6 - 0.3);
        float z = (float)(_random.NextDouble() * 0.6 - 0.3);
        return new Vector3(x, 0f, z);
    }

    private static Rarity RollRarity()
    {
        int totalWeight = 0;
        foreach ((Rarity _, int weight) in _rarityWeights)
        {
            totalWeight += weight;
        }

        int roll = _random.Next(totalWeight);
        int cumulative = 0;

        foreach ((Rarity rarity, int weight) in _rarityWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return rarity;
            }
        }

        return Rarity.Common;
    }
}
