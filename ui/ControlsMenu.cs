using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Device = InputBindings.Device;

// Options' Controls screen: a tab per device, each row an action, its binding and a reset. Pressing a
// binding listens for the next press to become it; Escape, or ListenTimeout with nothing, cancels. A
// press another live action already uses is refused, naming it - the player rebinds that one first.
public partial class ControlsMenu : Control
{
    public event Action Closed;

    private const double ListenTimeout = 5.0;

    private readonly Dictionary<Device, Button> _tabButtons = new();
    private readonly List<(InputBindings.Row Row, Button Binding, Button Reset)> _shown = new();

    private Device _device;
    private VBoxContainer _rows;
    private Label _status;

    private InputBindings.Row _listeningFor;
    private ulong _listenStartFrame;
    private double _listenRemaining;

    public override void _Ready()
    {
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);

        ColorRect dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        CenterContainer center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        PanelContainer frame = new PanelContainer { CustomMinimumSize = new Vector2(1400f, 1200f) };
        center.AddChild(frame);

        MarginContainer margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        frame.AddChild(margin);

        VBoxContainer column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        margin.AddChild(column);

        Label title = new Label { Text = "Controls" };
        title.AddThemeFontSizeOverride("font_size", 28);
        column.AddChild(title);

        HBoxContainer tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        column.AddChild(tabs);
        AddTab(tabs, Device.KeyboardMouse, "Keyboard & Mouse");
        AddTab(tabs, Device.Controller, "Controller");

        ScrollContainer scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        column.AddChild(scroll);

        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_rows);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_status);

        HBoxContainer bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        bottom.AddThemeConstantOverride("separation", 8);
        column.AddChild(bottom);

        Button resetAll = new Button { Text = "Reset All", CustomMinimumSize = new Vector2(240f, 56f) };
        resetAll.Pressed += ResetAll;
        bottom.AddChild(resetAll);

        Button back = new Button { Text = "Back", CustomMinimumSize = new Vector2(240f, 56f) };
        back.Pressed += Close;
        bottom.AddChild(back);
    }

    private void AddTab(Container tabs, Device device, string text)
    {
        Button button = new Button { Text = text, ToggleMode = true, CustomMinimumSize = new Vector2(320f, 56f) };
        button.Pressed += () => ShowDevice(device);
        tabs.AddChild(button);
        _tabButtons[device] = button;
    }

    public void Open()
    {
        Visible = true;
        UiPanelStack.Push(this, Close);
        ShowDevice(InputDevice.UsingController ? Device.Controller : Device.KeyboardMouse);
    }

    private void ShowDevice(Device device)
    {
        StopListening();
        _device = device;

        foreach ((Device each, Button button) in _tabButtons)
        {
            button.ButtonPressed = each == device;
        }

        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        _shown.Clear();
        string section = null;

        foreach (InputBindings.Row row in InputBindings.Rows.Where(row => row.IsOn(device)))
        {
            if (row.Section != section)
            {
                section = row.Section;
                Label header = new Label { Text = section };
                header.AddThemeFontSizeOverride("font_size", 20);
                _rows.AddChild(header);
            }

            HBoxContainer line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 8);
            line.AddChild(new Label { Text = row.Label, SizeFlagsHorizontal = SizeFlags.ExpandFill });

            Button binding = new Button { CustomMinimumSize = new Vector2(320f, 48f) };
            binding.Pressed += () => StartListening(row, binding);
            line.AddChild(binding);

            Button reset = new Button { Text = "Reset", CustomMinimumSize = new Vector2(160f, 48f) };
            reset.Pressed += () => ResetRow(row);
            line.AddChild(reset);

            _rows.AddChild(line);
            _shown.Add((row, binding, reset));
        }

        RefreshRows();
        _status.Text = "";

        if (_shown.Count > 0)
        {
            _shown[0].Binding.GrabFocus();
        }
    }

    // Every row, since a rebind or reset can change more than the row it was made on.
    private void RefreshRows()
    {
        foreach ((InputBindings.Row row, Button binding, Button reset) in _shown)
        {
            InputEvent current = InputBindings.Binding(row, _device);
            binding.Text = row == _listeningFor ? ListeningText
                : current == null ? "Unbound"
                : InputPromptHelper.NameOf(current);
            reset.Disabled = InputBindings.IsDefault(row, _device);
        }
    }

    private string ListeningText => _device == Device.Controller ? "Press a button..." : "Press a key...";

    private void StartListening(InputBindings.Row row, Button button)
    {
        _listeningFor = row;
        _listenStartFrame = Engine.GetProcessFrames();
        _listenRemaining = ListenTimeout;
        button.Text = ListeningText;
        _status.Text = "Escape cancels.";
    }

    private void StopListening()
    {
        if (_listeningFor == null)
        {
            return;
        }

        _listeningFor = null;
        RefreshRows();
    }

    public override void _Process(double delta)
    {
        if (_listeningFor == null)
        {
            return;
        }

        _listenRemaining -= delta;

        if (_listenRemaining <= 0.0)
        {
            StopListening();
            _status.Text = "";
        }
    }

    // _Input, ahead of every GUI control and gameplay handler: while listening, each press is the
    // listener's alone. The press that started it is skipped by frame - on a controller it arrives as
    // the same button the player might mean to bind.
    public override void _Input(InputEvent @event)
    {
        if (_listeningFor == null || @event is InputEventMouseMotion)
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (!@event.IsPressed() || @event.IsEcho() || Engine.GetProcessFrames() == _listenStartFrame)
        {
            return;
        }

        if (@event is InputEventKey { PhysicalKeycode: Key.Escape })
        {
            StopListening();
            _status.Text = "";
            return;
        }

        InputBindings.Result result = InputBindings.TryRebind(_listeningFor, _device, @event, out InputBindings.Row clash);

        if (result == InputBindings.Result.Unbindable)
        {
            return;
        }

        StopListening();
        _status.Text = result == InputBindings.Result.Clash
            ? $"{InputPromptHelper.NameOf(@event)} is already used by {clash.Label}."
            : "";
    }

    private void ResetRow(InputBindings.Row row)
    {
        InputBindings.Result result = InputBindings.TryReset(row, _device, out InputBindings.Row clash);
        RefreshRows();
        _status.Text = result == InputBindings.Result.Clash
            ? $"{row.Label}'s default is already used by {clash.Label}."
            : "";
    }

    private void ResetAll()
    {
        InputBindings.ResetAll();
        RefreshRows();
        _status.Text = "";
    }

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        StopListening();
        Visible = false;
        UiPanelStack.Pop(this);
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
