using System.Reflection;

namespace RaccoonSql.CoreRework;

/// <summary>
/// An exception that signals that the validation of a model has failed.
/// </summary>
public abstract class CheckConstraintException : Exception;

[AttributeUsage(AttributeTargets.Property)]
public abstract class PropertyCheckConstraintAttribute : Attribute
{
    public abstract IModelValidator GetValidator(PropertyInfo propertyInfo);
}

[AttributeUsage(AttributeTargets.Class)]
public abstract class ModelCheckConstraintAttribute : Attribute
{
    public abstract IModelValidator GetValidator(Type modelType);
}

/// <summary>
/// A validator for a model.
/// </summary>
/// <remarks>A validator may not modify a model.</remarks>
public interface IModelValidator
{
    /// <summary>
    /// Validates a model.
    /// </summary>
    /// <remarks>All validation exceptions thrown should implement <see cref="CheckConstraintException"/></remarks>
    /// <exception cref="CheckConstraintException">If the value is not valid.</exception>
    /// <param name="model">The model to validate.</param>
    void Check(ModelBase model);
}

/// <summary>
/// A typed validator for a model.
/// </summary>
/// <remarks>A validator may not modify a model.</remarks>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IModelValidator<in TModel> : IModelValidator
    where TModel : ModelBase
{
    /// <summary>
    /// Validates a model.
    /// </summary>
    /// <remarks>All validation exceptions thrown should implement <see cref="CheckConstraintException"/></remarks>
    /// <exception cref="CheckConstraintException">If the value is not valid.</exception>
    /// <param name="model">The model to validate.</param>
    void Check(TModel model);

    /// <inheritdoc cref="IModelValidator.Check"/>
    void IModelValidator.Check(ModelBase model) => Check((TModel)model);
}