using Godot;

public abstract partial class Sentinel : PlayerCharacter
{
    public abstract CharacterId Id { get; }

    public override float Speed => BaseSpeed * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMoveSpeed));
    public override float MaxHealth => BaseMaxHealth * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMaxHealth));
}
