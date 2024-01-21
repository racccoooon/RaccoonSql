namespace RaccoonSql.Core;

// [AttributeUsage(AttributeTargets.Property)]
// public class ForeignKeyConstraintAttribute(Type refType, string? setName = null) : Attribute
// {
//     public Type RefType { get; } = refType;
//     public string? SetName { get; } = setName;
// }

[AttributeUsage(AttributeTargets.Property)]
public abstract class CheckConstraintAttribute : Attribute
{
    public abstract ICheckConstraint CreateConstraint(Type modelType, Type propertyType);
}

public class LengthCheckConstraintAttribute(int maxLength) : CheckConstraintAttribute
{
    private readonly int? _minLength;

    public LengthCheckConstraintAttribute(int minLength, int maxLength) : this(maxLength)
    {
        _minLength = minLength;
    }
    
    public override ICheckConstraint CreateConstraint(Type modelType, Type propertyType)
    {
        return new LengthCheckConstraint(_minLength, maxLength);
    }
}

public class LengthCheckConstraint(int? minLength, int maxLength) : ICheckConstraint<ModelBase, string>
{
    public bool Check(ModelBase model, string value)
    {
        var length = value.Length;
        return length >= minLength && length <= maxLength;
    }
}

public class TypedCheckConstraintAttribute : CheckConstraintAttribute
{
    private readonly Type _implType;

    public TypedCheckConstraintAttribute(Type implType)
    {
        //TODO: better check for generic version
        if (!implType.GetInterfaces().Any(x => x.IsAssignableTo(typeof(ICheckConstraint))))
            throw new ArgumentException($"{implType.Name} does not implement {nameof(ICheckConstraint)}",
                nameof(implType));

        _implType = implType;
    }

    //TODO: add support for injection/IServiceProvider
    public override ICheckConstraint CreateConstraint(Type modelType, Type propertyType)
    {
        var type = _implType;
        
        if (_implType.IsGenericType)
            type = _implType.MakeGenericType(propertyType);
        
        return (ICheckConstraint)Activator.CreateInstance(type)!;
    }
}

public interface ICheckConstraint
{
    bool Check(ModelBase model, object? value);
}

public interface ICheckConstraint<in TModel, in TValue> : ICheckConstraint
    where TModel : ModelBase
{
    bool Check(TModel model, TValue? value);
    bool ICheckConstraint.Check(ModelBase model, object? value) => Check((TModel)model, (TValue?)value);
}

[AttributeUsage(AttributeTargets.Class)]
public class TriggerAttribute(Type implType) : Attribute
{
    public Type ImplType { get; } = implType;
}

public interface ICreateTrigger
{
    void OnCreate(ModelBase model);
}

public interface ICreateTrigger<in TModel> : ICreateTrigger
    where TModel : ModelBase
{
    void OnCreate(TModel model);
    void ICreateTrigger.OnCreate(ModelBase model) => OnCreate((TModel)model);
}

public interface IUpdateTrigger
{
    void OnUpdate(ModelBase model, Dictionary<string, object?> changes);
}

public interface IUpdateTrigger<in TModel> : IUpdateTrigger
    where TModel : ModelBase
{
    void OnUpdate(TModel model, Dictionary<string, object?> changes);
    void IUpdateTrigger.OnUpdate(ModelBase model, Dictionary<string, object?> changes) => OnUpdate((TModel)model, changes);
}

public interface IDeleteTrigger
{
    void OnDelete(ModelBase model);
}

public interface IDeleteTrigger<in TModel> : IDeleteTrigger
    where TModel : ModelBase
{
    void OnDelete(TModel model);
    void IDeleteTrigger.OnDelete(ModelBase model) => OnDelete((TModel)model);
}