using System;

// The permanent name a class is saved under, in place of its script path, so renaming or moving the
// class never breaks a save. Never change a tag once saves exist - a class renamed in code keeps its
// old tag. Not inherited: every saved class names its own. See SaveCodec.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SaveTagAttribute : Attribute
{
    public SaveTagAttribute(string tag)
    {
        Tag = tag;
    }

    public string Tag { get; }
}
