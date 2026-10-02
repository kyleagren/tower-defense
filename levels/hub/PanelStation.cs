using Godot;

// A station that opens a panel meant to be used standing at it. The character who opened it walking
// out of StationLeaveReach closes it the way Escape does.
public abstract partial class PanelStation : StaticBody3D, IInteractable
{
    private PlayerCharacter _user;

    protected abstract Control Panel { get; }

    public abstract string GetPromptText(PlayerCharacter character);

    protected abstract void OpenPanel(PlayerCharacter character);

    public void Interact(PlayerCharacter character)
    {
        OpenPanel(character);
        _user = character;
    }

    public override void _Process(double delta)
    {
        if (_user == null)
        {
            return;
        }

        if (!Panel.Visible)
        {
            _user = null;
            return;
        }

        if (!IsInstanceValid(_user) || GeometryUtils.FlatDistanceSquared(_user.GlobalPosition, GlobalPosition) > GameRules.StationLeaveReach * GameRules.StationLeaveReach)
        {
            _user = null;
            UiPanelStack.CloseAll();
        }
    }
}
