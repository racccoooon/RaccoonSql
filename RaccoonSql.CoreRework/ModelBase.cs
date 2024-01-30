using System.Reflection;

namespace RaccoonSql.CoreRework;

/// <summary>
/// The base class for all database models.
/// </summary>
public abstract class ModelBase
{
    private Guid _id;

    /// <summary>
    /// The id of the model. It is generated on first access or when a <see cref="ITransaction"/> is committed.
    /// </summary>
    public Guid Id
    {
        get
        {
            if (_id == default)
                _id = Guid.NewGuid();
            
            return _id;
        }
        internal set => _id = value;
    }

    internal Action<Guid>? OnChange;
    internal Dictionary<PropertyInfo, object?> Changes = default!;
    internal Dictionary<MethodInfo, PropertyInfo> SetterPropertyMap = default!;
    internal bool TrackChanges { get; set; }
}