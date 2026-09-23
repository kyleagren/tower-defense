using Godot;

public partial class WallCastEffect : CastEffect
{
    public override bool CanHandle(SpellCastContext context) => context.Wall != null;
    public override bool IsOngoing => true;

    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        if (context.Wall == null)
        {
            return;
        }

        IceWall wall = new IceWall();
        spawnPoint.AddChild(wall);
        wall.Initialize(context.Wall.Start, context.Wall.End, context.Wall);

        wall.Expired += () => EmitSignal(SignalName.Concluded);
        wall.Destroyed += () => EmitSignal(SignalName.Concluded);
    }
}
