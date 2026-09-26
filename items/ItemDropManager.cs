using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Rolls drop chances as enemies die, but holds every result until the level is won. The whole
// pile then sprays out of the level's primary objective during PostGame, ordered
// common-to-legendary, and the objective itself is freed as the spray starts - the shield defended
// all level becomes the reward. A loss never fires GameEnded(true), so queued loot simply never spawns, which is
// the "nothing to loot after a loss" rule applied to everything.
public partial class ItemDropManager : Node
{
    [Export] public PackedScene WorldItemScene { get; private set; }
    [Export] public float ChestSprayRadius = 5f;
    [Export] public float GroundHeight = 0f;
    [Export] public float SprayArcHeight = 3f;
    [Export] public float SprayItemDuration = 1.2f;
    [Export] public float SprayStaggerInterval = 0.15f;

    private const float GoldenAngle = 2.399963f;

    private static readonly System.Random _random = new();

    private readonly List<Item> _pendingLoot = new();

    public override void _Ready()
    {
        EventBus.Instance.EnemyKilled += OnEnemyKilled;
        EventBus.Instance.GameEnded += OnGameEnded;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.EnemyKilled -= OnEnemyKilled;
        EventBus.Instance.GameEnded -= OnGameEnded;
    }

    // PoE-style: quantity (LootRolls) increases the number of independent drop-chance rolls per
    // kill, not the size of a single roll - each roll either drops one item or doesn't, so a kill
    // can end up with anywhere from 0 to LootRolls items.
    private void OnEnemyKilled(int reward, int goldReward, Vector3 position, float dropChance, int lootRolls)
    {
        for (int i = 0; i < lootRolls; i++)
        {
            if (_random.NextDouble() < dropChance)
            {
                _pendingLoot.Add(LootGenerator.GenerateRandomDrop());
            }
        }
    }

    private void OnGameEnded(bool victory)
    {
        if (victory && _pendingLoot.Count > 0)
        {
            _ = SprayLoot();
        }
    }

    private async Task SprayLoot()
    {
        List<Item> sorted = _pendingLoot.OrderBy(item => (int)item.Rarity).ToList();
        _pendingLoot.Clear();

        DefendableObjective objective = LevelContext.Current.PrimaryObjective;
        Vector3 chestPosition = objective?.GlobalPosition ?? Vector3.Zero;
        objective?.QueueFree();

        for (int i = 0; i < sorted.Count; i++)
        {
            LaunchItem(sorted[i], chestPosition, i, sorted.Count);
            await ToSignal(GetTree().CreateTimer(SprayStaggerInterval), "timeout");
        }
    }

    private void LaunchItem(Item item, Vector3 chestPosition, int index, int total)
    {
        WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();
        worldItem.Initialize(item);
        GetTree().CurrentScene.AddChild(worldItem);
        worldItem.GlobalPosition = chestPosition;

        Vector3 landingPoint = SpiralLandingPoint(chestPosition, index, total);
        float arcHeight = SprayArcHeight;

        Tween tween = CreateTween();
        tween.TweenMethod(
            Callable.From((float t) => worldItem.GlobalPosition = ArcLerp(chestPosition, landingPoint, t, arcHeight)),
            0f, 1f, SprayItemDuration);
    }

    private static Vector3 ArcLerp(Vector3 from, Vector3 to, float t, float arcHeight)
    {
        Vector3 point = from.Lerp(to, t);
        point.Y += 4f * arcHeight * t * (1f - t);
        return point;
    }

    // Vogel's method / golden-angle spiral - spreads N points across a circle with even density
    // and no overlap, so items don't need physics collision to separate themselves. Lands at
    // GroundHeight rather than the chest's own Y - WorldItem's own mesh is already offset up by
    // its radius internally (world_item.tscn), so GlobalPosition is meant to be the ground contact
    // point, not the sphere's center.
    private Vector3 SpiralLandingPoint(Vector3 chestPosition, int index, int total)
    {
        float radius = ChestSprayRadius * Mathf.Sqrt((index + 1f) / total);
        float angle = index * GoldenAngle;
        Vector3 groundPoint = chestPosition;
        groundPoint.Y = GroundHeight;
        return groundPoint + new Vector3(radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
    }
}
