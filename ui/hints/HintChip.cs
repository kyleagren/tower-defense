using Godot;

// The button half of a hint: a pill naming the binding, wide enough for a key name like "Escape",
// with the PlayStation face shapes and d-pad arrows drawn rather than typed, as PromptBadge does.
public partial class HintChip : PanelContainer
{
    public const float Height = 36f;

    private const int FontSize = 18;
    private const float TextPadding = 12f;

    private static readonly Color _backgroundColor = new Color(0f, 0f, 0f, 0.85f);

    public static HintChip Create(Hint hint)
    {
        ButtonGlyph.Shape shape = InputPromptHelper.GetGlyph(hint.Action);
        bool drawn = shape != ButtonGlyph.Shape.None;

        HintChip chip = new HintChip
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(Height, Height)
        };

        StyleBoxFlat style = HudCircle.BuildCircularStyle(_backgroundColor);
        style.ContentMarginLeft = drawn ? 0f : TextPadding;
        style.ContentMarginRight = drawn ? 0f : TextPadding;
        chip.AddThemeStyleboxOverride("panel", style);

        if (drawn)
        {
            chip.AddChild(new ButtonGlyph
            {
                Current = shape,
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(Height, Height)
            });
            return chip;
        }

        Label label = new Label
        {
            Text = TextFor(hint),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        chip.AddChild(label);
        return chip;
    }

    private static string TextFor(Hint hint)
    {
        string binding = InputPromptHelper.GetPrompt(hint.Action);
        return InputDevice.UsingController || hint.MouseText == null ? binding : hint.MouseText.Replace("{0}", binding);
    }
}
