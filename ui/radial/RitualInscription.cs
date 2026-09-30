using Godot;

// The build menu's Ritual Inscription, where runes are inscribed onto a spell to make it a complete
// ritual: sockets as circles on the diagonals, the build's actions as buttons on the cardinals.
public partial class RitualInscription : ButtonWheel
{
    // A socket takes twice the angle an action does: sockets are picked most, and the diagonals they
    // sit on are the hardest directions to land.
    public const float SocketWeight = 2f;
    public const float SocketDiameter = 182f;

    private const int Slots = 8;
    private const float SlotDistance = 325f;

    private static readonly Vector2 _actionSize = new Vector2(247f, 83f);

    // Sockets sit on the diagonals, actions on the cardinals.
    public static bool IsSocketSlot(int slot) => slot % 2 == 1;

    // Where a slot sits relative to this control's center.
    public static Vector2 SlotOffset(int slot) =>
        Vector2.FromAngle(-Mathf.Pi / 2f + slot * Mathf.Tau / Slots) * SlotDistance;

    protected override int SlotCount => Slots;
    protected override Vector2 OffsetOf(int slot) => SlotOffset(slot);
    protected override Vector2 SizeOf(int slot) => IsSocketSlot(slot) ? new Vector2(SocketDiameter, SocketDiameter) : _actionSize;
    protected override bool IsRound(int slot) => IsSocketSlot(slot);
    protected override float WeightOf(int slot) => IsSocketSlot(slot) ? SocketWeight : 1f;
    protected override float BackdropRadius => 533f;
    protected override float PointerTravel => 143f;
    protected override float PointerDotRadius => 18f;
    protected override Vector2 CenterInfoSize => new Vector2(338f, 182f);
}
