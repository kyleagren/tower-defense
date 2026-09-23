using Godot;

// The physical, world-space representation of a dropped Item - a small sphere for now (see
// ItemInteractor for the "look at it, press E" pickup flow). Not simulated by physics; it's placed
// once at a fixed position and just sits there to be raycast against.
public partial class WorldItem : StaticBody3D
{
    [Export] public MeshInstance3D VisualMesh { get; private set; }

    public Item Item { get; private set; }

    private static readonly Color _currencyGoldColor = new Color(0.83f, 0.69f, 0.22f);

    private LevelContext _level;

    public override void _Ready()
    {
        _level = LevelContext.Current;
        _level.WorldItems.Register(this);
    }

    public override void _ExitTree()
    {
        _level.WorldItems.Unregister(this);
    }

    // Assigns a fresh material rather than editing VisualMesh's shared scene material - every
    // WorldItem instance comes from the same packed scene, so mutating a shared sub-resource here
    // would recolor every other drop currently in the world too.
    public void Initialize(Item item)
    {
        Item = item;

        VisualMesh.MaterialOverride = item is Currency
            ? new StandardMaterial3D { AlbedoColor = _currencyGoldColor, Metallic = 1f, Roughness = 0.3f }
            : BuildRarityMaterial(item.Rarity);
    }

    private static StandardMaterial3D BuildRarityMaterial(Rarity rarity)
    {
        Color color = RarityColors.Get(rarity);
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 1.2f
        };
    }

    // Adds the item to the shared Inventory's first empty slot and removes itself from the world.
    // If Inventory is completely full, nothing happens - the item stays in the world until there's
    // room, which beats InventoryPanel's own "displaced item is lost" fallback for the same case.
    public void PickUp()
    {
        CharacterId character = PlayerSentinels.Get(PlayerSentinels.Local);
        EquippedLoadout loadout = SaveManager.Instance.Data.GetOrCreateLoadout(character);

        if (Item.IsCompatibleWith(character) && loadout.TryEquip(Item))
        {
            SaveManager.Instance.RequestSave();
            EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
            QueueFree();
            return;
        }

        Godot.Collections.Array<Item> inventory = SaveManager.Instance.Data.Inventory;
        int emptySlotIndex = inventory.IndexOf(null);

        if (emptySlotIndex < 0)
        {
            return;
        }

        inventory[emptySlotIndex] = Item;
        SaveManager.Instance.RequestSave();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
        QueueFree();
    }
}
