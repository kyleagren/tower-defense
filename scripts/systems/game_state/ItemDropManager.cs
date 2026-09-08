using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Rolls a chance to drop items wherever an enemy dies, same as before - but instead of spawning
// each drop into the world immediately, accumulates them for the whole level and sprays them all
// out of LootObjective's position at once during PostGame, ordered common-to-legendary. LootObjective
// itself is removed the moment the spray starts - the shield you spent the level defending becomes
// the reward. A failed level never fires GameEnded(true), so undelivered loot in the queue just
// never spawns - the existing "nothing to loot after a loss" rule, now covering everything.
public partial class ItemDropManager : Node
{
    [Export] public PackedScene WorldItemScene { get; private set; }
    [Export] public DefendableObjective LootObjective { get; private set; }
    [Export] public float ChestSprayRadius = 5f;
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

        Vector3 chestPosition = LootObjective?.GlobalPosition ?? Vector3.Zero;
        LootObjective?.QueueFree();

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
    // and no overlap, so items don't need physics collision to separate themselves.
    private Vector3 SpiralLandingPoint(Vector3 chestPosition, int index, int total)
    {
        float radius = ChestSprayRadius * Mathf.Sqrt((index + 1f) / total);
        float angle = index * GoldenAngle;
        return chestPosition + new Vector3(radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
    }
}
