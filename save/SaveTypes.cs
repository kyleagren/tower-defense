using System;
using System.Collections.Generic;
using System.Reflection;

// Every class that can be written into a save, by its SaveTag. Built from the assembly on first use;
// two classes sharing a tag throws, since a save couldn't tell them apart.
public static class SaveTypes
{
    private static readonly Dictionary<string, Type> _typesByTag = new();
    private static readonly Dictionary<Type, string> _tagsByType = new();

    static SaveTypes()
    {
        foreach (Type type in typeof(SaveTypes).Assembly.GetTypes())
        {
            SaveTagAttribute attribute = type.GetCustomAttribute<SaveTagAttribute>(inherit: false);

            if (attribute == null)
            {
                continue;
            }

            if (_typesByTag.TryGetValue(attribute.Tag, out Type existing))
            {
                throw new InvalidOperationException($"SaveTypes: {existing.Name} and {type.Name} both use the save tag \"{attribute.Tag}\".");
            }

            _typesByTag[attribute.Tag] = type;
            _tagsByType[type] = attribute.Tag;
        }
    }

    public static bool TryGetType(string tag, out Type type) => _typesByTag.TryGetValue(tag ?? "", out type);

    // Null for a class with no tag of its own.
    public static string TagOf(Type type) => _tagsByType.GetValueOrDefault(type);
}
