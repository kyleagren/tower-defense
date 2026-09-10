using Godot;
using System.Linq;

// README's "any player, mid-wave" token deployment: Q toggles a lightweight pick mode
// that remaps 1/2/3 to the Sentinel's current tokens, only usable during the Combat phase.
// Pressing a mapped, non-empty slot hands straight off to RitualPlacer's ordinary ghost/reticle
// flow - this class's only remaining job past that point is consuming the token once placement is
// actually confirmed.
public partial class TokenDeployer : Node
{
    // Not exported - same reason as RitualPlacer.Character: this lives in the shared
    // SentinelBody.tscn base scene, whose root has no script yet when Godot type-checks an
    // [Export] NodePath assignment, so a typed export here fails the cast at scene-build time.
    public PlayerCharacter Character { get; private set; }

    [Export] public RitualPlacer Placer { get; private set; }

    // Set by SentinelSpawner for a spawner with no phase system to wait for (the hub) - mirrors
    // that spawner's own ActivateImmediately flag, so token testing works the same "no real match"
    // way the Architect's own DPS testing already does there.
    public bool AllowOutsideCombat { get; set; }

    public bool IsPickModeActive { get; private set; }

    private static readonly string[] _slotActions = { "token_slot_1", "token_slot_2", "token_slot_3" };

    private GamePhase _currentPhase;

    public override void _Ready()
    {
        Character = (PlayerCharacter)GetParent();
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;

        if (IsPickModeActive)
        {
            SetPickModeActive(false);
        }
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        _currentPhase = phase;

        if (phase != GamePhase.Combat)
        {
            SetPickModeActive(false);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // While an actual placement is underway, RitualPlacer owns escape-to-cancel and
        // PlayerCharacter.ExtraCombatInputGate already suppresses attack/alt_action - nothing
        // here needs to react until that resolves.
        if (!Character.IsActive || UiPanelStack.AnyOpen || Placer.IsPlacing)
        {
            return;
        }

        if (@event.IsActionPressed("toggle_token_mode"))
        {
            if (IsPickModeActive || AllowOutsideCombat || _currentPhase == GamePhase.Combat)
            {
                SetPickModeActive(!IsPickModeActive);
            }

            return;
        }

        if (!IsPickModeActive)
        {
            return;
        }

        if (@event.IsActionPressed("alt_action"))
        {
            SetPickModeActive(false);
            return;
        }

        for (int i = 0; i < _slotActions.Length; i++)
        {
            if (@event.IsActionPressed(_slotActions[i]))
            {
                // 1/2/3 are also the Sentinel's ability keys - marking this handled is what stops
                // the same press from firing an ability once pick mode exits below.
                GetViewport().SetInputAsHandled();
                TryDeployToken(i);
                return;
            }
        }
    }

    private void TryDeployToken(int tokenIndex)
    {
        RuneLoadout token = SaveManager.Instance.Data.Tokens[tokenIndex];

        if (token == null)
        {
            return;
        }

        SetPickModeActive(false);

        Godot.Collections.Array<Rune> runes = new Godot.Collections.Array<Rune>(token.Runes.Where(r => r != null));
        Placer.BeginPlacement(token.Spell, runes, () => ConsumeToken(tokenIndex));
    }

    private void ConsumeToken(int tokenIndex)
    {
        SaveManager.Instance.Data.Tokens[tokenIndex] = null;
        SaveManager.Instance.Save();
        EventBus.Instance.EmitSignal(EventBus.SignalName.TokensChanged);
    }

    private void SetPickModeActive(bool active)
    {
        IsPickModeActive = active;
        EventBus.Instance.EmitSignal(EventBus.SignalName.TokenPickModeChanged, active);
    }

    // Exposed so Sentinel's own ui_cancel handling can back out of pick mode instead of toggling
    // mouse capture - keeps that decision in one place rather than having both nodes independently
    // react to the same Escape press in an order-dependent way.
    public void ExitPickMode() => SetPickModeActive(false);
}
