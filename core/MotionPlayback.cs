using Godot;
using System;
using System.Collections.Generic;

// Smooths a copied body's movement. On the owning machine the synchronizer reads the body's position,
// facing and the time through this node; everywhere else it keeps the last few of those snapshots and
// shows the body where it was PlaybackDelay ago, blended between the snapshots either side of that
// moment. Stamped with the sender's clock rather than arrival time, so uneven arrival can't wobble the
// speed. Only yaw is played back - bodies never pitch or roll.
public partial class MotionPlayback : Node
{
    // Two update intervals plus how unevenly updates are arriving - enough that the snapshot after the
    // moment being shown has almost always arrived, and no more. Measured as it goes, so a smooth LAN
    // plays back tighter than a jittery connection.
    private const double MinPlaybackDelay = 0.03;
    private const double MaxPlaybackDelay = 0.15;
    // How quickly the measured jitter forgets an old spike.
    private const double JitterDecayPerSecond = 0.5;
    // How fast the clock offset creeps back up between updates, so it follows two clocks drifting
    // apart instead of holding the smallest gap ever seen. Far faster than any real clock drift, far
    // slower than jitter.
    private const double ClockOffsetRelaxPerSecond = 0.001;

    [Export] public MultiplayerSynchronizer Synchronizer { get; private set; }

    private readonly List<(double Time, Vector3 Position, float Yaw)> _snapshots = new();
    private Node3D _body;
    private double _clockOffset = double.MaxValue;
    private double _updateInterval = 1.0 / 30.0;
    private double _jitter;
    private double _lastUpdateTime;
    private double _receivedTime;
    private Vector3 _receivedPosition;
    private float _receivedYaw;
    private bool _stopped;

    public double SyncedTime
    {
        get => Now;
        set => _receivedTime = value;
    }

    public Vector3 SyncedPosition
    {
        get => Body.GlobalPosition;
        set => _receivedPosition = value;
    }

    public float SyncedYaw
    {
        get => Body.Rotation.Y;
        set => _receivedYaw = value;
    }

    private Node3D Body => _body ??= GetParent<Node3D>();

    private double PlaybackDelay => Math.Clamp(2.0 * _updateInterval + _jitter, MinPlaybackDelay, MaxPlaybackDelay);

    private static double Now => Time.GetTicksUsec() / 1_000_000.0;

    public override void _Ready()
    {
        if (IsMultiplayerAuthority())
        {
            SetProcess(false);
            return;
        }

        // Its own process mode rather than the body's: another player's body is a puppet that
        // doesn't process, and this still has to move it.
        ProcessMode = ProcessModeEnum.Pausable;
        Synchronizer.Synchronized += OnSynchronized;
    }

    // Stops moving the body - a copy that died, handing over to its ragdoll. Kept rather than
    // freed: an update already on its way still needs somewhere to land.
    public void Stop()
    {
        _stopped = true;
        SetProcess(false);
    }

    // After a whole update has landed, so the three values are always from the same moment.
    private void OnSynchronized()
    {
        if (_stopped)
        {
            return;
        }

        double now = Now;

        // The smallest recent gap is the update that spent least time in transit - the best estimate
        // of how the two clocks line up.
        _clockOffset = Math.Min(_clockOffset + ClockOffsetRelaxPerSecond * (now - _lastUpdateTime), now - _receivedTime);

        // How late this update is compared to the fastest one seen - arrival jitter. Spikes are kept
        // at once and fade slowly, so one late packet widens the buffer rather than stalling playback.
        double lateness = now - _receivedTime - _clockOffset;
        _jitter = Math.Max(lateness, _jitter * Math.Exp(-JitterDecayPerSecond * (now - _lastUpdateTime)));
        _lastUpdateTime = now;

        if (_snapshots.Count > 0 && _receivedTime <= _snapshots[^1].Time)
        {
            return;
        }

        if (_snapshots.Count > 0)
        {
            _updateInterval = Mathf.Lerp(_updateInterval, _receivedTime - _snapshots[^1].Time, 0.1);
        }

        _snapshots.Add((_receivedTime, _receivedPosition, _receivedYaw));
    }

    public override void _Process(double delta)
    {
        if (_snapshots.Count == 0)
        {
            return;
        }

        double showing = Now - _clockOffset - PlaybackDelay;

        while (_snapshots.Count > 2 && _snapshots[1].Time <= showing)
        {
            _snapshots.RemoveAt(0);
        }

        (double Time, Vector3 Position, float Yaw) from = _snapshots[0];
        (double Time, Vector3 Position, float Yaw) to = _snapshots.Count > 1 ? _snapshots[1] : from;

        // Holds at the newest snapshot rather than guessing past it.
        float weight = to.Time > from.Time ? (float)Math.Clamp((showing - from.Time) / (to.Time - from.Time), 0.0, 1.0) : 1f;

        Body.GlobalPosition = from.Position.Lerp(to.Position, weight);
        Body.Rotation = new Vector3(0f, Mathf.LerpAngle(from.Yaw, to.Yaw, weight), 0f);
    }
}
