using Godot;
using System.Collections.Generic;
using System.Linq;

// Overdrive's element pick: a small ring centered on the screen while the local Soldier holds the
// slot. Reports what it points at to the ability, which commits it on release. Pointing at nothing -
// the center, in the gauge's own color - spends the gauge as harvested. Polled per frame, like
// CrosshairCue.
public partial class OverdriveElementWheel : Control
{
    // The hole clears Health Link's arcs around the crosshair; CrosshairCue's timers sit just below
    // the ring.
    private const float OuterRadius = 84f;
    private const float InnerRadiusFraction = 0.62f;
    private const float CenterRadius = 22f;
    private const float SlotAlpha = 0.6f;
    private const float CenterAlpha = 0.6f;
    private const float CenterFadedAlpha = 0.2f;

    private RadialWheel _wheel;
    private OverdriveAbility _picking;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _wheel = new RadialWheel
        {
            InnerRadiusFraction = InnerRadiusFraction,
            HandlesCommit = false,
            PointsWithLookStick = true,
            ClearsHighlightAtCenter = true,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -OuterRadius,
            OffsetRight = OuterRadius,
            OffsetTop = -OuterRadius,
            OffsetBottom = OuterRadius
        };
        _wheel.HighlightChanged += OnHighlightChanged;
        AddChild(_wheel);

        Visible = false;
    }

    public override void _Process(double delta)
    {
        OverdriveAbility picking = FindPickingOverdrive();

        if (picking == _picking)
        {
            return;
        }

        _picking = picking;
        Visible = picking != null;
        _wheel.AcceptsInput = picking != null;

        if (picking != null)
        {
            _wheel.SetItems(Items());
            QueueRedraw();
        }
    }

    private void OnHighlightChanged(int slot)
    {
        _picking?.PointAt(slot < 0 ? null : ElementTags.All[slot]);
        QueueRedraw();
    }

    // Drawn under the wheel, so its pointer passes over it.
    public override void _Draw()
    {
        if (_picking == null)
        {
            return;
        }

        Color color = DamageTypeColors.Get(_picking.Harvest.GaugeElement);
        DrawCircle(Size / 2f, CenterRadius, color with { A = _wheel.Highlighted < 0 ? CenterAlpha : CenterFadedAlpha });
    }

    private static OverdriveAbility FindPickingOverdrive()
    {
        Sentinel sentinel = Sentinel.Local;

        if (sentinel == null || LevelContext.Current.LocalActiveCharacter != sentinel)
        {
            return null;
        }

        foreach (Ability ability in sentinel.AllAbilities)
        {
            if (IsInstanceValid(ability) && ability is OverdriveAbility { IsPickingElement: true } overdrive)
            {
                return overdrive;
            }
        }

        return null;
    }

    private static List<RadialWheelItem> Items()
    {
        return ElementTags.All
            .Select(element => new RadialWheelItem { Color = DamageTypeColors.Get(element) with { A = SlotAlpha } })
            .ToList();
    }
}
