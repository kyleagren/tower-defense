// Marks everything standing in it with Water, without a hit - so nothing reacts to it, and nothing
// is damaged.
public partial class RainField : GroundEffect
{
    protected override void OnTick(Enemy enemy)
    {
        enemy.SetStatus(Tag.Water);
    }
}
