using Godot;

// The HUD pill for whichever of the local Sentinel's abilities carries a gauge, tinted by that
// gauge's element. Hidden when none does. Polled per frame for the same reason AbilityBar is.
public partial class AbilityGauge : TextureProgressBar
{
    [Export] public int BarWidth = 80;
    [Export] public int BarHeight = 176;
    [Export] public float BorderThickness = 4f;
    [Export] public Color BorderColor = new Color(0.1f, 0.1f, 0.12f);
    [Export] public Color EmptyColor = new Color(0.16f, 0.16f, 0.18f);
    // Placeholder cue for a gauge being spent, until the crosshair carries that.
    [Export] public Color ActiveColor = Colors.White;
    [Export] public Label GaugeLabel { get; private set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(BarWidth, BarHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        FillMode = (int)FillModeEnum.BottomToTop;
        MinValue = 0f;
        MaxValue = 1f;
        Step = 0f;
        Value = 0f;

        int cornerRadius = BarWidth / 2;
        TextureUnder = HudBarTexture.Build(BarWidth, BarHeight, cornerRadius, BorderThickness, BorderColor, EmptyColor);

        // White with a transparent border, so TintProgress alone recolors the fill and the border
        // always comes from TextureUnder.
        TextureProgress = HudBarTexture.Build(BarWidth, BarHeight, cornerRadius, BorderThickness, Colors.Transparent, Colors.White);
    }

    public override void _Process(double delta)
    {
        Ability gaugeAbility = FindGaugeAbility();
        Visible = gaugeAbility != null;

        if (gaugeAbility == null)
        {
            return;
        }

        float fraction = gaugeAbility.GaugeFraction.Value;
        Value = fraction;
        GaugeLabel.Text = $"{Mathf.RoundToInt(fraction * 100f)}/100";
        TintProgress = gaugeAbility.IsActive ? ActiveColor : DamageTypeColors.Get(gaugeAbility.GaugeElement);
    }

    // An ability spending its gauge wins over one filling it, so Overdrive's draining shots show in
    // place of Harvest's empty charge. Re-fetched every frame rather than cached - the local Sentinel
    // can swap out (hub character select), same as AbilityBar.
    private static Ability FindGaugeAbility()
    {
        Sentinel sentinel = Sentinel.Local;
        if (sentinel == null)
        {
            return null;
        }

        Ability filling = null;

        foreach (Ability ability in sentinel.AllAbilities)
        {
            if (!IsInstanceValid(ability) || !ability.GaugeFraction.HasValue)
            {
                continue;
            }

            if (ability.IsActive)
            {
                return ability;
            }

            filling ??= ability;
        }

        return filling;
    }
}
