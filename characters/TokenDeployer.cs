using Godot;
using System.Linq;

// README's "any player, mid-wave" token deployment: Q toggles (or R1 holds) a lightweight pick mode
// that remaps 1/2/3 (d-pad left/up/right) to the Sentinel's current tokens, only usable during the
// Combat phase.
// Pressing a mapped, non-empty slot hands straight off to RitualPlacer's ordinary ghost/reticle
// flow - this class's only remaining job past that point is consuming the token once placement is
// actually confirmed.
public partial class TokenDeployer : Node
{
    // Not exported - same reason as RitualPlacer.Character: this lives in the shared
    // sentinel_body.tscn base scene, whose root has no script yet when Godot type-checks an
    // [Export] NodePath assignment, so a typed export here fails the cast at scene-build time.
    public PlayerCharacter Character { get; private set; }

    [Export] public RitualPlacer Placer { get; private set; }

    public bool IsPickModeActive { get; private set; }

    private static readonly string[] _slotActions = { "token_slot_1", "token_slot_2", "token_slot_3" };

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
        if (phase != GamePhase.Combat)
        {
            SetPickModeActive(false);
        }
    }

    // Outside a match (the hub) tokens work anytime, for testing; in a match, only in Combat.
    private static bool CanDeployNow
    {
        get
        {
            MatchState match = LevelContext.Current.Match;
            return match == null || match.Phase == GamePhase.Combat;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionReleased("token_modifier"))
        {
            if (IsPickModeActive)
            {
                SetPickModeActive(false);
            }

            return;
        }

        // While an actual placement is underway, RitualPlacer owns escape-to-cancel and
        // PlayerCharacter.ExtraCombatInputGate already suppresses attack/alt_action - nothing
        // here needs to react until that resolves.
        if (!Character.CanAct || UiPanelStack.AnyOpen || Placer.IsPlacing)
        {
            return;
        }

        // Keyboard toggles pick mode; a controller holds R1 for exactly as long as it wants it.
        if (@event.IsActionPressed("token_modifier"))
        {
            if (CanDeployNow)
            {
                SetPickModeActive(true);
            }

            return;
        }

        if (@event.IsActionPressed("toggle_token_mode"))
        {
            if (IsPickModeActive || CanDeployNow)
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
        RuneLoadout token = PlayerProfiles.Get(Character.OwnerId).Data.Tokens[tokenIndex];

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
        PlayerProfiles.Get(Character.OwnerId).SetToken(tokenIndex, null);
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
