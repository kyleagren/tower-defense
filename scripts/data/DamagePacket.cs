public struct DamagePacket
{
    public float Physical;
    public float Fire;
    public float Water;
    public float Cold;
    public float Lightning;
    public float Wind;

    public readonly float Total => Physical + Fire + Water + Cold + Lightning + Wind;

    public static DamagePacket operator *(DamagePacket packet, float scalar)
    {
        return new DamagePacket
        {
            Physical = packet.Physical * scalar,
            Fire = packet.Fire * scalar,
            Water = packet.Water * scalar,
            Cold = packet.Cold * scalar,
            Lightning = packet.Lightning * scalar,
            Wind = packet.Wind * scalar
        };
    }
}
