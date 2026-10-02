using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A fixed-size grid where each item covers a GridSize rectangle of cells from its top-left anchor, and
// no two items overlap. Wraps the saved dictionary rather than copying it, so every change lands
// straight in the save data. A key is the anchor's cell index, row by row.
public class ItemGrid
{
    private readonly Godot.Collections.Dictionary<int, Item> _items;

    public ItemGrid(Godot.Collections.Dictionary<int, Item> items, int columns, int rows)
    {
        _items = items;
        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; }
    public int Rows { get; }

    // Whether both wrap the same saved items - an ItemGrid is a fresh wrapper each time it's asked for.
    public bool IsSameGridAs(ItemGrid other) => other != null && _items == other._items;

    public List<(Item Item, Vector2I Anchor)> Placements() => _items
        .Where(pair => pair.Value != null)
        .Select(pair => (pair.Value, CellOf(pair.Key)))
        .ToList();

    public bool Contains(Item item) => AnchorOf(item) != null;

    public Vector2I? AnchorOf(Item item)
    {
        foreach ((int key, Item placed) in _items)
        {
            if (placed == item)
            {
                return CellOf(key);
            }
        }

        return null;
    }

    // The item covering cell, if any.
    public Item At(Vector2I cell) =>
        Placements().FirstOrDefault(placement => Footprint(placement.Item, placement.Anchor).HasPoint(cell)).Item;

    // False when item at anchor would leave the grid or overlap more than one item. Otherwise displaced
    // is the one item it would overlap, or null for open space. The item itself never counts, so an
    // item can be moved within the grid it's already in.
    public bool CanPlace(Item item, Vector2I anchor, out Item displaced)
    {
        displaced = null;
        Rect2I footprint = Footprint(item, anchor);

        if (!new Rect2I(0, 0, Columns, Rows).Encloses(footprint))
        {
            return false;
        }

        foreach ((Item placed, Vector2I placedAnchor) in Placements())
        {
            if (placed == item || !Footprint(placed, placedAnchor).Intersects(footprint))
            {
                continue;
            }

            if (displaced != null)
            {
                return false;
            }

            displaced = placed;
        }

        return true;
    }

    // Puts item at anchor and returns whatever it displaced, which leaves the grid.
    public Item Place(Item item, Vector2I anchor)
    {
        if (!CanPlace(item, anchor, out Item displaced))
        {
            throw new InvalidOperationException($"ItemGrid: {item.Name} doesn't fit at {anchor}.");
        }

        Remove(item);
        Remove(displaced);
        _items[IndexOf(anchor)] = item;
        return displaced;
    }

    public bool Remove(Item item)
    {
        return item != null && AnchorOf(item) is Vector2I anchor && _items.Remove(IndexOf(anchor));
    }

    public bool TryAdd(Item item)
    {
        if (FindOpenAnchor(item) is not Vector2I anchor)
        {
            return false;
        }

        _items[IndexOf(anchor)] = item;
        return true;
    }

    public bool HasRoomFor(Item item) => FindOpenAnchor(item) != null;

    // Down each column before moving right, so tall items pack together on the left.
    private Vector2I? FindOpenAnchor(Item item)
    {
        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                Vector2I anchor = new Vector2I(column, row);

                if (CanPlace(item, anchor, out Item displaced) && displaced == null)
                {
                    return anchor;
                }
            }
        }

        return null;
    }

    // The anchor that puts item's middle on cell, pulled back inside the grid at an edge.
    public Vector2I AnchorCenteredOn(Item item, Vector2I cell)
    {
        Vector2I size = item.GridSize;
        Vector2I anchor = cell - (size - Vector2I.One) / 2;
        return anchor.Clamp(Vector2I.Zero, new Vector2I(Columns, Rows) - size);
    }

    // Takes out whatever can't stay where it's saved: an empty entry, an item off the grid, or one
    // overlapping an item kept before it. Returns the items taken out, for the caller to find room for.
    public List<Item> RemoveMisplaced()
    {
        List<Item> removed = new();
        List<Rect2I> kept = new();
        Rect2I bounds = new Rect2I(0, 0, Columns, Rows);

        foreach (int key in _items.Keys.ToList())
        {
            Item item = _items[key];

            if (item != null)
            {
                Rect2I footprint = Footprint(item, CellOf(key));

                if (bounds.Encloses(footprint) && !kept.Any(other => other.Intersects(footprint)))
                {
                    kept.Add(footprint);
                    continue;
                }

                removed.Add(item);
            }

            _items.Remove(key);
        }

        return removed;
    }

    private static Rect2I Footprint(Item item, Vector2I anchor) => new Rect2I(anchor, item.GridSize);

    private int IndexOf(Vector2I cell) => cell.Y * Columns + cell.X;

    // A negative index lands off the grid, which is what quarantine recovery relies on (SaveFile).
    private Vector2I CellOf(int index) => new Vector2I(index % Columns, index / Columns);
}
