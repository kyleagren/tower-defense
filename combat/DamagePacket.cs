public struct DamagePacket
{
    public DamageOrigin Origin;
    public DamageSource Source;
    public bool IsCrit;

    // Bypasses mitigation. Inert today - Enemy.TakeDamage subtracts Total directly and nothing
    // reduces damage anywhere - so this exists for the resistance layer that doesn't exist yet:
    // a mirrored Health Link hit is a copy of damage that was already mitigated once on the way
    // in, and must not be mitigated a second time on the way out.
    public bool IsTrueDamage;

    public float PhysicalDamage;
    public float FireDamage;
    public float WaterDamage;
    public float ColdDamage;
    public float LightningDamage;
    public float WindDamage;

    public readonly float Total => PhysicalDamage + FireDamage + WaterDamage + ColdDamage + LightningDamage + WindDamage;

    // Flattened for an RPC, which can't carry a C# struct: the six amounts, then Origin, Source,
    // IsCrit and IsTrueDamage as numbers.
    public readonly float[] ToNetwork() => new[]
    {
        PhysicalDamage, FireDamage, WaterDamage, ColdDamage, LightningDamage, WindDamage,
        (float)Origin, (float)Source, IsCrit ? 1f : 0f, IsTrueDamage ? 1f : 0f
    };

    public static DamagePacket FromNetwork(float[] data) => new DamagePacket
    {
        PhysicalDamage = data[0],
        FireDamage = data[1],
        WaterDamage = data[2],
        ColdDamage = data[3],
        LightningDamage = data[4],
        WindDamage = data[5],
        Origin = (DamageOrigin)(int)data[6],
        Source = (DamageSource)(int)data[7],
        IsCrit = data[8] != 0f,
        IsTrueDamage = data[9] != 0f
    };

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

    // Whichever of the six deals the most, Physical and Wind included - null when Physical does, ties
    // included.
    public readonly Tag? LeadingElement
    {
        get
        {
            Tag? leading = null;
            float leadingAmount = PhysicalDamage;

            if (FireDamage > leadingAmount) { leading = Tag.Fire; leadingAmount = FireDamage; }
            if (WaterDamage > leadingAmount) { leading = Tag.Water; leadingAmount = WaterDamage; }
            if (ColdDamage > leadingAmount) { leading = Tag.Cold; leadingAmount = ColdDamage; }
            if (LightningDamage > leadingAmount) { leading = Tag.Lightning; leadingAmount = LightningDamage; }
            if (WindDamage > leadingAmount) { leading = Tag.Wind; }

            return leading;
        }
    }

    // Physical isn't a Tag, so only the elemental fields are reachable through this.
    public void AddElement(Tag element, float amount)
    {
        switch (element)
        {
            case Tag.Fire:
                FireDamage += amount;
                break;
            case Tag.Water:
                WaterDamage += amount;
                break;
            case Tag.Cold:
                ColdDamage += amount;
                break;
            case Tag.Lightning:
                LightningDamage += amount;
                break;
            case Tag.Wind:
                WindDamage += amount;
                break;
            default:
                throw new System.ArgumentException($"{element} is not a damage element.", nameof(element));
        }
    }

    public readonly float Get(Tag element) => element switch
    {
        Tag.Fire => FireDamage,
        Tag.Water => WaterDamage,
        Tag.Cold => ColdDamage,
        Tag.Lightning => LightningDamage,
        Tag.Wind => WindDamage,
        _ => throw new System.ArgumentException($"{element} is not a damage element.", nameof(element))
    };

    public static DamagePacket operator *(DamagePacket packet, float scalar)
    {
        return new DamagePacket
        {
            Origin = packet.Origin,
            Source = packet.Source,
            IsCrit = packet.IsCrit,
            IsTrueDamage = packet.IsTrueDamage,
            PhysicalDamage = packet.PhysicalDamage * scalar,
            FireDamage = packet.FireDamage * scalar,
            WaterDamage = packet.WaterDamage * scalar,
            ColdDamage = packet.ColdDamage * scalar,
            LightningDamage = packet.LightningDamage * scalar,
            WindDamage = packet.WindDamage * scalar
        };
    }
}
