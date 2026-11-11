using Godot;

// How close a Ritual is to its next cast, drawn by RitualVisual as the rune circle's outer ring. The host
// sends the time to the next cast on each cast, and every copy counts it down on its own.
public partial class RitualReadiness : Node
{
    private Ritual _ritual;
    private bool _tracked;
    private float _total;
    private float _remaining;
    private bool _ongoing;

    // Every machine, off the countdown: what the Activate prompt reads.
    public bool IsReady => !_ritual.Health.IsBroken && !_ongoing && _remaining <= 0f;

    // 0 just cast, 1 ready. Empty while broken or while an ongoing effect stands.
    public float Fill { get; private set; } = 1f;

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
    }

    // A Ritual firing on cast time alone would just flicker the ring, so only one with a cooldown is
    // tracked. Any other reads full whenever it isn't broken.
    public void Setup(bool tracked)
    {
        _tracked = tracked;
    }

    // Host only. Every copy, this one included, then counts the time down on its own. An untracked
    // Ritual is untracked on every copy, so there's nothing to tell them.
    public void Broadcast(float duration, bool ongoing)
    {
        if (!_tracked)
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
        bool broken = _ritual.Health.IsBroken;

        if (!_tracked)
        {
            Fill = broken ? 0f : 1f;
            return;
        }

        if (!broken && _remaining > 0f)
        {
            _remaining = Mathf.Max(0f, _remaining - (float)delta);
        }

        bool empty = broken || _ongoing;
        Fill = empty ? 0f : _total <= 0f ? 1f : 1f - _remaining / _total;
    }
}
