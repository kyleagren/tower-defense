// Applied by ChilledGround to anything standing on it. Its own type rather than a ChillAilment, so
// frozen terrain and a chilling hit stack instead of competing for one slot.
public class ChilledGroundAilment : SlowAilment
{
    public ChilledGroundAilment(float slowPercent, float duration) : base(slowPercent, duration)
    {
    }
}
