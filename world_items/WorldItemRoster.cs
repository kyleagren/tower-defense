using Godot;

// Items players have dropped from their inventories, shared with everyone - how items change hands.
// Loot from a victory isn't here: it exists only for the player it was rolled for until they drop it.
// The host spawns each dropped item on every machine through Spawner and decides who picks it up, so
// two players reaching for it at once can't both get it.
public partial class WorldItemRoster : Node
{
    [Export] public MultiplayerSpawner Spawner { get; private set; }
    [Export] public PackedScene WorldItemScene { get; private set; }

    // Host only: numbers each spawn, so every item's name is unique and identical on every machine.
    private int _spawnCount;

    public override void _Ready()
    {
        LevelContext.Current.WorldItemRoster = this;
        Spawner.SpawnFunction = new Callable(this, MethodName.BuildItem);
    }

    public void Drop(Item item, Vector3 position)
    {
        string data = SaveCodec.EncodeItem(item);

        if (NetworkSession.Instance.IsHost)
        {
            Spawn(data, position);
        }
        else
        {
            RpcId(1, MethodName.ReceiveDrop, data, position);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDrop(string data, Vector3 position)
    {
        if (NetworkSession.Instance.SeatOf(Multiplayer.GetRemoteSenderId()) != default
            && SaveCodec.DecodeItem(data) != null)
        {
            Spawn(data, position);
        }
    }

    // Host only.
    private void Spawn(string data, Vector3 position)
    {
        _spawnCount++;
        Spawner.Spawn(new Godot.Collections.Array { data, position, _spawnCount });
    }

    // Spawner's spawn function - runs on every machine, host included, for each item the host spawns.
    private Node BuildItem(Variant spawnData)
    {
        Godot.Collections.Array args = spawnData.AsGodotArray();

        WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();
        worldItem.Name = $"Item_{args[2].AsInt32()}";
        worldItem.Initialize(SaveCodec.DecodeItem(args[0].AsString()));
        worldItem.IsShared = true;
        worldItem.Position = args[1].AsVector3();
        return worldItem;
    }

    // Only once there's room, so a full inventory never claims an item it can't hold. An item
    // already queued to go was claimed by someone else this frame.
    public void RequestPickUp(WorldItem worldItem)
    {
        if (worldItem.IsQueuedForDeletion() || !PlayerProfiles.Local.CanPickUp(worldItem.Item))
        {
            return;
        }

        if (NetworkSession.Instance.IsHost)
        {
            PlayerProfiles.Local.PickUp(worldItem.Item);
            worldItem.QueueFree();
        }
        else
        {
            RpcId(1, MethodName.ReceivePickUpRequest, worldItem.Name);
        }
    }

    // First come, first served: an item already claimed this frame is queued to go.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePickUpRequest(string itemName)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        WorldItem worldItem = GetNodeOrNull<WorldItem>(itemName);

        if (NetworkSession.Instance.SeatOf(sender) == default || worldItem == null || worldItem.IsQueuedForDeletion())
        {
            return;
        }

        worldItem.QueueFree();
        RpcId(sender, MethodName.ReceivePickUpGranted, SaveCodec.EncodeItem(worldItem.Item));
    }

    // Carries the item itself, since its node here may already have gone. If the room has run out
    // since asking, it goes back on the ground at the player's feet.
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePickUpGranted(string data)
    {
        Item item = SaveCodec.DecodeItem(data);

        if (item != null && !PlayerProfiles.Local.PickUp(item))
        {
            Drop(item, LevelContext.Current.LocalActiveCharacter.GlobalPosition);
        }
    }
}
