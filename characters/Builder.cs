using Godot;

// Building, for the Sentinel it sits on. During build time - Build, or anywhere in the hub - the
// build menu button opens the build menu, and a token slot pressed with R1 held opens it straight
// into authoring that token. In Combat the same token input deploys instead (TokenDeployer).
// Keyboard 1/2/3 stay the Sentinel's abilities; on a keyboard, tokens are authored from inside the
// menu.
public partial class Builder : Node
{
    // Not exported, for the same inherited-scene reason as RitualPlacer.Character.
    public Sentinel Character { get; private set; }

    [Export] public BuildMenu Menu { get; private set; }
    [Export] public RitualPlacer Placer { get; private set; }

    private static readonly string[] _tokenSlotActions = { "token_slot_1", "token_slot_2", "token_slot_3" };

    public override void _Ready()
    {
        Character = (Sentinel)GetParent();
        Menu.Confirmed += OnMenuConfirmed;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    // The menu belongs to build time, so leaving it closes the menu, whatever screen it's on.
    private void OnGamePhaseChanged(GamePhase phase)
    {
        Menu.CloseIfOpen();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Character.CanAct || UiPanelStack.AnyOpen || Placer.IsPlacing || !LevelContext.Current.IsBuildTime)
        {
            return;
        }

        // Without the ability modifier, since L1 + Triangle is the Sentinel's fourth ability.
        if (InputChords.IsPressedWithoutAbilityModifier(@event, "open_build_menu"))
        {
            Menu.Open();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventJoypadButton || !InputChords.TokenModifierHeld)
        {
            return;
        }

        for (int token = 0; token < _tokenSlotActions.Length; token++)
        {
            if (@event.IsActionPressed(_tokenSlotActions[token]))
            {
                Menu.Open(token);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    private void OnMenuConfirmed(Spell spell, Godot.Collections.Array<Rune> runes)
    {
        Placer.BeginPlacement(spell, runes);
    }
}
