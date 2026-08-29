using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class TagSigilPreview : Control
{
    private static readonly Color ActiveColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color InactiveColor = new Color(0.4f, 0.4f, 0.45f);

    [Export] public HBoxContainer TagButtonRow { get; private set; }
    [Export] public TagAnchorDisplay AnchorDisplay { get; private set; }

    private readonly HashSet<Tag> _activeTags = new HashSet<Tag>();
    private TagSigilCandidatePanel[] _candidatePanels;

    public override void _Ready()
    {
        _candidatePanels = GetNode<Control>("CandidateGrid").GetChildren()
            .OfType<TagSigilCandidatePanel>()
            .ToArray();

        foreach (Tag tag in TagSigilGenerator.AllTags)
        {
            Button button = new Button
            {
                Text = tag.ToString(),
                ToggleMode = true,
                Modulate = InactiveColor
            };
            button.Toggled += pressed =>
            {
                button.Modulate = pressed ? ActiveColor : InactiveColor;
                OnTagToggled(tag, pressed);
            };
            TagButtonRow.AddChild(button);
        }
    }

    private void OnTagToggled(Tag tag, bool active)
    {
        if (active)
        {
            _activeTags.Add(tag);
        }
        else
        {
            _activeTags.Remove(tag);
        }

        AnchorDisplay.SetActiveTags(_activeTags);

        (List<Tag> activeSorted, int[][] pairings) = TagSigilGenerator.GenerateUniquePairings(_activeTags, _candidatePanels.Length);

        for (int i = 0; i < _candidatePanels.Length; i++)
        {
            _candidatePanels[i].SetPairing(activeSorted, pairings[i]);
        }
    }
}
