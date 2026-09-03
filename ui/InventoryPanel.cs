using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class InventoryPanel : Control
{
    private static readonly Color DisabledBg = new Color(0.32f, 0.14f, 0.14f);

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
        BuildItemVisual(WeaponSlotButton);
        BuildItemVisual(HelmetSlotButton);
        BuildItemVisual(ChestSlotButton);
        BuildItemVisual(LegsSlotButton);
        BuildItemVisual(BootsSlotButton);
        BuildItemVisual(GlovesSlotButton);

        WeaponSlotButton.Pressed += () => { _loadout.Weapon = null; SaveManager.Instance.Save(); Refresh(); };
        HelmetSlotButton.Pressed += () => { _loadout.Helmet = null; SaveManager.Instance.Save(); Refresh(); };
        ChestSlotButton.Pressed += () => { _loadout.Chest = null; SaveManager.Instance.Save(); Refresh(); };
        LegsSlotButton.Pressed += () => { _loadout.Legs = null; SaveManager.Instance.Save(); Refresh(); };
        BootsSlotButton.Pressed += () => { _loadout.Boots = null; SaveManager.Instance.Save(); Refresh(); };
        GlovesSlotButton.Pressed += () => { _loadout.Gloves = null; SaveManager.Instance.Save(); Refresh(); };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_inventory"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
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
        UiPanelStack.Push(this);
    }

    private void Close()
    {
        Visible = false;
        UiPanelStack.Pop(this);
    }

    private CharacterId ActiveCharacter => SelectedSentinel.Current;

    private void Refresh()
    {
        _loadout = GetOrCreateLoadout();
        CharacterSheetCache.Invalidate(ActiveCharacter);
        RefreshEquipmentSlots();
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

    private void RefreshEquipmentSlots()
    {
        SetItemVisual(WeaponSlotButton, _loadout.Weapon?.Icon, _loadout.Weapon?.Name ?? "Empty");
        SetItemVisual(HelmetSlotButton, _loadout.Helmet?.Icon, _loadout.Helmet?.Name ?? "Empty");
        SetItemVisual(ChestSlotButton, _loadout.Chest?.Icon, _loadout.Chest?.Name ?? "Empty");
        SetItemVisual(LegsSlotButton, _loadout.Legs?.Icon, _loadout.Legs?.Name ?? "Empty");
        SetItemVisual(BootsSlotButton, _loadout.Boots?.Icon, _loadout.Boots?.Name ?? "Empty");
        SetItemVisual(GlovesSlotButton, _loadout.Gloves?.Icon, _loadout.Gloves?.Name ?? "Empty");
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
                Disabled = !compatible,
                CustomMinimumSize = new Vector2(120f, 100f)
            };

            BuildItemVisual(button);
            SetItemVisual(button, item.Icon, item.Name);

            if (!compatible)
            {
                button.AddThemeStyleboxOverride("disabled", CreateBackground(DisabledBg));
            }

            if (compatible)
            {
                button.Pressed += () => Equip(item);
            }

            Grid.AddChild(button);
        }
    }

    private static StyleBoxFlat CreateBackground(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }

    private static void BuildItemVisual(Button button)
    {
        button.Text = "";

        VBoxContainer content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.AddThemeConstantOverride("separation", 4);

        TextureRect icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        Label label = new Label
        {
            Name = "Label",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };

        content.AddChild(icon);
        content.AddChild(label);
        button.AddChild(content);
    }

    private static void SetItemVisual(Button button, Texture2D icon, string name)
    {
        button.GetNode<TextureRect>("Content/Icon").Texture = icon;
        button.GetNode<Label>("Content/Label").Text = name;
    }

    private HashSet<Item> GetAllEquippedItems()
    {
        HashSet<Item> equipped = new HashSet<Item>();

        foreach (EquippedLoadout loadout in SaveManager.Instance.Data.Loadouts.Values)
        {
            foreach (Item item in loadout.EquippedItems())
            {
                equipped.Add(item);
            }
        }

        return equipped;
    }

    private bool IsCompatible(Item item)
    {
        return item.EquippableCharacter == ActiveCharacter || item.EquippableCharacter == CharacterId.AnySentinel;
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

        SaveManager.Instance.Save();
        Refresh();
    }
}
