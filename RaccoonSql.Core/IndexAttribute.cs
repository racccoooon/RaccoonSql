namespace RaccoonSql.Core;


public enum IndexType
{
    BTree,
    Hash,
}

[AttributeUsage(AttributeTargets.Property)]
public class IndexAttribute : Attribute
{
    public IndexType? Type { get; }
    public string? Name { get; }

    public IndexAttribute(IndexType? type, string? name)
    {
        Type = type;
        Name = name;
    }

    public IndexAttribute() : this(null, null)
    {
        
    }

    public IndexAttribute(IndexType type) : this(type, null)
    {
        
    }

    public IndexAttribute(string name) : this(null, name)
    {
        
    }
}