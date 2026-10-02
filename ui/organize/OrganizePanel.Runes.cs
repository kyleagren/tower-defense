using Godot;
using System.Collections.Generic;
using System.Linq;

// The Runes tab: one order for every rune, which each rune wheel bucket shows its runes in. A rune is
// arranged once even when it sits in several buckets.
public partial class OrganizePanel
{
    private ReorderList _runeList;
    private readonly List<Rune> _listedRunes = new();

    private Control BuildRunesPage()
    {
        VBoxContainer page = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);

        page.AddChild(new Label
        {
            Text = "Runes, in the order every rune wheel bucket shows them",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        ScrollContainer scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        page.AddChild(scroll);

        _runeList = new ReorderList { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runeList.Moved += OnRuneMoved;
        scroll.AddChild(_runeList);

        return page;
    }

    private void ShowRunesPage()
    {
        _listedRunes.Clear();
        _listedRunes.AddRange(Profile.InRuneOrder(Profile.HighestOwnedTiers));

        _runeList.SetEntries(_listedRunes.Select(rune => rune.Name), AddRuneBuckets);
        _runeList.FocusFirst();
    }

    // Which buckets the rune shows in, beside it.
    private void AddRuneBuckets(int index, HBoxContainer row)
    {
        IEnumerable<string> buckets = _listedRunes[index].Tags.Where(RuneTags.IsBucket).Select(RuneTags.BucketName);

        row.AddChild(new Label
        {
            Text = string.Join(", ", buckets),
            CustomMinimumSize = new Vector2(360f, 0f),
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    private void OnRuneMoved(int from, int to)
    {
        Rune rune = _listedRunes[from];
        _listedRunes.RemoveAt(from);
        _listedRunes.Insert(to, rune);
        Profile.SetRuneOrder(_listedRunes);
    }

    private bool CancelRunesInProgress()
    {
        if (!_runeList.IsHolding)
        {
            return false;
        }

        _runeList.CancelHold();
        return true;
    }
}
