public struct DamagePacket
{
    public DamageOrigin Origin;
    public float PhysicalDamage;
    public float FireDamage;
    public float WaterDamage;
    public float ColdDamage;
    public float LightningDamage;
    public float WindDamage;

    public readonly float Total => PhysicalDamage + FireDamage + WaterDamage + ColdDamage + LightningDamage + WindDamage;

    public readonly Tag? DominantElement
    {
        get
        {
            Tag? highest = null;
            float highestAmount = 0f;

            if (FireDamage > highestAmount) { highest = Tag.Fire; highestAmount = FireDamage; }
            if (WaterDamage > highestAmount) { highest = Tag.Water; highestAmount = WaterDamage; }
            if (ColdDamage > highestAmount) { highest = Tag.Cold; highestAmount = ColdDamage; }
            if (LightningDamage > highestAmount) { highest = Tag.Lightning; highestAmount = LightningDamage; }

            return highest;
        }
    }

    public static DamagePacket operator *(DamagePacket packet, float scalar)
    {
        return new DamagePacket
        {
            Origin = packet.Origin,
            PhysicalDamage = packet.PhysicalDamage * scalar,
            FireDamage = packet.FireDamage * scalar,
            WaterDamage = packet.WaterDamage * scalar,
            ColdDamage = packet.ColdDamage * scalar,
            LightningDamage = packet.LightningDamage * scalar,
            WindDamage = packet.WindDamage * scalar
        };
    }
}
