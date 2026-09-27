using Godot;

// The spell-specific part of placing a Ritual: anything picked before the Ritual itself (a wall's
// endpoints), where the Ritual may then go, and the PlacementData it keeps. RitualPlacer owns
// everything every spell shares - the cursor, the ghost, overlap and affordability checks, and
// asking for the Ritual. This base is the plain case: nothing to pick, and the Ritual goes wherever it fits.
public class PlacementFlow
{
    public virtual bool IsPickingPoints => false;
    public virtual bool ShowsRangeIndicator => true;
    public virtual PlacementData Data => null;

    // Called every physics tick while IsPickingPoints.
    public virtual void UpdatePoints(Vector3 cursor, bool confirmPressed)
    {
    }

    public virtual Vector3 ConstrainRitualPosition(Vector3 cursor) => cursor;

    public virtual bool IsRitualPositionAllowed(Vector3 position) => true;

    // Frees anything the flow put in the world - on confirm and cancel alike.
    public virtual void End()
    {
    }
}
