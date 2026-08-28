using Godot;
using System.Collections.Generic;

public partial class TagSigilPreview : Control
{
    [Export] public HBoxContainer TagButtonRow { get; private set; }
    [Export] public TagAnchorDisplay AnchorDisplay { get; private set; }
    [Export] public TagSigilCandidatePanel CandidatePanelA { get; private set; }
    [Export] public TagSigilCandidatePanel CandidatePanelB { get; private set; }
    [Export] public TagSigilCandidatePanel CandidatePanelC { get; private set; }
    [Export] public TagSigilCandidatePanel CandidatePanelD { get; private set; }

    private readonly HashSet<Tag> _activeTags = new HashSet<Tag>();
    private readonly List<Tag> _activationOrder = new List<Tag>();
    private TagSigilCandidatePanel[] _candidatePanels;

    public override void _Ready()
    {
        _candidatePanels = new[] { CandidatePanelA, CandidatePanelB, CandidatePanelC, CandidatePanelD };

        foreach (Tag tag in TagSigilGenerator.AllTags)
        {
            Button button = new Button
            {
                Text = tag.ToString(),
                ToggleMode = true
            };
            button.Toggled += pressed => OnTagToggled(tag, pressed);
            TagButtonRow.AddChild(button);
        }
    }

    private void OnTagToggled(Tag tag, bool active)
    {
        if (active)
        {
            _activeTags.Add(tag);
            _activationOrder.Add(tag);
        }
        else
        {
            _activeTags.Remove(tag);
            _activationOrder.Remove(tag);
        }

        AnchorDisplay.SetActiveTags(_activeTags);

        foreach (TagSigilCandidatePanel panel in _candidatePanels)
        {
            panel.Refresh(_activeTags, _activationOrder);
        }
    }
}
