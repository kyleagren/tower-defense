using Godot;
using System.Collections.Generic;
using System.Linq;

// Shares each player's gear with every other machine - only the resolved stat totals, which are all
// anyone else needs (a Ritual scales with its owner's), while the items stay in their owner's save.
// Sent whenever the local player's gear or Sentinel pick changes, and again whenever someone joins.
public partial class ProfileSync : Node
{
    // A received sheet raises InventoryChanged here too, so an unchanged sheet isn't sent again -
    // otherwise every machine would keep answering every other one.
    private string _lastSent;

    public override void _Ready()
    {
        EventBus.Instance.InventoryChanged += SendLocalSheet;
        NetworkSession.Instance.SeatsChanged += OnSeatsChanged;
    }

    private void OnSeatsChanged()
    {
        _lastSent = null;
        SendLocalSheet();
    }

    private void SendLocalSheet()
    {
        if (!PlayerProfiles.IsMultiplayer || PlayerProfiles.LocalId == default)
        {
            return;
        }

        PlayerProfile local = PlayerProfiles.Local;
        CharacterSheet sheet = local.SelectedSheet;

        Godot.Collections.Array payload = new()
        {
            (int)local.SelectedCharacter,
            sheet.Bonuses.Keys.Select(stat => (int)stat).ToArray(),
            sheet.Bonuses.Values.ToArray(),
            sheet.LocalTotals.Keys.Select(stat => (int)stat).ToArray(),
            sheet.LocalTotals.Values.ToArray()
        };

        string signature = GD.VarToStr(payload);

        if (signature == _lastSent)
        {
            return;
        }

        _lastSent = signature;
        Rpc(MethodName.ReceiveSheet, payload);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSheet(Godot.Collections.Array payload)
    {
        PlayerId sender = NetworkSession.Instance.SenderSeat;

        if (sender == default)
        {
            return;
        }

        CharacterSheet sheet = new CharacterSheet(
            ToStats(payload[1].AsInt32Array(), payload[2].AsFloat32Array()),
            ToStats(payload[3].AsInt32Array(), payload[4].AsFloat32Array()));

        PlayerProfiles.Get(sender).ReceiveSheet((CharacterId)payload[0].AsInt32(), sheet);
    }

    private static Dictionary<StatId, float> ToStats(int[] stats, float[] values)
    {
        Dictionary<StatId, float> result = new();

        for (int i = 0; i < stats.Length; i++)
        {
            result[(StatId)stats[i]] = values[i];
        }

        return result;
    }
}
