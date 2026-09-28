using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

// Turns saved Resources into JSON and back, keyed by what survives a rename: a class by its SaveTag, a
// project asset by its UID (path as a fallback), an enum value by its name. Walks each class's
// [Export] properties by C# reflection, so the exact types - which enum, what an array holds - come
// from the code rather than from Godot's type hints.
//
// A tagged class is written out in full; any other Resource (a Spell, a Rune, a Texture2D) is a project
// asset, written as a reference to it.
//
// Decoding never drops anything. What can't be decoded goes to quarantine at the nearest point it can
// be cut loose without disturbing the rest: any Item, wherever it sits, and otherwise one field, list
// element or dictionary entry of the profile itself. Something nested inside a piece that fails goes
// with that piece, never on its own as well.
public static class SaveCodec
{
    private const string TypeKey = "$type";
    private const string UidKey = "$uid";
    private const string PathKey = "$path";

    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propertiesByType = new();

    public static JsonObject EncodeProfile(PlayerSaveData data) => EncodeFields(data, new JsonObject());

    public static PlayerSaveData DecodeProfile(JsonObject profile, List<QuarantineEntry> quarantine)
    {
        Decoder decoder = new Decoder(quarantine);
        PlayerSaveData data = new PlayerSaveData();
        Dictionary<string, PropertyInfo> properties = SavedProperties(typeof(PlayerSaveData));

        foreach ((string name, JsonNode value) in profile)
        {
            if (properties.TryGetValue(name, out PropertyInfo property))
            {
                decoder.DecodeProfileField(data, property, value);
            }
            else
            {
                decoder.SetAside(decoder.Mark, new QuarantineEntry
                {
                    Where = name,
                    Field = name,
                    Reason = $"PlayerSaveData has no property \"{name}\"",
                    Data = value?.DeepClone()
                });
            }
        }

        return data;
    }

    // An item on its own - how items travel between machines.
    public static string EncodeItem(Item item) => Encode(item).ToJsonString();

    // Null for anything that doesn't decode to an item.
    public static Item DecodeItem(string json)
    {
        try
        {
            return new Decoder(new List<QuarantineEntry>()).Decode(JsonNode.Parse(json), typeof(Item), "item") as Item;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException or DecodeFailure)
        {
            return null;
        }
    }

    // Every [Export] property of a class and its bases, by name. Most-derived first, so an overriding
    // [Export] hides the one it overrides; each read from its own declaring type, the only way a
    // private setter declared on a base class can be reached.
    private static Dictionary<string, PropertyInfo> SavedProperties(Type type)
    {
        if (_propertiesByType.TryGetValue(type, out Dictionary<string, PropertyInfo> cached))
        {
            return cached;
        }

        Dictionary<string, PropertyInfo> properties = new();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type current = type; current != null && current != typeof(Resource); current = current.BaseType)
        {
            foreach (PropertyInfo property in current.GetProperties(flags))
            {
                if (property.IsDefined(typeof(ExportAttribute), inherit: false))
                {
                    properties.TryAdd(property.Name, property);
                }
            }
        }

        _propertiesByType[type] = properties;
        return properties;
    }

    private static JsonObject EncodeFields(Resource resource, JsonObject into)
    {
        foreach ((string name, PropertyInfo property) in SavedProperties(resource.GetType()))
        {
            into[name] = Encode(property.GetValue(resource));
        }

        return into;
    }

    private static JsonNode Encode(object value)
    {
        switch (value)
        {
            case null: return null;
            case Enum e: return JsonValue.Create(EnumName(e));
            case bool b: return JsonValue.Create(b);
            case int i: return JsonValue.Create(i);
            case long l: return JsonValue.Create(l);
            case float f: return JsonValue.Create(f);
            case double d: return JsonValue.Create(d);
            case string s: return JsonValue.Create(s);
            case Resource resource:
                return SaveTypes.TagOf(resource.GetType()) is string tag
                    ? EncodeFields(resource, new JsonObject { [TypeKey] = tag })
                    : EncodeAsset(resource);
        }

        Type type = value.GetType();

        if (IsGodotDictionary(type, out _, out _))
        {
            JsonObject entries = new JsonObject();

            foreach (object pair in (IEnumerable)value)
            {
                Type pairType = pair.GetType();
                entries[EncodeKey(pairType.GetProperty("Key").GetValue(pair))] = Encode(pairType.GetProperty("Value").GetValue(pair));
            }

            return entries;
        }

        if (IsGodotArray(type, out _))
        {
            return new JsonArray(((IEnumerable)value).Cast<object>().Select(Encode).ToArray());
        }

        throw new InvalidOperationException($"SaveCodec: saves can't hold a {type.Name}.");
    }

    private static JsonObject EncodeAsset(Resource asset)
    {
        string path = asset.ResourcePath;

        if (string.IsNullOrEmpty(path) || path.Contains("::"))
        {
            throw new InvalidOperationException($"SaveCodec: a {asset.GetType().Name} has no SaveTag and isn't a project file, so it can't be saved.");
        }

        JsonObject reference = new JsonObject { [PathKey] = path };
        long uid = ResourceLoader.GetResourceUid(path);

        if (uid != ResourceUid.InvalidId)
        {
            reference[UidKey] = ResourceUid.IdToText(uid);
        }

        return reference;
    }

    private static string EncodeKey(object key) => key switch
    {
        Enum e => EnumName(e),
        int or long or string => key.ToString(),
        _ => throw new InvalidOperationException($"SaveCodec: a {key.GetType().Name} can't be a dictionary key in a save.")
    };

    private static string EnumName(Enum value) =>
        Enum.GetName(value.GetType(), value)
        ?? throw new InvalidOperationException($"SaveCodec: {value.GetType().Name} {Convert.ToInt64(value)} has no name to save it under.");

    private static bool IsGodotArray(Type type, out Type elementType)
    {
        bool isArray = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Godot.Collections.Array<>);
        elementType = isArray ? type.GetGenericArguments()[0] : null;
        return isArray;
    }

    private static bool IsGodotDictionary(Type type, out Type keyType, out Type valueType)
    {
        bool isDictionary = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Godot.Collections.Dictionary<,>);
        keyType = isDictionary ? type.GetGenericArguments()[0] : null;
        valueType = isDictionary ? type.GetGenericArguments()[1] : null;
        return isDictionary;
    }

    private sealed class DecodeFailure : Exception
    {
        public DecodeFailure(string reason) : base(reason)
        {
        }
    }

    private sealed class Decoder
    {
        private readonly List<QuarantineEntry> _quarantine;

        public Decoder(List<QuarantineEntry> quarantine)
        {
            _quarantine = quarantine;
        }

        // Taken before decoding a piece that might be set aside, so whatever it set aside of its own
        // insides can be taken back if the whole piece goes.
        public int Mark => _quarantine.Count;

        public void SetAside(int mark, QuarantineEntry entry)
        {
            _quarantine.RemoveRange(mark, _quarantine.Count - mark);
            _quarantine.Add(entry);
        }

        // A list or dictionary is decoded one element or entry at a time, so one bad one doesn't take the
        // rest with it. A null leaves the property at its default.
        public void DecodeProfileField(PlayerSaveData data, PropertyInfo property, JsonNode value)
        {
            if (value == null)
            {
                return;
            }

            string name = property.Name;
            Type type = property.PropertyType;

            if (IsGodotArray(type, out Type elementType) && value is JsonArray elements)
            {
                object list = Activator.CreateInstance(type);
                MethodInfo add = type.GetMethod("Add", new[] { elementType });

                for (int i = 0; i < elements.Count; i++)
                {
                    int mark = Mark;
                    string where = $"{name}[{i}]";

                    try
                    {
                        add.Invoke(list, new[] { Decode(elements[i], elementType, where) });
                    }
                    catch (DecodeFailure failure)
                    {
                        SetAside(mark, new QuarantineEntry { Where = where, Field = name, Index = i, Reason = failure.Message, Data = elements[i]?.DeepClone() });

                        // A gap keeps later elements where they were; a list of values can't hold one.
                        if (!elementType.IsValueType)
                        {
                            add.Invoke(list, new object[] { null });
                        }
                    }
                }

                property.SetValue(data, list);
                return;
            }

            if (IsGodotDictionary(type, out Type keyType, out Type valueType) && value is JsonObject entries)
            {
                object map = Activator.CreateInstance(type);
                MethodInfo add = type.GetMethod("Add", new[] { keyType, valueType });

                foreach ((string key, JsonNode entry) in entries)
                {
                    int mark = Mark;
                    string where = $"{name}.{key}";

                    try
                    {
                        add.Invoke(map, new[] { DecodeKey(key, keyType, where), Decode(entry, valueType, where) });
                    }
                    catch (DecodeFailure failure)
                    {
                        SetAside(mark, new QuarantineEntry { Where = where, Field = name, Key = key, Reason = failure.Message, Data = entry?.DeepClone() });
                    }
                }

                property.SetValue(data, map);
                return;
            }

            int fieldMark = Mark;

            try
            {
                property.SetValue(data, Decode(value, type, name));
            }
            catch (DecodeFailure failure)
            {
                SetAside(fieldMark, new QuarantineEntry { Where = name, Field = name, Reason = failure.Message, Data = value.DeepClone() });
            }
        }

        // An Item is set aside on its own wherever it fails, leaving a gap where it was.
        public object Decode(JsonNode node, Type target, string where)
        {
            if (node == null || !typeof(Item).IsAssignableFrom(target))
            {
                return DecodeValue(node, target, where);
            }

            int mark = Mark;

            try
            {
                return DecodeValue(node, target, where);
            }
            catch (DecodeFailure failure)
            {
                SetAside(mark, new QuarantineEntry { Where = where, IsItem = true, Reason = failure.Message, Data = node.DeepClone() });
                return null;
            }
        }

        private object DecodeValue(JsonNode node, Type target, string where)
        {
            if (node == null)
            {
                return target.IsValueType ? throw new DecodeFailure($"{where}: missing a {target.Name}") : null;
            }

            if (target.IsEnum)
            {
                return DecodeEnum(Scalar<string>(node, where), target, where);
            }

            if (target == typeof(bool)) return Scalar<bool>(node, where);
            if (target == typeof(int)) return Scalar<int>(node, where);
            if (target == typeof(long)) return Scalar<long>(node, where);
            if (target == typeof(float)) return Scalar<float>(node, where);
            if (target == typeof(double)) return Scalar<double>(node, where);
            if (target == typeof(string)) return Scalar<string>(node, where);

            if (IsGodotArray(target, out Type elementType))
            {
                if (node is not JsonArray elements)
                {
                    throw new DecodeFailure($"{where}: expected a list");
                }

                object list = Activator.CreateInstance(target);
                MethodInfo add = target.GetMethod("Add", new[] { elementType });

                for (int i = 0; i < elements.Count; i++)
                {
                    add.Invoke(list, new[] { Decode(elements[i], elementType, $"{where}[{i}]") });
                }

                return list;
            }

            if (IsGodotDictionary(target, out Type keyType, out Type valueType))
            {
                if (node is not JsonObject entries)
                {
                    throw new DecodeFailure($"{where}: expected a dictionary");
                }

                object map = Activator.CreateInstance(target);
                MethodInfo add = target.GetMethod("Add", new[] { keyType, valueType });

                foreach ((string key, JsonNode entry) in entries)
                {
                    add.Invoke(map, new[] { DecodeKey(key, keyType, where), Decode(entry, valueType, $"{where}.{key}") });
                }

                return map;
            }

            if (typeof(Resource).IsAssignableFrom(target))
            {
                if (node is not JsonObject obj)
                {
                    throw new DecodeFailure($"{where}: expected an object");
                }

                if (obj.ContainsKey(TypeKey))
                {
                    return DecodeTagged(obj, target, where);
                }

                if (obj.ContainsKey(UidKey) || obj.ContainsKey(PathKey))
                {
                    return DecodeAsset(obj, target, where);
                }

                throw new DecodeFailure($"{where}: neither a saved object nor an asset reference");
            }

            throw new DecodeFailure($"{where}: saves can't hold a {target.Name}");
        }

        private Resource DecodeTagged(JsonObject obj, Type target, string where)
        {
            string tag = Scalar<string>(obj[TypeKey], $"{where}.{TypeKey}");

            if (!SaveTypes.TryGetType(tag, out Type type))
            {
                throw new DecodeFailure($"{where}: no class has the save tag \"{tag}\"");
            }

            if (!target.IsAssignableFrom(type))
            {
                throw new DecodeFailure($"{where}: \"{tag}\" is a {type.Name}, not a {target.Name}");
            }

            Resource resource = (Resource)Activator.CreateInstance(type);
            Dictionary<string, PropertyInfo> properties = SavedProperties(type);

            foreach ((string name, JsonNode value) in obj)
            {
                if (name == TypeKey)
                {
                    continue;
                }

                if (!properties.TryGetValue(name, out PropertyInfo property))
                {
                    throw new DecodeFailure($"{where}: {type.Name} has no property \"{name}\"");
                }

                property.SetValue(resource, Decode(value, property.PropertyType, $"{where}.{name}"));
            }

            return resource;
        }

        // Only the project's own files - the UID first, since it survives the file being moved or renamed.
        private static Resource DecodeAsset(JsonObject reference, Type target, string where)
        {
            string path = null;

            if (reference[UidKey] is JsonValue uidValue && uidValue.TryGetValue(out string uidText))
            {
                long uid = ResourceUid.TextToId(uidText);

                if (uid != ResourceUid.InvalidId && ResourceUid.HasId(uid))
                {
                    path = ResourceUid.GetIdPath(uid);
                }
            }

            if (path == null && reference[PathKey] is JsonValue pathValue && pathValue.TryGetValue(out string savedPath))
            {
                path = savedPath;
            }

            if (path == null || !path.StartsWith("res://") || !ResourceLoader.Exists(path))
            {
                throw new DecodeFailure($"{where}: can't find the asset {reference.ToJsonString()}");
            }

            Resource asset = ResourceLoader.Load(path);

            if (!target.IsInstanceOfType(asset))
            {
                throw new DecodeFailure($"{where}: {path} is a {asset?.GetType().Name ?? "missing resource"}, not a {target.Name}");
            }

            return asset;
        }

        private static object DecodeKey(string key, Type keyType, string where)
        {
            if (keyType.IsEnum)
            {
                return DecodeEnum(key, keyType, where);
            }

            if (keyType == typeof(int) && int.TryParse(key, out int number))
            {
                return number;
            }

            if (keyType == typeof(long) && long.TryParse(key, out long longNumber))
            {
                return longNumber;
            }

            if (keyType == typeof(string))
            {
                return key;
            }

            throw new DecodeFailure($"{where}: \"{key}\" isn't a {keyType.Name}");
        }

        // By name only - a number would read as whatever value holds that number now.
        private static object DecodeEnum(string name, Type enumType, string where)
        {
            if (name == null || !Enum.GetNames(enumType).Contains(name))
            {
                throw new DecodeFailure($"{where}: {enumType.Name} has no value \"{name}\"");
            }

            return Enum.Parse(enumType, name);
        }

        private static T Scalar<T>(JsonNode node, string where)
        {
            if (node is JsonValue value && value.TryGetValue(out T result))
            {
                return result;
            }

            throw new DecodeFailure($"{where}: expected a {typeof(T).Name}, found {node?.ToJsonString() ?? "nothing"}");
        }
    }
}
