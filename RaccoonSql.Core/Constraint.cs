namespace RaccoonSql.Core;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyConstraintAttribute(Type refType, string? setName = null) : Attribute
{
    public Type RefType { get; } = refType;
    public string? SetName { get; } = setName;
}

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

public class LengthCheckConstraint(int? minLength, int maxLength) : ICheckConstraint<IModel, string>
{
    public bool Check(IModel model, string value)
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
        return (ICheckConstraint)Activator.CreateInstance(_implType.MakeGenericType(propertyType))!;
    }
}

public interface ICheckConstraint
{
    bool Check(IModel model, object value);
}

public interface ICheckConstraint<in TModel, in TValue> : ICheckConstraint
    where TModel : IModel
{
    bool Check(TModel model, TValue value);
    bool ICheckConstraint.Check(IModel model, object value) => Check((TModel)model, (TValue)value);
}

[AttributeUsage(AttributeTargets.Class)]
public class TriggerAttribute(Type implType) : Attribute
{
    public Type ImplType { get; } = implType;
}

public interface ICreateTrigger
{
    void OnCreate(IModel model);
}

public interface ICreateTrigger<in TModel> : ICreateTrigger
    where TModel : IModel
{
    void OnCreate(TModel model);
    void ICreateTrigger.OnCreate(IModel model) => OnCreate((TModel)model);
}

public interface IUpdateTrigger
{
    void OnUpdate(IModel model, Dictionary<string, object?> changes);
}

public interface IUpdateTrigger<in TModel> : IUpdateTrigger
    where TModel : IModel
{
    void OnUpdate(TModel model, Dictionary<string, object?> changes);
    void IUpdateTrigger.OnUpdate(IModel model, Dictionary<string, object?> changes) => OnUpdate((TModel)model, changes);
}

public interface IDeleteTrigger
{
    void OnDelete(IModel model);
}

public interface IDeleteTrigger<in TModel> : IDeleteTrigger
    where TModel : IModel
{
    void OnDelete(TModel model);
    void IDeleteTrigger.OnDelete(IModel model) => OnDelete((TModel)model);
}