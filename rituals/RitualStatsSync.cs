using Godot;
using System.Collections.Generic;

// The numbers behind the Ritual details panel. The host counts them (Ritual.Stats); a joiner's open
// panel asks for a snapshot, and nothing is sent otherwise. Every copy knows its own spell and runes,
// so only the numbers travel.
public partial class RitualStatsSync : Node
{
    private Ritual _ritual;

    // The latest snapshot the host sent this copy - null until the first arrives.
    public float[] Received { get; private set; }

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
    }

    // Host only. In a fixed order: since placed, then this wave, each as damage, overkill, kills,
    // casts and one count per rune slot (0 for an empty slot or a rune with nothing to count); then
    // the mana it's cost.
    public float[] Snapshot()
    {
        List<float> values = new List<float>();
        AppendTally(values, _ritual.Stats.SincePlaced);
        AppendTally(values, _ritual.Stats.Wave);
        values.Add(_ritual.ManaPaid);
        return values.ToArray();
    }

    private void AppendTally(List<float> values, RitualStats.Tally tally)
    {
        values.Add(tally.Damage);
        values.Add(tally.Overkill);
        values.Add(tally.Kills);
        values.Add(tally.Casts);

        foreach (Rune rune in _ritual.RuneSlots)
        {
            values.Add(rune is OnHitRune onHit ? tally.RuneEffects.GetValueOrDefault(onHit) : 0f);
        }
    }

    // A copy asks the host for a fresh snapshot; it lands in Received.
    public void Request()
    {
        RpcId(1, MethodName.ReceiveRequest);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveRequest()
    {
        if (NetworkSession.Instance.SenderSeat != default)
        {
            RpcId(Multiplayer.GetRemoteSenderId(), MethodName.ReceiveSnapshot, Snapshot());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveSnapshot(float[] snapshot)
    {
        Received = snapshot;
    }
}
