using Godot;

// Lifts a CharacterBody3D onto a ledge no taller than stepHeight that it's walking into, which
// MoveAndSlide won't do on its own - it only climbs slopes. Call just before MoveAndSlide with the
// velocity about to be used. The lift is vertical only, so MoveAndSlide still does the walking.
public static class StepClimber
{
    // A slow walker covers almost nothing per frame; probing at least this far finds the step
    // before it's pressed flat against it.
    private const float MinProbeDistance = 0.1f;

    public static void TryStepUp(CharacterBody3D body, Vector3 velocity, float delta, float stepHeight)
    {
        if (stepHeight <= 0f || velocity.Y > 0f || !body.IsOnFloor())
        {
            return;
        }

        Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);

        if (horizontal.LengthSquared() < 0.0001f)
        {
            return;
        }

        Vector3 forward = horizontal * delta;

        if (forward.Length() < MinProbeDistance)
        {
            forward = horizontal.Normalized() * MinProbeDistance;
        }

        Transform3D start = body.GlobalTransform;
        KinematicCollision3D blocker = new KinematicCollision3D();

        // Only walking into something wall-like is a step; a slope is MoveAndSlide's to climb.
        if (!body.TestMove(start, forward, blocker) || blocker.GetNormal().AngleTo(Vector3.Up) <= body.FloorMaxAngle)
        {
            return;
        }

        Vector3 lift = Vector3.Up * stepHeight;

        if (body.TestMove(start, lift))
        {
            return;
        }

        Transform3D raised = start.Translated(lift);

        // Still blocked at full height: a wall, not a step.
        if (body.TestMove(raised, forward))
        {
            return;
        }

        KinematicCollision3D landing = new KinematicCollision3D();

        if (!body.TestMove(raised.Translated(forward), -lift, landing)
            || landing.GetNormal().AngleTo(Vector3.Up) > body.FloorMaxAngle)
        {
            return;
        }

        body.GlobalPosition += Vector3.Up * (stepHeight - landing.GetTravel().Length());
    }
}
