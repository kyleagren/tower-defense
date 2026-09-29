using Godot;

// Shared "look at something in the world, press a key" plumbing: one forward raycast per physics
// tick from the character's camera, a world-space prompt floating over whatever it found, and the
// bookkeeping to clear both when the character goes inactive or nothing is in view.
//
// Subclasses supply which physics layer to search, how to turn a raycast hit into their own target
// type, and what the prompt should say. Everything else is shared.
//
// Deliberately not generic: Godot's C# source generators skip generic classes, so [Export]s
// declared on a generic base wouldn't register and the scenes' Character/PromptScene wiring would
// silently break. Target is a Node3D and subclasses cast - ResolveTarget guarantees the type.
public abstract partial class LookInteractor : Node
{
    // Not exported - resolved from the parent instead of a wired NodePath, since this is always a
    // direct child of the character it belongs to and every PlayerCharacter subclass qualifies.
    // Wiring it as an export meant Godot had to type-check the assignment while building the scene
    // tree, which broke the moment the interactor lived in a shared base scene inherited by a
    // subclass: the base scene's root has no script yet at that point, so the parent is still a
    // bare CharacterBody3D and the cast fails. Resolving it here instead, once the whole merged,
    // fully-scripted scene actually exists, sidesteps that entirely.
    protected PlayerCharacter Character { get; private set; }

    [Export] public PackedScene PromptScene { get; private set; }
    [Export] public float PromptHeightOffset { get; private set; } = 0.3f;

    protected Node3D Target { get; private set; }

    private InteractionPrompt _prompt;

    protected abstract uint CollisionMask { get; }
    protected abstract float Range { get; }

    // A Ritual's collision body is a child of the Ritual; a WorldItem is its own body - so mapping
    // a raycast hit back to a target is the subclass's call, not something to guess at here.
    protected abstract Node3D ResolveTarget(Node3D collider);

    protected abstract string BuildPromptText(Node3D target);

    // An optional line under the prompt in its own color. Null text shows nothing.
    protected virtual (string Text, Color Color) BuildPromptDetail(Node3D target) => (null, Colors.White);

    // Fires whenever the target changes, null included - the hook for resetting per-target state
    // (a destroy hold timer, a cached tooltip) exactly once instead of every tick.
    protected virtual void OnTargetChanged(Node3D target)
    {
    }

    // Per-tick work while a target is in view. A subclass may call ClearTarget() from here to
    // consume the target outright, which suppresses the prompt for this tick.
    protected virtual void UpdateTarget(Node3D target, float delta)
    {
    }

    public override void _Ready()
    {
        Character = (PlayerCharacter)GetParent();
        _prompt = PromptScene.Instantiate<InteractionPrompt>();
        LevelContext.Current.LevelRoot.CallDeferred(Node.MethodName.AddChild, _prompt);
        _prompt.HidePrompt();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_prompt))
        {
            _prompt.QueueFree();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_prompt.IsInsideTree())
        {
            return;
        }

        SetTarget(Character.CanAct ? FindTargetAhead() : null);

        if (Target == null)
        {
            _prompt.HidePrompt();
            return;
        }

        UpdateTarget(Target, (float)delta);

        if (Target == null)
        {
            _prompt.HidePrompt();
            return;
        }

        (string detail, Color detailColor) = BuildPromptDetail(Target);
        _prompt.ShowPrompt(Target.GlobalPosition + Vector3.Up * PromptHeightOffset, BuildPromptText(Target), detail, detailColor);
    }

    protected void ClearTarget()
    {
        SetTarget(null);
    }

    private void SetTarget(Node3D target)
    {
        if (ReferenceEquals(target, Target))
        {
            return;
        }

        Target = target;
        OnTargetChanged(target);
    }

    // Reused every tick rather than made fresh - a new one each physics tick is a steady stream of
    // objects for the garbage collector to clear.
    private readonly PhysicsRayQueryParameters3D _query = new();

    protected virtual Node3D FindTargetAhead()
    {
        _query.From = Character.Camera.GlobalPosition;
        _query.To = Character.GetCameraAimPoint(Range);
        _query.CollisionMask = CollisionMask;

        Godot.Collections.Dictionary result = Character.GetWorld3D().DirectSpaceState.IntersectRay(_query);

        if (result.Count == 0)
        {
            return null;
        }

        return ResolveTarget(result["collider"].As<Node3D>());
    }
}
