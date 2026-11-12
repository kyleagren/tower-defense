using Godot;
using System.Threading.Tasks;

// Covers the screen while a level switches, with a bar and a line of status. Every machine shows its
// own, and while it's up it swallows all input, so nobody can ready up or act in a level someone else
// is still loading.
public partial class LoadingScreen : CanvasLayer
{
    [Export] public Control Cover { get; private set; }
    [Export] public ProgressBar Bar { get; private set; }
    [Export] public Label Status { get; private set; }
    [Export] public float FadeTime { get; private set; } = 0.25f;
    // How quickly the bar catches up to the real progress - higher is snappier.
    [Export] public float BarCatchUpRate { get; private set; } = 8f;

    private Tween _fade;
    private float _targetAlpha;
    private float _progress;

    public override void _Ready()
    {
        SetCoverAlpha(0f);
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        Bar.Value = Mathf.Lerp((float)Bar.Value, _progress, 1f - Mathf.Exp(-BarCatchUpRate * (float)delta));
    }

    public void Begin(string status)
    {
        _progress = 0f;
        Bar.Value = 0f;
        Status.Text = status;
    }

    // Never moves the bar backwards.
    public void SetProgress(float progress) => _progress = Mathf.Max(_progress, progress);

    public void SetStatus(string status) => Status.Text = status;

    public async Task CoverScreen()
    {
        Visible = true;
        await FadeTo(1f);
    }

    public void CoverInstantly()
    {
        _fade?.Kill();
        _targetAlpha = 1f;
        SetCoverAlpha(1f);
        Visible = true;
    }

    public async Task UncoverScreen()
    {
        await FadeTo(0f);

        // A cover may have started while this faded out.
        if (_targetAlpha == 0f)
        {
            Visible = false;
        }
    }

    // Waits on a timer rather than the tween's finished signal: a fade cut short by the next one never
    // finishes, and whoever awaited it would wait forever.
    private async Task FadeTo(float alpha)
    {
        _fade?.Kill();
        _targetAlpha = alpha;
        _fade = CreateTween();
        _fade.TweenProperty(Cover, "modulate:a", alpha, FadeTime);
        await ToSignal(GetTree().CreateTimer(FadeTime, processAlways: true), SceneTreeTimer.SignalName.Timeout);
    }

    private void SetCoverAlpha(float alpha) => Cover.Modulate = new Color(Cover.Modulate, alpha);
}
