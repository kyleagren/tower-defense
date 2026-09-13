using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// One tooltip panel's Controls and content-building logic - header, plain body, or the advanced
// grouped-by-type body with per-mod tier badges. Shared by ItemTooltip (inventory hover, including
// its comparison panels) and ItemNameplateController (the world-space version shown when directly
// looking at a dropped item), so both render from the exact same styling instead of two
// implementations that could drift apart.
public class TooltipPanelView
{
    private const float GroupLabelWidth = 50f;
    private static readonly Color EquippedLabelColor = new Color(0.6f, 0.9f, 0.6f);
    private static readonly Color ItemStatsColor = new Color(0.4f, 0.65f, 0.95f);
    public static readonly Color DefaultBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);

    private readonly PanelContainer _panel;
    private readonly Label _equippedLabel;
    private readonly Label _headerLabel;
    private readonly Label _itemStatsLabel;
    private readonly Label _plainBodyLabel;
    private readonly VBoxContainer _advancedBody;
    private readonly Action<Control, RolledAffix> _wireTierBadge;

    public TooltipPanelView(CanvasLayer parent, Action<Control, RolledAffix> wireTierBadge)
    {
        _wireTierBadge = wireTierBadge;

        _panel = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panel.AddThemeStyleboxOverride("panel", BuildBackgroundStyle(DefaultBackgroundColor));

        VBoxContainer content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(content);

        _equippedLabel = new Label { Text = "Equipped", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _equippedLabel.AddThemeColorOverride("font_color", EquippedLabelColor);
        content.AddChild(_equippedLabel);

        _headerLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        content.AddChild(_headerLabel);

        _itemStatsLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _itemStatsLabel.AddThemeColorOverride("font_color", ItemStatsColor);
        content.AddChild(_itemStatsLabel);

        _plainBodyLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(_plainBodyLabel);

        _advancedBody = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(_advancedBody);

        parent.AddChild(_panel);
    }

    public bool Visible => _panel.Visible;
    public Vector2 Size => _panel.Size;
    public Vector2 Position => _panel.Position;
    public Control Root => _panel;

    public void SetPosition(Vector2 position)
    {
        _panel.Position = position;
    }

    public void Show(Item item, bool advanced, bool isEquipped = false, Color? backgroundColor = null)
    {
        _panel.AddThemeStyleboxOverride("panel", BuildBackgroundStyle(backgroundColor ?? DefaultBackgroundColor));

        _equippedLabel.Visible = isEquipped;
        _headerLabel.Text = item.HeaderText;

        if (item is Armor armor)
        {
            _itemStatsLabel.Text = BuildArmorStatsText(armor);
            _itemStatsLabel.Visible = true;
        }
        else if (item is Weapon weapon)
        {
            _itemStatsLabel.Text = BuildWeaponStatsText(weapon);
            _itemStatsLabel.Visible = true;
        }
        else
        {
            _itemStatsLabel.Visible = false;
        }

        _plainBodyLabel.Visible = !advanced;
        _advancedBody.Visible = advanced;

        if (advanced)
        {
            BuildAdvancedBody(item);
        }
        else
        {
            _plainBodyLabel.Text = string.Join("\n", item.Affixes.Select(roll => roll.BuildDescription()));
        }

        _panel.Visible = true;

        // Force a fresh fit to whatever's actually shown now - see ItemTooltip's own notes on
        // why a Control's Size needs an explicit reset to shrink back down reliably.
        _panel.Size = Vector2.Zero;
    }

    public void Hide()
    {
        _panel.Visible = false;
    }

    private static string BuildArmorStatsText(Armor armor)
    {
        List<string> parts = new();
        float armorValue = armor.ResolvedValue(StatId.SentinelArmor);
        float shieldValue = armor.ResolvedValue(StatId.SentinelShield);

        if (armorValue > 0f)
        {
            parts.Add($"Armor: {Mathf.RoundToInt(armorValue)}");
        }

        if (shieldValue > 0f)
        {
            parts.Add($"Shield: {Mathf.RoundToInt(shieldValue)}");
        }

        return string.Join("\n", parts);
    }

    private static string BuildWeaponStatsText(Weapon weapon)
    {
        return string.Join("\n", new[]
        {
            $"Base Attack Damage: {Mathf.RoundToInt(weapon.ResolvedBaseAttackDamage)}",
            $"Base Attack Speed: {weapon.ResolvedBaseAttackSpeed:0.##}",
            $"Base Crit Chance: {Mathf.RoundToInt(weapon.ResolvedBaseCritChance * 100f)}%"
        });
    }

    private void BuildAdvancedBody(Item item)
    {
        // Free() immediately, not QueueFree() - a deferred free would let old and new rows briefly
        // coexist.
        foreach (Node child in _advancedBody.GetChildren())
        {
            child.Free();
        }

        List<RolledAffix> prefixes = item.Affixes.Where(roll => roll.Affix.AffixType == AffixType.Prefix).ToList();
        List<RolledAffix> suffixes = item.Affixes.Where(roll => roll.Affix.AffixType == AffixType.Suffix).ToList();

        if (prefixes.Count > 0)
        {
            _advancedBody.AddChild(BuildGroup("Prefix", prefixes));
        }

        if (suffixes.Count > 0)
        {
            _advancedBody.AddChild(BuildGroup("Suffix", suffixes));
        }
    }

    private HBoxContainer BuildGroup(string label, List<RolledAffix> rolls)
    {
        HBoxContainer group = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

        Label groupLabel = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(GroupLabelWidth, 0f),
            VerticalAlignment = VerticalAlignment.Top,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        group.AddChild(groupLabel);

        VBoxContainer rows = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        foreach (RolledAffix roll in rolls)
        {
            AffixRow built = AffixRowView.Build(roll);
            rows.AddChild(built.Root);
            _wireTierBadge(built.TierBadge, roll);
        }

        group.AddChild(rows);

        return group;
    }

    public static StyleBoxFlat BuildBackgroundStyle(Color bgColor)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }
}
