using Godot;
using System.Collections.Generic;

// Only the host runs Rituals, so everything their casts put in the world reaches the other machines
// through here. The host decides every target, jump and hit; the others replay what it sends as
// visuals that never deal damage - so a chain jumps to the same enemies on every screen.
public partial class SpellEffects : Node
{
    private LevelContext _level;

    // Host only: numbers each projectile sent out, so its turns and its end find the right copy.
    private int _projectileCount;

    // The copies of the host's projectiles, by the number it gave them.
    private readonly Dictionary<int, Projectile> _projectileCopies = new();

    // Host only: numbers each sweep sent out, so an infusion finds the right copy.
    private int _sweepCount;

    // The copies of the host's sweeps, by the number it gave them.
    private readonly Dictionary<int, GroundSweep> _sweepCopies = new();

    // The copies of the host's standing walls, by the name of the Ritual that raised each.
    private readonly Dictionary<string, IceWall> _wallCopies = new();

    public override void _Ready()
    {
        _level = LevelContext.Current;
        _level.SpellEffects = this;
    }

    public void ShowArc(PackedScene scene, Vector3 from, Vector3 to)
    {
        SpawnArc(scene, from, to);
        Rpc(MethodName.ReceiveArc, scene.ResourcePath, from, to);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveArc(string scenePath, Vector3 from, Vector3 to)
    {
        SpawnArc(GD.Load<PackedScene>(scenePath), from, to);
    }

    private void SpawnArc(PackedScene scene, Vector3 from, Vector3 to)
    {
        ArcVisual visual = scene.Instantiate<ArcVisual>();
        _level.LevelRoot.AddChild(visual);
        visual.Show(from, to);
    }

    public void ShowSlash(PackedScene scene, Vector3 center, Vector3 facing, float tilt, float extraHold)
    {
        SpawnSlash(scene, center, facing, tilt, extraHold);
        Rpc(MethodName.ReceiveSlash, scene.ResourcePath, center, facing, tilt, extraHold);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveSlash(string scenePath, Vector3 center, Vector3 facing, float tilt, float extraHold)
    {
        SpawnSlash(GD.Load<PackedScene>(scenePath), center, facing, tilt, extraHold);
    }

    private void SpawnSlash(PackedScene scene, Vector3 center, Vector3 facing, float tilt, float extraHold)
    {
        SlashVisual visual = scene.Instantiate<SlashVisual>();
        _level.LevelRoot.AddChild(visual);
        visual.Show(center, facing, tilt, extraHold);
    }

    public void ShowExplosion(PackedScene scene, Vector3 center, float radius)
    {
        SpawnExplosion(scene, center, radius);
        Rpc(MethodName.ReceiveExplosion, scene.ResourcePath, center, radius);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveExplosion(string scenePath, Vector3 center, float radius)
    {
        SpawnExplosion(GD.Load<PackedScene>(scenePath), center, radius);
    }

    private void SpawnExplosion(PackedScene scene, Vector3 center, float radius)
    {
        ExplosionVisual visual = scene.Instantiate<ExplosionVisual>();
        _level.LevelRoot.AddChild(visual);
        visual.Show(center, radius);
    }

    // Host only, once the volley is in place. One message for the whole volley: each machine lays it
    // out again through ProjectileCastEffect.Layout, numbering the copies in the same order.
    public void LaunchVolley(List<Projectile> volley, PackedScene scene, Transform3D launch, ProjectileCastData data)
    {
        int firstId = _projectileCount + 1;

        foreach (Projectile projectile in volley)
        {
            _projectileCount++;
            projectile.ReplicaId = _projectileCount;
        }

        Projectile first = volley[0];
        Rpc(MethodName.ReceiveVolleyLaunched, firstId, scene.ResourcePath, launch, data.ProjectileCount, data.Rows,
            data.SpreadAngleDegrees, data.IsNova, first.Speed, first.Radius, first.Length, first.Crosswise);
    }

    public void TurnProjectile(Projectile projectile)
    {
        Rpc(MethodName.ReceiveProjectileTurned, projectile.ReplicaId, projectile.GlobalTransform);
    }

    public void EndProjectile(Projectile projectile)
    {
        Rpc(MethodName.ReceiveProjectileEnded, projectile.ReplicaId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveVolleyLaunched(int firstId, string scenePath, Transform3D launch, int count, int rows,
        float spreadAngleDegrees, bool isNova, float speed, float radius, float length, bool crosswise)
    {
        PackedScene scene = GD.Load<PackedScene>(scenePath);
        int id = firstId;

        foreach (Transform3D start in ProjectileCastEffect.Layout(launch, count, rows, spreadAngleDegrees, isNova))
        {
            Projectile copy = scene.Instantiate<Projectile>();
            copy.InitializeCopy(speed, radius, length, crosswise);
            _level.LevelRoot.AddChild(copy);
            copy.GlobalTransform = start;
            int copyId = id++;
            _projectileCopies[copyId] = copy;
            copy.TreeExiting += () => _projectileCopies.Remove(copyId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveProjectileTurned(int id, Transform3D transform)
    {
        if (_projectileCopies.TryGetValue(id, out Projectile copy))
        {
            copy.GlobalTransform = transform;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveProjectileEnded(int id)
    {
        if (_projectileCopies.Remove(id, out Projectile copy))
        {
            copy.QueueFree();
        }
    }

    // Host only, once the volume is in place. Bombardments and ground effects alike.
    public void ShowVolume(PackedScene scene, Vector3 position, float radius, float duration)
    {
        Rpc(MethodName.ReceiveVolume, scene.ResourcePath, position, radius, duration);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveVolume(string scenePath, Vector3 position, float radius, float duration)
    {
        EffectVolume copy = GD.Load<PackedScene>(scenePath).Instantiate<EffectVolume>();
        _level.LevelRoot.AddChild(copy);
        copy.InitializeCopy(position, radius, duration);
    }

    // Host only, once the beam is in place.
    public void ShowBeam(PackedScene scene, Vector3 origin, Vector3 direction, float length, float halfWidth, float duration)
    {
        Rpc(MethodName.ReceiveBeam, scene.ResourcePath, origin, direction, length, halfWidth, duration);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveBeam(string scenePath, Vector3 origin, Vector3 direction, float length, float halfWidth, float duration)
    {
        Beam copy = GD.Load<PackedScene>(scenePath).Instantiate<Beam>();
        _level.LevelRoot.AddChild(copy);
        copy.InitializeCopy(origin, direction, length, halfWidth, duration);
    }

    // Host only, before the sweep's first hit.
    public void ShowSweep(GroundSweep sweep, PackedScene scene, Vector3 start, Vector3 direction, float speed, float radius, float duration)
    {
        _sweepCount++;
        sweep.ReplicaId = _sweepCount;
        Rpc(MethodName.ReceiveSweep, _sweepCount, scene.ResourcePath, start, direction, speed, radius, duration);
    }

    public void InfuseSweep(GroundSweep sweep, Tag element)
    {
        Rpc(MethodName.ReceiveSweepInfused, sweep.ReplicaId, (int)element);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSweep(int id, string scenePath, Vector3 start, Vector3 direction, float speed, float radius, float duration)
    {
        GroundSweep copy = GD.Load<PackedScene>(scenePath).Instantiate<GroundSweep>();
        _level.LevelRoot.AddChild(copy);
        copy.InitializeCopy(start, direction, speed, radius, duration);
        _sweepCopies[id] = copy;
        copy.TreeExiting += () => _sweepCopies.Remove(id);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSweepInfused(int id, int element)
    {
        if (_sweepCopies.TryGetValue(id, out GroundSweep copy))
        {
            copy.Infuse((Tag)element);
        }
    }

    // Host only. Each machine's copy of the Ritual already holds the wall's placement.
    public void RaiseWall(Ritual caster)
    {
        Rpc(MethodName.ReceiveWallRaised, caster.Name);
    }

    public void BreakWall(Ritual caster)
    {
        Rpc(MethodName.ReceiveWallBroken, caster.Name);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveWallRaised(string casterName)
    {
        Ritual caster = _level.RitualRoster.GetNodeOrNull<Ritual>(casterName);

        if (caster == null)
        {
            return;
        }

        WallCastData data = caster.ResolveWithOwner().Wall;
        IceWall wall = new IceWall();
        caster.SpawnPoint.AddChild(wall);
        wall.Initialize(data.Start, data.End, data);
        _wallCopies[casterName] = wall;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveWallBroken(string casterName)
    {
        if (_wallCopies.Remove(casterName, out IceWall wall) && IsInstanceValid(wall))
        {
            wall.Break();
        }
    }
}
