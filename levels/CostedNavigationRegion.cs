using Godot;
using System.Collections.Generic;

// Godot's travel cost is per region, so each cost gets a region of its own, cut from this one's bake.
// Every piece shares the bake's exact vertices, which is what lets the navigation server merge them
// into one connected map - regions baked separately would leave an agent_radius gap at each seam.
public partial class CostedNavigationRegion : NavigationRegion3D
{
    // Its NavCostZone children mark the costed areas. Must sit outside this region, since everything
    // under it is bake source. Ground outside every zone costs this region's own TravelCost.
    [Export] public Node3D CostZones { get; private set; }

    private readonly List<NavigationRegion3D> _pieces = new();

    public override void _Ready()
    {
        Split();
    }

    // Rebuilds the pieces from the current bake - call again after re-baking.
    public void Split()
    {
        foreach (NavigationRegion3D piece in _pieces)
        {
            piece.QueueFree();
        }
        _pieces.Clear();

        List<NavCostZone> zones = new();
        if (CostZones != null)
        {
            foreach (Node child in CostZones.GetChildren())
            {
                if (child is NavCostZone zone)
                {
                    zones.Add(zone);
                }
            }
        }

        NavigationMesh baked = NavigationMesh;
        if (baked == null || zones.Count == 0)
        {
            Enabled = true;
            return;
        }

        Vector3[] vertices = baked.Vertices;
        Dictionary<float, List<int[]>> polygonsByCost = new();
        for (int i = 0; i < baked.GetPolygonCount(); i++)
        {
            int[] polygon = baked.GetPolygon(i);
            float cost = CostAt(GlobalTransform * Centroid(vertices, polygon), zones);

            if (!polygonsByCost.TryGetValue(cost, out List<int[]> polygons))
            {
                polygons = new List<int[]>();
                polygonsByCost[cost] = polygons;
            }
            polygons.Add(polygon);
        }

        foreach ((float cost, List<int[]> polygons) in polygonsByCost)
        {
            NavigationRegion3D piece = new()
            {
                NavigationMesh = BuildPiece(baked, vertices, polygons),
                TravelCost = cost,
                NavigationLayers = NavigationLayers,
            };
            AddChild(piece);
            _pieces.Add(piece);
        }

        Enabled = false;
    }

    // Where zones overlap, the costliest wins.
    private float CostAt(Vector3 globalPoint, List<NavCostZone> zones)
    {
        float cost = TravelCost;
        bool inZone = false;

        foreach (NavCostZone zone in zones)
        {
            if (zone.Contains(globalPoint) && (!inZone || zone.TravelCost > cost))
            {
                cost = zone.TravelCost;
                inZone = true;
            }
        }

        return cost;
    }

    private static Vector3 Centroid(Vector3[] vertices, int[] polygon)
    {
        Vector3 sum = Vector3.Zero;
        foreach (int index in polygon)
        {
            sum += vertices[index];
        }
        return sum / polygon.Length;
    }

    // Cell size must match the navigation map's, or the server rejects the mesh.
    private static NavigationMesh BuildPiece(NavigationMesh baked, Vector3[] vertices, List<int[]> polygons)
    {
        Dictionary<int, int> remap = new();
        List<Vector3> pieceVertices = new();
        List<int[]> piecePolygons = new();

        foreach (int[] polygon in polygons)
        {
            int[] mapped = new int[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                if (!remap.TryGetValue(polygon[i], out int index))
                {
                    index = pieceVertices.Count;
                    remap[polygon[i]] = index;
                    pieceVertices.Add(vertices[polygon[i]]);
                }
                mapped[i] = index;
            }
            piecePolygons.Add(mapped);
        }

        NavigationMesh piece = new()
        {
            CellSize = baked.CellSize,
            CellHeight = baked.CellHeight,
            Vertices = pieceVertices.ToArray(),
        };
        foreach (int[] polygon in piecePolygons)
        {
            piece.AddPolygon(polygon);
        }
        return piece;
    }
}
