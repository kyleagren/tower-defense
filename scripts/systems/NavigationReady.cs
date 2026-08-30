using Godot;
using System;
using System.Threading.Tasks;

// The navigation map isn't queryable for the first frames after a scene loads, so anything calling
// NavigationServer3D.MapGetPath at startup has to wait for it. Shared so the two callers agree on
// how long to wait, and so a map that never becomes usable reports itself instead of silently
// leaving a spawner facing the wrong way or a route preview that never appears.
public static class NavigationReady
{
    private const int MaxAttempts = 30;

    public static async Task<bool> WaitFor(Node node, Func<bool> isReady, string description)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await node.ToSignal(node.GetTree(), SceneTree.SignalName.PhysicsFrame);

            if (isReady())
            {
                return true;
            }
        }

        GD.PushWarning($"{description}: navigation map still unusable after {MaxAttempts} physics frames; giving up.");
        return false;
    }
}
