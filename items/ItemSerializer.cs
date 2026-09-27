using Godot;

// Turns an item into plain data another machine can rebuild - how items change hands. Walks the same
// exported properties the save file stores, so a new item type or field travels with nothing added
// here. A resource that lives inside the item (a rolled affix, a base stat) goes along whole; one
// that's a project file of its own (an icon, a tome's rune) goes as its path.
public static class ItemSerializer
{
    // The only scripts ever created from another machine's data.
    private const string ScriptRoot = "res://items/";

    public static Godot.Collections.Dictionary Serialize(Resource resource)
    {
        Godot.Collections.Dictionary properties = new();

        foreach (Godot.Collections.Dictionary property in resource.GetPropertyList())
        {
            PropertyUsageFlags usage = (PropertyUsageFlags)property["usage"].AsInt64();

            if (!usage.HasFlag(PropertyUsageFlags.ScriptVariable) || !usage.HasFlag(PropertyUsageFlags.Storage))
            {
                continue;
            }

            string name = property["name"].AsString();
            properties[name] = Encode(resource.Get(name));
        }

        return new Godot.Collections.Dictionary
        {
            ["script"] = ((Script)resource.GetScript()).ResourcePath,
            ["properties"] = properties
        };
    }

    // Null for data that doesn't build an item.
    public static Item Deserialize(Godot.Collections.Dictionary data) => Build(data) as Item;

    private static Variant Encode(Variant value)
    {
        if (value.VariantType == Variant.Type.Array)
        {
            Godot.Collections.Array encoded = new();

            foreach (Variant element in value.AsGodotArray())
            {
                encoded.Add(Encode(element));
            }

            return encoded;
        }

        if (value.VariantType != Variant.Type.Object || value.AsGodotObject() is not Resource resource)
        {
            return value;
        }

        // A resource saved inside another file (the save's own sub-resources) has "::" in its path.
        bool isOwnFile = !string.IsNullOrEmpty(resource.ResourcePath) && !resource.ResourcePath.Contains("::");
        return isOwnFile ? new Godot.Collections.Dictionary { ["path"] = resource.ResourcePath } : Serialize(resource);
    }

    private static Resource Build(Godot.Collections.Dictionary data)
    {
        string scriptPath = data["script"].AsString();

        if (!scriptPath.StartsWith(ScriptRoot) || ResourceLoader.Load(scriptPath) is not CSharpScript script
            || script.New().AsGodotObject() is not Resource resource)
        {
            return null;
        }

        foreach ((Variant key, Variant value) in data["properties"].AsGodotDictionary())
        {
            string name = key.AsString();
            Variant current = resource.Get(name);

            // Filled in place: the property's own array is already typed, which a fresh one wouldn't be.
            if (current.VariantType == Variant.Type.Array)
            {
                Godot.Collections.Array target = current.AsGodotArray();
                target.Clear();

                foreach (Variant element in value.AsGodotArray())
                {
                    target.Add(Decode(element));
                }

                continue;
            }

            resource.Set(name, Decode(value));
        }

        return resource;
    }

    private static Variant Decode(Variant value)
    {
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return value;
        }

        Godot.Collections.Dictionary data = value.AsGodotDictionary();

        if (!data.ContainsKey("path"))
        {
            return Build(data);
        }

        string path = data["path"].AsString();
        return path.StartsWith("res://") ? ResourceLoader.Load(path) : default;
    }
}
