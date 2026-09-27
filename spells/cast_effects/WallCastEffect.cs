using Godot;

public class WallCastEffect : CastEffect
{
    public override bool IsOngoing => true;

    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        IceWall wall = new IceWall();
        spawnPoint.AddChild(wall);
        wall.Initialize(context.Wall.Start, context.Wall.End, context.Wall);

        // The wall lives under the caster's spawn point, so it can't outlive the caster to call
        // back into a freed Ritual.
        wall.Expired += caster.ConcludeOngoingEffect;
        wall.Destroyed += caster.ConcludeOngoingEffect;

        SpellEffects effects = LevelContext.Current.SpellEffects;
        effects.RaiseWall(caster);
        wall.Destroyed += () => effects.BreakWall(caster);
    }
}
