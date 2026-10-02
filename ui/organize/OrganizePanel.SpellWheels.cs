using Godot;
using System.Collections.Generic;
using System.Linq;

// The Spell Wheels tab: each element's spells, in the order its spell ring shows them.
public partial class OrganizePanel
{
    private readonly Dictionary<SpellWheelGroup, Button> _groupButtons = new();
    private Label _spellListTitle;
    private ReorderList _spellList;
    private SpellWheelGroup _openGroup;
    private readonly List<Spell> _listedSpells = new();

    private Control BuildSpellWheelsPage()
    {
        HBoxContainer page = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 24);

        VBoxContainer groups = new VBoxContainer();
        groups.AddThemeConstantOverride("separation", 8);
        page.AddChild(groups);

        foreach (SpellWheelGroup group in _groups)
        {
            Button button = new Button { Text = SpellWheelGroups.NameOf(group), CustomMinimumSize = new Vector2(240f, 48f) };
            button.Pressed += () => ShowGroup(group, focusList: true);
            groups.AddChild(button);
            _groupButtons[group] = button;
        }

        VBoxContainer listColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        listColumn.AddThemeConstantOverride("separation", 8);
        page.AddChild(listColumn);

        _spellListTitle = new Label();
        listColumn.AddChild(_spellListTitle);

        ScrollContainer scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        listColumn.AddChild(scroll);

        _spellList = new ReorderList { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _spellList.Moved += OnSpellMoved;
        scroll.AddChild(_spellList);

        return page;
    }

    private void ShowSpellWheelsPage()
    {
        foreach ((SpellWheelGroup group, Button button) in _groupButtons)
        {
            button.Disabled = Profile.SpellRing(group).Count == 0;
        }

        SpellWheelGroup first = _groups.FirstOrDefault(group => !_groupButtons[group].Disabled);
        ShowGroup(first, focusList: false);
        _groupButtons[first].GrabFocus();
    }

    private void ShowGroup(SpellWheelGroup group, bool focusList)
    {
        _openGroup = group;
        _listedSpells.Clear();
        _listedSpells.AddRange(Profile.SpellRing(group));

        _spellListTitle.Text = $"{SpellWheelGroups.NameOf(group)} spells, in wheel order";
        _spellList.SetEntries(_listedSpells.Select(spell => spell.Name));

        if (focusList)
        {
            _spellList.FocusFirst();
        }
    }

    private void OnSpellMoved(int from, int to)
    {
        Spell spell = _listedSpells[from];
        _listedSpells.RemoveAt(from);
        _listedSpells.Insert(to, spell);
        Profile.SetSpellRing(_openGroup, _listedSpells);
    }

    private bool CancelSpellWheelsInProgress()
    {
        if (!_spellList.IsHolding)
        {
            return false;
        }

        _spellList.CancelHold();
        return true;
    }
}
