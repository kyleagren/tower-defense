// A Wind hit's habit of taking on the first element it meets: once something it hits turns out to
// carry a Status, every hit after moves Share of its Wind into that element, for the rest of whatever
// carries this - a sweep, a blade. Wind still leads, so those hits keep swirling.
public class WindInfusion
{
    private readonly float _share;

    public WindInfusion(float share)
    {
        _share = share;
    }

    public Tag? Element { get; private set; }

    // After a hit lands on an enemy that carried statusBefore. True the moment an element is picked
    // up, so the caller can show it.
    public bool TryPickUp(Tag? statusBefore)
    {
        if (Element != null || _share <= 0f || statusBefore is not Tag status)
        {
            return false;
        }

        Element = status;
        return true;
    }

    public DamagePacket Apply(DamagePacket damage)
    {
        if (Element is not Tag element)
        {
            return damage;
        }

        float moved = damage.WindDamage * _share;
        damage.WindDamage -= moved;
        damage.AddElement(element, moved);
        return damage;
    }
}
