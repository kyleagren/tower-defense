using Godot;

public partial class DebugToggle : Node
{
    private const float DebugHeatStep = 20f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        // F1/F2 nudge the Tank's heat dial either way, for testing it without a wave's worth of hits.
        if (keyEvent.PhysicalKeycode == Key.F1)
        {
            (Sentinel.Local as Tank)?.AddHeat(DebugHeatStep);
        }
        else if (keyEvent.PhysicalKeycode == Key.F2)
        {
            (Sentinel.Local as Tank)?.AddHeat(-DebugHeatStep);
        }
        else if (keyEvent.PhysicalKeycode == Key.F3)
        {
            FillLocalHarvestGauge();
        }
    }

    // Fills the local Soldier's Harvest gauge from one random element, for testing Overdrive without
    // harvesting a wave's worth of Statuses. Wind is included, though Harvest can't actually be fed
    // it, to try out how Wind shots feel.
    private static void FillLocalHarvestGauge()
    {
        if (Sentinel.Local is not Soldier soldier)
        {
            return;
        }

        foreach (Ability ability in soldier.AllAbilities)
        {
            if (ability is HarvestAbility harvest)
            {
                harvest.FillGauge(ElementTags.All[GD.RandRange(0, ElementTags.All.Count - 1)]);
            }
        }
    }
}
