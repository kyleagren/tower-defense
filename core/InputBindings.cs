using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The player's own bindings over the Input Map's defaults. Gameplay only ever names actions, so a
// rebind is an Input Map edit, and every prompt, badge and hint follows on its own. Only what differs
// from the defaults is saved (user://controls.cfg), so an action added later keeps its default. An
// autoload listed first, so the saved bindings are in place before anything reads input.
public partial class InputBindings : Node
{
    public enum Device
    {
        KeyboardMouse,
        Controller
    }

    // When an action is live. Two rows clash only if they share one of these on the same device -
    // which is what lets Q be the build menu in Build and token mode in Combat, or the d-pad be
    // repair in the field and tokens under R1.
    [Flags]
    public enum Context
    {
        None = 0,
        BuildField = 1 << 0,
        CombatField = 1 << 1,
        Placement = 1 << 2,

        // Token pick mode and authoring, where the abilities sharing 1/2/3 stand aside.
        Tokens = 1 << 3,

        // The inventory and the stations beside it.
        Menus = 1 << 4,
        AbilityChord = 1 << 5,
        TokenChord = 1 << 6,
        Field = BuildField | CombatField
    }

    public enum Result
    {
        Applied,
        Clash,
        Unbindable
    }

    // One line on the Controls screen. Its actions move together - Interact and Pick Up are one key on
    // purpose, and never drift apart. A row with no actions for a device isn't on that tab.
    public sealed class Row
    {
        public string Label { get; }
        public string Section { get; }
        private readonly string[] _keyboardActions;
        private readonly string[] _controllerActions;
        private readonly Context _keyboardContexts;
        private readonly Context _controllerContexts;

        public Row(string section, string label, string[] keyboard, Context keyboardContexts,
            string[] controller, Context controllerContexts)
        {
            Section = section;
            Label = label;
            _keyboardActions = keyboard;
            _keyboardContexts = keyboardContexts;
            _controllerActions = controller;
            _controllerContexts = controllerContexts;
        }

        public IReadOnlyList<string> Actions(Device device) => device == Device.Controller ? _controllerActions : _keyboardActions;

        public Context Contexts(Device device) => device == Device.Controller ? _controllerContexts : _keyboardContexts;

        public bool IsOn(Device device) => Actions(device).Count > 0;
    }

    private const string FilePath = "user://controls.cfg";

    private static readonly string[] _none = Array.Empty<string>();

    private static Row Both(string section, string label, Context contexts, params string[] actions) =>
        new(section, label, actions, contexts, actions, contexts);

    private static Row KeyboardOnly(string section, string label, Context contexts, params string[] actions) =>
        new(section, label, actions, contexts, _none, Context.None);

    private static Row ControllerOnly(string section, string label, Context contexts, params string[] actions) =>
        new(section, label, _none, Context.None, actions, contexts);

    // Look, the radial-wheel stick and ui_* aren't here: the sticks are sensitivity settings, and menu
    // navigation, confirm and back stay fixed. Pause on keyboard is Escape, which is ui_cancel.
    public static readonly IReadOnlyList<Row> Rows = new[]
    {
        KeyboardOnly("Movement", "Move Forward", Context.Field, "move_forward"),
        KeyboardOnly("Movement", "Move Back", Context.Field, "move_back"),
        KeyboardOnly("Movement", "Move Left", Context.Field, "move_left"),
        KeyboardOnly("Movement", "Move Right", Context.Field, "move_right"),
        Both("Movement", "Jump", Context.Field, "jump"),
        Both("Movement", "Sprint", Context.Field, "sprint"),
        Both("Movement", "Dodge", Context.Field, "dodge"),

        Both("Combat", "Attack", Context.Field, "attack"),
        Both("Combat", "Alt Action", Context.Field | Context.Placement, "alt_action"),
        new("Combat", "Ability 1", new[] { "ability_1" }, Context.Field, new[] { "ability_1" }, Context.AbilityChord),
        new("Combat", "Ability 2", new[] { "ability_2" }, Context.Field, new[] { "ability_2" }, Context.AbilityChord),
        new("Combat", "Ability 3", new[] { "ability_3" }, Context.Field, new[] { "ability_3" }, Context.AbilityChord),
        new("Combat", "Ability 4", new[] { "ability_4" }, Context.Field, new[] { "ability_4" }, Context.AbilityChord),
        ControllerOnly("Combat", "Ability Modifier", Context.Field, "ability_modifier"),
        Both("Combat", "Interact / Pick Up", Context.Field, "interact", "pickup_item"),

        Both("Rituals", "Build Menu", Context.BuildField, "open_build_menu"),
        Both("Rituals", "Confirm Placement", Context.Placement, "confirm_placement"),
        Both("Rituals", "Repair", Context.Field, "repair_ritual"),
        Both("Rituals", "Destroy", Context.Field, "destroy_ritual"),
        Both("Rituals", "Ready Up", Context.Field, "start_wave"),
        KeyboardOnly("Rituals", "Token Mode", Context.CombatField, "toggle_token_mode"),
        ControllerOnly("Rituals", "Token Modifier", Context.Field, "token_modifier"),
        new("Rituals", "Token 1", new[] { "token_slot_1" }, Context.Tokens, new[] { "token_slot_1" }, Context.TokenChord),
        new("Rituals", "Token 2", new[] { "token_slot_2" }, Context.Tokens, new[] { "token_slot_2" }, Context.TokenChord),
        new("Rituals", "Token 3", new[] { "token_slot_3" }, Context.Tokens, new[] { "token_slot_3" }, Context.TokenChord),

        Both("Menus", "Inventory", Context.Field | Context.Menus, "toggle_inventory"),
        Both("Menus", "Character Sheet", Context.Field, "character_sheet"),
        ControllerOnly("Menus", "Pause", Context.Field | Context.Menus, "pause"),
        Both("Menus", "Use Item", Context.Menus, "inventory_use"),
        Both("Menus", "Quick Move", Context.Menus, "quick_move"),
        Both("Menus", "Details", Context.Field | Context.Menus, "advanced_tooltip"),
        KeyboardOnly("Menus", "Compare / Quick Equip", Context.Menus, "compare_item", "quick_equip"),
        ControllerOnly("Menus", "Compare", Context.Menus, "compare_item")
    };

    // Each action's project.godot binding per device, read before anything is applied.
    private static readonly Dictionary<(string, Device), InputEvent> _defaults = new();

    public override void _EnterTree()
    {
        CaptureDefaults();
        Load();
    }

    public static InputEvent Binding(Row row, Device device) => CurrentEvent(row.Actions(device)[0], device);

    public static bool IsDefault(Row row, Device device) => Same(Binding(row, device), Default(row, device));

    // A clash leaves everything as it was; clashWith says which row already has it.
    public static Result TryRebind(Row row, Device device, InputEvent pressed, out Row clashWith)
    {
        clashWith = null;
        InputEvent binding = Normalize(pressed);

        if (binding == null || DeviceOf(binding) != device)
        {
            return Result.Unbindable;
        }

        return TrySet(row, device, binding, out clashWith);
    }

    public static Result TryReset(Row row, Device device, out Row clashWith) =>
        TrySet(row, device, Default(row, device), out clashWith);

    // Every default together, so nothing can clash.
    public static void ResetAll()
    {
        foreach (Row row in Rows)
        {
            foreach (Device device in Enum.GetValues<Device>())
            {
                if (row.IsOn(device))
                {
                    Apply(row, device, Default(row, device));
                }
            }
        }

        Save();
    }

    private static Result TrySet(Row row, Device device, InputEvent binding, out Row clashWith)
    {
        clashWith = Rows.FirstOrDefault(other => other != row && other.IsOn(device)
            && (other.Contexts(device) & row.Contexts(device)) != 0
            && Same(Binding(other, device), binding));

        if (clashWith != null)
        {
            return Result.Clash;
        }

        Apply(row, device, binding);
        Save();
        return Result.Applied;
    }

    private static InputEvent Default(Row row, Device device) => _defaults.GetValueOrDefault((row.Actions(device)[0], device));

    private static void CaptureDefaults()
    {
        foreach (Row row in Rows)
        {
            foreach (Device device in Enum.GetValues<Device>())
            {
                foreach (string action in row.Actions(device))
                {
                    _defaults[(action, device)] = CurrentEvent(action, device);
                }
            }
        }
    }

    private static InputEvent CurrentEvent(string action, Device device) =>
        InputMap.ActionGetEvents(action).FirstOrDefault(binding => DeviceOf(binding) == device);

    // Replaces the device's binding on every action in the row, leaving the other device's alone.
    private static void Apply(Row row, Device device, InputEvent binding)
    {
        foreach (string action in row.Actions(device))
        {
            foreach (InputEvent existing in InputMap.ActionGetEvents(action).Where(each => DeviceOf(each) == device))
            {
                InputMap.ActionEraseEvent(action, existing);
            }

            if (binding != null)
            {
                InputMap.ActionAddEvent(action, (InputEvent)binding.Duplicate());
            }
        }
    }

    // Keyed by device and the row's first action - a renamed or removed action is simply skipped.
    private static void Save()
    {
        ConfigFile file = new ConfigFile();

        foreach (Row row in Rows)
        {
            foreach (Device device in Enum.GetValues<Device>())
            {
                if (row.IsOn(device) && !IsDefault(row, device) && Encode(Binding(row, device)) is { } encoded)
                {
                    file.SetValue(SectionFor(device), row.Actions(device)[0], encoded);
                }
            }
        }

        Error result = file.Save(FilePath);

        if (result != Error.Ok)
        {
            GD.PushError($"InputBindings: failed to save {FilePath} ({result}).");
        }
    }

    private static void Load()
    {
        ConfigFile file = new ConfigFile();

        if (file.Load(FilePath) != Error.Ok)
        {
            return;
        }

        foreach (Row row in Rows)
        {
            foreach (Device device in Enum.GetValues<Device>())
            {
                if (!row.IsOn(device) || !file.HasSectionKey(SectionFor(device), row.Actions(device)[0]))
                {
                    continue;
                }

                InputEvent binding = Decode(file.GetValue(SectionFor(device), row.Actions(device)[0]).AsGodotDictionary());

                if (binding != null && DeviceOf(binding) == device)
                {
                    Apply(row, device, binding);
                }
            }
        }
    }

    private static string SectionFor(Device device) => device == Device.Controller ? "controller" : "keyboard_mouse";

    private static Device? DeviceOf(InputEvent binding) => binding switch
    {
        InputEventKey or InputEventMouseButton => Device.KeyboardMouse,
        InputEventJoypadButton or InputEventJoypadMotion => Device.Controller,
        _ => null
    };

    // A clean copy of a press, holding only what identifies the input - or null for one that can't be
    // a binding. Only the triggers count as axes: a stick nudged while listening shouldn't become one.
    private static InputEvent Normalize(InputEvent pressed) => pressed switch
    {
        InputEventKey key when key.PhysicalKeycode != Key.None => new InputEventKey { PhysicalKeycode = key.PhysicalKeycode },
        InputEventMouseButton mouse => new InputEventMouseButton { ButtonIndex = mouse.ButtonIndex },
        InputEventJoypadButton button => new InputEventJoypadButton { ButtonIndex = button.ButtonIndex },
        InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } motion when motion.AxisValue > 0.5f =>
            new InputEventJoypadMotion { Axis = motion.Axis, AxisValue = 1f },
        _ => null
    };

    private static bool Same(InputEvent a, InputEvent b) => (a, b) switch
    {
        (InputEventKey x, InputEventKey y) => x.PhysicalKeycode == y.PhysicalKeycode,
        (InputEventMouseButton x, InputEventMouseButton y) => x.ButtonIndex == y.ButtonIndex,
        (InputEventJoypadButton x, InputEventJoypadButton y) => x.ButtonIndex == y.ButtonIndex,
        (InputEventJoypadMotion x, InputEventJoypadMotion y) => x.Axis == y.Axis && Mathf.Sign(x.AxisValue) == Mathf.Sign(y.AxisValue),
        (null, null) => true,
        _ => false
    };

    // Plain values rather than a serialized InputEvent, so the file can never carry a script.
    private static Godot.Collections.Dictionary Encode(InputEvent binding) => binding switch
    {
        InputEventKey key => new() { ["type"] = "key", ["code"] = (long)key.PhysicalKeycode },
        InputEventMouseButton mouse => new() { ["type"] = "mouse", ["code"] = (long)mouse.ButtonIndex },
        InputEventJoypadButton button => new() { ["type"] = "button", ["code"] = (long)button.ButtonIndex },
        InputEventJoypadMotion motion => new() { ["type"] = "axis", ["code"] = (long)motion.Axis, ["sign"] = Mathf.Sign(motion.AxisValue) },
        _ => null
    };

    private static InputEvent Decode(Godot.Collections.Dictionary data)
    {
        long code = data.TryGetValue("code", out Variant value) ? value.AsInt64() : -1;

        return data.TryGetValue("type", out Variant type) ? type.AsString() switch
        {
            "key" => new InputEventKey { PhysicalKeycode = (Key)code },
            "mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)code },
            "button" => new InputEventJoypadButton { ButtonIndex = (JoyButton)code },
            "axis" => new InputEventJoypadMotion
            {
                Axis = (JoyAxis)code,
                AxisValue = data.TryGetValue("sign", out Variant sign) ? sign.AsInt32() : 1
            },
            _ => null
        } : null;
    }
}
