using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class InventoryPanel : Control
{
    [Export] public TileGrid Grid { get; private set; }
    [Export] public Button WeaponSlotButton { get; private set; }
    [Export] public Button HelmetSlotButton { get; private set; }
    [Export] public Button ChestSlotButton { get; private set; }
    [Export] public Button LegsSlotButton { get; private set; }
    [Export] public Button BootsSlotButton { get; private set; }
    [Export] public Button GlovesSlotButton { get; private set; }

    private EquippedLoadout _loadout;

    public override void _Ready()
    {
        // TODO: rename "paper doll" (RefreshPaperDoll et al.) - not happy with that name, no better one yet.
        WeaponSlotButton.Pressed += () => { _loadout.Weapon = null; Refresh(); };
        HelmetSlotButton.Pressed += () => { _loadout.Helmet = null; Refresh(); };
        ChestSlotButton.Pressed += () => { _loadout.Chest = null; Refresh(); };
        LegsSlotButton.Pressed += () => { _loadout.Legs = null; Refresh(); };
        BootsSlotButton.Pressed += () => { _loadout.Boots = null; Refresh(); };
        GlovesSlotButton.Pressed += () => { _loadout.Gloves = null; Refresh(); };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_inventory"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void Open()
    {
        if (PlayerCharacter.ActiveCombatant == null)
        {
            return;
        }

        Visible = true;
        Refresh();
        WeaponSlotButton.GrabFocus();
    }

    private void Close()
    {
        Visible = false;
    }

    private CharacterId ActiveCharacter => PlayerCharacter.ActiveCombatant.Id;

    private void Refresh()
    {
        _loadout = GetOrCreateLoadout();
        RefreshPaperDoll();
        RefreshGrid();
    }

    private EquippedLoadout GetOrCreateLoadout()
    {
        Godot.Collections.Dictionary<CharacterId, EquippedLoadout> loadouts = SaveManager.Instance.Data.Loadouts;

        if (!loadouts.TryGetValue(ActiveCharacter, out EquippedLoadout loadout))
        {
            loadout = new EquippedLoadout();
            loadouts[ActiveCharacter] = loadout;
        }

        return loadout;
    }

    private void RefreshPaperDoll()
    {
        SetSlotButton(WeaponSlotButton, _loadout.Weapon);
        SetSlotButton(HelmetSlotButton, _loadout.Helmet);
        SetSlotButton(ChestSlotButton, _loadout.Chest);
        SetSlotButton(LegsSlotButton, _loadout.Legs);
        SetSlotButton(BootsSlotButton, _loadout.Boots);
        SetSlotButton(GlovesSlotButton, _loadout.Gloves);
    }

    private static void SetSlotButton(Button button, Item item)
    {
        button.Text = item?.Name ?? "Empty";
        button.Icon = item?.Icon;
    }

    private void RefreshGrid()
    {
        foreach (Node child in Grid.GetChildren())
        {
            child.QueueFree();
        }

        HashSet<Item> equipped = GetAllEquippedItems();

        foreach (Item item in SaveManager.Instance.Data.OwnedItems.Where(i => !equipped.Contains(i)))
        {
            bool compatible = IsCompatible(item);

            Button button = new Button
            {
                Text = item.Name,
                Icon = item.Icon,
                Disabled = !compatible,
                CustomMinimumSize = new Vector2(120f, 80f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            if (compatible)
            {
                button.Pressed += () => Equip(item);
            }

            Grid.AddChild(button);
        }
    }

    private HashSet<Item> GetAllEquippedItems()
    {
        HashSet<Item> equipped = new HashSet<Item>();

        foreach (EquippedLoadout loadout in SaveManager.Instance.Data.Loadouts.Values)
        {
            if (loadout.Weapon != null) equipped.Add(loadout.Weapon);
            if (loadout.Helmet != null) equipped.Add(loadout.Helmet);
            if (loadout.Chest != null) equipped.Add(loadout.Chest);
            if (loadout.Legs != null) equipped.Add(loadout.Legs);
            if (loadout.Boots != null) equipped.Add(loadout.Boots);
            if (loadout.Gloves != null) equipped.Add(loadout.Gloves);
        }

        return equipped;
    }

    // AnySentinel counts as compatible for any Sentinel, but not for the Architect - that's the
    // pool boundary. An exact CharacterId match (including Architect-to-Architect) always works.
    private bool IsCompatible(Item item)
    {
        if (item.EquippableCharacter == ActiveCharacter)
        {
            return true;
        }

        return item.EquippableCharacter == CharacterId.AnySentinel && ActiveCharacter != CharacterId.Architect;
    }

    private void Equip(Item item)
    {
        switch (item)
        {
            case Weapon weapon: _loadout.Weapon = weapon; break;
            case Helmet helmet: _loadout.Helmet = helmet; break;
            case Chest chest: _loadout.Chest = chest; break;
            case Legs legs: _loadout.Legs = legs; break;
            case Boots boots: _loadout.Boots = boots; break;
            case Gloves gloves: _loadout.Gloves = gloves; break;
        }

        Refresh();
    }
}
