using Godot;

// The build menu's token wheel: the three token slots as round buttons at left, top and right,
// matching the field's d-pad left/up/right and keys 1/2/3. The four directions are weighted so
// pointing splits into top, right and left thirds; down has no weight, so it can't be reached.
public partial class TokenWheel : ButtonWheel
{
    private const int Slots = 4;
    private const int UpSlot = 0;
    private const int RightSlot = 1;
    private const int LeftSlot = 3;
    private const float SlotDistance = 250f;
    private const float TokenDiameter = 200f;

    // Token i sits at _tokenSlots[i].
    private static readonly int[] _tokenSlots = { LeftSlot, UpSlot, RightSlot };

    public static int SlotOf(int token) => _tokenSlots[token];

    // -1 for the unused down slot.
    public static int TokenAt(int slot) => System.Array.IndexOf(_tokenSlots, slot);

    protected override int SlotCount => Slots;

    protected override Vector2 OffsetOf(int slot) =>
        Vector2.FromAngle(-Mathf.Pi / 2f + slot * Mathf.Tau / Slots) * SlotDistance;

    protected override Vector2 SizeOf(int slot) => new Vector2(TokenDiameter, TokenDiameter);
    protected override bool IsRound(int slot) => true;
    protected override float WeightOf(int slot) => slot == UpSlot ? 2f : TokenAt(slot) >= 0 ? 1f : 0f;
    protected override float BackdropRadius => 420f;
    protected override float WedgeInnerRadius => 140f;
    protected override float PointerTravel => 120f;
}
