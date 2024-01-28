using System.ComponentModel;
using System.Reflection;
using JetBrains.Annotations;

namespace RaccoonSql.CoreRework.Indexes;

/// <summary>
/// Predefined index types.
/// </summary>
[PublicAPI]
public enum IndexType
{
    /// <summary>
    /// Automatically chooses an index type based on the property type.
    /// </summary>
    Auto,
    /// <summary>
    /// A BTree index is useful for properties that have a well defined order. 
    /// </summary>
    BTree,
    /// <summary>
    /// A hash index is useful for properties where equality is needed but expensive to compute.
    /// </summary>
    Hash,
}

/// <summary>
/// Represents an exceptional state for an index configuration.
/// </summary>
/// <param name="message">The exception message.</param>
public class IllegalIndexAttributeException(string message) : Exception(message);

/// <summary>
/// Used to configure indexes on model properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IndexAttribute : Attribute
{
    /// <summary>
    /// The name of the index. If not specified a name will be generated. It is necessary to provide a name if the property has multiple indexes.
    /// </summary>
    /// <exception cref="IllegalIndexAttributeException">thrown if no name is provided and the property has multiple <see cref="IndexAttribute"/> attributes.</exception>
    public string? Name { get; set; }
    
    /// <summary>
    /// Allows to choose from a set of predefined index types.
    /// This may only be used if <see cref="IndexType"/> is set to <see cref="Indexes.IndexType.Auto"/> otherwise an <see cref="IllegalIndexAttributeException"/> will be thrown.
    /// </summary>
    [DefaultValue(IndexType.Auto)]
    public IndexType IndexType { get; set; } = IndexType.Auto;
    
    /// <summary>
    /// Allows to set a custom index type.
    /// The type must extend from <see cref="IndexBase"/>.
    /// This may only be used if <see cref="IndexType"/> is set to <see cref="Indexes.IndexType.Auto"/> (default).
    /// </summary>
    /// <exception cref="IllegalIndexAttributeException">thrown if a custom and predefined index type was specified or if the custom index type does not extend from <see cref="IndexBase"/>.</exception>
    public Type? CustomType { get; set; }

    internal IndexBase CreateIndex(PropertyInfo propertyInfo)
    {
        if (IndexType != IndexType.Auto && CustomType is not null)
            throw new IllegalIndexAttributeException("IndexType must be Auto (default) when using a CustomType.");

        if (CustomType is not null)
        {
            return CreateCustomIndex(propertyInfo);
        }

        switch (IndexType)
        {
            case IndexType.Auto:
                return CreateAutoIndex(propertyInfo);
            case IndexType.BTree:
                return new BTreeIndex(propertyInfo);
            case IndexType.Hash:
                return new HashIndex(propertyInfo);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private IndexBase CreateAutoIndex(PropertyInfo propertyInfo)
    {
        throw new NotImplementedException();
    }

    private IndexBase CreateCustomIndex(PropertyInfo propertyInfo)
    {
        if (CustomType!.IsAbstract)
            throw new IllegalIndexAttributeException("CustomType must not be abstract.");

        if (CustomType.IsGenericType)
            throw new IllegalIndexAttributeException("CustomType must not be generic.");

        var ctor = CustomType.GetConstructor([typeof(PropertyInfo)]);
        if (ctor is null)
            throw new IllegalIndexAttributeException("CustomType does not supply constructor with PropertyInfo parameter.");

        return (IndexBase)ctor.Invoke([propertyInfo]);
    }
}