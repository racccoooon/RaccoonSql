using System.ComponentModel;

namespace RaccoonSql.CoreRework;

/// <summary>
/// Predefined index types.
/// </summary>
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
public class IndexAttribute : Attribute
{
    /// <summary>
    /// The name of the index. If not specified a name will be generated. It is necessary to provide a name if the property has multiple indexes.
    /// </summary>
    /// <exception cref="IllegalIndexAttributeException">thrown if no name is provided and the property has multiple <see cref="IndexAttribute"/> attributes.</exception>
    public string? Name { get; set; }
    
    /// <summary>
    /// Allows to choose from a set of predefined index types.
    /// This may only be used if <see cref="IndexType"/> is set to <see cref="CoreRework.IndexType.Auto"/> otherwise an <see cref="IllegalIndexAttributeException"/> will be thrown.
    /// </summary>
    [DefaultValue(IndexType.Auto)]
    public IndexType IndexType { get; set; } = IndexType.Auto;
    
    /// <summary>
    /// Allows to set a custom index type.
    /// The type must implement the TODO interface.
    /// This may only be used if <see cref="IndexType"/> is set to <see cref="CoreRework.IndexType.Auto"/> (default).
    /// </summary>
    /// <exception cref="IllegalIndexAttributeException">thrown if a custom and predefined index type was specified or if the custom index type does not implement the TODO interface.</exception>
    public Type? CustomType { get; set; }
}