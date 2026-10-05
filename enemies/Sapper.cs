using Godot;

// A tower-killer: rather than chipping at a Ritual, it stands at one and channels, and if it's still
// channelling when the time's up the Ritual breaks outright. Crowd control, a shove or a taunt cuts
// the channel short, and it starts over on its next attack. A red disc grows under it as it channels.
public partial class Sapper : WalkingEnemy
{
    [Export] public float ChannelDuration = 4f;
    [Export] public float ChannelDiscRadius = 1f;

    private static readonly StandardMaterial3D _channelMaterial = PreviewMaterials.CreateFlatOverlay(new Color(1f, 0.3f, 0.1f, 0.6f));

    // Host only - a copy just plays the disc.
    private Ritual _channelTarget;
    private float _channelElapsed = -1f;
    private MeshInstance3D _channelDisc;

    private bool IsChannelling => _channelElapsed >= 0f;

    protected override bool IsCommittedToAttack => IsChannelling;

    public override void _Ready()
    {
        base._Ready();

        _channelDisc = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = ChannelDiscRadius, BottomRadius = ChannelDiscRadius, Height = 0.02f },
            MaterialOverride = _channelMaterial,
            Position = new Vector3(0f, 0.03f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        AddChild(_channelDisc);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!IsChannelling)
        {
            return;
        }

        if (IsSimulatedHere && ChannelBroken())
        {
            StopChannel();
            Rpc(MethodName.ReceiveChannelStopped);
            return;
        }

        _channelElapsed += (float)delta;
        float progress = Mathf.Min(1f, _channelElapsed / ChannelDuration);
        _channelDisc.Scale = new Vector3(progress, 1f, progress);

        if (_channelElapsed < ChannelDuration)
        {
            return;
        }

        if (IsSimulatedHere)
        {
            _channelTarget.Break();
        }

        StopChannel();
    }

    protected override void DealDamage(IDamageable target)
    {
        if (target is not Ritual ritual)
        {
            base.DealDamage(target);
            return;
        }

        if (IsChannelling)
        {
            return;
        }

        StartChannel(ritual);
        Rpc(MethodName.ReceiveChannelStarted);
    }

    public override void Die(Vector3 ragdollImpulse, DeathStyle style)
    {
        StopChannel();
        base.Die(ragdollImpulse, style);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveChannelStarted()
    {
        StartChannel(null);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveChannelStopped()
    {
        StopChannel();
    }

    private bool ChannelBroken() =>
        IsDead || IsActionPrevented || IsKnockedBack || ForcedTarget != null
        || !IsInstanceValid(_channelTarget) || _channelTarget.IsDisabled;

    private void StartChannel(Ritual ritual)
    {
        _channelTarget = ritual;
        _channelElapsed = 0f;
        _channelDisc.Scale = new Vector3(0f, 1f, 0f);
        _channelDisc.Visible = true;
    }

    private void StopChannel()
    {
        _channelTarget = null;
        _channelElapsed = -1f;
        _channelDisc.Visible = false;
    }
}
