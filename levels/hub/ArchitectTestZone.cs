using Godot;

// Covers Room B's footprint in the hub - if the Architect specifically leaves this zone (walked
// back toward Room A), swap back to whichever Sentinel the player actually has selected. A
// Sentinel leaving does nothing; this only reacts to Architect.
public partial class ArchitectTestZone : Area3D
{
    public override void _Ready()
    {
        BodyExited += OnBodyExited;
    }

    private void OnBodyExited(Node3D body)
    {
        // BodyExited fires mid-physics-step, while the scene tree is blocked against structural
        // changes - SelectSentinel's AddChild/QueueFree calls need to happen after that, or Godot
        // rejects them ("Parent node is busy setting up children").
        if (body is Architect architect && architect.IsLocallyControlled)
        {
            CallDeferred(nameof(RespawnSentinel), architect);
        }
    }

    private void RespawnSentinel(Node body)
    {
        // The Architect also "exits" when the hub is torn down around it, with no level left to swap in.
        if (!IsInsideTree())
        {
            return;
        }

        if (body is Architect architect && IsInstanceValid(architect))
        {
            SentinelSpawner spawner = LevelContext.Current.SentinelSpawners[architect.OwnerId];

            if (spawner.Current == architect)
            {
                spawner.SelectSentinel(PlayerProfiles.Get(architect.OwnerId).SelectedCharacter);
            }
        }
    }
}
