using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The rune wheel - bucket ring, rune ring and tier wheel - all opened around the socket being filled,
// with the rune info panel beside it.
public partial class BuildMenu
{
    // The tier wheel: Swap at the top, the tiers rising clockwise from the top-right, Clear on the left.
    private const int TierWheelSlots = 8;
    private const int SwapSlot = 0;
    private const int FirstTierSlot = 1;
    private const int ClearSocketSlot = 6;

    // The rune wheel wraps the socket being filled: its hole is the socket, plus a gap that keeps the
    // socket's highlight border visible.
    private const float RuneRingGap = 4f;
    private const float RuneRingThickness = 150f;

    // The rune info panel sits on the ring's side of the inscription - above for the top sockets,
    // below for the bottom ones - and grows away from the ring when a description runs long.
    private const float RuneInfoNearEdge = 545f;
    private const float RuneInfoFarEdge = 685f;

    private static readonly Rarity[] _runeTiers = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

    private static readonly Color _bucketColor = new Color(0.3f, 0.3f, 0.35f);

    private int _socketBeingAssigned = -1;
    private Tag _openBucket;
    // Null while the bucket's own ring is open.
    private Tag? _openSubBucket;
    private readonly List<Tag> _ringSubBuckets = new();
    private readonly List<Rune> _ringRunes = new();
    private readonly Rune[] _tierRunes = new Rune[5];

    private static IEnumerable<Rune> KnownRunes => PlayerProfiles.Local.Data.KnownRunes.Select(entry => entry.Rune);

    private void PlaceRuneWheelAround(int socket)
    {
        Vector2 center = RitualInscription.SlotOffset(SocketSlot(socket));
        float innerRadius = RitualInscription.SocketDiameter / 2f + RuneRingGap;
        float outerRadius = innerRadius + RuneRingThickness;

        RuneWheel.OffsetLeft = center.X - outerRadius;
        RuneWheel.OffsetRight = center.X + outerRadius;
        RuneWheel.OffsetTop = center.Y - outerRadius;
        RuneWheel.OffsetBottom = center.Y + outerRadius;
        RuneWheel.InnerRadiusFraction = innerRadius / outerRadius;
        PlaceRuneInfoPanel(socket);
    }

    private void PlaceRuneInfoPanel(int socket)
    {
        bool above = RitualInscription.SlotOffset(SocketSlot(socket)).Y < 0f;

        RuneInfoPanel.OffsetTop = above ? -RuneInfoFarEdge : RuneInfoNearEdge;
        RuneInfoPanel.OffsetBottom = above ? -RuneInfoNearEdge : RuneInfoFarEdge;
        RuneInfoPanel.GrowVertical = above ? GrowDirection.Begin : GrowDirection.End;
    }

    // At the highest tier owned - the tier wheel is what picks a lower one - in the player's rune order.
    private IEnumerable<Rune> CompatibleRunesIn(Tag bucket) => PlayerProfiles.Local.InRuneOrder(PlayerProfiles.Local.HighestOwnedTiers
        .Where(rune => rune.Tags.Contains(bucket) && rune.IsCompatibleWith(_build.Spell)));

    // Whether a rune can go in the socket being filled, beside what the other sockets hold.
    private bool IsPickable(Rune rune) =>
        !_build.ConflictsWithOtherSocket(rune, _socketBeingAssigned)
        && RuneCatalog.IsAvailable(rune)
        && !(IsAuthoringToken && IsLimited(rune));

    private void ShowBucketRing(int highlighted = -1)
    {
        SetScreen(Screen.BucketRing);
        PlaceRuneWheelAround(_socketBeingAssigned);

        RuneWheel.SetItems(RuneTags.Buckets.Select(bucket => new RadialWheelItem
        {
            Label = RuneTags.BucketName(bucket),
            Color = _bucketColor,
            Enabled = CompatibleRunesIn(bucket).Any(IsPickable)
        }).ToList(), highlighted);

        ShowRuneScreenInfo(highlighted);
    }

    // A bucket's ring holds its sub-buckets first, then its runes that aren't in one; a sub-bucket's
    // ring holds only its own runes.
    private void ShowRuneRing(Tag bucket, Tag? subBucket = null, Tag? highlightedSubBucket = null)
    {
        SetScreen(Screen.RuneRing);
        PlaceRuneWheelAround(_socketBeingAssigned);
        _openBucket = bucket;
        _openSubBucket = subBucket;
        _ringSubBuckets.Clear();
        _ringRunes.Clear();

        if (subBucket.HasValue)
        {
            _ringRunes.AddRange(CompatibleRunesIn(subBucket.Value));
        }
        else
        {
            IReadOnlyList<Tag> subBuckets = RuneTags.SubBucketsOf(bucket);
            _ringSubBuckets.AddRange(subBuckets.Where(sub => CompatibleRunesIn(sub).Any()));
            _ringRunes.AddRange(CompatibleRunesIn(bucket).Where(rune => !rune.Tags.Any(tag => subBuckets.Contains(tag))));
        }

        int highlighted = highlightedSubBucket.HasValue ? _ringSubBuckets.IndexOf(highlightedSubBucket.Value) : HighlightForSocketedRune();

        IEnumerable<RadialWheelItem> subBucketItems = _ringSubBuckets.Select(sub => new RadialWheelItem
        {
            Label = RuneTags.BucketName(sub),
            Color = _bucketColor,
            Enabled = CompatibleRunesIn(sub).Any(IsPickable)
        });

        IEnumerable<RadialWheelItem> runeItems = _ringRunes.Select(rune => new RadialWheelItem
        {
            Label = rune.Name,
            Color = RarityColors.Get(rune.Rarity),
            Enabled = IsPickable(rune)
        });

        RuneWheel.SetItems(PadToRing(subBucketItems.Concat(runeItems)), highlighted);
        ShowRuneScreenInfo(highlighted);
    }

    // The socketed rune if it's in this ring, otherwise the sub-bucket holding it.
    private int HighlightForSocketedRune()
    {
        Rune current = _build.RuneIn(_socketBeingAssigned);

        if (current == null)
        {
            return -1;
        }

        int runeIndex = _ringRunes.FindIndex(rune => rune.GetType() == current.GetType());
        return runeIndex >= 0 ? _ringSubBuckets.Count + runeIndex : _ringSubBuckets.FindIndex(sub => current.Tags.Contains(sub));
    }

    private void SocketRune(Rune rune)
    {
        _build.SetRune(_socketBeingAssigned, rune);
        ShowInscription(NextEmptySocketSlot(_socketBeingAssigned));
    }

    private void ShowTierWheel()
    {
        SetScreen(Screen.TierWheel);
        PlaceRuneWheelAround(_socketBeingAssigned);
        Rune current = _build.RuneIn(_socketBeingAssigned);
        List<Rune> owned = KnownRunes.Where(rune => rune.GetType() == current.GetType()).ToList();

        RadialWheelItem[] items = new RadialWheelItem[TierWheelSlots];
        items[SwapSlot] = new RadialWheelItem { Label = "Swap", Color = _actionColor };
        items[ClearSocketSlot] = new RadialWheelItem { Label = "Clear", Color = _clearColor };

        for (int i = 0; i < _runeTiers.Length; i++)
        {
            _tierRunes[i] = owned.FirstOrDefault(rune => rune.Rarity == _runeTiers[i]);
            items[FirstTierSlot + i] = new RadialWheelItem
            {
                Label = _runeTiers[i].ToString(),
                Color = RarityColors.Get(_runeTiers[i]),
                Enabled = _tierRunes[i] != null
            };
        }

        int highlighted = FirstTierSlot + Array.IndexOf(_runeTiers, current.Rarity);
        RuneWheel.SetItems(items, highlighted);
        ShowRuneScreenInfo(highlighted);
    }

    private void OnTierWheelCommitted(int slot)
    {
        switch (slot)
        {
            case SwapSlot:
                ShowBucketRing();
                return;

            case ClearSocketSlot:
                _build.SetRune(_socketBeingAssigned, null);
                break;

            default:
                _build.SetRune(_socketBeingAssigned, _tierRunes[slot - FirstTierSlot]);
                break;
        }

        ShowInscription(SocketSlot(_socketBeingAssigned));
    }

    private void ShowRuneScreenInfo(int slot)
    {
        switch (_screen)
        {
            case Screen.BucketRing when slot >= 0:
                ShowBucketInfo(RuneTags.Buckets[slot]);
                break;

            case Screen.RuneRing when slot >= 0 && slot < _ringSubBuckets.Count:
                ShowBucketInfo(_ringSubBuckets[slot]);
                break;

            case Screen.RuneRing when slot >= _ringSubBuckets.Count && slot < _ringSubBuckets.Count + _ringRunes.Count:
                ShowRuneInfo(_ringRunes[slot - _ringSubBuckets.Count]);
                break;

            case Screen.TierWheel when slot == SwapSlot:
                ShowRunePanel("Swap", "", "Choose a different rune for this socket.");
                PreviewSocket(null);
                break;

            case Screen.TierWheel when slot == ClearSocketSlot:
                ShowRunePanel("Clear", "", "Remove this rune.");
                PreviewSocket(null);
                break;

            case Screen.TierWheel when slot >= FirstTierSlot && slot < FirstTierSlot + _runeTiers.Length:
                Rune tier = _tierRunes[slot - FirstTierSlot];

                if (tier != null)
                {
                    ShowRuneInfo(tier);
                }
                else
                {
                    ShowRunePanel(_runeTiers[slot - FirstTierSlot].ToString(), "", "Not owned.");
                    PreviewSocket(null);
                }

                break;

            default:
                ShowRunePanel("", "", "");
                PreviewSocket(null);
                break;
        }
    }

    private void ShowBucketInfo(Tag bucket)
    {
        int runeCount = CompatibleRunesIn(bucket).Count();
        ShowRunePanel(RuneTags.BucketName(bucket), "", runeCount == 1 ? "1 rune" : $"{runeCount} runes");
        PreviewSocket(null);
    }

    private void ShowRuneInfo(Rune rune)
    {
        int costDelta = _build.ManaCostWith(_socketBeingAssigned, rune) - _build.ManaCostWith(_socketBeingAssigned, null);
        ShowRunePanel(rune.Name, $"+{costDelta} mana", rune.Description);
        PreviewSocket(rune);
    }

    private void ShowRunePanel(string title, string cost, string description)
    {
        RuneTitleLabel.Text = title;
        RuneCostLabel.Text = cost;
        RuneDescriptionLabel.Text = description;
        RuneInfoPanel.Visible = title != "";
    }

    // The socket being filled shows what's pointed at, and the inscription's total follows it. Null
    // shows what the socket actually holds.
    private void PreviewSocket(Rune candidate)
    {
        Rune shown = candidate ?? _build.RuneIn(_socketBeingAssigned);
        Inscription.SetSlotLabel(SocketSlot(_socketBeingAssigned), shown?.Name ?? "Empty");
        ShowInscriptionCost(candidate != null ? _build.ManaCostWith(_socketBeingAssigned, candidate) : _build.ManaCost);
    }
}
