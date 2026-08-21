using Godot;

// TODO: This bakes the navmesh at runtime on every scene load, as a stand-in for the
// standard approach (selecting this NavigationRegion3D in the editor and clicking the
// toolbar's "Bake NavigationMesh" button once). Once the level geometry is solidified,
// consider switching to a manual editor bake and deleting this script to avoid the
// runtime bake cost.
public partial class NavigationBaker : NavigationRegion3D
{
    public override void _Ready()
    {
        BakeNavigationMesh();
    }
}
