using Godot;
using System;

// Left-anchored, mirrors InventoryPanel's own toggle/open/close shape - opposite screen side on
// purpose, so both can be open at once without overlapping. Top section (Health/Armor/Shield)
// refreshes every frame while visible, since Shield's passive regen (Sentinel.cs) ticks
// continuously and wouldn't otherwise animate live. The scrollable stat list only rebuilds on
// EventBus.InventoryChanged, since % bonuses only ever change when gear changes.
public partial class CharacterSheetPanel : Control
{
    [Export] public Label NameLabel { get; private set; }
    [Export] public Label HealthLabel { get; private set; }
    [Export] public Label ArmorLabel { get; private set; }
    [Export] public Label ArmorReductionLabel { get; private set; }
    [Export] public Label ShieldLabel { get; private set; }
    [Export] public VBoxContainer StatsList { get; private set; }

    public override void _Ready()
    {
        EventBus.Instance.InventoryChanged += RefreshStatsList;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.InventoryChanged -= RefreshStatsList;
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            RefreshTopSection();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("character_sheet"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            UiPanelStack.CloseAll();
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
        RefreshTopSection();
        RefreshStatsList();
        UiPanelStack.Push(this, Close);
    }

    private void Close()
    {
        Visible = false;
        UiPanelStack.Pop(this);
    }

    private void RefreshTopSection()
    {
        Sentinel sentinel = Sentinel.Local;
        if (sentinel == null)
        {
            return;
        }

        NameLabel.Text = sentinel.Id.ToString();
        HealthLabel.Text = $"Health: {Mathf.RoundToInt(sentinel.CurrentHealth)} / {Mathf.RoundToInt(sentinel.MaxHealth)}";
        ArmorLabel.Text = $"Armor: {Mathf.RoundToInt(sentinel.TotalArmor)}";
        ArmorReductionLabel.Text = $"-{Mathf.RoundToInt(sentinel.MeleeDamageReductionPercent * 100f)}% Melee Dmg";
        ShieldLabel.Text = $"Shield: {Mathf.RoundToInt(sentinel.CurrentShield)} / {Mathf.RoundToInt(sentinel.MaxShield)}";
    }

    private void RefreshStatsList()
    {
        foreach (Node child in StatsList.GetChildren())
        {
            child.Free();
        }

        Sentinel sentinel = Sentinel.Local;
        if (sentinel == null)
        {
            return;
        }

        CharacterSheet sheet = CharacterSheetCache.GetSheet(sentinel.Id);

        if (sentinel.BaseHealthRegenPerSecond > 0f)
        {
            AddStatRow($"Base Health Regenerated per Second: {Mathf.RoundToInt(sentinel.BaseHealthRegenPerSecond)}");
            AddStatRow($"Total Health Regenerated per Second: {Mathf.RoundToInt(sentinel.TotalHealthRegenPerSecond)}");
        }

        foreach (StatId stat in Enum.GetValues<StatId>())
        {
            if (stat == StatId.SentinelBaseHealthRegen)
            {
                continue;
            }

            if (!sheet.Bonuses.TryGetValue(stat, out float bonus) || Mathf.IsZeroApprox(bonus))
            {
                continue;
            }

            // Every remaining DescriptionTemplate follows "+{0}% X" or, for the two cost-reduction
            // stats, "-{0}% X" - stripping whichever prefix applies leaves just the readable stat
            // name, so rows read "X : +N%" (or "Reduced X : -N%" for the two reductions) instead of
            // the raw "+{0}% X" template.
            Affix affix = AffixTable.Get(stat);
            bool isReduction = affix.DescriptionTemplate.StartsWith("-{0}% ");
            string statName = affix.DescriptionTemplate.Replace(isReduction ? "-{0}% " : "+{0}% ", "");

            if (isReduction)
            {
                statName = $"Reduced {statName}";
            }

            string sign = isReduction ? "-" : "+";
            AddStatRow($"{statName} : {sign}{Mathf.RoundToInt(bonus * 100f)}%");
        }
    }

    private void AddStatRow(string text)
    {
        StatsList.AddChild(new Label { Text = text });
    }
}
