using Godot;

// The ring on the ground around a Ritual: it fills toward the next cast in a dimmed element color,
// lights up full when ready, and stays empty while broken or while an ongoing effect stands. The host
// sends the time to the next cast on each cast, and every copy counts it down on its own.
public partial class RitualReadiness : Node
{
    [Export] public MeshInstance3D Ring { get; private set; }
    // How much darker the ring is while the next cast is still coming.
    [Export] public float RecoveringDarken { get; private set; } = 0.55f;

    private Ritual _ritual;
    private Color _color = Colors.White;
    private float _total;
    private float _remaining;
    private bool _ongoing;

    // Every machine, off the countdown: what the Activate prompt reads.
    public bool IsReady => !_ritual.Health.IsBroken && !_ongoing && _remaining <= 0f;

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
        Ring.Visible = false;
    }

    public void Setup(bool shown, Color color)
    {
        Ring.Visible = shown;
        _color = color;
    }

    // Host only. Every copy, this one included, then counts the time down on its own. A hidden ring
    // is hidden on every copy, so there's nothing to tell them.
    public void Broadcast(float duration, bool ongoing)
    {
        if (!Ring.Visible)
        {
            return;
        }

        StartCountdown(duration, ongoing);
        Rpc(MethodName.ReceiveCountdown, duration, ongoing);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveCountdown(float duration, bool ongoing)
    {
        StartCountdown(duration, ongoing);
    }

    private void StartCountdown(float duration, bool ongoing)
    {
        _total = duration;
        _remaining = duration;
        _ongoing = ongoing;
    }

    // A broken Ritual's timer is paused on the host, so the copy pauses too.
    public override void _Process(double delta)
    {
        if (!Ring.Visible)
        {
            return;
        }

        bool broken = _ritual.Health.IsBroken;

        if (!broken && _remaining > 0f)
        {
            _remaining = Mathf.Max(0f, _remaining - (float)delta);
        }

        bool empty = broken || _ongoing;
        bool ready = !empty && _remaining <= 0f;
        float fill = empty ? 0f : _total <= 0f ? 1f : 1f - _remaining / _total;

        Ring.SetInstanceShaderParameter("fill", fill);
        Ring.SetInstanceShaderParameter("lit_color", ready ? _color : _color.Darkened(RecoveringDarken));
    }
}
