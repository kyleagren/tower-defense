using Godot;

public partial class BuildMenu : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] public SpellGrid Grid { get; private set; }

    private int pageIndex;

    public override void _Ready()
    {
        var spells = SaveManager.Instance.Data.KnownSpells;
        int pageSize = Grid.Columns * Grid.Rows;
        int startIndex = pageIndex * pageSize;

        for (int i = 0; i < pageSize; i++)
        {
            int spellIndex = startIndex + i;

            if (spellIndex < spells.Count)
            {
                Spell spell = spells[spellIndex];
                Button button = new Button
                {
                    CustomMinimumSize = new Vector2(120f, 80f),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsVertical = SizeFlags.ExpandFill
                };

                Label label = new Label
                {
                    Text = spell.Name,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                label.AddThemeFontSizeOverride("font_size", 22);
                label.SetAnchorsPreset(LayoutPreset.TopWide);
                button.AddChild(label);

                button.Pressed += () => OnSpellSelected(spell);
                Grid.AddChild(button);
            }
            else
            {
                Control spacer = new Control
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Ignore
                };

                Grid.AddChild(spacer);
            }
        }
    }

    public void Open()
    {
        Visible = true;

        if (Grid.GetChildCount() > 0)
        {
            (Grid.GetChild(0) as Control)?.GrabFocus();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnSpellSelected(Spell spell)
    {
        GD.Print($"Selected spell: {spell.Name}");
        Close();
    }

    private void Close()
    {
        Visible = false;
        EmitSignal(SignalName.Closed);
    }
}
