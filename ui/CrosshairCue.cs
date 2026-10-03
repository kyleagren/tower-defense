using Godot;

// The local Sentinel's ability cues around the crosshair: shots left for an ability holding the
// attack binding, and a bar per running timer. Polled per frame for the same reason AbilityBar is.
// Every element has a fixed position, so a timer bar doesn't jump when a row of shots appears.
public partial class CrosshairCue : Control
{
    // The empty space kept clear around the crosshair, as if it were this big. Every cue sits
    // outside it.
    private const float ReservedRadius = 40f;
    private const float CueGap = 8f;

    private const float ArcRadius = ReservedRadius + CueGap;
    private const float ArcHalfSpanDegrees = 40f;
    private const float ArcWidth = 6f;
    private const int ArcPointCount = 32;

    // Further out than the arcs, so the shots and bars hang a little below the crosshair.
    private const float BelowOffset = 10f;

    private const float RowTop = ReservedRadius + CueGap + BelowOffset;
    private const float RoundGap = 5f;
    private static readonly Vector2 _roundSize = new Vector2(7f, 22f);

    // Clear of OverdriveElementWheel's ring, which can be open while a timer runs.
    private const float TimerTop = RowTop + 22f + CueGap + 8f;
    private const float TimerSpacing = 13f;
    private static readonly Vector2 _timerSize = new Vector2(120f, 7f);

    private const float OutlineWidth = 1.5f;

    private static readonly Color _spentColor = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color _outlineColor = new Color(0f, 0f, 0f, 0.6f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        Sentinel sentinel = Sentinel.Local;

        if (sentinel == null || LevelContext.Current.LocalActiveCharacter != sentinel)
        {
            return;
        }

        Vector2 center = Size / 2f;
        int timerRow = 0;

        foreach (Ability ability in sentinel.AllAbilities)
        {
            if (!IsInstanceValid(ability))
            {
                continue;
            }

            Color color = ability.CrosshairCueColor;

            if (ability.CrosshairShots is CrosshairShots shots)
            {
                if (shots.Style == CrosshairShotStyle.FlankingArcs)
                {
                    DrawFlankingArcs(center, shots, color);
                }
                else
                {
                    DrawRow(center, shots, color);
                }
            }

            if (ability.HasCrosshairTimer)
            {
                if (ability.CrosshairTimerFraction is float fraction)
                {
                    DrawTimer(center + new Vector2(0f, TimerTop + timerRow * TimerSpacing), fraction, color);
                }

                timerRow++;
            }
        }
    }

    // Spaced evenly around the crosshair starting from the left, so two sit left and right. Spent
    // in order from the left.
    private void DrawFlankingArcs(Vector2 center, CrosshairShots shots, Color color)
    {
        float halfSpan = Mathf.DegToRad(ArcHalfSpanDegrees);
        int spent = shots.Total - shots.Remaining;

        for (int i = 0; i < shots.Total; i++)
        {
            float middle = Mathf.Pi + i * Mathf.Tau / shots.Total;
            DrawArc(center, ArcRadius, middle - halfSpan, middle + halfSpan, ArcPointCount, _outlineColor, ArcWidth + OutlineWidth * 2f, true);
            DrawArc(center, ArcRadius, middle - halfSpan, middle + halfSpan, ArcPointCount, i < spent ? _spentColor : color, ArcWidth, true);
        }
    }

    // Spent from the right.
    private void DrawRow(Vector2 center, CrosshairShots shots, Color color)
    {
        float width = shots.Total * _roundSize.X + (shots.Total - 1) * RoundGap;
        float left = center.X - width / 2f;

        for (int i = 0; i < shots.Total; i++)
        {
            Rect2 round = new Rect2(new Vector2(left + i * (_roundSize.X + RoundGap), center.Y + RowTop), _roundSize);
            DrawRect(round, i < shots.Remaining ? color : _spentColor);
            DrawRect(round, _outlineColor, false, OutlineWidth);
        }
    }

    // Drains from the right.
    private void DrawTimer(Vector2 topCenter, float fraction, Color color)
    {
        Rect2 bar = new Rect2(topCenter - new Vector2(_timerSize.X / 2f, 0f), _timerSize);
        DrawRect(bar, _spentColor);
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp(fraction, 0f, 1f), bar.Size.Y)), color);
        DrawRect(bar, _outlineColor, false, OutlineWidth);
    }
}
