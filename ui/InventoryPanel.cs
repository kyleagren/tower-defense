using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class InventoryPanel : Control
{
    private static readonly Color DisabledBg = new Color(0.32f, 0.14f, 0.14f);
    private static readonly Color PlaceholderTint = new Color(1f, 1f, 1f, 0.4f);

    [Export] public Control SlotArea { get; private set; }
    [Export] public TileGrid Grid { get; private set; }
    [Export] public Control PhaseIndicator { get; private set; }

    private Button _mainHandWeaponButton;
    private Button _offHandWeaponButton;
    private Button _helmetButton;
    private Button _chestButton;
    private Button _legsButton;
    private Button _bootsButton;
    private Button _glovesButton;
    private Button _beltButton;
    private Button _amuletButton;
    private Button _leftRingButton;
    private Button _rightRingButton;
    private Button _relicButton;

    private EquippedLoadout _loadout;

    // Fractional position within SlotArea, roughly a human silhouette: head row (Relic/Helmet/
    // Amulet), weapon+torso row, ring+belt row, legs, then gloves/boots at the bottom.
    public override void _Ready()
    {
        _relicButton = BuildSlotButton(new Vector2(0.30f, 0.10f));
        _helmetButton = BuildSlotButton(new Vector2(0.50f, 0.10f));
        _amuletButton = BuildSlotButton(new Vector2(0.70f, 0.10f));
        _mainHandWeaponButton = BuildSlotButton(new Vector2(0.08f, 0.32f));
        _chestButton = BuildSlotButton(new Vector2(0.50f, 0.32f));
        _offHandWeaponButton = BuildSlotButton(new Vector2(0.92f, 0.32f));
        _leftRingButton = BuildSlotButton(new Vector2(0.30f, 0.54f));
        _beltButton = BuildSlotButton(new Vector2(0.50f, 0.54f));
        _rightRingButton = BuildSlotButton(new Vector2(0.70f, 0.54f));
        _legsButton = BuildSlotButton(new Vector2(0.50f, 0.72f));
        _glovesButton = BuildSlotButton(new Vector2(0.30f, 0.90f));
        _bootsButton = BuildSlotButton(new Vector2(0.70f, 0.90f));

        _mainHandWeaponButton.Pressed += UnequipMainHand;
        _offHandWeaponButton.Pressed += UnequipOffHand;
        _helmetButton.Pressed += () => { _loadout.Helmet = null; SaveManager.Instance.Save(); Refresh(); };
        _chestButton.Pressed += () => { _loadout.Chest = null; SaveManager.Instance.Save(); Refresh(); };
        _legsButton.Pressed += () => { _loadout.Legs = null; SaveManager.Instance.Save(); Refresh(); };
        _bootsButton.Pressed += () => { _loadout.Boots = null; SaveManager.Instance.Save(); Refresh(); };
        _glovesButton.Pressed += () => { _loadout.Gloves = null; SaveManager.Instance.Save(); Refresh(); };
        _beltButton.Pressed += () => { _loadout.Belt = null; SaveManager.Instance.Save(); Refresh(); };
        _amuletButton.Pressed += () => { _loadout.Amulet = null; SaveManager.Instance.Save(); Refresh(); };
        _leftRingButton.Pressed += () => { _loadout.LeftRing = null; SaveManager.Instance.Save(); Refresh(); };
        _rightRingButton.Pressed += () => { _loadout.RightRing = null; SaveManager.Instance.Save(); Refresh(); };
        _relicButton.Pressed += () => { _loadout.Relic = null; SaveManager.Instance.Save(); Refresh(); };

        // Sit just below PhaseIndicator's actual height rather than a hardcoded number that could
        // silently drift out of sync with it - CallDeferred because a Control's Size isn't settled
        // until after the first layout pass, and Resized keeps it correct if that height ever changes.
        PhaseIndicator.Resized += UpdateTopOffset;
        CallDeferred(nameof(UpdateTopOffset));
    }

    private void UpdateTopOffset()
    {
        OffsetTop = PhaseIndicator.Size.Y + 4f;
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
        _helmetButton.GrabFocus();
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
        SetEquipmentSlotVisual(_mainHandWeaponButton, _loadout.MainHandWeapon, "Main Hand");
        SetEquipmentSlotVisual(_offHandWeaponButton, _loadout.OffHandWeapon, "Off Hand");
        SetEquipmentSlotVisual(_helmetButton, _loadout.Helmet, "Helmet");
        SetEquipmentSlotVisual(_chestButton, _loadout.Chest, "Chest");
        SetEquipmentSlotVisual(_legsButton, _loadout.Legs, "Legs");
        SetEquipmentSlotVisual(_bootsButton, _loadout.Boots, "Boots");
        SetEquipmentSlotVisual(_glovesButton, _loadout.Gloves, "Gloves");
        SetEquipmentSlotVisual(_beltButton, _loadout.Belt, "Belt");
        SetEquipmentSlotVisual(_amuletButton, _loadout.Amulet, "Amulet");
        SetEquipmentSlotVisual(_leftRingButton, _loadout.LeftRing, "Ring");
        SetEquipmentSlotVisual(_rightRingButton, _loadout.RightRing, "Ring");
        SetEquipmentSlotVisual(_relicButton, _loadout.Relic, "Relic");
    }

    // Empty slots show their own name as a faint placeholder instead of "Empty". The label is
    // hidden entirely once something's equipped, rather than just left blank - an empty Label
    // still reserves its own row in the VBoxContainer, which was squeezing the icon into the
    // button's top half instead of letting it fill the whole thing.
    private static void SetEquipmentSlotVisual(Button button, Item item, string slotName)
    {
        Label label = button.GetNode<Label>("Content/Label");

        if (item != null)
        {
            SetItemVisual(button, item.Icon, "");
            label.Visible = false;
        }
        else
        {
            SetItemVisual(button, null, slotName);
            label.Visible = true;
            label.Modulate = PlaceholderTint;
        }
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
                CustomMinimumSize = new Vector2(90f, 90f)
            };

            BuildIconOnlyVisual(button);
            SetIconOnlyVisual(button, item.Icon);

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

    private Button BuildSlotButton(Vector2 anchor)
    {
        Button button = new Button();
        CenterOnAnchor(button, anchor, new Vector2(130f, 130f));
        BuildItemVisual(button);
        SlotArea.AddChild(button);

        return button;
    }

    private static void CenterOnAnchor(Control control, Vector2 anchorPoint, Vector2 size)
    {
        control.AnchorLeft = anchorPoint.X;
        control.AnchorRight = anchorPoint.X;
        control.AnchorTop = anchorPoint.Y;
        control.AnchorBottom = anchorPoint.Y;

        control.OffsetLeft = -size.X / 2f;
        control.OffsetRight = size.X / 2f;
        control.OffsetTop = -size.Y / 2f;
        control.OffsetBottom = size.Y / 2f;
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

    // Grid tiles show only the thumbnail - no name until a tooltip exists to show it on hover.
    private static void BuildIconOnlyVisual(Button button)
    {
        button.Text = "";

        TextureRect icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);

        button.AddChild(icon);
    }

    private static void SetIconOnlyVisual(Button button, Texture2D icon)
    {
        button.GetNode<TextureRect>("Icon").Texture = icon;
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
            case Weapon weapon: EquipWeapon(weapon); break;
            case Helmet helmet: _loadout.Helmet = helmet; break;
            case Chest chest: _loadout.Chest = chest; break;
            case Legs legs: _loadout.Legs = legs; break;
            case Boots boots: _loadout.Boots = boots; break;
            case Gloves gloves: _loadout.Gloves = gloves; break;
            case Belt belt: _loadout.Belt = belt; break;
            case Amulet amulet: _loadout.Amulet = amulet; break;
            case Relic relic: _loadout.Relic = relic; break;
            case Ring ring: EquipRing(ring); break;
        }

        SaveManager.Instance.Save();
        Refresh();
    }

    // A two-handed weapon occupies both slots as the same reference. Equipping any weapon while
    // one's currently two-handed clears both first, since it can't share a hand with anything.
    private void EquipWeapon(Weapon weapon)
    {
        if (_loadout.MainHandWeapon != null && _loadout.MainHandWeapon == _loadout.OffHandWeapon)
        {
            _loadout.MainHandWeapon = null;
            _loadout.OffHandWeapon = null;
        }

        if (weapon.TwoHanded)
        {
            _loadout.MainHandWeapon = weapon;
            _loadout.OffHandWeapon = weapon;
        }
        else if (_loadout.MainHandWeapon == null)
        {
            _loadout.MainHandWeapon = weapon;
        }
        else
        {
            _loadout.OffHandWeapon = weapon;
        }
    }

    // A ring doesn't know which hand it's for - fill whichever slot is open, preferring Left.
    private void EquipRing(Ring ring)
    {
        if (_loadout.LeftRing == null)
        {
            _loadout.LeftRing = ring;
        }
        else
        {
            _loadout.RightRing = ring;
        }
    }

    private void UnequipMainHand()
    {
        if (_loadout.MainHandWeapon != null && _loadout.MainHandWeapon == _loadout.OffHandWeapon)
        {
            _loadout.OffHandWeapon = null;
        }

        _loadout.MainHandWeapon = null;
        SaveManager.Instance.Save();
        Refresh();
    }

    private void UnequipOffHand()
    {
        if (_loadout.OffHandWeapon != null && _loadout.OffHandWeapon == _loadout.MainHandWeapon)
        {
            _loadout.MainHandWeapon = null;
        }

        _loadout.OffHandWeapon = null;
        SaveManager.Instance.Save();
        Refresh();
    }
}
