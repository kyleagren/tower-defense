using Godot;
using System.Text;

// Top-left while the local player Alt-looks at a Ritual: what it has done, to judge whether it's
// earning its keep. Totals since it was placed beside the current (or last) wave, then damage per
// mana. The host reads its Ritual directly; a joiner asks the host and shows the latest reply.
public partial class RitualDetailsPanel : PanelContainer
{
    private const float RefreshInterval = 0.5f;
    private static readonly string[] _tallyLabels = { "Damage", "Overkill", "Kills", "Casts" };

    private RichTextLabel _text;
    private Ritual _ritual;
    private float _refreshIn;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(320f, 0f)
        };
        AddChild(_text);

        EventBus.Instance.InspectedRitualChanged += OnInspectedRitualChanged;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.InspectedRitualChanged -= OnInspectedRitualChanged;
    }

    private void OnInspectedRitualChanged(Ritual ritual)
    {
        _ritual = ritual;
        Visible = ritual != null;
        _refreshIn = 0f;
    }

    public override void _Process(double delta)
    {
        if (_ritual == null)
        {
            return;
        }

        if (!IsInstanceValid(_ritual))
        {
            OnInspectedRitualChanged(null);
            return;
        }

        _refreshIn -= (float)delta;

        if (_refreshIn > 0f)
        {
            return;
        }

        _refreshIn = RefreshInterval;

        if (_ritual.IsMultiplayerAuthority())
        {
            ShowStats(_ritual.StatsSync.Snapshot());
        }
        else
        {
            _ritual.StatsSync.Request();
            ShowStats(_ritual.StatsSync.Received);
        }
    }

    private void ShowStats(float[] snapshot)
    {
        StringBuilder text = new StringBuilder();
        text.Append($"[font_size=22]{_ritual.Spell.Name}[/font_size]\n");
        AppendRunes(text);

        if (snapshot == null)
        {
            _text.Text = text.Append("...").ToString();
            return;
        }

        Rune[] runes = _ritual.RuneSlots;
        int perTally = _tallyLabels.Length + runes.Length;

        text.Append("[table=3]");
        AppendRow(text, "", "Total", "Wave");

        for (int i = 0; i < _tallyLabels.Length; i++)
        {
            AppendRow(text, _tallyLabels[i], $"{snapshot[i]:N0}", $"{snapshot[perTally + i]:N0}");
        }

        for (int slot = 0; slot < runes.Length; slot++)
        {
            if (runes[slot] is OnHitRune { EffectLabel: string label })
            {
                int index = _tallyLabels.Length + slot;
                AppendRow(text, label, $"{snapshot[index]:N0}", $"{snapshot[perTally + index]:N0}");
            }
        }

        text.Append("[/table]\n");

        float manaPaid = snapshot[perTally * 2];
        float perMana = manaPaid > 0f ? snapshot[0] / manaPaid : 0f;
        text.Append($"Damage per mana: {perMana:0.#}");

        _text.Text = text.ToString();
    }

    private void AppendRunes(StringBuilder text)
    {
        bool first = true;

        foreach (Rune rune in _ritual.RuneSlots)
        {
            if (rune == null)
            {
                continue;
            }

            text.Append(first ? "" : "  ·  ");
            text.Append($"[color=#{RarityColors.GetText(rune.Rarity).ToHtml(false)}]{rune.Name}[/color]");
            first = false;
        }

        text.Append('\n');
    }

    private static void AppendRow(StringBuilder text, string label, string total, string wave)
    {
        text.Append($"[cell]{label}   [/cell][cell]{total}   [/cell][cell]{wave}[/cell]");
    }
}
