using Godot;

public partial class WallCastEffect : CastEffect
{
    public override bool CanHandle(SpellCastContext context) => context.Wall != null;

    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        if (context.Wall == null || caster.WallStart == null || caster.WallEnd == null)
        {
            return;
        }

        IceWall wall = new IceWall();
        spawnPoint.AddChild(wall);
        wall.Initialize(caster.WallStart.Value, caster.WallEnd.Value, context.Wall);
    }
}
