using Godot;

// Where one seat's player first appears in a level or the hub. Every level needs one per seat.
public partial class PlayerSpawnPoint : Marker3D
{
    [Export] public PlayerId Seat { get; private set; } = PlayerId.P1;

    public override void _Ready()
    {
        LevelContext.Current.SpawnPoints[Seat] = this;
    }
}
